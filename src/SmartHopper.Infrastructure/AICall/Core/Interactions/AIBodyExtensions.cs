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
using System.Linq;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AIModels;

namespace SmartHopper.Infrastructure.AICall.Core.Interactions
{
    /// <summary>
    /// Extension helpers for immutable AI request bodies.
    /// Provides pending tool call detection and common queries without mutation.
    /// </summary>
    public static class AIBodyExtensions
    {
        /// <summary>
        /// Gets the last interaction in the immutable body, or null if none.
        /// </summary>
        /// <param name="body">The AI body to query.</param>
        /// <returns>The last interaction, or null if there are no interactions.</returns>
        public static IAIInteraction GetLastInteraction(this AIBody body)
        {
            return body?.Interactions?.LastOrDefault();
        }

        /// <summary>
        /// Gets the last interaction matching the specified agent, or null if none.
        /// Mirrors legacy AIBody.GetLastInteraction(AIAgent).
        /// </summary>
        /// <param name="body">The AI body to query.</param>
        /// <param name="agent">Agent to filter interactions by.</param>
        /// <returns>The last matching interaction, or null if none exist.</returns>
        public static IAIInteraction GetLastInteraction(this AIBody body, AIAgent agent)
        {
            return body?.Interactions?.LastOrDefault(i => i.Agent == agent);
        }

        /// <summary>
        /// Gets the last interaction whose agent name matches the provided string, or null if none.
        /// Mirrors legacy AIBody.GetLastInteraction(string).
        /// </summary>
        /// <param name="body">The AI body to query.</param>
        /// <param name="agent">Agent name to filter by.</param>
        /// <returns>The last matching interaction, or null if none exist.</returns>
        public static IAIInteraction GetLastInteraction(this AIBody body, string agent)
        {
            return body?.Interactions?.LastOrDefault(i => i.Agent.ToString() == agent);
        }

        /// <summary>
        /// Gets the content of the last text interaction in the body.
        /// </summary>
        /// <param name="body">The AI body to query.</param>
        /// <returns>The text content of the last AIInteractionText, or null if no text interaction exists.</returns>
        public static string GetLastText(this AIBody body)
        {
            return body?.Interactions?.LastOrDefault(i => i is AIInteractionText) is AIInteractionText textInteraction
                ? textInteraction.Content
                : null;
        }

        /// <summary>
        /// Computes the number of pending tool calls by matching tool call Ids
        /// against tool result Ids in the interactions list.
        /// </summary>
        /// <param name="body">The AI body to query.</param>
        /// <returns>The number of pending tool calls.</returns>
        public static int PendingToolCallsCount(this AIBody body)
        {
            if (body?.Interactions == null || body.Interactions.Count == 0)
            {
                return 0;
            }

            var toolCalls = body.Interactions.OfType<AIInteractionToolCall>();
            var toolResults = body.Interactions.OfType<AIInteractionToolResult>();
            var resultIds = new HashSet<string>(toolResults.Select(tr => tr.Id), StringComparer.Ordinal);

            int matched = toolCalls.Count(tc => resultIds.Contains(tc.Id));
            int pending = toolCalls.Count() - matched;
            return pending;
        }

        /// <summary>
        /// Gets the list of pending tool calls by matching tool call Ids against
        /// tool result Ids in the interactions list.
        /// </summary>
        /// <param name="body">The AI body to query.</param>
        /// <returns>A list of tool calls without corresponding results.</returns>
        public static List<AIInteractionToolCall> PendingToolCallsList(this AIBody body)
        {
            if (body?.Interactions == null || body.Interactions.Count == 0)
            {
                return new List<AIInteractionToolCall>();
            }

            var toolCalls = body.Interactions.OfType<AIInteractionToolCall>();
            var toolResults = body.Interactions.OfType<AIInteractionToolResult>();
            var resultIds = new HashSet<string>(toolResults.Select(tr => tr.Id), StringComparer.Ordinal);

            return toolCalls.Where(tc => !resultIds.Contains(tc.Id)).ToList();
        }

