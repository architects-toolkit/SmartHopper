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
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Metrics;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.Diagnostics;

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
            if (results == null || sentinelTree == null)
            {
                // Still finalize with an empty tree to clear any previous sentinels and emit metrics
                var emptyTree = new GH_Structure<T>();
                this.FinishResults(outputParamName, emptyTree);
                return emptyTree;
            }

            var providerName = this.GetActualAIProviderName();
            var provider = SmartHopper.Infrastructure.AIProviders.ProviderManager.Instance.GetProvider(providerName);
            if (provider == null)
            {
                var emptyTree = new GH_Structure<T>();
                this.FinishResults(outputParamName, emptyTree);
                return emptyTree;
            }

            // Build mapping from custom_id to branch path for clearer error messages
            var customIdToBranchPath = new Dictionary<string, string>();
            foreach (var path in sentinelTree.Paths)
            {
                var branch = sentinelTree.get_Branch(path);
                foreach (var item in branch)
                {
                    var str = (item as GH_String)?.Value ?? string.Empty;
                    if (BatchSentinel.TryExtract(str, out var customId))
                    {
                        customIdToBranchPath[customId] = path.ToString();
                    }
                }
            }

            var allInteractions = new List<IAIInteraction>();
            var allMetrics = new List<AIMetrics>();

            // Accumulate extra outputs returned by SentinelTransformOutputs across all sentinels.
            // Key: output param name → merged GH_Structure<IGH_Goo> (one slot per sentinel path).
            var extraOutputAccumulator = new Dictionary<string, GH_Structure<IGH_Goo>>();

            var reconstructedTree = ReconstructOutputTree<T>(
                sentinelTree,
                results,
                (customId, resultBody) =>
                {
                    try
                    {
                        var interactions = provider.Decode(resultBody);
                        if (interactions != null)
                        {
                            allInteractions.AddRange(interactions);

                            // Extract metrics from each interaction
                            foreach (var interaction in interactions)
                            {
                                if (interaction.Metrics != null)
                                {
                                    allMetrics.Add(interaction.Metrics);
                                }
                            }
                        }

                        var primaryItem = decode(customId, resultBody);

                        // Call SentinelTransformOutputs so derived components can reshape/split results
                        var context = new ProcessingUnitContext { SentinelId = customId };
                        var decodedMap = new Dictionary<string, IGH_Goo> { [outputParamName] = primaryItem };
                        var transformed = this.SentinelTransformOutputs(decodedMap, context);

                        // Accumulate any extra keys returned by SentinelTransformOutputs
                        if (transformed != null)
                        {
                            foreach (var kvp in transformed)
                            {
                                if (kvp.Key == outputParamName) continue;
                                if (!extraOutputAccumulator.TryGetValue(kvp.Key, out var extraTree))
                                {
                                    extraTree = new GH_Structure<IGH_Goo>();
                                    extraOutputAccumulator[kvp.Key] = extraTree;
                                }

                                extraTree.Append(kvp.Value);
                            }
                        }

                        return primaryItem;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AIStatefulAsync] Batch decode error for {customId}: {ex.Message}");
                        return decode(customId, resultBody);
                    }
                });

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
                // Aggregate all per-interaction metrics into one named instance
                var aggregatedMetrics = new AIMetrics
                {
                    Provider = this.GetActualAIProviderName(),
                    Model = this.GetModel(),
                };
                foreach (var m in allMetrics)
                {
                    aggregatedMetrics.Combine(m);
                }

                System.Diagnostics.Debug.WriteLine($"[AIStatefulAsync] Aggregated batch metrics: {allMetrics.Count} items, " +
                              $"InputTokens={aggregatedMetrics.InputTokens}, OutputTokens={aggregatedMetrics.OutputTokens}");

                // Store as the single authoritative source for SetMetricsOutput
                this._batchState.PersistedMetrics = aggregatedMetrics;

                // Build AIReturn for body/interactions (used by CurrentAIReturnSnapshot consumers)
                var batchReturn = new AIReturn();
                var batchRequest = new AIRequestCall();
                batchRequest.Initialize(
                    aggregatedMetrics.Provider,
                    aggregatedMetrics.Model,
                    new List<IAIInteraction>(),
                    endpoint: "batch_complete",
                    capability: AICapability.None,
                    toolFilter: null);
                batchReturn.CreateSuccess(allInteractions, request: batchRequest);
                this.SetAIReturnSnapshot(batchReturn);
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
