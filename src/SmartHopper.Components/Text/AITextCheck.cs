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
using Grasshopper.Documentation;
using System.Linq;

namespace SmartHopper.Components.Text
{
    public class AITextCheck : AIStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("D3EB06A8-C219-46E3-854E-15EC798AD63A");
        protected override System.Drawing.Bitmap Icon => Resources.textcheck;
        public override GH_Exposure Exposure => GH_Exposure.primary;

        public AITextCheck()
            : base("AI Text Check", "AITextCheck",
                  "Ask true or false questions agains a text using natural language.\nIf a tree structure is provided, prompts and instructions will only match within the same branch paths.",
                  "SmartHopper", "Text")
        {
        }

        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Text", "T", "The text to evaluate", GH_ParamAccess.tree);
            pManager.AddTextParameter("Question", "Q", "Ask a true or false question. The AI will answer it based on the input text", GH_ParamAccess.tree, string.Empty);
        }

        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Result", "R", "The AI's response", GH_ParamAccess.tree);
        }

        protected override string GetEndpoint()
        {
            return "text-check";
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new AITextCheckWorker(this, AddRuntimeMessage);
        }

        private class AITextCheckWorker : AsyncWorkerBase
        {
            private Dictionary<string, GH_Structure<GH_String>> _inputTree;
            private Dictionary<string, GH_Structure<GH_Boolean>> _result;
            private readonly AITextCheck _parent;

            public AITextCheckWorker(
            AITextCheck parent,
            Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
            : base(parent, addRuntimeMessage)
            {
                _parent = parent;
                _result = new Dictionary<string, GH_Structure<GH_Boolean>>
                {
                    { "Result", new GH_Structure<GH_Boolean>() }
                };
            }

            public override void GatherInput(IGH_DataAccess DA)
            {
                _inputTree = new Dictionary<string, GH_Structure<GH_String>>();

                // Get the input trees
                var textTree = new GH_Structure<GH_String>();
                var questionTree = new GH_Structure<GH_String>();

                DA.GetDataTree("Text", out textTree);
                DA.GetDataTree("Question", out questionTree);

                // The first defined tree is the one that overrides paths in case they don't match between trees
                _inputTree["Text"] = textTree;
                _inputTree["Question"] = questionTree;
            }

            public override async Task DoWorkAsync(CancellationToken token)
            {
                try
                {
                    Debug.WriteLine($"[Worker] Starting DoWorkAsync");
                    Debug.WriteLine($"[Worker] Input tree keys: {string.Join(", ", _inputTree.Keys)}");
                    Debug.WriteLine($"[Worker] Input tree data counts: {string.Join(", ", _inputTree.Select(kvp => $"{kvp.Key}: {kvp.Value.DataCount}"))}");

                    _result = await DataTreeProcessor.RunFunctionAsync<GH_String, GH_Boolean>(
                        _inputTree,
                        async branches => 
                        {
                            Debug.WriteLine($"[Worker] ProcessData called with {branches.Count} branches");
                            return await ProcessData(branches, _parent);
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

            private static async Task<Dictionary<string, List<GH_Boolean>>> ProcessData(Dictionary<string, List<GH_String>> branches, AITextCheck parent)
            {
                /*
                 * Inputs will be available as a dictionary
                 * of branches. No need to deal with paths.
                 *
                 * Outputs should be a dictionary where keys
                 * are each output parameter, and values are
                 * the output values.
                 */

                Debug.WriteLine($"[Worker] Processing {branches.Count} trees");
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

                    // Initiate the messages array
                    var messages = new List<KeyValuePair<string, string>>();

                    // Add the system prompt
                    messages.Add(new KeyValuePair<string, string>("system", "You are a text evaluator. Your task is to analyze a text and return a boolean value indicating whether the text matches the given criteria.\n\nRespond with TRUE or FALSE, nothing else.\n\nIn case the text does not match the criteria, respond with FALSE."));

                    // Add the user message
                    messages.Add(new KeyValuePair<string, string>("user", $"This is my question: \"{questionTree[i].Value}\"\n\nAnswer to the previous question on the following text:\n{textTree[i].Value}\n\n"));

                    var response = await parent.GetResponse(messages);

                    if (response.FinishReason == "error")
                    {
                        parent.AIErrorToPersistentRuntimeMessage(response);
                        outputs["Result"].Add(null);
                        i++;
                        continue;
                    }

                    var result = ParseBooleanFromResponse(response.Response);

                    if (result == null)
                    {
                        parent.SetPersistentRuntimeMessage("ai_error", GH_RuntimeMessageLevel.Error, $"The AI returned an invalid response:\n{response.Response}", false);
                        outputs["Result"].Add(null);
                        i++;
                        continue;
                    }
                    else
                    {
                        outputs["Result"].Add(new GH_Boolean(result ?? false));
                    }

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

            private static bool? ParseBooleanFromResponse(string response)
            {
                if (string.IsNullOrWhiteSpace(response)) return null;

                var lowerResponse = response.ToLowerInvariant();
                bool hasTrue = lowerResponse.Contains("true");
                bool hasFalse = lowerResponse.Contains("false");

                if (hasTrue && !hasFalse) return true;
                if (hasFalse && !hasTrue) return false;
                return null;
            }
        }
    }
}
