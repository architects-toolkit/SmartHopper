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
    public class AIListCheck : AIStatefulComponentBase
    {
        private GH_Structure<GH_Boolean> lastResult = null;
        private int branches_input = 0;
        private IGH_DataAccess DA;

        protected override string GetEndpoint()
        {
            return "list-check";
        }

        public AIListCheck()
            : base("AI List Check", "AIListCheck",
                "Check a condition on a list using natural language questions. This components takes the list as a whole. This means that every question will return True or False for each provided list. If a tree structure is provided, questions and lists will only match within the same branch paths.",
                "SmartHopper", "List")
        {
        }

        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Question", "Q", "Natural language question about the list", GH_ParamAccess.tree);
            pManager.AddGenericParameter("List", "L", "List of elements to check", GH_ParamAccess.tree);
        }

        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Result", "R", "Boolean result based on the question", GH_ParamAccess.tree);
        }

        public override Guid ComponentGuid => new Guid("B5F8C492-E6F3-4D9D-BF7A-2C4E9D3C8F1F");

        protected override System.Drawing.Bitmap Icon => Resources.listcheck;

        protected override AsyncWorker CreateWorker(Action<string> progressReporter)
        {
            Debug.WriteLine("[AIListCheck] Creating new worker");
            var worker = new AIListCheckWorker(progressReporter, this, AddRuntimeMessage, DA);
            Debug.WriteLine($"[AIListCheck] Worker created: {(worker == null ? "null" : "initialized")}");
            return worker;
        }

        private bool? ParseBooleanResponse(string response)
        {
            if (string.IsNullOrWhiteSpace(response)) return null;

            var lowerResponse = response.ToLowerInvariant();
            bool hasTrue = lowerResponse.Contains("true");
            bool hasFalse = lowerResponse.Contains("false");

            if (hasTrue && !hasFalse) return true;
            if (hasFalse && !hasTrue) return false;
            return null;
        }

        protected override bool ProcessFinalResponse(IGH_DataAccess DA)
        {
            Debug.WriteLine("[AIListCheck] ProcessAIResponse - Start");

            // Get the worker's processed response tree
            var worker = (AIListCheckWorker)CurrentWorker;
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
        }

        private class AIListCheckWorker : AIWorkerBase
        {
            private GH_Structure<IGH_Goo> inputTree;
            private GH_Structure<GH_String> questionTree;
            internal GH_Structure<GH_Boolean> result;
            private readonly AIListCheck _parentListCheck;

            public AIListCheckWorker(AIListCheck parent)
                : this(null, parent, null, null)
            {
            }

            public AIListCheckWorker(Action<string> progressReporter, AIListCheck parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage, IGH_DataAccess dataAccess)
    : base(progressReporter, parent, addRuntimeMessage)
            {
                Debug.WriteLine($"[AITextGenerateWorker] Constructor - DataAccess is null? {dataAccess == null}");
                _parentListCheck = parent;
            }

            public override void GatherInput(IGH_DataAccess DA)
            {
                Debug.WriteLine($"[AITextGenerateWorker] GatherInput - Start. DA is null? {DA == null}");
                base.GatherInput(DA);

                // Get instructions tree
                inputTree = new GH_Structure<IGH_Goo>();
                if (!DA.GetDataTree("List", out inputTree))
                {
                    Debug.WriteLine("[AITextGenerateWorker] GatherInput - Failed to get list tree");
                    _parentListCheck.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Failed to get list data");
                    return;
                }
                Debug.WriteLine($"[GatherInput] Got input tree with {inputTree?.DataCount ?? 0} items");

                // Concatenate all items from each tree as comma-separated string
                inputTree = ConcatenateItemsPerBranch(inputTree);

                // Get questions tree
                questionTree = new GH_Structure<GH_String>();
                if (!DA.GetDataTree("Question", out questionTree))
                {
                    Debug.WriteLine("[AITextGenerateWorker] GatherInput - Failed to get question tree");
                    _parentListCheck.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Failed to get question data");
                    return;
                }
                Debug.WriteLine($"[GatherInput] Got question tree with {questionTree?.DataCount ?? 0} questions");

                if (questionTree == null || questionTree.DataCount == 0)
                {
                    throw new ArgumentException("No questions provided");
                }
            }

            private GH_Structure<IGH_Goo> ConcatenateItemsPerBranch(GH_Structure<IGH_Goo> inputTree)
            {
                var result = new GH_Structure<IGH_Goo>();

                foreach (var path in inputTree.Paths)
                {
                    var branch = inputTree.get_Branch(path);

                    var stringList = new List<string>();

                    foreach (var item in branch)
                    {
                        stringList.Add(item.ToString());
                    }

                    var concatenatedString = "- " + string.Join("\n- ", stringList);

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
                        ReportWarning("Input tree is empty - no items to check");
                        Debug.WriteLine("[AIListCheckWorker] DoWorkAsync - Empty input tree");
                        return;
                    }

                    if (questionTree == null || questionTree.DataCount == 0)
                    {
                        ReportWarning("No questions provided - nothing to check");
                        Debug.WriteLine("[AIListCheckWorker] DoWorkAsync - Empty questions");
                        return;
                    }

                    Debug.WriteLine($"[AIListCheckWorker] Input tree paths: {inputTree.Paths?.Count ?? 0}, DataCount: {inputTree.DataCount}");

                    // Create local copies of trees to avoid thread safety issues
                    var localInputTree = new GH_Structure<GH_String>();
                    var localQuestionTree = new GH_Structure<GH_String>();

                    foreach (var path in inputTree.Paths)
                    {
                        var branch = inputTree.get_Branch(path);
                        foreach (var item in branch)
                        {
                            localInputTree.Append(item as GH_String, path);
                        }
                    }

                    foreach (var path in questionTree.Paths)
                    {
                        var branch = questionTree.get_Branch(path);
                        foreach (var item in branch)
                        {
                            localQuestionTree.Append(item as GH_String, path);
                        }
                    }

                    // Process trees using MultiTreeProcessor
                    var processedResult = await MultiTreeProcessor.ProcessTreesInParallel<GH_Boolean>(
                        new GH_Structure<GH_String>[] { localInputTree, localQuestionTree },
                        ProcessBranch,
                        onlyMatchingPaths: false,  // Process all unique paths
                        groupIdenticalBranches: true,  // Group or not?
                        token);

                    result = new GH_Structure<GH_Boolean>();
                    foreach (var path in processedResult.Paths)
                    {
                        var branch = processedResult.get_Branch(path);
                        result.AppendRange(branch.Cast<GH_Boolean>(), path);
                    }

                    // Store branches count to parent component
                    _parentListCheck.branches_input = processedResult.Paths.Count;

                    OnWorkCompleted();
                }
                catch (Exception ex)
                {
                    ReportError($"Error in DoWorkAsync: {ex.Message}");
                    Debug.WriteLine($"[AIListCheckWorker] DoWorkAsync - Error: {ex}");
                }
            }

            protected async Task<List<GH_Boolean>> ProcessBranch(Dictionary<GH_Structure<GH_String>, List<GH_String>> branches, GH_Path path, CancellationToken ct)
            {
                Debug.WriteLine($"[ProcessBranch] Processing branch at path: {path}");
                Debug.WriteLine($"[ProcessBranch] Branches count: {branches?.Count}");

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
                            string questionList = string.Empty;

                            // Get all items from first tree
                            if (items.Values.Any())
                            {
                                if (items.Values.ElementAt(0) != null)
                                {
                                    itemsList = items.Values.ElementAt(0).Value;
                                    Debug.WriteLine($"[ProcessBranch] Processing items list: {itemsList}");
                                }
                            }
                            else
                            {
                                Debug.WriteLine("[ProcessBranch] Items are not defined for path: " + path);
                            }

                            // Get questions from second tree
                            if (items.Values.Count() > 1)
                            {
                                var questions = items.Values.ElementAt(1);
                                if (questions != null)
                                {
                                    questionList = questions.Value;
                                    Debug.WriteLine($"[ProcessBranch] Got questions: {questionList}");
                                }
                            }
                            else
                            {
                                Debug.WriteLine("[ProcessBranch] Questions are not defined for path: " + path);
                            }

                            var result = await ProcessAIResponse(itemsList, questionList, ct);
                            var boolResult = result.Select(r =>
                            {
                                var boolValue = _parentListCheck.ParseBooleanResponse(r.ToString());
                                return new GH_Boolean(boolValue ?? false);
                            });
                            return boolResult.ToList();
                        },
                        ct);

                    return processedItems.SelectMany(x => x).ToList();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ProcessBranch] Error: {ex.Message}");
                    Debug.WriteLine($"[ProcessBranch] Stack trace: {ex.StackTrace}");
                    return new List<GH_Boolean>();
                }
            }

            protected override async Task<List<IGH_Goo>> ProcessAIResponse(string list, string question, CancellationToken ct)
            {
                var messages = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("system", "You are a helpful assistant that analyzes lists. You will be given a list of items and a question about the list. Your task is to return true or false based on the question. Respond with ONLY true or false."),
                    new KeyValuePair<string, string>("user", $"Here is the list of items:\n{list}\n\nQuestion: {question}\n\nRespond with ONLY true or false.")
                };

                var response = await GetResponse(messages, ct);

                if (ct.IsCancellationRequested) return new List<IGH_Goo> { new GH_Boolean(false) };

                if (response?.FinishReason == "error")
                {
                    ReportError($"AI error: {response.Response}");
                    return new List<IGH_Goo> { new GH_Boolean(false) };
                }

                var result = _parentListCheck.ParseBooleanResponse(response.Response);
                if (!result.HasValue)
                {
                    ReportError($"Could not determine boolean value from response: {response.Response}");
                    return new List<IGH_Goo> { new GH_Boolean(false) };
                }

                return new List<IGH_Goo> { new GH_Boolean(result.Value) };
            }

            public override void SetOutput(IGH_DataAccess DA, out string doneMessage)
            {
                Debug.WriteLine($"[SetOutput] Starting with result DataCount: {result?.DataCount ?? 0}");
                doneMessage = null;

                if (result != null)
                {
                    _parentListCheck.ProcessFinalResponse(DA);
                }
            }
        }
    }
}
