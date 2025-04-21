/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using Grasshopper.Kernel;
using SmartHopper.Core.DataTree;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Components.Properties;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using System.Collections.Generic;
using System.Linq;
using SmartHopper.Core.Grasshopper.Tools;
using System.Drawing;

namespace SmartHopper.Components.Text
{
    public class AITextGenerate : AIStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("EB073C7A-A500-4265-A45B-B1BFB38BA58E");
        protected override Bitmap Icon => Resources.textgenerate;
        public override GH_Exposure Exposure => GH_Exposure.primary;

        public AITextGenerate()
            : base("AI Text Generate", "AITextGenerate",
                  "Generate text from natural language instructions.\nIf a tree structure is provided, prompts and instructions will only match within the same branch paths.",
                  "SmartHopper", "Text")
        {
        }

        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Prompt", "P", "REQUIRED The user's prompt", GH_ParamAccess.tree);
            pManager.AddTextParameter("Instructions", "I", "Optionally, specify what the AI should do when receiving the prompt", GH_ParamAccess.tree, string.Empty);
        }

        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Result", "R", "The AI's response", GH_ParamAccess.tree);
        }

        protected override string GetEndpoint()
        {
            return "text-generate";
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new AITextGenerateWorker(this, AddRuntimeMessage);
        }

        private class AITextGenerateWorker : AsyncWorkerBase
        {
            private Dictionary<string, GH_Structure<GH_String>> _inputTree;
            private Dictionary<string, GH_Structure<GH_String>> _result;
            private readonly AITextGenerate _parent;

            public AITextGenerateWorker(
            AITextGenerate parent,
            Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
            : base(parent, addRuntimeMessage)
            {
                _parent = parent;
                _result = new Dictionary<string, GH_Structure<GH_String>>
                {
                    { "Result", new GH_Structure<GH_String>() }
                };
            }

            public override void GatherInput(IGH_DataAccess DA)
            {
                _inputTree = new Dictionary<string, GH_Structure<GH_String>>();

                // Get the input trees
                var promptTree = new GH_Structure<GH_String>();
                var instructionsTree = new GH_Structure<GH_String>();

                DA.GetDataTree("Prompt", out promptTree);
                DA.GetDataTree("Instructions", out instructionsTree);

                // The first defined tree is the one that overrides paths in case they don't match between trees
                _inputTree["Prompt"] = promptTree;
                _inputTree["Instructions"] = instructionsTree;
            }

            public override async Task DoWorkAsync(CancellationToken token)
            {
                try
                {
                    Debug.WriteLine($"[Worker] Starting DoWorkAsync");
                    Debug.WriteLine($"[Worker] Input tree keys: {string.Join(", ", _inputTree.Keys)}");
                    Debug.WriteLine($"[Worker] Input tree data counts: {string.Join(", ", _inputTree.Select(kvp => $"{kvp.Key}: {kvp.Value.DataCount}"))}");

                    _result = await DataTreeProcessor.RunFunctionAsync<GH_String, GH_String>(
                        _inputTree,
                        async (branches, reuseCount) => 
                        {
                            Debug.WriteLine($"[Worker] ProcessData called with {branches.Count} branches, reuse count: {reuseCount}");
                            return await ProcessData(branches, _parent, reuseCount);
                        },
                        onlyMatchingPaths: false,
                        groupIdenticalBranches: true,
                        token);
                        
                    Debug.WriteLine($"[Worker] Finished DoWorkAsync - Result keys: {string.Join(", ", _result.Keys)}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Worker] Error: {ex.Message}");
                }
            }

            private static async Task<Dictionary<string, List<GH_String>>> ProcessData(Dictionary<string, List<GH_String>> branches, AITextGenerate parent, int reuseCount = 1)
            {
                /*
                 * Inputs will be available as a dictionary
                 * of branches. No need to deal with paths.
                 *
                 * Outputs should be a dictionary where keys
                 * are each output parameter, and values are
                 * the output values.
                 */

                Debug.WriteLine($"[Worker] Processing {branches.Count} trees with reuse count: {reuseCount}");
                Debug.WriteLine($"[Worker] Items per tree: {branches.Values.Max(branch => branch.Count)}");

                // Get the trees
                var promptTree = branches["Prompt"];
                var instructionsTree = branches["Instructions"];

                // Normalize tree lengths
                var normalizedLists = DataTreeProcessor.NormalizeBranchLengths(new List<List<GH_String>> { promptTree, instructionsTree });

                // Reassign normalized branches
                promptTree = normalizedLists[0];
                instructionsTree = normalizedLists[1];

                Debug.WriteLine($"[ProcessData] After normalization - Prompts count: {promptTree.Count}, Instructions count: {instructionsTree.Count}");

                // Initialize the output
                var outputs = new Dictionary<string, List<GH_String>>();
                outputs["Result"] = new List<GH_String>();

                // Iterate over the branches
                // For each item in the prompt tree, get the response from AI
                int i = 0;
                foreach (var prompt in promptTree)
                {
                    Debug.WriteLine($"[ProcessData] Processing prompt {i + 1}/{promptTree.Count}");

                    // Use the generic tool to generate text
                    var result = await TextTools.GenerateTextAsync(
                        promptTree[i],
                        instructionsTree[i], 
                        messages => parent.GetResponse(messages, contextProviderFilter: "-environment,-time", reuseCount: reuseCount));

                    if (!result.Success)
                    {
                        if (result.Response?.FinishReason == "error")
                        {
                            parent.AIErrorToPersistentRuntimeMessage(result.Response);
                        }
                        else
                        {
                            parent.SetPersistentRuntimeMessage("ai_error", result.ErrorLevel, result.ErrorMessage, false);
                        }
                        outputs["Result"].Add(new GH_String(string.Empty));
                        i++;
                        continue;
                    }

                    outputs["Result"].Add(result.Result);
                    i++;
                }

                return outputs;
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                Debug.WriteLine($"[Worker] Setting output - Available keys: {string.Join(", ", _result.Keys)}");
                
                if (!_result.ContainsKey("Result"))
                {
                    Debug.WriteLine("[Worker] Warning: Result key not found in output dictionary");
                    message = "Error: No result available";
                    return;
                }

                _parent.SetPersistentOutput("Result", _result["Result"], DA);
                message = "Done :)";
            }
        }
    }
}
