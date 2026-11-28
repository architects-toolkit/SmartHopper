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
using System.Linq;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using SmartHopper.Core.ComponentBase.Batch;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Requests;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.AICall.Metrics;
using SmartHopper.ProviderSdk.AIModels;
using SmartHopper.ProviderSdk.Diagnostics;

using SmartHopper.ProviderSdk.Logging;
namespace SmartHopper.Core.ComponentBase
{
    /// <summary>
    /// Data tree processing and batch result reconstruction for AIStatefulAsyncComponentBase.
    /// Handles output tree reconstruction and result processing.
    /// </summary>
    public abstract partial class AIStatefulAsyncComponentBase
    {
        /// <summary>
        /// Helper method to process batch results: reconstructs output tree by replacing sentinels,
        /// calls <see cref="SentinelTransformOutputs"/> on each decoded item to allow post-processing,
        /// aggregates metrics from all batch items, surfaces any batch errors via <see cref="AIReturn"/>,
        /// and delegates to <see cref="FinishResults{T}"/> to persist all outputs atomically and emit metrics.
        /// Call this from <see cref="OnBatchCompleted"/> to handle common batch completion logic.
        /// </summary>
        /// <typeparam name="T">The output Grasshopper goo type.</typeparam>
        /// <param name="outputParamName">Name of the primary output parameter to persist (e.g., "Result").</param>
        /// <param name="sentinelTree">Tree containing sentinel strings from batch submission.</param>
        /// <param name="results">Dictionary from customId to provider response body.</param>
        /// <param name="decode">Function that converts (customId, resultBody) into a goo item.</param>
        /// <param name="messages">Optional item-level diagnostic messages (errors/warnings) from the provider.</param>
        /// <returns>The reconstructed output tree with sentinels replaced by decoded values.</returns>
        protected GH_Structure<T> ProcessBatchResults<T>(
            string outputParamName,
            GH_Structure<GH_String> sentinelTree,
            IReadOnlyDictionary<string, Newtonsoft.Json.Linq.JObject> results,
            Func<string, Newtonsoft.Json.Linq.JObject, T> decode,
            IReadOnlyList<SHRuntimeMessage> messages = null)
            where T : IGH_Goo
        {
            return this.ProcessBatchResultsCore(
                outputParamName,
                sentinelTree,
                results,
                (customId, resultBody) => new List<T> { decode(customId, resultBody) },
                messages);
        }

        /// <summary>
        /// Variant of <see cref="ProcessBatchResults{T}"/> for units whose single batch
        /// call expands into zero or more output items (e.g. <c>AI List Filter</c>,
        /// where one call returns an index list that materializes as a whole branch).
        /// Each decoded item replaces the sentinel positionally inside the stored
        /// fragment, so reuse/persistence see the complete unit output.
        /// </summary>
        /// <typeparam name="T">The output Grasshopper goo type.</typeparam>
        /// <param name="outputParamName">Name of the primary output parameter to persist.</param>
        /// <param name="sentinelTree">Tree containing sentinel strings from batch submission.</param>
        /// <param name="results">Dictionary from customId to provider response body.</param>
        /// <param name="decode">Function that converts (customId, resultBody) into the unit's output items.</param>
        /// <param name="messages">Optional item-level diagnostic messages (errors/warnings) from the provider.</param>
        /// <returns>The reconstructed output tree with sentinels replaced by decoded items.</returns>
        protected GH_Structure<T> ProcessBatchResultsMany<T>(
            string outputParamName,
            GH_Structure<GH_String> sentinelTree,
            IReadOnlyDictionary<string, Newtonsoft.Json.Linq.JObject> results,
            Func<string, Newtonsoft.Json.Linq.JObject, List<T>> decode,
            IReadOnlyList<SHRuntimeMessage> messages = null)
            where T : IGH_Goo
        {
            return this.ProcessBatchResultsCore(outputParamName, sentinelTree, results, decode, messages);
        }

