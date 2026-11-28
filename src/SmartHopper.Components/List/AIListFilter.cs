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
 * along with this library; if not, see <https://www.gnu.org/licenses/lgpl-3.0.html>.
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
using SmartHopper.Core.ComponentBase.Batch;
using SmartHopper.Core.DataTree;
using SmartHopper.Core.Grasshopper.Utils.Parsing;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AIProviders;
using SmartHopper.ProviderSdk.Diagnostics;

using SmartHopper.ProviderSdk.Logging;
namespace SmartHopper.Components.List
{
    public class AIListFilter : AIStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new ("CD2E5F8A-94D4-48D7-8E68-8185341245D0");

        protected override Bitmap Icon => Resources.AIListFilter;

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        /// <inheritdoc/>
        public override IEnumerable<string> Keywords => new[] {
            "list_filter",
            "List Filter",
            "Filter List",
            "Alter List",
            "List Reorder",
            "List Shuffle",
            "List Process",
            "List Alter",
        };

        /// <inheritdoc/>
        protected override IReadOnlyList<string> UsingAiTools => new[] { "list_filter" };

        protected override ProcessingOptions ComponentProcessingOptions => new ProcessingOptions
        {
            Topology = ProcessingTopology.BranchToBranch,
            OnlyMatchingPaths = false,
            GroupIdenticalBranches = true,
        };