        /// <summary>
        /// Returns a new immutable body with the provided interaction appended.
        /// </summary>
        /// <param name="body">The AI body to mutate.</param>
        /// <param name="interaction">The interaction to append.</param>
        /// <returns>A new immutable body including the appended interaction.</returns>
        public static AIBody WithAppended(this AIBody body, IAIInteraction interaction)
        {
            // When mutating an existing immutable body for session history, clear previous 'new' markers
            // so only the newly appended item is considered new.
            var builder = AIBodyBuilder.FromImmutable(body).ClearNewMarkers();
            builder.Add(interaction);
            return builder.Build();
        }

        /// <summary>
        /// Returns a new immutable body with the provided interactions appended.
        /// </summary>
        /// <param name="body">The AI body to mutate.</param>
        /// <param name="interactions">The interactions to append.</param>
        /// <returns>A new immutable body including the appended interactions.</returns>
        public static AIBody WithAppendedRange(this AIBody body, IEnumerable<IAIInteraction> interactions)
        {
            // Clear previous 'new' markers so only the appended range is considered new
            var builder = AIBodyBuilder.FromImmutable(body).ClearNewMarkers();
            builder.AddRange(interactions);
            return builder.Build();
        }

        /// <summary>
        /// Returns the interactions that were newly added or replaced in the last mutation
        /// that produced this immutable body, based on <see cref="AIBody.InteractionsNew"/>.
        /// </summary>
        /// <param name="body">The AI body to query.</param>
        /// <returns>A list of interactions that were marked as new or replaced.</returns>
        public static List<IAIInteraction> GetNewInteractions(this AIBody body)
        {
            var result = new List<IAIInteraction>();
            if (body == null || body.Interactions == null || body.Interactions.Count == 0)
            {
                return result;
            }

            var indices = body.InteractionsNew ?? new List<int>();
            foreach (var idx in indices)
            {
                if (idx >= 0 && idx < body.Interactions.Count)
                {
                    var it = body.Interactions[idx];
                    if (it != null)
                    {
                        result.Add(it);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Gets the effective token count for the body, using the maximum of:
        /// - metrics-based token count (from interactions with metrics)
        /// - estimated token count (from interaction content lengths)
        /// This prevents undercounting when large tool payloads don't have metrics.
        /// Populates AIMetrics.Estimated* fields only when actual tokens are not provided.
        /// </summary>
        /// <param name="body">The AI body to query.</param>
        /// <returns>The effective token count.</returns>
        public static int GetEffectiveTokenCount(this AIBody body)
        {
            if (body == null)
            {
                return 0;
            }

            var metrics = body.Metrics;
            if (metrics == null)
            {
                return 0;
            }

            // Only estimate if actual tokens are not provided by the AI provider
            var hasActualInputTokens = metrics.InputTokens > 0;
            var hasActualOutputTokens = metrics.OutputTokens > 0;

            if (!hasActualInputTokens || !hasActualOutputTokens)
            {
                var (estimatedInput, estimatedOutput) = EstimateTokensFromInteractions(body.Interactions);
                
                // Only populate estimated fields when actual tokens are missing
                if (!hasActualInputTokens)
                {
                    metrics.EstimatedInputTokens = estimatedInput;
                }
                if (!hasActualOutputTokens)
                {
                    metrics.EstimatedOutputTokens = estimatedOutput;
                }
            }

            var effectiveTokens = metrics.EffectiveTotalTokens;
            Debug.WriteLine($"[AIBody.GetEffectiveTokenCount] Actual tokens: {metrics.TotalTokens}, Estimated tokens: {metrics.TotalEstimatedTokens}, Effective tokens: {effectiveTokens}");
            return effectiveTokens;
        }

        /// <summary>
        /// Estimates the token count from interaction content lengths, separated by input and output.
        /// Uses a heuristic approximation (3.5 chars/token) to detect when prompts balloon due to large tool results.
        /// This is a conservative estimate meant for triggering context management safeguards, not exact billing.
        /// </summary>
        /// <param name="interactions">The interactions to estimate tokens for.</param>
        /// <returns>Tuple of (estimatedInputTokens, estimatedOutputTokens) based on content length.</returns>
        private static (int estimatedInput, int estimatedOutput) EstimateTokensFromInteractions(IReadOnlyList<IAIInteraction> interactions)
        {
            if (interactions == null || interactions.Count == 0)
            {
                Debug.WriteLine("[EstimateTokensFromInteractions] No interactions to estimate");
                return (0, 0);
            }

            try
            {
                // Heuristic: approximate tokens from UTF-16 string length.
                // A common rough rule-of-thumb is ~4 characters per token for English.
                // We intentionally bias upward a bit by using 3.5 chars/token.
                // This is only used to detect when the prompt is ballooning (e.g., huge tool results).
                const double charsPerToken = 3.5;

                long inputChars = 0;
                long outputChars = 0;
                int toolCallCount = 0;
                int toolResultCount = 0;
                int textCount = 0;

                Debug.WriteLine($"[EstimateTokensFromInteractions] Processing {interactions.Count} interactions");

                foreach (var it in interactions)
                {
                    if (it == null)
                    {
                        continue;
                    }

                    // Classify interactions as input (user/system/context) or output (assistant/tool result)
                    var isInput = it.Agent == AIAgent.User || it.Agent == AIAgent.System || it.Agent == AIAgent.Context;

                    switch (it)
                    {
                        case AIInteractionText t:
                            textCount++;
                            var textChars = (t.Content?.Length ?? 0) + (t.Reasoning?.Length ?? 0);
                            if (isInput)
                            {
                                inputChars += textChars;
                                Debug.WriteLine($"[EstimateTokensFromInteractions] Text input: {t.Agent}, chars: {textChars}");
                            }
                            else
                            {
                                outputChars += textChars;
                                Debug.WriteLine($"[EstimateTokensFromInteractions] Text output: {t.Agent}, chars: {textChars}");
                            }
                            break;

                        case AIInteractionToolResult tr:
                            toolResultCount++;
                            // Tool results are output (AI's tool execution results)
                            var resultChars = (tr.Name?.Length ?? 0) + (tr.Result?.ToString()?.Length ?? 0) + (tr.Id?.Length ?? 0);
                            outputChars += resultChars;
                            Debug.WriteLine($"[EstimateTokensFromInteractions] Tool result '{tr.Name}': {resultChars} chars");
                            break;

                        case AIInteractionToolCall tc:
                            toolCallCount++;
                            // Tool calls are output (AI requesting tool execution)
                            var callChars = (tc.Name?.Length ?? 0) + (tc.Arguments?.ToString()?.Length ?? 0) + (tc.Id?.Length ?? 0) + (tc.Reasoning?.Length ?? 0);
                            outputChars += callChars;
                            Debug.WriteLine($"[EstimateTokensFromInteractions] Tool call '{tc.Name}': {callChars} chars");
                            break;

                        case AIInteractionError err:
                            // Errors are typically output (provider/system errors)
                            outputChars += (err.Content?.Length ?? 0);
                            Debug.WriteLine($"[EstimateTokensFromInteractions] Error: {err.Content?.Length ?? 0} chars");
                            break;

                        default:
                            var defaultChars = it.ToString()?.Length ?? 0;
                            if (isInput)
                            {
                                inputChars += defaultChars;
                                Debug.WriteLine($"[EstimateTokensFromInteractions] Default input: {it.Agent}, chars: {defaultChars}");
                            }
                            else
                            {
                                outputChars += defaultChars;
                                Debug.WriteLine($"[EstimateTokensFromInteractions] Default output: {it.Agent}, chars: {defaultChars}");
                            }
                            break;
                    }
                }

                var inputTokens = inputChars > 0 ? (int)Math.Ceiling(inputChars / charsPerToken) : 0;
                var outputTokens = outputChars > 0 ? (int)Math.Ceiling(outputChars / charsPerToken) : 0;

                Debug.WriteLine($"[EstimateTokensFromInteractions] Summary: {textCount} text, {toolCallCount} tool calls, {toolResultCount} tool results");
                Debug.WriteLine($"[EstimateTokensFromInteractions] Char counts - Input: {inputChars}, Output: {outputChars}");
                Debug.WriteLine($"[EstimateTokensFromInteractions] Token estimates - Input: {inputTokens}, Output: {outputTokens}");

                return (Math.Max(0, inputTokens), Math.Max(0, outputTokens));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[EstimateTokensFromInteractions] Error: {ex.Message}");
                // Best-effort only.
                return (0, 0);
            }
        }
    }
}
