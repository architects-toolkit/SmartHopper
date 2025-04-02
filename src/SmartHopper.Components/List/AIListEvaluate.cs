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

namespace SmartHopper.Components.List
{
    public class AIListEvaluate : AIStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("A8BAD48D-8723-42AD-B13C-A875F940B69C");
        protected override System.Drawing.Bitmap Icon => Resources.listevaluate;
        public override GH_Exposure Exposure => GH_Exposure.primary;

        public AIListEvaluate()
            : base("AI List Evaluate", "AIListEvaluate",
                  "Use natural language to evaluate a list and get a true/false answer.\nThis components takes the list as a whole. This means that every question will return True or False for each provided list (not for each individual items).\nIf a tree structure is provided, questions and lists will only match within the same branch paths.",
                  "SmartHopper", "List")
        {
        }

        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("List", "L", " REQUIRED List of items to evaluate", GH_ParamAccess.tree);
            pManager.AddTextParameter("Question", "Q", "REQUIRED True or false question. The AI will answer it based on the input list.", GH_ParamAccess.tree);
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
                var questionTree = new GH_Structure<GH_String>();

                DA.GetDataTree("List", out listTree);
                DA.GetDataTree("Question", out questionTree);

                // Convert generic data to string structure
                var stringListTree = ConvertToGHString(listTree);

                // Store the converted trees
                _inputTree["List"] = stringListTree;
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
                var questionTree = branches["Question"];

                // Convert list to JSON format before normalization
                var listTreeJson = ParsingTools.ConcatenateItemsToJsonList(listTreeOriginal);

                // Normalize tree lengths
                var normalizedLists = DataTreeProcessor.NormalizeBranchLengths(new List<List<GH_String>> { listTreeJson, questionTree });

                // Reassign normalized branches
                listTreeJson = normalizedLists[0];
                questionTree = normalizedLists[1];

                Debug.WriteLine($"[ProcessData] After normalization - Questions count: {questionTree.Count}, List count: {listTreeJson.Count}");

                // Initialize the output
                var outputs = new Dictionary<string, List<GH_Boolean>>();
                outputs["Result"] = new List<GH_Boolean>();

                // Iterate over the branches
                // For each item in the prompt tree, get the response from AI
                int i = 0;
                foreach (var question in questionTree)
                {
                    Debug.WriteLine($"[ProcessData] Processing prompt {i + 1}/{questionTree.Count}");
                    
                    var currentList = listTreeJson[i];

                    // Use the generic ListTools.EvaluateListAsync method
                    var evaluationResult = await ListTools.EvaluateListAsync(
                        currentList.Value,
                        question,
                        messages => parent.GetResponse(messages, contextProviderFilter: "-environment,-time"));

                    if (!evaluationResult.Success)
                    {
                        // Handle error
                        if (evaluationResult.Response != null && evaluationResult.Response.FinishReason == "error")
                        {
                            parent.AIErrorToPersistentRuntimeMessage(evaluationResult.Response);
                        }
                        else
                        {
                            parent.SetPersistentRuntimeMessage("ai_error", evaluationResult.ErrorLevel, evaluationResult.ErrorMessage, false);
                        }
                        
                        outputs["Result"].Add(null);
                    }
                    else
                    {
                        // Add the result
                        outputs["Result"].Add(new GH_Boolean(evaluationResult.Result));
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
        }
    }
}
