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
using Newtonsoft.Json;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AIModels;
using SmartHopper.ProviderSdk.Diagnostics;

namespace SmartHopper.ProviderSdk.AICall.Metrics
{
    public class AIMetrics
    {
        /// <summary>
        /// Optional human-readable label for this metrics entry, e.g.
        /// "main", "fallback:ImageToText", "tool:img2text".
        /// Null for the primary call (serialized as absent, not null).
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Role { get; set; }

        /// <summary>
        /// Per-role data item count set by the caller. Null when not applicable.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? DataCount { get; set; }

        /// <summary>
        /// Per-role iteration count set by the caller. Null when not applicable.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? IterationsCount { get; set; }

        /// <summary>
        /// Gets or sets the reason for the finish of the AI call.
        /// </summary>
        public string FinishReason { get; set; }

        /// <summary>
        /// Gets or sets the completion time of the AI call.
        /// </summary>
        public double CompletionTime { get; set; }

        /// <summary>
        /// Gets or sets the provider used for the AI call.
        /// </summary>
        public string Provider { get; set; }

        /// <summary>
        /// Gets or sets the model used for the AI call.
        /// </summary>
        public string Model { get; set; }

        /// <summary>
        /// Gets or sets the number of cached input tokens used by the AI call.
        /// </summary>
        public int InputTokensCached { get; set; }

        /// <summary>
        /// Gets or sets the number of input tokens written to the cache by the AI call (Anthropic: cache_creation_input_tokens).
        /// These are billed at a higher rate than normal input tokens on first write.
        /// </summary>
        public int InputTokensCacheWrite { get; set; }

        /// <summary>
        /// Gets or sets the number of input tokens from the prompt that were used by the AI call.
        /// </summary>
        public int InputTokensPrompt { get; set; }

        /// <summary>
        /// Gets the number of input tokens used by the AI call.
        /// </summary>
        public int InputTokens => this.InputTokensCached + this.InputTokensCacheWrite + this.InputTokensPrompt;

        /// <summary>
        /// Gets or sets the number of output tokens for reasoning that were used by the AI call.
        /// </summary>
        public int OutputTokensReasoning { get; set; }

        /// <summary>
        /// Gets or sets the number of output tokens for response generation that were used by the AI call.
        /// </summary>
        public int OutputTokensGeneration { get; set; }

        /// <summary>
        /// Gets the number of output tokens used by the AI call.
        /// </summary>
        public int OutputTokens => this.OutputTokensReasoning + this.OutputTokensGeneration;

        /// <summary>
        /// Gets the total number of tokens used by the AI call.
        /// </summary>
        public int TotalTokens => this.InputTokens + this.OutputTokens;

        /// <summary>
        /// Gets or sets the estimated number of input tokens (heuristic-based).
        /// Only populated when actual InputTokens are not provided by the AI provider.
        /// Uses character-length approximation (~3.5 chars/token).
        /// </summary>
        public int EstimatedInputTokens { get; set; }

        /// <summary>
        /// Gets or sets the estimated number of output tokens (heuristic-based).
        /// Only populated when actual OutputTokens are not provided by the AI provider.
        /// Uses character-length approximation (~3.5 chars/token).
        /// </summary>
        public int EstimatedOutputTokens { get; set; }

        /// <summary>
        /// Gets the total estimated tokens (input + output).
        /// </summary>
        public int TotalEstimatedTokens => this.EstimatedInputTokens + this.EstimatedOutputTokens;

        /// <summary>
        /// Gets the effective total token count, using the maximum of actual and estimated tokens.
        /// This prevents undercounting when large tool payloads don't report metrics.
        /// </summary>
        public int EffectiveTotalTokens => System.Math.Max(this.TotalTokens, this.TotalEstimatedTokens);

        public int LastEffectiveTotalTokens { get; set; }