        public AIListFilter()
            : base(
                "AI List Filter",
                "AIListFilter",
                "Filter, sort, reorder, shuffle, repeat, or transform a list of items using natural-language criteria.\nProcesses each list (branch) as a whole. When data trees are provided, lists and criteria are matched by branch path.",
                "SmartHopper",
                "I. Text & Lists")
        {
        }

        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("List", "L", "List of items to process.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Criteria", "C", "Natural-language instructions describing how to filter, sort, reorder, or transform the list.", GH_ParamAccess.tree);
        }

        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Result", "R", "Processed list of items.", GH_ParamAccess.tree);
        }

        /// <summary>
        /// Source list items per queued batch customId, captured while the worker
        /// emits sentinels. Consumed by <see cref="OnBatchCompleted"/> to map the
        /// model's index response back onto the unit's original items.
        /// </summary>
        private Dictionary<string, List<GH_String>> _batchListCache;

        /// <inheritdoc/>
        /// <remarks>
        /// Each queued call decodes to zero or more output items — the model returns
        /// kept indices, which expand to the unit's filtered list at the sentinel's
        /// branch. Uses <see cref="ProcessBatchResultsMany{T}"/> so multi-item unit
        /// outputs replace their sentinel positionally inside the stored fragment.
        /// </remarks>
        protected override void OnBatchCompleted(IReadOnlyDictionary<string, JObject>? results, IReadOnlyList<SHRuntimeMessage>? messages = null)
        {
            var sentinel = this.GetSentinelTree("Result");
            if (results == null || sentinel == null) return;

            // Decode with the provider that ran the batch — the live selection may
            // have changed while it was in flight (mirrors ProcessBatchResultsCore).
            var provider = ProviderManager.Instance.GetProvider(this.GetSubmittedProviderName());
            if (provider == null) return;

            var listCache = this._batchListCache;
            this.ProcessBatchResultsMany<GH_String>(
                "Result",
                sentinel,
                results,
                (customId, resultBody) =>
                {
                    var interactions = provider.Decode(resultBody);
                    var lastText = interactions
                        ?.OfType<AIInteractionText>()
                        .LastOrDefault(i => i.Agent == AIAgent.Assistant);
                    if (string.IsNullOrEmpty(lastText?.Content))
                    {
                        return new List<GH_String>();
                    }

                    var indices = AIResponseParser.ParseIndicesFromResponse(lastText.Content) ?? new List<int>();
                    if (listCache == null || !listCache.TryGetValue(customId, out var sourceItems) || sourceItems == null)
                    {
                        return new List<GH_String>();
                    }

                    return indices
                        .Where(idx => idx >= 0 && idx < sourceItems.Count)
                        .Select(idx => new GH_String(sourceItems[idx]?.Value ?? string.Empty))
                        .ToList();
                },
                messages);

            this._batchListCache = null;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Persists the source items behind each queued customId so an in-flight
        /// batch resumed after save/reopen can still map returned indices onto the
        /// original list — without it a valid index response would decode to an
        /// empty output indistinguishable from a legitimate empty selection.
        /// </remarks>
        protected override JObject CaptureBatchDecodeContext()
        {
            var cache = this._batchListCache;
            if (cache == null || cache.Count == 0)
            {
                return null;
            }

            var items = new JObject();
            foreach (var kvp in cache)
            {
                items[kvp.Key] = new JArray(kvp.Value?.Select(s => s?.Value ?? string.Empty) ?? Enumerable.Empty<string>());
            }

            return new JObject { ["items"] = items };
        }

        /// <inheritdoc/>
        protected override void RestoreBatchDecodeContext(JObject context)
        {
            if (context?["items"] is not JObject items)
            {
                return;
            }

            var restored = new Dictionary<string, List<GH_String>>(StringComparer.Ordinal);
            foreach (var prop in items.Properties())
            {
                if (prop.Value is JArray arr)
                {
                    restored[prop.Name] = arr.Values<string>().Select(v => new GH_String(v)).ToList();
                }
            }

            if (restored.Count > 0)
            {
                this._batchListCache = restored;
            }
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new AIListFilterWorker(this, this.AddRuntimeMessage, this.ComponentProcessingOptions);
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

                Logger.For<AIListFilterWorker>().Trace($"[Worker] String list tree: {stringListTree}");

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
                    Logger.For<AIListFilterWorker>().Trace($"[Worker] Starting DoWorkAsync");
                    Logger.For<AIListFilterWorker>().Trace($"[Worker] Input tree keys: {string.Join(", ", this.inputTree.Keys)}");
                    Logger.For<AIListFilterWorker>().Trace($"[Worker] Input tree data counts: {string.Join(", ", this.inputTree.Select(kvp => $"{kvp.Key}: {kvp.Value.DataCount}"))}");

                    this.result = await this.parent.RunProcessingAsync(
                        this.inputTree,
                        async (branches) =>
                        {
                            Logger.For<AIListFilterWorker>().Trace($"[Worker] ProcessData called with {branches.Count} branches");
                            return await ProcessData(branches, this.parent, token).ConfigureAwait(false);
                        },
                        this.processingOptions,
                        token).ConfigureAwait(false);

                    Logger.For<AIListFilterWorker>().Trace($"[Worker] Finished DoWorkAsync - Result keys: {string.Join(", ", this.result.Keys)}");
                }
                catch (Exception ex)
                {
                    Logger.For<AIListFilterWorker>().Exception(ex, $"[Worker] Error: {ex.Message}");
                    this.CollectMessage(SHRuntimeMessageSeverity.Error, ex.Message);
                }
            }

            private static async Task<Dictionary<string, List<GH_String>>> ProcessData(Dictionary<string, List<GH_String>> branches, AIListFilter parent, CancellationToken cancellationToken)
            {
                /*
                 * Inputs will be available as a dictionary
                 * of branches. No need to deal with paths.
                 *
                 * Outputs should be a dictionary where keys
                 * are each output parameter, and values are
                 * the output values.
                 */

                Logger.For<AIListFilterWorker>().Trace($"[Worker] Processing {branches.Count} trees");
                Logger.For<AIListFilterWorker>().Trace($"[Worker] Items per tree: {branches.Values.Max(branch => branch.Count)}");

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

                Logger.For<AIListFilterWorker>().Trace($"[ProcessData] After normalization - Criteria count: {normalizedCriteriaTree.Count}, List count: {normalizedListTree.Count}");

                // Initialize the output
                var outputs = new Dictionary<string, List<GH_String>>();
                outputs["Result"] = new List<GH_String>();

                // Iterate over the branches
                // For each item in the prompt tree, get the response from AI
                int i = 0;
                foreach (var criterion in normalizedCriteriaTree)
                {
                    Logger.For<AIListFilterWorker>().Trace($"[ProcessData] Processing prompt {i + 1}/{normalizedCriteriaTree.Count}");
                    var listToken = normalizedListTree[i];
                    var criterionValue = criterion?.Value;

                    // Skip entries where we do not have valid list JSON or criteria text
                    if (listToken == null || string.IsNullOrWhiteSpace(listToken.Value))
                    {
                        Logger.For<AIListFilterWorker>().Warning($"[ProcessData] Skipping index {i}: list JSON is null or empty");
                        i++;
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(criterionValue))
                    {
                        Logger.For<AIListFilterWorker>().Warning($"[ProcessData] Skipping index {i}: criterion is null or empty");
                        i++;
                        continue;
                    }

                    // Call the AI tool through the tool manager
                    var parameters = new JObject
                    {
                        ["list"] = JArray.Parse(listToken.Value),
                        ["criteria"] = criterionValue,
                        ["contextFilter"] = "-*",
                    };

                    Logger.For<AIListFilterWorker>().Trace("[ProcessData] Calling AI tool 'list_filter'");

                    var toolResult = await parent.CallAIToolAsync(
                        "list_filter", parameters, cancellationToken)
                        .ConfigureAwait(false);

                    // In batch mode, CallAIToolAsync returns a sentinel placeholder under "result".
                    // Forward it so ReconstructOutputTree can replace it after the batch completes,
                    // and cache the source list so OnBatchCompleted can map indices → items.
                    var sentinel = toolResult?["result"]?.ToString();
                    if (BatchSentinel.Is(sentinel))
                    {
                        outputs["Result"].Add(new GH_String(sentinel));
                        if (BatchSentinel.TryExtract(sentinel, out var customId))
                        {
                            (parent._batchListCache ??= new Dictionary<string, List<GH_String>>())[customId] = branches["List"];
                        }

                        i++;
                        continue;
                    }

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
                Logger.For<AIListFilterWorker>().Trace($"[Worker] Setting output - Available keys: {string.Join(", ", this.result.Keys)}");

                if (!this.result.TryGetValue("Result", out GH_Structure<GH_String>? value))
                {
                    Logger.For<AIListFilterWorker>().Trace("[Worker] Warning: Result key not found in output dictionary");
                    message = "Error: No result available";
                    return;
                }

                this.parent.SetPersistentOutput("Result", value, DA);
                this.parent.SetMetricsOutput(DA);
                message = string.Empty;
            }
        }
    }
}
