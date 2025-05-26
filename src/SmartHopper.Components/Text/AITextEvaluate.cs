/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Components.Properties;
using SmartHopper.Config.Managers;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Core.DataTree;
using SmartHopper.Core.Grasshopper.Tools;
using CommonDrawing = System.Drawing;

namespace SmartHopper.Components.Text
{
    public class AITextEvaluate : AIStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new("D3EB06A8-C219-46E3-854E-15EC798AD63A");

        protected override CommonDrawing::Bitmap Icon => Resources.textevaluate;

        public override GH_Exposure Exposure => GH_Exposure.primary;

        public AITextEvaluate()
            : base("AI Text Evaluate", "AITextEvaluate",
                  "Use natural language to ask a true or false question about a text.\nIf a tree structure is provided, questions and texts will only match within the same branch paths.",
                  "SmartHopper", "Text")
        {
        }

        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Text", "T", "The text to evaluate", GH_ParamAccess.tree);
            pManager.AddTextParameter("Question", "Q", "REQUIRED True or false question.\nThe AI will answer this question based on the input text", GH_ParamAccess.tree);
        }

        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Result", "R", "Result of the evaluation", GH_ParamAccess.tree);
        }

        protected override string GetEndpoint()
        {
            return "text-evaluate";
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new AITextEvaluateWorker(this, this.AddRuntimeMessage);
        }

        private class AITextEvaluateWorker : AsyncWorkerBase
        {
            private readonly AITextEvaluate parent;
            private Dictionary<string, GH_Structure<GH_String>> inputTree;
            private Dictionary<string, GH_Structure<GH_Boolean>> result;

            public AITextEvaluateWorker(
            AITextEvaluate parent,
            Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
            : base(parent, addRuntimeMessage)
            {
                this.parent = parent;
                this.result = new Dictionary<string, GH_Structure<GH_Boolean>>
                {
                    { "Result", new GH_Structure<GH_Boolean>() },
                };
            }

            public override void GatherInput(IGH_DataAccess DA)
            {
                this.inputTree = new Dictionary<string, GH_Structure<GH_String>>();

                // Get the input trees
                var textTree = new GH_Structure<GH_String>();
                var questionTree = new GH_Structure<GH_String>();

                DA.GetDataTree("Text", out textTree);
                DA.GetDataTree("Question", out questionTree);

                // The first defined tree is the one that overrides paths in case they don't match between trees
                this.inputTree["Text"] = textTree;
                this.inputTree["Question"] = questionTree;
            }

            public override async Task DoWorkAsync(CancellationToken token)
            {
                try
                {
                    Debug.WriteLine($"[Worker] Starting DoWorkAsync");
                    Debug.WriteLine($"[Worker] Input tree keys: {string.Join(", ", this.inputTree.Keys)}");
                    Debug.WriteLine($"[Worker] Input tree data counts: {string.Join(", ", this.inputTree.Select(kvp => $"{kvp.Key}: {kvp.Value.DataCount}"))}");

                    this.result = await DataTreeProcessor.RunFunctionAsync(
                        this.inputTree,
                        async (branches, reuseCount) =>
                        {
                            Debug.WriteLine($"[Worker] ProcessData called with {branches.Count} branches, reuse count: {reuseCount}");
                            return await ProcessData(branches, this.parent, reuseCount).ConfigureAwait(false);
                        },
                        onlyMatchingPaths: false,
                        groupIdenticalBranches: true,
                        token).ConfigureAwait(false);

                    Debug.WriteLine($"[Worker] Finished DoWorkAsync - Result keys: {string.Join(", ", this.result.Keys)}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Worker] Error: {ex.Message}");
                }
            }

            private static async Task<Dictionary<string, List<GH_Boolean>>> ProcessData(Dictionary<string, List<GH_String>> branches, AITextEvaluate parent, int reuseCount = 1)
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
                var textTree = branches["Text"];
                var questionTree = branches["Question"];

                // Normalize tree lengths
                var normalizedLists = DataTreeProcessor.NormalizeBranchLengths(new List<List<GH_String>> { textTree, questionTree });

                // Reassign normalized branches
                textTree = normalizedLists[0];
                questionTree = normalizedLists[1];

                Debug.WriteLine($"[ProcessData] After normalization - Text count: {textTree.Count}, Question count: {questionTree.Count}");

                // Initialize the output
                var outputs = new Dictionary<string, List<GH_Boolean>>();
                outputs["Result"] = new List<GH_Boolean>();

                // Iterate over the branches
                // For each item in the prompt tree, get the response from AI
                int i = 0;
                foreach (var text in textTree)
                {
                    Debug.WriteLine($"[ProcessData] Processing text {i + 1}/{textTree.Count}");

                    // Call the AI tool through the tool manager
                    var parameters = new JObject
                    {
                        ["text"] = textTree[i]?.Value,
                        ["question"] = questionTree[i]?.Value,
                        ["contextProviderFilter"] = "-environment,-time",
                        ["reuseCount"] = reuseCount,
                        ["provider"] = parent.GetActualProviderName(),
                        ["model"] = parent.GetModel()
                    };

                    var toolResult = await AIToolManager
                        .ExecuteTool("text_evaluate", parameters, null)
                        .ConfigureAwait(false) as JObject;

                    bool success = toolResult?["success"]?.ToObject<bool>() ?? false;
                    if (!success)
                    {
                        string errorMessage = toolResult?["error"]?.ToString() ?? "Unknown error occurred";
                        parent.SetPersistentRuntimeMessage("ai_error", GH_RuntimeMessageLevel.Error, errorMessage, false);
                        outputs["Result"].Add(null);
                        i++;
                        continue;
                    }

                    bool result = toolResult?["result"]?.ToObject<bool>() ?? false;
                    outputs["Result"].Add(new GH_Boolean(result));
                    i++;
                }

                return outputs;
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                Debug.WriteLine($"[Worker] Setting output - Available keys: {string.Join(", ", this.result.Keys)}");

                if (!this.result.TryGetValue("Result", out GH_Structure<GH_Boolean>? value))
                {
                    Debug.WriteLine("[Worker] Warning: Result key not found in output dictionary");
                    message = "Error: No result available";
                    return;
                }

                this.parent.SetPersistentOutput("Result", value, DA);
                message = "Done :)";
            }
        }
    }
}
