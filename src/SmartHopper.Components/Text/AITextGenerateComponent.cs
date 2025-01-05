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
using SmartHopper.Config.Interfaces;
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

namespace SmartHopper.Components.Text
{
    public class AITextGenerate : AIStatefulComponentBase, IEndpointProvider
    {
        private GH_Structure<GH_String> lastResponse = null;
        private int branches_input = 0;
        private int branches_processed = 0;
        private IGH_DataAccess DA;

        public string GetEndpoint()
        {
            return "text-generate";
        }

        public AITextGenerate()
          : base("AI Text Generator", "AITextGenerate", "Generate text using LLM. If a tree structure is provided, prompts and instructions will only match within the same branch paths.", "SmartHopper", "Text")
        {
        }

        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Prompt", "P", "The prompt to send to the AI", GH_ParamAccess.tree);
            pManager.AddTextParameter("Instructions", "I", "Specify what the AI should do when receiving the prompt", GH_ParamAccess.tree, string.Empty);
        }

        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Result", "R", "The AI's response", GH_ParamAccess.tree);
        }

        public override Guid ComponentGuid => new Guid("A4F7B391-D5E2-4C8D-9F6A-1B3E8D2C7F0D");

        protected override System.Drawing.Bitmap Icon => Resources.textgenerate;

        protected override string GetPrompt(IGH_DataAccess DA)
        {
            this.DA = DA;

            // Get the prompt tree
            GH_Structure<GH_String> promptTree = new GH_Structure<GH_String>();
            if (!DA.GetDataTree("Prompt", out promptTree))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Failed to get prompt data");
                return null;
            }

            // Combine all prompts from the tree
            var combinedPrompt = string.Join("\n",
                promptTree.AllData(true)
                    .Where(p => p != null)
                    .Select(p => ((GH_String)p).Value));

            return combinedPrompt;
            //return null;
        }

        protected override bool ProcessFinalResponse(AIResponse response, IGH_DataAccess DA)
        {
            Debug.WriteLine("[AITextGenerate] ProcessAIResponse - Start");

            if (response == null || response.FinishReason == "error")
            {
                Debug.WriteLine($"[AITextGenerate] ProcessAIResponse - Response null? {response == null}, Finish reason: {response?.FinishReason}");
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Error in AI response: {response?.Response ?? "No response"}");
                Debug.WriteLine("[AITextGenerate] ProcessAIResponse - Error: Null response or error finish reason");
                return false;
            }

            Debug.WriteLine($"[AITextGenerate] ProcessAIResponse - Response received. InTokens: {response.InTokens}, OutTokens: {response.OutTokens}, Model: {response.Model}");

            // Get the worker's processed response tree
            var worker = (AITextGenerateWorker)CurrentWorker;
            if (worker?.response != null)
            {
                lastResponse = worker.response;
                DA.SetDataTree(0, lastResponse);
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

        protected override AsyncWorker CreateWorker(Action<string> progressReporter)
        {
            Debug.WriteLine($"[AITextGenerate] CreateWorker - CurrentDataAccess is null? {DA == null}");
            return new AITextGenerateWorker(progressReporter, this, AddRuntimeMessage, DA);
        }

        protected class AITextGenerateWorker : AIWorkerBase
        {
            private GH_Structure<GH_String> instructionsTree;
            private GH_Structure<GH_String> promptTree;
            internal GH_Structure<GH_String> response;
            private readonly IGH_DataAccess _dataAccess;
            //private AIResponse _lastAIResponse;

            public AITextGenerateWorker(AITextGenerate parent)
                : this(null, parent, null, null)
            {
            }

            public AITextGenerateWorker(Action<string> progressReporter, AITextGenerate parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage, IGH_DataAccess dataAccess)
                : base(progressReporter, parent, addRuntimeMessage)
            {
                Debug.WriteLine($"[AITextGenerateWorker] Constructor - DataAccess is null? {dataAccess == null}");
                _dataAccess = dataAccess;
                response = parent.lastResponse;
            }

            private AITextGenerate ParentComponent => (AITextGenerate)_parent;

            public override void GatherInput(IGH_DataAccess DA)
            {
                Debug.WriteLine($"[AITextGenerateWorker] GatherInput - Start. DA is null? {DA == null}");
                base.GatherInput(DA);

                try
                {
                    Debug.WriteLine("[AITextGenerateWorker] GatherInput - Getting instructions tree");
                    // Get instructions tree
                    instructionsTree = new GH_Structure<GH_String>();
                    if (!DA.GetDataTree(1, out instructionsTree))
                    {
                        Debug.WriteLine("[AITextGenerateWorker] GatherInput - Failed to get instructions tree");
                        ParentComponent.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Failed to get instructions data");
                        return;
                    }

                    Debug.WriteLine("[AITextGenerateWorker] GatherInput - Getting prompt tree");
                    // Get prompt tree
                    promptTree = new GH_Structure<GH_String>();
                    if (!DA.GetDataTree(0, out promptTree))
                    {
                        Debug.WriteLine("[AITextGenerateWorker] GatherInput - Failed to get prompt tree");
                        ParentComponent.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Failed to get prompt data");
                        return;
                    }

                    // Validate trees have data
                    if (promptTree.DataCount == 0)
                    {
                        Debug.WriteLine("[AITextGenerateWorker] GatherInput - Prompt tree is empty");
                        ParentComponent.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Prompt tree is empty");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AITextGenerateWorker] GatherInput - Exception: {ex.Message}\n{ex.StackTrace}");
                    ParentComponent.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Error gathering input: {ex.Message}");
                }
            }

            protected async Task<List<GH_String>> ProcessBranch(Dictionary<GH_Structure<GH_String>, List<GH_String>> branches, GH_Path path, CancellationToken ct)
            {
                Debug.WriteLine($"[AITextGenerateWorker] ProcessBranch - Start. Path: {path}");
                Debug.WriteLine($"[AITextGenerateWorker] ProcessBranch - Branches count: {branches?.Count}");

                // Store branches count to parent component
                ParentComponent.branches_processed += 1;

                //if (branches != null)
                //{
                //    foreach (var kvp in branches)
                //    {
                //        Debug.WriteLine($"[AITextGenerateWorker] ProcessBranch - Branch values count: {kvp.Value?.Count}");
                //    }
                //}

                try
                {
                    // Process each pair of instruction/prompt items in parallel
                    var processedItems = await BranchProcessor.ProcessItemsInParallelAsync(
                        branches,
                        async items =>
                        {
                            Debug.WriteLine($"[AITextGenerateWorker] ProcessBranch - Processing items. Items count: {items?.Count}");
                            if (items != null)
                            {
                                Debug.WriteLine($"[AITextGenerateWorker] ProcessBranch - Values count: {items.Values?.Count()}");
                            }

                            string systemPrompt = string.Empty;
                            string userPrompt = string.Empty;

                            // Get instruction from first tree if available (the system instructions)
                            if (items.Values.Any())
                            {
                                var instruction = items.Values.ElementAt(0);
                                if (instruction != null)
                                {
                                    systemPrompt = instruction.Value;
                                    Debug.WriteLine($"[AITextGenerateWorker] ProcessBranch - Got instruction: {systemPrompt}");
                                }
                            }
                            else
                            {
                                Debug.WriteLine("[AITextGenerateWorker] ProcessBranch - Instruction is not defined for path: " + path);
                            }

                            // Get prompt from second tree if available (the user prompt)
                            if (items.Values.Count() > 1)
                            {
                                var prompt = items.Values.ElementAt(1);
                                if (prompt != null)
                                {
                                    userPrompt = prompt.Value;
                                    Debug.WriteLine($"[AITextGenerateWorker] ProcessBranch - Got prompt: {userPrompt}");
                                }
                            }
                            else
                            {
                                Debug.WriteLine("[AITextGenerateWorker] ProcessBranch - Prompt is not defined for path: " + path);
                            }

                            var result = await ProcessAIResponse(userPrompt, systemPrompt, ct);
                            return result.Cast<GH_String>().ToList();
                        },
                        ct);

                    // Flatten the list of lists into a single list
                    var flattenedResults = processedItems.SelectMany(x => x).ToList();
                    Debug.WriteLine($"[AITextGenerateWorker] ProcessBranch - Got result count: {flattenedResults?.Count}");
                    return flattenedResults;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AITextGenerateWorker] ProcessBranch - Error: {ex.Message}");
                    Debug.WriteLine($"[AITextGenerateWorker] ProcessBranch - Stack trace: {ex.StackTrace}");
                    return new List<GH_String>();
                }
            }

            // The ProcessAIResponse inside the Worker class
            protected override async Task<List<IGH_Goo>> ProcessAIResponse(string value, string prompt, CancellationToken ct)
            {
                var messages = new List<KeyValuePair<string, string>>();

                // Only add system prompt if it's not empty
                if (!string.IsNullOrWhiteSpace(prompt))
                {
                    messages.Add(new KeyValuePair<string, string>("system", prompt));
                }

                // Only add user message if it's not empty
                if (!string.IsNullOrWhiteSpace(value))
                {
                    messages.Add(new KeyValuePair<string, string>("user", value));
                }

                var response = await GetResponse(messages, ct);
                if (ct.IsCancellationRequested || response == null)
                    return new List<IGH_Goo>();

                if (response.FinishReason == "error")
                {
                    ReportError($"AI error: {response.Response}");
                    return new List<IGH_Goo>();
                }

                _lastAIResponse = response;
                return new List<IGH_Goo> { new GH_String(response.Response) };
            }

            public override async Task DoWorkAsync(CancellationToken token)
            {
                try
                {
                    Debug.WriteLine($"[AITextGenerateWorker] DoWorkAsync - Start. promptTree null? {promptTree == null}, instructionsTree null? {instructionsTree == null}");

                    // Validate input trees
                    if (promptTree == null || instructionsTree == null)
                    {
                        Debug.WriteLine("[AITextGenerateWorker] DoWorkAsync - Input trees are null");
                        return;
                    }

                    // Log tree details
                    Debug.WriteLine($"[AITextGenerateWorker] DoWorkAsync - promptTree paths: {promptTree.Paths?.Count}, data count: {promptTree.DataCount}");
                    Debug.WriteLine($"[AITextGenerateWorker] DoWorkAsync - instructionsTree paths: {instructionsTree.Paths?.Count}, data count: {instructionsTree.DataCount}");

                    // Make local copies of the trees to avoid thread safety issues
                    var localPromptTree = new GH_Structure<GH_String>();
                    var localInstructionsTree = new GH_Structure<GH_String>();

                    Debug.WriteLine("[AITextGenerateWorker] DoWorkAsync - Creating local copies of trees");
                    foreach (var path in promptTree.Paths)
                    {
                        var branch = promptTree.get_Branch(path);
                        localPromptTree.AppendRange(branch.Cast<GH_String>(), path);
                    }
                    foreach (var path in instructionsTree.Paths)
                    {
                        var branch = instructionsTree.get_Branch(path);
                        localInstructionsTree.AppendRange(branch.Cast<GH_String>(), path);
                    }

                    Debug.WriteLine($"[AITextGenerateWorker] DoWorkAsync - Local promptTree paths: {localPromptTree.Paths?.Count}, data count: {localPromptTree.DataCount}");
                    Debug.WriteLine($"[AITextGenerateWorker] DoWorkAsync - Local instructionsTree paths: {localInstructionsTree.Paths?.Count}, data count: {localInstructionsTree.DataCount}");

                    Debug.WriteLine("[AITextGenerateWorker] DoWorkAsync - About to start parallel processing");

                    response = await MultiTreeProcessor.ProcessTreesInParallel(
                        new GH_Structure<GH_String>[] { localInstructionsTree, localPromptTree },
                        ProcessBranch,
                        onlyMatchingPaths: false,  // Process all unique paths
                        groupIdenticalBranches: true,  // Enable grouping for optimization
                        token);

                    Debug.WriteLine($"[AITextGenerateWorker] DoWorkAsync - Parallel processing complete. Response null? {response == null}");
                    if (response != null)
                    {
                        Debug.WriteLine($"[AITextGenerateWorker] DoWorkAsync - Response paths: {response.Paths?.Count}, data count: {response.DataCount}");
                    }

                    // Store branches count to parent component
                    ParentComponent.branches_input = response.Paths.Count;

                    OnWorkCompleted();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AITextGenerateWorker] DoWorkAsync - Exception: {ex.GetType().Name} - {ex.Message}\n{ex.StackTrace}");
                    throw;
                }
            }

            public override void SetOutput(IGH_DataAccess DA, out string doneMessage)
            {
                doneMessage = null;

                if (_lastAIResponse != null)
                {
                    ParentComponent.ProcessFinalResponse(_lastAIResponse, DA);
                }
            }
        }
    }
}