/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using SmartHopper.Components.Properties;
using SmartHopper.Config.Models;
using SmartHopper.Core.Async.Components;
using SmartHopper.Core.Async.Workers;
using SmartHopper.Core.DataTree;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SmartHopper.Components.List
{
    public class AIListFilter : AIStatefulComponentBase
    {
        private GH_Structure<IGH_Goo> lastResult = null;
        private int branches_input = 0;
        private int branches_processed = 0;
        private IGH_DataAccess DA;

        public string GetEndpoint()
        {
            return "list-filter";
        }

        public AIListFilter()
            : base("AI List Filter", "AIListFilter",
                "Modify, filter, or reorder a list of elements using natural language prompts. Each prompt will be processed seperately against each list. If a tree structure is provided, questions and lists will only match within the same branch paths.",
                "SmartHopper", "List")
        {
        }

        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Prompt", "P", "Natural language prompt describing how to modify, filter, or reorder the list.", GH_ParamAccess.tree);
            pManager.AddGenericParameter("List", "L", "List of elements to filter", GH_ParamAccess.tree);
        }

        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Result", "L", "Filtered list based on the prompt", GH_ParamAccess.tree);
        }

        public override Guid ComponentGuid => new Guid("A4F7B391-D5E2-4C8D-9F6A-1B3E8D2C7F0E");

        protected override System.Drawing.Bitmap Icon => Resources.listfilter;

        protected override string GetPrompt(IGH_DataAccess DA)
        {
            // We'll handle prompts directly in ProcessAIResponse
            return null;
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

        protected override AsyncWorker CreateWorker(Action<string> progressReporter)
        {
            Debug.WriteLine("[AIListFilter] Creating new worker");
            var worker = new AIListFilterWorker(progressReporter, this, AddRuntimeMessage, DA);
            Debug.WriteLine($"[AIListFilter] Worker created: {(worker == null ? "null" : "initialized")}");
            return worker;
        }

        protected override bool ProcessFinalResponse(AIResponse response, IGH_DataAccess DA)
        {
            Debug.WriteLine("[AIListFilter] ProcessAIResponse - Start");
            Debug.WriteLine($"[AIListFilter] Response: {(response == null ? "null" : "not null")}");

            if (response == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No response received from AI");
                Debug.WriteLine("[AIListFilter] ProcessAIResponse - Error: Null response");
                return false;
            }

            if (response.FinishReason == "error")
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Error: {response.Response}");
                Debug.WriteLine("[AIListFilter] ProcessAIResponse - Error: " + response.Response);
                return false;
            }

            // Get the worker's processed response tree
            var worker = (AIListFilterWorker)CurrentWorker;
            if (worker?.result != null)
            {
                lastResult = worker.result;
                DA.SetDataTree(0, lastResult);
                SetMetricsOutput(DA, branches_input);
                RestoreMetrics();
                return true;
            }

            RestoreMetrics();
            return false;
        }

        protected void RestoreMetrics()
        {
            branches_input = 0;
            branches_processed = 0;
        }

        private class AIListFilterWorker : AIWorkerBase
        {
            private GH_Structure<IGH_Goo> inputTree;
            private GH_Structure<GH_String> promptTree;
            internal GH_Structure<IGH_Goo> result;
            private readonly IGH_DataAccess _dataAccess;
            //private AIResponse _lastAIResponse;

            public AIListFilterWorker(Action<string> progressReporter, AIListFilter parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage, IGH_DataAccess dataAccess)
                : base(progressReporter, parent, addRuntimeMessage)
            {
                Debug.WriteLine($"[AIListFilterWorker] Constructor - DataAccess is null? {dataAccess == null}");
                _dataAccess = dataAccess;
            }

            private AIListFilter ParentComponent => (AIListFilter)_parent;

            public override void GatherInput(IGH_DataAccess DA, GH_ComponentParamServer p)
            {
                Debug.WriteLine($"[AIListFilterWorker] GatherInput - Start. DA is null? {DA == null}");
                base.GatherInput(DA, p);

                // Get input tree
                inputTree = new GH_Structure<IGH_Goo>();
                if (!DA.GetDataTree("List", out inputTree))
                {
                    Debug.WriteLine("[AIListFilterWorker] GatherInput - Failed to get list tree");
                    ParentComponent.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Failed to get list data");
                    return;
                }
                Debug.WriteLine($"[GatherInput] Got input tree with {inputTree?.DataCount ?? 0} items");

                // Get prompt tree
                promptTree = new GH_Structure<GH_String>();
                if (!DA.GetDataTree("Prompt", out promptTree))
                {
                    Debug.WriteLine("[AIListFilterWorker] GatherInput - Failed to get prompt tree");
                    ParentComponent.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Failed to get prompt data");
                    return;
                }
                Debug.WriteLine($"[GatherInput] Got prompt tree with {promptTree?.DataCount ?? 0} prompts");

                if (promptTree == null || promptTree.DataCount == 0)
                {
                    throw new ArgumentException("No prompts provided");
                }
            }

            private string ConcatenateItems(IEnumerable<GH_String> items)
            {
                if (items == null) return string.Empty;
                var stringList = items
                    .Where(item => item != null)
                    .Select(item => item.Value ?? "");
                return "[" + string.Join(",", stringList) + "]";
            }

            private static GH_Structure<GH_String> ConcatenateItemsPerBranch(GH_Structure<GH_String> inputTree)
            {
                var result = new GH_Structure<GH_String>();

                foreach (var path in inputTree.Paths)
                {
                    var branch = inputTree.get_Branch(path);
                    var stringList = new List<string>();

                    foreach (var item in branch)
                    {
                        stringList.Add(item.ToString());
                    }

                    var concatenatedString = "{" + string.Join(",", stringList.Select((value, index) => $"\"{index}\":\"{value}\"")) + "}"; // Dictionary format
                    //var concatenatedString = "[" + string.Join(",", stringList) + "]"; // Array format
                    result.Append(new GH_String(concatenatedString), path);
                }

                return result;
            }

            public override async Task DoWorkAsync(CancellationToken token)
            {
                Debug.WriteLine("\n[DoWorkAsync] STARTING EXECUTION ----------------------------------------");

                try
                {
                    if (inputTree == null || inputTree.DataCount == 0)
                    {
                        ReportWarning("Input tree is empty - no items to filter");
                        Debug.WriteLine("[AIListFilterWorker] DoWorkAsync - Empty input tree");
                        return;
                    }

                    if (promptTree == null || promptTree.DataCount == 0)
                    {
                        ReportWarning("No prompts provided - nothing to filter");
                        Debug.WriteLine("[AIListFilterWorker] DoWorkAsync - Empty prompts");
                        return;
                    }

                    Debug.WriteLine($"[AIListFilterWorker] Input tree paths: {inputTree.Paths?.Count ?? 0}, DataCount: {inputTree.DataCount}");

                    // Create local copies of trees to avoid thread safety issues
                    var localInputTree = new GH_Structure<GH_String>();
                    var localPromptTree = new GH_Structure<GH_String>();

                    foreach (var path in inputTree.Paths)
                    {
                        var branch = inputTree.get_Branch(path);
                        foreach (var item in branch)
                        {
                            if (item != null)
                            {
                                localInputTree.Append(new GH_String(item.ToString()), path);
                            }
                        }
                    }

                    localInputTree = ConcatenateItemsPerBranch(localInputTree);

                    foreach (var path in promptTree.Paths)
                    {
                        var branch = promptTree.get_Branch(path);
                        foreach (var item in branch)
                        {
                            localPromptTree.Append(item as GH_String, path);
                        }
                    }

                    // Process trees using MultiTreeProcessor
                    var processedResult = await MultiTreeProcessor.ProcessTreesInParallel<GH_Integer>(
                        new GH_Structure<GH_String>[] { localInputTree, localPromptTree },
                        ProcessBranch,
                        onlyMatchingPaths: false,
                        groupIdenticalBranches: true,
                        token);


                    var filteredResult = new GH_Structure<IGH_Goo>();

                    // Handle potential path mismatches or one-to-many/many-to-one cases
                    var allPaths = DataTreeProcessor.GetAllUniquePaths(new List<GH_Structure<IGH_Goo>> { inputTree, processedResult.DuplicateCast<IGH_Goo>(item => item) });

                    foreach (var path in allPaths)
                    {
                        var indicesBranch = DataTreeProcessor.GetBranchFromTree(processedResult, path);
                        var inputBranch = DataTreeProcessor.GetBranchFromTree(inputTree, path);

                        if (indicesBranch == null || indicesBranch.Count == 0)
                        {
                            // Handle case where processedResult doesn't have this path
                            continue;
                        }

                        if (inputBranch == null || inputBranch.Count == 0)
                        {
                            // Handle case where inputTree doesn't have this path
                            continue;
                        }

                        for (int i = 0; i < indicesBranch.Count; i++)
                        {
                            var idx = indicesBranch[i] as GH_Integer;
                            if (idx != null && idx.Value >= 0 && idx.Value < inputBranch.Count)
                            {
                                var item = inputBranch[idx.Value] as IGH_Goo;
                                if (item != null)
                                {
                                    //var newPath = new GH_Path(path.Indices.Concat(new[] { i }).ToArray());
                                    filteredResult.Append(item, path);
                                }
                            }
                        }
                    }

                    result = filteredResult;

                    // Store branches count to parent component
                    ParentComponent.branches_input = processedResult.Paths.Count;

                    OnWorkCompleted();
                }
                catch (Exception ex)
                {
                    ReportError($"Error in DoWorkAsync: {ex.Message}");
                    Debug.WriteLine($"[AIListFilterWorker] DoWorkAsync - Error: {ex}");
                }
            }

            protected async Task<List<GH_Integer>> ProcessBranch(Dictionary<GH_Structure<GH_String>, List<GH_String>> branches, GH_Path path, CancellationToken ct)
            {
                Debug.WriteLine($"[ProcessBranch] Processing branch at path: {path}");
                Debug.WriteLine($"[ProcessBranch] Branches count: {branches?.Count}");

                // Store branches count to parent component
                ParentComponent.branches_processed += 1;

                try
                {
                    var processedItems = await BranchProcessor.ProcessItemsInParallelAsync(
                        branches,
                        async items =>
                        {
                            Debug.WriteLine($"[ProcessBranch] Processing items. Items count: {items?.Count}");
                            if (items != null)
                            {
                                Debug.WriteLine($"[ProcessBranch] Values count: {items.Values?.Count()}");
                            }

                            string itemsList = string.Empty;
                            string promptList = string.Empty;

                            // Get all items from first tree (input items)
                            if (items.Values.Any())
                            {
                                var firstBranch = items.Values.ElementAt(0);
                                if (firstBranch != null)
                                {
                                    itemsList = firstBranch.Value;
                                    Debug.WriteLine($"[ProcessBranch] First branch items: {itemsList}");
                                }
                            }

                            // Get all items from second tree (prompts)
                            if (items.Values.Count() > 1)
                            {
                                promptList = items.Values.ElementAt(1)?.ToString() ?? string.Empty;
                            }

                            Debug.WriteLine($"[ProcessBranch] Items: {itemsList}");
                            Debug.WriteLine($"[ProcessBranch] Prompt: {promptList}");

                            // Get response from AI
                            var response = await GetAIResponse(itemsList, promptList, ct);
                            if (response == null)
                            {
                                Debug.WriteLine("[ProcessBranch] Null response from AI");
                                return new List<GH_Integer>();
                            }

                            // Parse indices from response
                            var indices = AIListFilter.ParseIndicesFromResponse(response.Response);
                            if (!indices.Any())
                            {
                                Debug.WriteLine("[ProcessBranch] No valid indices found in response");
                                return new List<GH_Integer>();
                            }

                            return indices.Select(i => new GH_Integer(i)).ToList();
                        },
                        ct);

                    return processedItems.SelectMany(x => x).ToList();
                }
                catch (Exception ex)
                {
                    ReportError($"Error processing branch: {ex.Message}");
                    Debug.WriteLine($"[ProcessBranch] Error: {ex}");
                    return new List<GH_Integer>();
                }
            }

            private async Task<AIResponse> GetAIResponse(string items, string prompt, CancellationToken ct)
            {
                var messages = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("system", "You are a list filtering assistant. Your task is to analyze a list of items and return the indices (0-based) of items that match the given criteria. Return ONLY the comma-separated indices, nothing else."),
                    new KeyValuePair<string, string>("user", $"Given this list:\n{items}\n\nReturn the indices of items that match the following prompt: {prompt}\n\nRespond with ONLY the comma-separated indices of matching items.")
                };

                return await GetResponse(messages, ct);
            }

            protected override async Task<List<IGH_Goo>> ProcessAIResponse(string list, string prompt, CancellationToken ct)
            {
                var response = await GetAIResponse(list, prompt, ct);
                _lastAIResponse = response;

                if (ct.IsCancellationRequested) return new List<IGH_Goo>();

                if (response?.FinishReason == "error")
                {
                    ReportError($"AI error: {response.Response}");
                    return new List<IGH_Goo>();
                }

                var indices = AIListFilter.ParseIndicesFromResponse(response.Response);
                if (!indices.Any())
                {
                    ReportError($"Could not determine indices from response: {response.Response}");
                    return new List<IGH_Goo>();
                }

                var items = list.Split('\n');
                return indices
                    .Where(i => i >= 0 && i < items.Length)
                    .Select(i => new GH_String(items[i].Trim()))
                    .Cast<IGH_Goo>()
                    .ToList();
            }

            public override void SetOutput(IGH_DataAccess DA, out string doneMessage)
            {
                Debug.WriteLine($"[SetOutput] Starting with result DataCount: {result?.DataCount ?? 0}");
                doneMessage = null;

                if (result != null && _lastAIResponse != null)
                {
                    Debug.WriteLine($"[SetOutput] Processing AI response with metrics. InTokens: {_lastAIResponse.InTokens}, OutTokens: {_lastAIResponse.OutTokens}");
                    ParentComponent.ProcessFinalResponse(_lastAIResponse, DA);
                }
            }
        }
    }
}
