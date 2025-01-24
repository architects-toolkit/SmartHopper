/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 * 
 * Portions of this code adapted from:
 * https://github.com/specklesystems/GrasshopperAsyncComponent
 * Apache License 2.0
 * Copyright (c) 2021 Speckle Systems
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
using Grasshopper.Documentation;
using System.Linq;

namespace SmartHopper.Components.Text
{
    public class AITextGenerate : AIStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("EB073C7A-A500-4265-A45B-B1BFB38BA58E");
        protected override System.Drawing.Bitmap Icon => Resources.textgenerate;
        public override GH_Exposure Exposure => GH_Exposure.primary;

        public AITextGenerate()
            : base("AI Text Generate", "AITextGenerate",
                  "Generate text using LLM.\nIf a tree structure is provided, prompts and instructions will only match within the same branch paths.",
                  "SmartHopper", "Text")
        {
        }

        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Prompt", "P", "The prompt to send to the AI", GH_ParamAccess.tree);
            pManager.AddTextParameter("Instructions", "I", "Specify what the AI should do when receiving the prompt", GH_ParamAccess.tree, string.Empty);
        }

        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Result", "R", "The AI's response", GH_ParamAccess.tree);
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
                _result = new Dictionary<string, GH_Structure<GH_String>>();
            }

            public override void GatherInput(IGH_DataAccess DA)
            {
                DA.GetDataTree(0, out GH_Structure<GH_String> promptTree);

                DA.GetDataTree(1, out GH_Structure<GH_String> instructionsTree);

                _inputTree = new Dictionary<string, GH_Structure<GH_String>>();
                _inputTree["Prompt"] = promptTree;
                _inputTree["Instructions"] = instructionsTree;
            }

            public override async Task DoWorkAsync(CancellationToken token)
            {
                _result = await DataTreeProcessor.RunFunctionAsync<GH_String>(
                    _inputTree,
                    async branches => await ProcessData(branches, _parent),
                    onlyMatchingPaths: false,
                    groupIdenticalBranches: true,
                    token);
            }

            private static async Task<Dictionary<string, List<GH_String>>> ProcessData(Dictionary<string, List<GH_String>> branches, AITextGenerate parent)
            {
                /*
                 * When defining the function, the inputs will
                 * be available as the branches dictionary.
                 *
                 * Outputs should be a dictionary where the keys
                 * are each output parameter and the values are
                 * the output values.
                 */

                // Get the branches
                var instructionsTree = branches["Instructions"];
                var promptTree = branches["Prompt"];

                // Normalize branch lengths
                var normalizedLists = DataTreeProcessor.NormalizeBranchLengths(new List<List<GH_String>> { instructionsTree, promptTree });

                // Reassign normalized branches
                instructionsTree = normalizedLists[0];
                promptTree = normalizedLists[1];

                // Initialize the output
                var outputs = new Dictionary<string, List<GH_String>>();
                outputs["Result"] = new List<GH_String>();

                // Iterate over the branches
                // For each item in the prompt tree, get the response from AI
                int i = 0;
                foreach (var prompt in promptTree)
                {
                    // Initiate the messages array
                    var messages = new List<KeyValuePair<string, string>>();

                    // Add system prompt if available
                    var systemPrompt = instructionsTree[i].Value;
                    if (!string.IsNullOrWhiteSpace(systemPrompt))
                    {
                        messages.Add(new KeyValuePair<string, string>("system", systemPrompt));
                    }

                    // Add the user prompt
                    messages.Add(new KeyValuePair<string, string>("user", prompt.Value));

                    var response = await parent.GetResponse(messages);

                    if (response.FinishReason == "error")
                    {
                        parent.SetPersistentRuntimeMessage("ai_error", GH_RuntimeMessageLevel.Error, $"AI error while processing the response:\n{response.Response}", false);
                        outputs["Result"].Add(new GH_String(string.Empty));
                        continue;
                    }

                    outputs["Result"].Add(new GH_String(response.Response));
                    i++;
                }

                return outputs;
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                _parent.SetPersistentOutput("Result", _result["Result"], DA);
                message = "Done :)";
            }
        }
    }
}
