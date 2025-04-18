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
using Rhino.Commands;

namespace SmartHopper.Components.List
{
    public class AIListFilter : AIStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("CD2E5F8A-94D4-48D7-8E68-8185341245D0");
        protected override System.Drawing.Bitmap Icon => Resources.listfilter;
        public override GH_Exposure Exposure => GH_Exposure.primary;

        public AIListFilter()
            : base("AI List Filter", "AIListFilter",
                  "Filter, reorder, shuffle, repeat items or combine multiple tasks on lists of elements using natural language criteria.\nThis components takes the list as a whole. This means that each criterion will filter each full list.\nIf a tree structure is provided, criteria and lists will only match within the same branch paths.",
                  "SmartHopper", "List")
        {
        }

        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("List", "L", "REQUIRED List of items", GH_ParamAccess.tree);
            pManager.AddTextParameter("Criteria", "C", "REQUIRED Natural language prompt describing how to filter, reorder, shuffle, repeat items or combine multiple tasks on the list.", GH_ParamAccess.tree);
        }

        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Result", "R", "Result after processing the list", GH_ParamAccess.tree);
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
                var criteriaTree = new GH_Structure<GH_String>();

                DA.GetDataTree("List", out listTree);
                DA.GetDataTree("Criteria", out criteriaTree);

                // Convert generic data to string structure
                var stringListTree = ConvertToGHString(listTree);

                // Store the converted trees
                _inputTree["List"] = stringListTree;
                _inputTree["Criteria"] = criteriaTree;
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

            private static async Task<Dictionary<string, List<GH_String>>> ProcessData(Dictionary<string, List<GH_String>> branches, AIListFilter parent, int reuseCount = 1)
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
                var listTreeOriginal = branches["List"];
                var criteriaTree = branches["Criteria"];

                // Convert list to JSON format before normalization
                var listTreeJson = ParsingTools.ConcatenateItemsToJsonList(listTreeOriginal);

                // Normalize tree lengths
                var normalizedLists = DataTreeProcessor.NormalizeBranchLengths(new List<List<GH_String>> { listTreeJson, criteriaTree });

                // Reassign normalized branches
                listTreeJson = normalizedLists[0];
                var normalizedCriteriaTree = normalizedLists[1];

                Debug.WriteLine($"[ProcessData] After normalization - Criteria count: {normalizedCriteriaTree.Count}, List count: {listTreeJson.Count}");

                // Initialize the output
                var outputs = new Dictionary<string, List<GH_String>>();
                outputs["Result"] = new List<GH_String>();

                // Iterate over the branches
                // For each item in the prompt tree, get the response from AI
                int i = 0;
                foreach (var criterion in normalizedCriteriaTree)
                {
                    Debug.WriteLine($"[ProcessData] Processing prompt {i + 1}/{normalizedCriteriaTree.Count}");

                    // Use the generic ListTools.FilterListAsync method with the original list
                    var filterResult = await ListTools.FilterListAsync(
                        listTreeOriginal,
                        criterion,
                        messages => parent.GetResponse(messages, contextProviderFilter: "-environment,-time", reuseCount: reuseCount));

                    if (!filterResult.Success)
                    {
                        // Handle error
                        if (filterResult.Response != null && filterResult.Response.FinishReason == "error")
                        {
                            parent.AIErrorToPersistentRuntimeMessage(filterResult.Response);
                        }
                        else
                        {
                            parent.SetPersistentRuntimeMessage("ai_error", filterResult.ErrorLevel, filterResult.ErrorMessage, false);
                        }
                        
                        outputs["Result"].Add(new GH_String(string.Empty));
                    }
                    else
                    {
                        // Add the filtered results
                        outputs["Result"].AddRange(filterResult.Result);
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
