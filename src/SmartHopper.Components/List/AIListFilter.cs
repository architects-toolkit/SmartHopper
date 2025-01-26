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
    public class AIListFilter : AIStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("CD2E5F8A-94D4-48D7-8E68-8185341245D0");
        protected override System.Drawing.Bitmap Icon => Resources.listfilter;
        public override GH_Exposure Exposure => GH_Exposure.primary;

        public AIListFilter()
            : base("AI List Filter", "AIListFilter",
                  "Filter and reorder a list of elements using natural language prompts. Each prompt will be processed seperately against each list. If a tree structure is provided, questions and lists will only match within the same branch paths.",
                  "SmartHopper", "List")
        {
        }

        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("List", "L", "List of elements to filter or reorder", GH_ParamAccess.tree);
            pManager.AddTextParameter("Prompt", "P", "Natural language prompt describing how to modify, filter, or reorder the list.", GH_ParamAccess.tree);
        }

        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Result", "R", "Processed list", GH_ParamAccess.tree);
        }

        protected override string GetEndpoint()
        {
            return "list-filter";
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new AIListFilterWorker(this, AddRuntimeMessage);
        }

        private class AIListFilterWorker : AsyncWorkerBase
        {
            private Dictionary<string, GH_Structure<GH_String>> _inputTree;
            private Dictionary<string, GH_Structure<GH_String>> _result;
            private readonly AIListFilter _parent;

            public AIListFilterWorker(
            AIListFilter parent,
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
                var listTree = new GH_Structure<IGH_Goo>();
                var promptTree = new GH_Structure<GH_String>();

                DA.GetDataTree("List", out listTree);
                DA.GetDataTree("Prompt", out promptTree);

                // Convert generic data to string structure
                var stringListTree = new GH_Structure<GH_String>();
                foreach (var path in listTree.Paths)
                {
                    var branch = listTree.get_Branch(path);
                    var stringBranch = new List<GH_String>();
                    foreach (var item in branch)
                    {
                        stringBranch.Add(new GH_String(item.ToString()));
                    }
                    stringListTree.AppendRange(stringBranch, path);
                }

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

                    _result = await DataTreeProcessor.RunFunctionAsync<GH_String>(
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

            private static async Task<Dictionary<string, List<GH_String>>> ProcessData(Dictionary<string, List<GH_String>> branches, AIListFilter parent)
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
                var listTree = branches["List"];
                var promptTree = branches["Prompt"];

                // Wrap list to JSON string
                listTree = ConcatenateItems(listTree);

                // Normalize tree lengths
                var normalizedLists = DataTreeProcessor.NormalizeBranchLengths(new List<List<GH_String>> { listTree, promptTree });

                // Reassign normalized branches
                listTree = normalizedLists[0];
                promptTree = normalizedLists[1];

                Debug.WriteLine($"[ProcessData] After normalization - Prompts count: {promptTree.Count}, List count: {listTree.Count}");

                // Initialize the output
                var outputs = new Dictionary<string, List<GH_String>>();
                outputs["Result"] = new List<GH_String>();

                // Iterate over the branches
                // For each item in the prompt tree, get the response from AI
                int i = 0;
                foreach (var prompt in promptTree)
                {
                    Debug.WriteLine($"[ProcessData] Processing prompt {i + 1}/{promptTree.Count}");

                    // Initiate the messages array
                    var messages = new List<KeyValuePair<string, string>>();

                    // Add the system prompt
                    messages.Add(new KeyValuePair<string, string>("system", "You are a list processor assistant. Your task is to analyze a list of items and return the indices of items that match the given criteria.\n\nThe list will be provided as a JSON dictionary where the key is the index and the value is the item.\n\nYou can be asked to:\n- Reorder the list (return the same number of indices in a different order)\n- Filter the list (return less items than the original list)\n- Repeat some items (return some indices multiple times)\n- Shuffle the list (return a random order of indices)\n- Combination of the above\n\nReturn ONLY the comma-separated indices of the selected items in the final order."));

                    // Add the user message
                    messages.Add(new KeyValuePair<string, string>("user", $"Given this list of items:\n{listTree[i].Value}\n\nReturn the indices of items that match the following prompt: {prompt.Value}\n\nRespond with ONLY the comma-separated indices to be returned."));

                    var response = await parent.GetResponse(messages);

                    if (response.FinishReason == "error")
                    {
                        parent.SetPersistentRuntimeMessage("ai_error", GH_RuntimeMessageLevel.Error, $"AI error while processing the response:\n{response.Response}", false);
                        outputs["Result"].Add(new GH_String(string.Empty));
                        continue;
                    }

                    var indices = ParseIndicesFromResponse(response.Response);

                    var result = new List<GH_String>();
                    foreach (var index in indices)
                    {
                        result.Add(listTree[index]);
                    }

                    outputs["Result"].AddRange(result);
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

            private static List<int> ParseIndicesFromResponse(string response)
            {
                var indices = new List<int>();
                var parts = response.Split(',');
                foreach (var part in parts)
                {
                    if (int.TryParse(part.Trim(), out int index))
                    {
                        indices.Add(index);
                    }
                }
                return indices;
            }
        }
    }
}
