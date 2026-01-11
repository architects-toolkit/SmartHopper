/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024-2026 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this library; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Components.Properties;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Core.DataTree;
using SmartHopper.Core.Grasshopper.Utils.Parsing;

namespace SmartHopper.Components.List
{
    public class AIListFilter : AIStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new("CD2E5F8A-94D4-48D7-8E68-8185341245D0");

        protected override Bitmap Icon => Resources.listfilter;

        public override GH_Exposure Exposure => GH_Exposure.primary;

        /// <inheritdoc/>
        protected override IReadOnlyList<string> UsingAiTools => new[] { "list_filter" };

        protected override ProcessingOptions ComponentProcessingOptions => new ProcessingOptions
        {
            Topology = ProcessingTopology.BranchToBranch,
            OnlyMatchingPaths = false,
            GroupIdenticalBranches = true,
        };

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

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new AIListFilterWorker(this, this.AddRuntimeMessage, ComponentProcessingOptions);
        }

        private sealed class AIListFilterWorker : AsyncWorkerBase
        {
            private Dictionary<string, GH_Structure<GH_String>> inputTree;
            private Dictionary<string, GH_Structure<GH_String>> result;
            private readonly AIListFilter parent;
            private readonly ProcessingOptions processingOptions;

            public AIListFilterWorker(
            AIListFilter parent,
            Action<GH_RuntimeMessageLevel, string> addRuntimeMessage,
            ProcessingOptions processingOptions)
            : base(parent, addRuntimeMessage)
            {
                this.parent = parent;
                this.processingOptions = processingOptions;
                this.result = new Dictionary<string, GH_Structure<GH_String>>
                {
                    { "Result", new GH_Structure<GH_String>() },
                };
            }

            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                this.inputTree = new Dictionary<string, GH_Structure<GH_String>>();

                // Get the input trees
                var listTree = new GH_Structure<IGH_Goo>();
                var criteriaTree = new GH_Structure<GH_String>();

                DA.GetDataTree("List", out listTree);
                DA.GetDataTree("Criteria", out criteriaTree);

                // Convert generic data to string structure
                var stringListTree = ConvertToGHString(listTree);

                Debug.WriteLine($"[Worker] String list tree: {stringListTree}");

                // Store the converted trees
                this.inputTree["List"] = stringListTree;
                this.inputTree["Criteria"] = criteriaTree;

                // dataCount will be calculated automatically by RunProcessingAsync based on ProcessingOptions
                // (centralized logic in DataTreeProcessor.BuildProcessingPlan)
                dataCount = this.inputTree.Values.Sum(t => t.DataCount);
            }

            public override async Task DoWorkAsync(CancellationToken token)
            {
                try
                {
                    Debug.WriteLine($"[Worker] Starting DoWorkAsync");
                    Debug.WriteLine($"[Worker] Input tree keys: {string.Join(", ", this.inputTree.Keys)}");
                    Debug.WriteLine($"[Worker] Input tree data counts: {string.Join(", ", this.inputTree.Select(kvp => $"{kvp.Key}: {kvp.Value.DataCount}"))}");

                    this.result = await this.parent.RunProcessingAsync(
                        this.inputTree,
                        async (branches) =>
                        {
                            Debug.WriteLine($"[Worker] ProcessData called with {branches.Count} branches");
                            return await ProcessData(branches, this.parent).ConfigureAwait(false);
                        },
                        this.processingOptions,
                        token).ConfigureAwait(false);

                    Debug.WriteLine($"[Worker] Finished DoWorkAsync - Result keys: {string.Join(", ", this.result.Keys)}");
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
                var listAsJson = AIResponseParser.ConcatenateItemsToJson(branches["List"], "array");
                var criteriaTree = branches["Criteria"];

                // Normalize tree lengths
                var normalizedLists = DataTreeProcessor.NormalizeBranchLengths(
                    new List<List<GH_String>>
                    {
                        new (new GH_String[] { new (listAsJson.ToString()) }),
                        criteriaTree,
                    });

                // Reassign normalized branches
                var normalizedListTree = normalizedLists[0];
                var normalizedCriteriaTree = normalizedLists[1];

                Debug.WriteLine($"[ProcessData] After normalization - Criteria count: {normalizedCriteriaTree.Count}, List count: {normalizedListTree.Count}");

                // Initialize the output
                var outputs = new Dictionary<string, List<GH_String>>();
                outputs["Result"] = new List<GH_String>();

                // Iterate over the branches
                // For each item in the prompt tree, get the response from AI
                int i = 0;
                foreach (var criterion in normalizedCriteriaTree)
                {
                    Debug.WriteLine($"[ProcessData] Processing prompt {i + 1}/{normalizedCriteriaTree.Count}");
                    var listToken = normalizedListTree[i];
                    var criterionValue = criterion?.Value;

                    // Skip entries where we do not have valid list JSON or criteria text
                    if (listToken == null || string.IsNullOrWhiteSpace(listToken.Value))
                    {
                        Debug.WriteLine($"[ProcessData] Skipping index {i}: list JSON is null or empty");
                        i++;
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(criterionValue))
                    {
                        Debug.WriteLine($"[ProcessData] Skipping index {i}: criterion is null or empty");
                        i++;
                        continue;
                    }

                    Debug.WriteLine($"[ProcessData] List: {listToken.Value}");
                    Debug.WriteLine($"[ProcessData] Criterion: {criterionValue}");

                    // Call the AI tool through the tool manager
                    var parameters = new JObject
                    {
                        ["list"] = JArray.Parse(listToken.Value),
                        ["criteria"] = criterionValue,
                        ["contextFilter"] = "-*",
                    };

                    Debug.WriteLine($"[ProcessData] Calling AI tool 'list_filter' with parameters: {parameters}");

                    var toolResult = await parent.CallAiToolAsync(
                        "list_filter", parameters)
                        .ConfigureAwait(false);

                    var indices = toolResult?["result"]?.ToObject<List<int>>() ?? new List<int>();
                    var filteredItems = indices
                        .Where(idx => idx >= 0 && idx < branches["List"].Count)
                        .Select(idx =>
                        {
                            var sourceItem = branches["List"][idx];
                            return new GH_String(sourceItem != null ? sourceItem.Value : string.Empty);
                        });
                    outputs["Result"].AddRange(filteredItems);

                    i++;
                }

                return outputs;
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                Debug.WriteLine($"[Worker] Setting output - Available keys: {string.Join(", ", this.result.Keys)}");

                if (!this.result.TryGetValue("Result", out GH_Structure<GH_String>? value))
                {
                    Debug.WriteLine("[Worker] Warning: Result key not found in output dictionary");
                    message = "Error: No result available";
                    return;
                }

                this.parent.SetPersistentOutput("Result", value, DA);
                message = string.Empty;
            }
        }
    }
}