        private GH_Structure<T> ProcessBatchResultsCore<T>(
            string outputParamName,
            GH_Structure<GH_String> sentinelTree,
            IReadOnlyDictionary<string, Newtonsoft.Json.Linq.JObject> results,
            Func<string, Newtonsoft.Json.Linq.JObject, List<T>> decodeMany,
            IReadOnlyList<SHRuntimeMessage> messages = null)
            where T : IGH_Goo
        {
            if (results == null || sentinelTree == null)
            {
                // Still finalize with an empty tree to clear any previous sentinels and emit metrics
                var emptyTree = new GH_Structure<T>();
                this.FinishResults(outputParamName, emptyTree);
                return emptyTree;
            }

            // Decode with the provider that executed the batch — the live selection
            // may have changed while the batch was in flight.
            var providerName = this.GetSubmittedProviderName();
            var provider = SmartHopper.Infrastructure.AIProviders.ProviderManager.Instance.GetProvider(providerName);
            if (provider == null)
            {
                var emptyTree = new GH_Structure<T>();
                this.FinishResults(outputParamName, emptyTree);
                return emptyTree;
            }

            // Build mapping from custom_id to branch path for clearer error messages
            var customIdToBranchPath = new Dictionary<string, GH_Path>();
            foreach (var path in sentinelTree.Paths)
            {
                var branch = sentinelTree.get_Branch(path);
                foreach (var item in branch)
                {
                    var str = (item as GH_String)?.Value ?? string.Empty;
                    if (BatchSentinel.TryExtract(str, out var customId))
                    {
                        customIdToBranchPath[customId] = path;
                    }
                }
            }

            var allInteractions = new List<IAIInteraction>();

            // Per-customId metrics so they can be placed at the correct branch path in the tree.
            var customIdToMetrics = new Dictionary<string, List<AIMetrics>>();

            // Accumulate extra outputs returned by SentinelTransformOutputs across all sentinels.
            // Key: output param name → merged GH_Structure<IGH_Goo> (one slot per sentinel path).
            var extraOutputAccumulator = new Dictionary<string, GH_Structure<IGH_Goo>>();

            // Decode-once / place-many: a shared result id appears at every
            // sentinel seat, but provider decode, metrics, transforms and
            // fragment resolution are side effects that must run exactly once
            // per id. Decode every expected id up front; the reconstruction
            // walk then only looks up precomputed items per seat.
            var decodedItemsById = new Dictionary<string, List<T>>(StringComparer.Ordinal);
            foreach (var customId in customIdToBranchPath.Keys)
            {
                if (!results.TryGetValue(customId, out var resultBody) || resultBody == null)
                {
                    continue;
                }

                decodedItemsById[customId] = DecodeBatchItem(customId, resultBody);
            }

            var reconstructedTree = ReconstructOutputTreeMany<T>(
                sentinelTree,
                results,
                (customId, _) => decodedItemsById.TryGetValue(customId, out var items) ? items : null);

            // Local function kept after the call so captured state (provider,
            // metrics, accumulators) stays lexically near its consumers.
            List<T> DecodeBatchItem(string customId, Newtonsoft.Json.Linq.JObject resultBody)
            {
                try
                {
                    var interactions = provider.Decode(resultBody);
                    if (interactions != null)
                    {
                        allInteractions.AddRange(interactions);

                        // One combined metric per decoded call — AIBody.Metrics
                        // folds all interaction metrics exactly as the
                        // synchronous path does. Collecting per-interaction
                        // metrics would emit phantom empty entries for providers
                        // that split content and usage across interactions
                        // (Gemini's metrics-only sidecar).
                        var metricsList = new List<AIMetrics>();
                        if (interactions.Count > 0)
                        {
                            var combinedMetrics = AIBodyBuilder.Create()
                                .AddRange(interactions)
                                .Build()
                                .Metrics;
                            metricsList.Add(this.StampBatchMetrics(combinedMetrics, customId, providerName));
                        }

                        this.ResultStore.RecordMetricsForSentinel(customId, metricsList);

                        if (metricsList.Count > 0)
                        {
                            customIdToMetrics[customId] = metricsList;
                        }
                    }

                    var decodedItems = decodeMany(customId, resultBody) ?? new List<T>();
                    var primaryItem = decodedItems.Count > 0 ? decodedItems[0] : default;

                    // Call SentinelTransformOutputs so derived components can reshape/split results.
                    // A transform returning the primary output name REPLACES the decoded
                    // items — the transform owns the final unit output shape.
                    var context = new ProcessingUnitContext { SentinelId = customId };
                    var decodedMap = new Dictionary<string, IGH_Goo> { [outputParamName] = primaryItem };
                    var transformed = this.SentinelTransformOutputs(decodedMap, context);

                    var effectiveItems = transformed != null
                        && transformed.TryGetValue(outputParamName, out var transformedPrimary)
                        && transformedPrimary is T tp
                        ? new List<T> { tp }
                        : decodedItems;

                    // Resolve the sentinel inside the stored unit fragment so the
                    // cache entry is complete once every queued call is decoded.
                    var resolved = new Dictionary<string, List<IGH_Goo>>
                    {
                        [outputParamName] = effectiveItems.Cast<IGH_Goo>().ToList(),
                    };
                    if (transformed != null)
                    {
                        foreach (var kvp in transformed)
                        {
                            if (kvp.Key == outputParamName) continue;
                            resolved[kvp.Key] = new List<IGH_Goo> { kvp.Value };
                        }
                    }

                    this.ResultStore.ResolveSentinel(customId, resolved);

                    // Accumulate any extra keys returned by SentinelTransformOutputs.
                    // Path-aware: extras land at the sentinel's own branch, not at {0}.
                    if (transformed != null)
                    {
                        var sentinelPath = customIdToBranchPath.TryGetValue(customId, out var sp) ? sp : null;
                        foreach (var kvp in transformed)
                        {
                            if (kvp.Key == outputParamName) continue;
                            if (!extraOutputAccumulator.TryGetValue(kvp.Key, out var extraTree))
                            {
                                extraTree = new GH_Structure<IGH_Goo>();
                                extraOutputAccumulator[kvp.Key] = extraTree;
                            }

                            if (sentinelPath != null)
                            {
                                extraTree.Append(kvp.Value, sentinelPath);
                            }
                            else
                            {
                                extraTree.Append(kvp.Value);
                            }
                        }
                    }

                    return effectiveItems;
                }
                catch (Exception ex)
                {
                    // Record the item as locally failed instead of silently
                    // re-invoking the decoder (which would usually throw twice)
                    // or letting the unit complete with a phantom empty output.
                    // CompleteBatchAndTransition counts these toward the
                    // unresolved/all-failed decision.
                    Logger.For<AIStatefulAsyncComponentBase>().Exception(ex, $"[AIStatefulAsync] Batch decode error for {customId}: {ex.Message}");
                    this.RecordBatchItemDecodeError(customId, ex);
                    return new List<T>();
                }
            }

            // Surface any provider messages (errors/warnings) from individual batch items.
            // This mirrors how AIRequestCall.Exec() surfaces errors via SurfaceMessagesFromReturn.
            var errorInteractions = allInteractions
                .OfType<AIInteractionRuntimeMessage>()
                .Where(d => d.Severity == SHRuntimeMessageSeverity.Error)
                .ToList();
            if (errorInteractions.Count > 0 || (messages != null && messages.Count > 0))
            {
                // Synthetic AIReturn used purely to relay already-captured item-level messages.
                // Skip Request/Metrics validation so AIReturn.Messages does not auto-inject
                // spurious "Request must not be null" / "Metrics must not be null" errors
                // (see AIReturn.IsValid).
                var errorReturn = new AIReturn
                {
                    SkipRequestValidation = true,
                    SkipMetricsValidation = true,
                };
                foreach (var err in errorInteractions)
                {
                    errorReturn.AddRuntimeMessage(
                        SHRuntimeMessageSeverity.Error,
                        SHRuntimeMessageOrigin.Provider,
                        err.Content ?? "Provider returned an error");
                }

                // Surface item-level provider messages (errors, warnings, info)
                if (messages != null)
                {
                    foreach (var msg in messages)
                    {
                        // Replace custom_id with branch path for clarity
                        var messageContent = msg.Message;
                        foreach (var kvp in customIdToBranchPath)
                        {
                            messageContent = messageContent.Replace($"Batch item {kvp.Key}", $"Branch {kvp.Value}");
                        }
                        errorReturn.AddRuntimeMessage(msg.Severity, msg.Origin, messageContent);
                    }
                }

                this.SurfaceMessagesFromReturn(errorReturn, "batch_item");
            }

            // Build aggregated AIReturn so body/interactions are available after batch completion.
            // Also build _persistedMetrics as the single authoritative metrics instance:
            // AIReturn.Metrics is computed fresh on every access, so any mutation to it is a no-op.
            if (allInteractions.Count > 0)
            {
                // Build per-item metrics via AIMetricsList for multi-provider support
                var allMetrics = customIdToMetrics.Values.SelectMany(v => v).ToList();
                this.PersistedMetricsList = new ProviderSdk.AICall.Metrics.AIMetricsList();
                foreach (var m in allMetrics)
                {
                    // Role was already resolved per custom ID by StampBatchMetrics
                    // ("main" or "tool:{name}") — preserve it rather than re-stamping.
                    this.PersistedMetricsList.Add(m, m.Role);
                }

                var firstEntry = this.PersistedMetricsList.Entries.Count > 0 ? this.PersistedMetricsList.Entries[0] : null;
                Logger.For<AIStatefulAsyncComponentBase>().Trace($"[AIStatefulAsync] Aggregated batch metrics: {allMetrics.Count} items");

                // Build AIReturn for body/interactions (used by CurrentAIReturnSnapshot consumers)
                var batchReturn = new AIReturn();
                var batchRequest = new AIRequestCall();
                batchRequest.Initialize(
                    firstEntry?.Provider ?? providerName,
                    firstEntry?.Model ?? this.GetModel(),
                    new List<IAIInteraction>(),
                    origin: "batch_complete",
                    capability: AICapability.None,
                    toolFilter: null);
                batchReturn.CreateSuccess(allInteractions, request: batchRequest);
                this.SetAIReturnSnapshot(batchReturn);

                // Append metrics to the tree at each sentinel's branch path.
                // Multiple metrics per customId (e.g. fallback) become separate items in the same branch.
                foreach (var kvp in customIdToMetrics)
                {
                    var customId = kvp.Key;
                    if (!customIdToBranchPath.TryGetValue(customId, out var path))
                    {
                        continue;
                    }

                    foreach (var m in kvp.Value)
                    {
                        this.AppendMetricToTree(m, path);
                    }
                }
            }

            // Canonical assembly: when placements exist, rebuild every output
            // channel from stored fragments — including reused units whose values
            // never became sentinels and secondary channels that only exist inside
            // the fragment. Falls back to sentinel reconstruction for legacy
            // pending payloads restored without layout data.
            var rebuiltTrees = this.ResultStore.BuildRunTrees(this.ComponentProcessingOptions.Topology);
            if (rebuiltTrees != null)
            {
                reconstructedTree = DataTree.DataTreeProcessor.ExtractTypedTree<T>(rebuiltTrees, outputParamName);
                foreach (var kvp in rebuiltTrees)
                {
                    if (kvp.Key == outputParamName)
                    {
                        continue;
                    }

                    // Fragment-built tree wins — it carries typed items at their
                    // true placements for every unit (executed AND reused).
                    extraOutputAccumulator[kvp.Key] = kvp.Value;
                }
            }

            // Build additionalOutputs array for FinishResults
            var additionalOutputs = extraOutputAccumulator
                .Select(kvp => (kvp.Key, (object)kvp.Value))
                .ToArray();

            // Delegate to FinishResults: persists primary + extras + emits metrics atomically
            this.FinishResults(outputParamName, reconstructedTree, additionalOutputs);

            return reconstructedTree;
        }