        /// <summary>
        /// Gets the context usage percentage (0.0 to 1.0) based on EffectiveTotalTokens and the model's context limit.
        /// Calculated using provider/model from this metrics instance.
        /// Returns null if context limit is unknown or not applicable.
        /// </summary>
        public double? ContextUsagePercent
        {
            get
            {
                if (string.IsNullOrEmpty(this.Provider) || string.IsNullOrEmpty(this.Model))
                {
                    Debug.WriteLine("[AIMetrics.ContextUsagePercent] Missing provider or model");
                    return null;
                }

                var modelManager = AIModelCapabilityRegistry.Instance;
                var capabilities = modelManager?.GetCapabilities(this.Provider, this.Model);
                var contextLimit = capabilities?.ContextLimit;

                if (contextLimit == null || contextLimit.Value <= 0)
                {
                    Debug.WriteLine($"[AIMetrics.ContextUsagePercent] No context limit for {this.Provider}/{this.Model}");
                    return null;
                }

                var effectiveTokens = this.LastEffectiveTotalTokens > 0
                    ? this.LastEffectiveTotalTokens
                    : this.EffectiveTotalTokens;
                var usage = (double)effectiveTokens / contextLimit.Value;
                var roundedUsage = System.Math.Round(usage, 4);

                Debug.WriteLine($"[AIMetrics.ContextUsagePercent] Provider: {this.Provider}, Model: {this.Model}, EffectiveTokens: {effectiveTokens}, ContextLimit: {contextLimit.Value}, Usage: {roundedUsage}");

                return roundedUsage;
            }
        }

        /// <summary>
        /// Value indicating whether the structure of this AIMetrics is valid.
        /// </summary>
        public (bool IsValid, List<SHRuntimeMessage> Errors) IsValid()
        {
            var errors = new List<SHRuntimeMessage>();

            if (string.IsNullOrEmpty(this.Provider) || string.IsNullOrEmpty(this.Model))
            {
                errors.Add(new SHRuntimeMessage(
                    SHRuntimeMessageSeverity.Error,
                    SHRuntimeMessageOrigin.Validation,
                    SHMessageCode.BodyInvalid,
                    "Provider and model fields are required",
                    false));
            }

            if (this.InputTokens < 0 || this.OutputTokens < 0)
            {
                errors.Add(new SHRuntimeMessage(
                    SHRuntimeMessageSeverity.Error,
                    SHRuntimeMessageOrigin.Validation,
                    SHMessageCode.BodyInvalid,
                    "Input and output tokens must be greater than or equal to 0",
                    false));
            }

            if (string.IsNullOrEmpty(this.FinishReason))
            {
                errors.Add(new SHRuntimeMessage(
                    SHRuntimeMessageSeverity.Error,
                    SHRuntimeMessageOrigin.Validation,
                    SHMessageCode.BodyInvalid,
                    "Finish reason must be set",
                    false));
            }

            if (this.CompletionTime < 0)
            {
                errors.Add(new SHRuntimeMessage(
                    SHRuntimeMessageSeverity.Error,
                    SHRuntimeMessageOrigin.Validation,
                    SHMessageCode.BodyInvalid,
                    "Completion time must be greater than or equal to 0",
                    false));
            }

            return (errors.Count == 0, errors);
        }

