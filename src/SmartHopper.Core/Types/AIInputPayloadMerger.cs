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
using System.Linq;
using Grasshopper.Kernel.Data;
using SmartHopper.Core.Models;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AIModels;

namespace SmartHopper.Core.Types
{
    /// <summary>
    /// Merges multiple AIInputPayload inputs per branch path into a single AIBody.
    /// Respects branch path isolation and preserves interaction order.
    /// </summary>
    public static class AIInputPayloadMerger
    {
        /// <summary>
        /// Merges all AIInputPayload inputs on a given branch path into a single AIBody.
        /// Interactions are merged in the order payloads are received (wire index order).
        /// Context payloads are handled specially and converted to context filters.
        /// </summary>
        /// <param name="payloads">The list of AIInputPayload objects to merge (in wire order).</param>
        /// <returns>A merged AIBody, or null if no payloads provided.</returns>
        public static AIBody MergePerBranch(IEnumerable<AIInputPayload> payloads)
        {
            if (payloads == null)
            {
                return null;
            }

            var payloadList = payloads.ToList();
            if (payloadList.Count == 0)
            {
                return null;
            }

            var mergedInteractions = new List<IAIInteraction>();
            var contextFilters = new List<string>();
            var sourceCapabilities = new HashSet<AICapability>();

            // Merge all payloads in order, preserving interaction sequence
            foreach (var payload in payloadList)
            {
                if (payload == null || payload.Interactions == null)
                {
                    continue;
                }

                // Handle context payloads specially
                if (payload.PayloadType == AIInputPayloadType.Context)
                {
                    // Extract context filter from the interaction content
                    var contextInteraction = payload.Interactions.FirstOrDefault(i => i.Agent == AIAgent.Context);
                    if (contextInteraction is AIInteractionText contextText && !string.IsNullOrWhiteSpace(contextText.Content))
                    {
                        contextFilters.Add(contextText.Content);
                    }
                }
                else
                {
                    // Add all interactions from this payload
                    foreach (var interaction in payload.Interactions)
                    {
                        if (interaction != null)
                        {
                            mergedInteractions.Add(interaction);
                        }
                    }

                    // Track source capabilities
                    if (payload.InputCapabilityAtSource != AICapability.None)
                    {
                        sourceCapabilities.Add(payload.InputCapabilityAtSource);
                    }
                }
            }

            // Build context filter string (comma-separated if multiple)
            var contextFilter = contextFilters.Count > 0
                ? string.Join(",", contextFilters)
                : "-*";  // Default: no context if none specified

            Debug.WriteLine($"[AIInputPayloadMerger] Merged {payloadList.Count} payloads into {mergedInteractions.Count} interactions, context filter: {contextFilter}");

            // Create the merged AIBody
            var builder = new AIBodyBuilder();

            foreach (var interaction in mergedInteractions)
            {
                if (interaction is AIInteractionText textInteraction)
                {
                    builder.AddText(textInteraction.Agent, textInteraction.Content);
                }
                else if (interaction is AIInteractionImage imageInteraction)
                {
                    builder.Add(imageInteraction);
                }
                else if (interaction is AIInteractionAudio audioInteraction)
                {
                    builder.Add(audioInteraction);
                }
                else if (interaction is AIInteractionToolCall toolCall)
                {
                    builder.Add(toolCall);
                }
                else if (interaction is AIInteractionToolResult toolResult)
                {
                    builder.Add(toolResult);
                }
                else
                {
                    // Generic fallback
                    builder.Add(interaction);
                }
            }

            // Set context filter
            builder.WithContextFilter(contextFilter);

            return builder.Build();
        }

        /// <summary>
        /// Merges AIInputPayload inputs from a data tree, respecting branch path isolation.
        /// Returns a dictionary mapping each branch path to its merged AIBody.
        /// </summary>
        /// <param name="payloadTree">The data tree of GH_AIInputPayload objects.</param>
        /// <returns>A dictionary mapping GH_Path to merged AIBody.</returns>
        public static Dictionary<GH_Path, AIBody> MergePayloadsPerBranch(GH_Structure<GH_AIInputPayload> payloadTree)
        {
            var result = new Dictionary<GH_Path, AIBody>();

            if (payloadTree == null || payloadTree.DataCount == 0)
            {
                return result;
            }

            // Group payloads by branch path
            var pathGroups = new Dictionary<GH_Path, List<AIInputPayload>>();

            foreach (var path in payloadTree.Paths)
            {
                var branch = payloadTree.get_Branch(path);
                if (branch == null || branch.Count == 0)
                {
                    continue;
                }

                var payloads = new List<AIInputPayload>();
                foreach (GH_AIInputPayload gooItem in branch)
                {
                    if (gooItem != null && gooItem.IsValid && gooItem.Value != null)
                    {
                        payloads.Add(gooItem.Value);
                    }
                }

                if (payloads.Count > 0)
                {
                    pathGroups[path] = payloads;
                }
            }

            // Merge payloads for each path
            foreach (var kvp in pathGroups)
            {
                var mergedBody = MergePerBranch(kvp.Value);
                if (mergedBody != null)
                {
                    result[kvp.Key] = mergedBody;
                }
            }

            Debug.WriteLine($"[AIInputPayloadMerger] Merged {payloadTree.DataCount} payloads across {result.Count} branch paths");

            return result;
        }
    }
}