        /// <summary>
        /// Reconstructs a Grasshopper data tree by replacing sentinel placeholder strings
        /// (format: <c>##SH_BATCH:{customId}##</c>) with decoded values.
        /// Paths and non-sentinel items are preserved unchanged.
        /// </summary>
        /// <typeparam name="T">The output Grasshopper goo type.</typeparam>
        /// <param name="sentinelTree">Tree containing sentinel strings and normal items.</param>
        /// <param name="results">Dictionary from customId to provider response body.</param>
        /// <param name="decode">Function that converts (customId, resultBody) into a goo item.</param>
        /// <returns>New tree with sentinels replaced by decoded values.</returns>
        protected static GH_Structure<T> ReconstructOutputTree<T>(
            GH_Structure<GH_String> sentinelTree,
            IReadOnlyDictionary<string, Newtonsoft.Json.Linq.JObject> results,
            Func<string, Newtonsoft.Json.Linq.JObject, T> decode)
            where T : IGH_Goo
        {
            var newTree = new GH_Structure<T>();
            if (sentinelTree == null) return newTree;

            foreach (var path in sentinelTree.Paths)
            {
                var branch = sentinelTree.get_Branch(path);
                var newBranch = new List<T>();
                foreach (GH_String item in branch)
                {
                    var str = item?.Value ?? string.Empty;
                    if (BatchSentinel.TryExtract(str, out var customId))
                    {
                        if (results != null && results.TryGetValue(customId, out var resultBody))
                        {
                            newBranch.Add(decode(customId, resultBody));
                            continue;
                        }
                    }

                    if (item is T t) newBranch.Add(t);
                }

                newTree.AppendRange(newBranch, path);
            }

            return newTree;
        }