        /// <summary>
        /// Combines the metrics of another AIMetrics object into this one.
        /// </summary>
        /// <param name="other">The AIMetrics object to combine with.</param>
        public void Combine(AIMetrics other)
        {
            var skipLog = IsDefault(this) && IsDefault(other);

            var otherLast = other.LastEffectiveTotalTokens > 0
                ? other.LastEffectiveTotalTokens
                : other.EffectiveTotalTokens;

            if (!skipLog)
            {
                Debug.WriteLine($"[AIMetrics] Combining metrics:\nProvider: {this.Provider} -> {other.Provider}\nModel: {this.Model} -> {other.Model}\nInputTokensPrompt: {this.InputTokensPrompt} -> {this.InputTokensPrompt + other.InputTokensPrompt}\nInputTokensCached: {this.InputTokensCached} -> {this.InputTokensCached + other.InputTokensCached}\nInputTokensCacheWrite: {this.InputTokensCacheWrite} -> {this.InputTokensCacheWrite + other.InputTokensCacheWrite}\nOutputTokensReasoning: {this.OutputTokensReasoning} -> {this.OutputTokensReasoning + other.OutputTokensReasoning}\nOutputTokensGeneration: {this.OutputTokensGeneration} -> {this.OutputTokensGeneration + other.OutputTokensGeneration}\nEstimatedInputTokens: {this.EstimatedInputTokens} -> {this.EstimatedInputTokens + other.EstimatedInputTokens}\nEstimatedOutputTokens: {this.EstimatedOutputTokens} -> {this.EstimatedOutputTokens + other.EstimatedOutputTokens}\nCompletionTime: {this.CompletionTime} -> {this.CompletionTime + other.CompletionTime}\nFinishReason: {this.FinishReason} -> {other.FinishReason}");
            }

            if (!string.IsNullOrEmpty(other.Provider) && !string.Equals(other.Provider, "Unknown", System.StringComparison.Ordinal))
            {
                this.Provider = other.Provider;
            }

            if (!string.IsNullOrEmpty(other.Model))
            {
                this.Model = other.Model;
            }

            if (other.FinishReason != null)
            {
                this.FinishReason = other.FinishReason;
            }

            this.InputTokensPrompt += other.InputTokensPrompt;
            this.InputTokensCached += other.InputTokensCached;
            this.InputTokensCacheWrite += other.InputTokensCacheWrite;
            this.OutputTokensReasoning += other.OutputTokensReasoning;
            this.OutputTokensGeneration += other.OutputTokensGeneration;
            this.EstimatedInputTokens += other.EstimatedInputTokens;
            this.EstimatedOutputTokens += other.EstimatedOutputTokens;
            this.CompletionTime += other.CompletionTime;
            this.LastEffectiveTotalTokens = otherLast;

            if (this.DataCount.HasValue || other.DataCount.HasValue)
            {
                this.DataCount = (this.DataCount ?? 0) + (other.DataCount ?? 0);
            }

            if (this.IterationsCount.HasValue || other.IterationsCount.HasValue)
            {
                this.IterationsCount = (this.IterationsCount ?? 0) + (other.IterationsCount ?? 0);
            }
        }

        /// <summary>
        /// Combines two string values into a comma-separated list of unique values.
        /// If <paramref name="current"/> is null/empty or matches the default marker,
        /// returns <paramref name="other"/> directly. If <paramref name="other"/> is
        /// null/empty or matches the default marker, returns <paramref name="current"/>
        /// unchanged. If both differ, appends <paramref name="other"/> to the list
        /// only if it is not already present (case-insensitive).
        /// </summary>
        private static string CombineCommaSeparated(string current, string other, string defaultValue = null)
        {
            if (string.IsNullOrEmpty(other))
            {
                return current;
            }

            if (string.Equals(other, defaultValue, System.StringComparison.OrdinalIgnoreCase))
            {
                return current;
            }

            if (string.IsNullOrEmpty(current) || string.Equals(current, defaultValue, System.StringComparison.OrdinalIgnoreCase))
            {
                return other;
            }

            var parts = current.Split(',').Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToList();
            if (!parts.Contains(other, StringComparer.OrdinalIgnoreCase))
            {
                parts.Add(other);
            }

            return string.Join(", ", parts);
        }

        private static bool IsDefault(AIMetrics metrics)
        {
            if (metrics == null)
            {
                return true;
            }

            var providerDefault = string.IsNullOrEmpty(metrics.Provider) || string.Equals(metrics.Provider, "Unknown", System.StringComparison.Ordinal);
            var modelDefault = string.IsNullOrEmpty(metrics.Model);
            var tokensZero = metrics.InputTokensCached == 0
                             && metrics.InputTokensCacheWrite == 0
                             && metrics.InputTokensPrompt == 0
                             && metrics.OutputTokensReasoning == 0
                             && metrics.OutputTokensGeneration == 0;
            var timeZero = metrics.CompletionTime == 0;
            var finishDefault = string.IsNullOrEmpty(metrics.FinishReason);

            return providerDefault && modelDefault && tokensZero && timeZero && finishDefault;
        }
    }
}
