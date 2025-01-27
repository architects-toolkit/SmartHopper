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

namespace SmartHopper.Components.List
{
    public class AIListEvaluate : AIStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("A8BAD48D-8723-42AD-B13C-A875F940B69C");
        protected override System.Drawing.Bitmap Icon => Resources.listevaluate;
        public override GH_Exposure Exposure => GH_Exposure.primary;

        public AIListEvaluate()
            : base("AI List Evaluate", "AIListEvaluate",
                  "Evaluate a condition on a list using natural language questions.\nThis components takes the list as a whole. This means that every question will return True or False for each provided list. If a tree structure is provided, questions and lists will only match within the same branch paths.",
                  "SmartHopper", "List")
        {
        }

        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("List", "L", "List of elements to evaluate", GH_ParamAccess.tree);
            pManager.AddTextParameter("Prompt", "P", "Natural language question about the list.", GH_ParamAccess.tree);
        }

        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Result", "R", "Result of the evaluation", GH_ParamAccess.tree);
        }

        protected override string GetEndpoint()
        {
            return "list-evaluate";
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new AIListEvaluateWorker(this, AddRuntimeMessage);
        }

        private class AIListEvaluateWorker : AsyncWorkerBase
        {
            private Dictionary<string, GH_Structure<GH_String>> _inputTree;
            private Dictionary<string, GH_Structure<GH_Boolean>> _result;
            private readonly AIListEvaluate _parent;

            public AIListEvaluateWorker(
            AIListEvaluate parent,
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
                var listTree = new GH_Structure<IGH_Goo>();
                var promptTree = new GH_Structure<GH_String>();

                DA.GetDataTree("List", out listTree);
                DA.GetDataTree("Prompt", out promptTree);

                // Convert generic data to string structure
                var stringListTree = ConvertToGHString(listTree);

                // Store the converted trees
                _inputTree["List"] = stringListTree;
                _inputTree["Prompt"] = promptTree;
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

            private static async Task<Dictionary<string, List<GH_Boolean>>> ProcessData(Dictionary<string, List<GH_String>> branches, AIListEvaluate parent)
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
                var listTreeOriginal = branches["List"];
                var promptTree = branches["Prompt"];

                // Wrap list to JSON string
                var listTree = ConcatenateItems(listTreeOriginal);

                // Normalize tree lengths
                var normalizedLists = DataTreeProcessor.NormalizeBranchLengths(new List<List<GH_String>> { listTree, promptTree });

                // Reassign normalized branches
                listTree = normalizedLists[0];
                promptTree = normalizedLists[1];

                Debug.WriteLine($"[ProcessData] After normalization - Prompts count: {promptTree.Count}, List count: {listTree.Count}");

                // Initialize the output
                var outputs = new Dictionary<string, List<GH_Boolean>>();
                outputs["Result"] = new List<GH_Boolean>();

                // Iterate over the branches
                // For each item in the prompt tree, get the response from AI
                int i = 0;
                foreach (var prompt in promptTree)
                {
                    Debug.WriteLine($"[ProcessData] Processing prompt {i + 1}/{promptTree.Count}");

                    // Initiate the messages array
                    var messages = new List<KeyValuePair<string, string>>();

                    // Add the system prompt
                    messages.Add(new KeyValuePair<string, string>("system", "You are a list analyzer. Your task is to analyze a list of items and return a boolean value indicating whether the list matches the given criteria.\n\nThe list will be provided as a JSON dictionary where the key is the index and the value is the item.\n\nMainly you will base your answers on the item itself, unless the user asks for something regarding the position of items in the list.\n\nRespond with TRUE or FALSE, nothing else."));

                    // Add the user message
                    messages.Add(new KeyValuePair<string, string>("user", $"This is my question: \"{prompt.Value}\"\n\nAnswer to the previous question with the following list:\n{listTree[i].Value}\n\n"));

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

            private static List<GH_String> ConcatenateItems(List<GH_String> inputList)
            {
                var result = new List<GH_String>();

                var stringList = new List<string>();

                foreach (var item in inputList)
                {
                    stringList.Add(item.ToString());
                }

                var concatenatedString = "{" + string.Join(",", stringList.Select((value, index) => $"\"{index}\":\"{value}\"")) + "}"; // Dictionary format
                result.Add(new GH_String(concatenatedString));

                return result;
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