        /// <summary>
        /// Reconstructs a Grasshopper data tree by replacing each sentinel placeholder
        /// (<c>##SH_BATCH:{customId}##</c>) with zero or more decoded items. Paths and
        /// non-sentinel items are preserved unchanged.
        /// </summary>
        /// <typeparam name="T">The output Grasshopper goo type.</typeparam>
        /// <param name="sentinelTree">Tree containing sentinel strings and normal items.</param>
        /// <param name="results">Dictionary from customId to provider response body.</param>
        /// <param name="decode">Function that converts (customId, resultBody) into output items.</param>
        /// <returns>New tree with sentinels replaced by decoded items.</returns>
        protected static GH_Structure<T> ReconstructOutputTreeMany<T>(
            GH_Structure<GH_String> sentinelTree,
            IReadOnlyDictionary<string, Newtonsoft.Json.Linq.JObject> results,
            Func<string, Newtonsoft.Json.Linq.JObject, List<T>> decode)
            where T : IGH_Goo
        {
            var newTree = new GH_Structure<T>();
            if (sentinelTree == null) return newTree;

            foreach (var path in sentinelTree.Paths)
            {
                var branch = sentinelTree.get_Branch(path);
                var newBranch = new List<T>();
                foreach (GH_String item in branch)
                {
                    var str = item?.Value ?? string.Empty;
                    if (BatchSentinel.TryExtract(str, out var customId))
                    {
                        if (results != null && results.TryGetValue(customId, out var resultBody))
                        {
                            var decoded = decode(customId, resultBody);
                            if (decoded != null)
                            {
                                newBranch.AddRange(decoded);
                            }

                            continue;
                        }
                    }

                    if (item is T t) newBranch.Add(t);
                }

                newTree.AppendRange(newBranch, path);
            }

            return newTree;
        }

        /// <summary>
        /// Converts a GH_Structure to a GH_Structure of GH_String by converting each item to string.
        /// </summary>
        protected static GH_Structure<GH_String> ConvertToGHString(GH_Structure<IGH_Goo> tree)
        {
            var stringTree = new GH_Structure<GH_String>();
            foreach (var path in tree.Paths)
            {
                var branch = tree.get_Branch(path);
                var stringBranch = new List<GH_String>();
                foreach (var item in branch)
                {
                    stringBranch.Add(new GH_String(item.ToString()));
                }

                stringTree.AppendRange(stringBranch, path);
            }

            return stringTree;
        }
    }
}
