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
using Newtonsoft.Json;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AIModels;
using SmartHopper.ProviderSdk.Diagnostics;
using SmartHopper.ProviderSdk.Logging;
namespace SmartHopper.ProviderSdk.AICall.Metrics
{
    /// <summary>
    /// Usage metrics for one AI call. Since <c>smarthopper-models/2</c> the
    /// measured usage lives in <see cref="Quantities"/> — a free-form
    /// <c>"&lt;kind&gt;/&lt;measured-unit&gt;" → value</c> dictionary sharing
    /// the <see cref="AIQuantityKey"/> vocabulary with
    /// <see cref="AIPriceEntry.Kind"/> — instead of fixed token fields, so
    /// token-, duration-, character-, item- and invocation-metered usage all
    /// fit the same shape. Cost estimates are per-currency
    /// (<see cref="EstimatedCosts"/>); provider-reported billed amounts stay
    /// separate in <see cref="ReportedCosts"/>.
    /// </summary>
    public record AIMetrics
    {
        /// <summary>
        /// Optional human-readable label identifying which call inside a
        /// component solve produced the usage — e.g. "main" (primary component
        /// request), "tool:{name}" (direct tool call), "fallback" (request
        /// preparation/helper call), "passthrough:{name}" (conversion step).
        /// Null/absent for entries produced without explicit attribution
        /// (serialized as absent, not null).
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Role { get; init; }

        /// <summary>
        /// Per-role data item count set by the caller. Null when not applicable.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? DataCount { get; init; }

        /// <summary>
        /// Per-role iteration count set by the caller. Null when not applicable.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? IterationsCount { get; init; }

        /// <summary>
        /// Gets or sets the reason for the finish of the AI call.
        /// </summary>
        public string FinishReason { get; init; }

        /// <summary>
        /// Gets or sets the completion time of the AI call.
        /// </summary>
        public double CompletionTime { get; init; }

        /// <summary>
        /// UTC instant at which the call completed (or at which a batch result
        /// became available locally). Distinct from <see cref="CompletionTime"/>,
        /// which is the elapsed duration. Null for metrics produced before
        /// completion timestamps existed — unknown timestamps are never
        /// fabricated at restore or display time.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public DateTimeOffset? CompletedAt { get; init; }

        /// <summary>
        /// Stable identifier of the execution that produced these metrics.
        /// Synchronous calls get a generated id; batch calls use their customId
        /// so attribution survives save/reopen. Every replay of a shared result
        /// carries the same id, letting aggregators deduplicate identical
        /// provenance instead of charging the call once per placement.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string ExecutionId { get; init; }

        /// <summary>
        /// Gets or sets the provider used for the AI call.
        /// </summary>
        public string Provider { get; init; }

        /// <summary>
        /// Gets or sets the model used for the AI call.
        /// </summary>
        public string Model { get; init; }

        private Dictionary<string, double> _quantities;

        /// <summary>
        /// Gets the measured usage quantities: <c>"&lt;kind&gt;/&lt;unit&gt;" → value</c>.
        /// Decoders record what the provider reported, un-normalized — e.g.
        /// <c>text_input/token</c>, <c>audio_input/second</c>,
        /// <c>tool:web_search/invocation</c>. Conversion to billed units happens
        /// in <see cref="AICostCalculator"/>, never here.
        /// </summary>
        [JsonProperty("quantities", NullValueHandling = NullValueHandling.Ignore)]
        public IReadOnlyDictionary<string, double> Quantities
        {
            get => this._quantities;
            init => this._quantities = value != null
                ? new Dictionary<string, double>(value, StringComparer.Ordinal)
                : null;
        }

        private Dictionary<string, decimal> _reportedCosts;

        /// <summary>
        /// Gets the cost reported by the provider itself (e.g. OpenRouter's
        /// <c>usage.cost</c>), keyed by currency. This is the billed ground
        /// truth when present — kept distinct from the catalogue-derived
        /// <see cref="EstimatedCosts"/>.
        /// </summary>
        [JsonProperty("reported_costs", NullValueHandling = NullValueHandling.Ignore)]
        public IReadOnlyDictionary<string, decimal> ReportedCosts
        {
            get => this._reportedCosts;
            init => this._reportedCosts = value != null
                ? new Dictionary<string, decimal>(value, StringComparer.Ordinal)
                : null;
        }

        private IReadOnlyDictionary<string, decimal> _estimatedCosts;

        /// <summary>
        /// Initializes a new instance of the <see cref="AIMetrics"/> class.
        /// </summary>
        public AIMetrics()
        {
        }

        /// <summary>
        /// Copy constructor: clones the quantity and cost dictionaries so
        /// <c>with</c> expressions never share mutable storage with the source.
        /// </summary>
        /// <param name="original">The metrics to copy.</param>
        protected AIMetrics(AIMetrics original)
        {
            this.Role = original.Role;
            this.DataCount = original.DataCount;
            this.IterationsCount = original.IterationsCount;
            this.FinishReason = original.FinishReason;
            this.CompletionTime = original.CompletionTime;
            this.CompletedAt = original.CompletedAt;
            this.ExecutionId = original.ExecutionId;
            this.Provider = original.Provider;
            this.Model = original.Model;
            this._quantities = original._quantities != null
                ? new Dictionary<string, double>(original._quantities, StringComparer.Ordinal)
                : null;
            this._reportedCosts = original._reportedCosts != null
                ? new Dictionary<string, decimal>(original._reportedCosts, StringComparer.Ordinal)
                : null;
            this._estimatedCosts = original._estimatedCosts != null
                ? new Dictionary<string, decimal>(original._estimatedCosts, StringComparer.Ordinal)
                : null;
            this.LastEffectiveTotalTokens = original.LastEffectiveTotalTokens;
        }

        /// <summary>
        /// Gets the catalogue-derived estimated cost per currency.
        /// Calculated lazily by <see cref="AICostCalculator"/> from
        /// <see cref="Quantities"/> and the model's <see cref="AIModelPricing"/>
        /// entries — never merged across currencies.
        /// </summary>
        [JsonProperty("estimated_costs", NullValueHandling = NullValueHandling.Ignore)]
        public IReadOnlyDictionary<string, decimal> EstimatedCosts
        {
            get
            {
                this._estimatedCosts ??= AICostCalculator.Calculate(this);
                return this._estimatedCosts;
            }

            init => this._estimatedCosts = value;
        }

        public int LastEffectiveTotalTokens { get; init; }

        /// <summary>Returns the quantity recorded under <c>kind/unit</c>, or 0.</summary>
        public double Quantity(string kind, AIPriceUnit unit)
        {
            if (this.Quantities != null && this.Quantities.TryGetValue(AIQuantityKey.Key(kind, unit), out var v))
            {
                return v;
            }

            return 0d;
        }

        /// <summary>
        /// Returns a copy with <c>kind/unit</c> set to <paramref name="value"/>.
        /// Any previously computed <see cref="EstimatedCosts"/> is invalidated.
        /// </summary>
        public AIMetrics WithQuantity(string kind, AIPriceUnit unit, double value)
        {
            var quantities = this.Quantities != null
                ? new Dictionary<string, double>(this.Quantities, StringComparer.Ordinal)
                : new Dictionary<string, double>(StringComparer.Ordinal);
            quantities[AIQuantityKey.Key(kind, unit)] = value;
            return this with { Quantities = quantities, EstimatedCosts = null };
        }

        /// <summary>
        /// Returns a copy with <paramref name="value"/> added to the quantity
        /// recorded under <c>kind/unit</c> (streaming accumulation).
        /// </summary>
        public AIMetrics WithAddedQuantity(string kind, AIPriceUnit unit, double value)
            => this.WithQuantity(kind, unit, this.Quantity(kind, unit) + value);

        /// <summary>Builds a quantities dictionary from (kind, unit, value) triples; zero values are skipped.</summary>
        public static IReadOnlyDictionary<string, double> QuantitiesOf(params (string kind, AIPriceUnit unit, double value)[] items)
        {
            var dict = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var (kind, unit, value) in items)
            {
                if (value != 0d)
                {
                    dict[AIQuantityKey.Key(kind, unit)] = value;
                }
            }

            return dict;
        }

        /// <summary>Writes <c>kind/unit</c> into the quantities dictionary; used by init-time convenience accessors.</summary>
        private void SetQuantity(string kind, AIPriceUnit unit, double value)
        {
            this._quantities ??= new Dictionary<string, double>(StringComparer.Ordinal);
            this._quantities[AIQuantityKey.Key(kind, unit)] = value;
        }

        /// <summary>Gets or init-sets the number of cached input tokens (<c>cache_read/token</c>).</summary>
        [JsonIgnore]
        public int InputTokensCached
        {
            get => (int)this.Quantity(AIQuantityKey.CacheRead, AIPriceUnit.Token);
            init => this.SetQuantity(AIQuantityKey.CacheRead, AIPriceUnit.Token, value);
        }

        /// <summary>Gets or init-sets the number of tokens written to the provider cache (<c>cache_write/token</c>).</summary>
        [JsonIgnore]
        public int InputTokensCacheWrite
        {
            get => (int)this.Quantity(AIQuantityKey.CacheWrite, AIPriceUnit.Token);
            init => this.SetQuantity(AIQuantityKey.CacheWrite, AIPriceUnit.Token, value);
        }

        /// <summary>Gets or init-sets the number of non-cached input tokens (<c>text_input/token</c>).</summary>
        [JsonIgnore]
        public int InputTokensPrompt
        {
            get => (int)this.Quantity(AIQuantityKey.TextInput, AIPriceUnit.Token);
            init => this.SetQuantity(AIQuantityKey.TextInput, AIPriceUnit.Token, value);
        }

        /// <summary>
        /// Gets the number of input tokens used by the AI call.
        /// </summary>
        [JsonIgnore]
        public int InputTokens => this.InputTokensCached + this.InputTokensCacheWrite + this.InputTokensPrompt;

        /// <summary>Gets or init-sets the number of hidden reasoning tokens (<c>internal_reasoning/token</c>).</summary>
        [JsonIgnore]
        public int OutputTokensReasoning
        {
            get => (int)this.Quantity(AIQuantityKey.InternalReasoning, AIPriceUnit.Token);
            init => this.SetQuantity(AIQuantityKey.InternalReasoning, AIPriceUnit.Token, value);
        }

        /// <summary>Gets or init-sets the number of generated output tokens (<c>text_output/token</c>).</summary>
        [JsonIgnore]
        public int OutputTokensGeneration
        {
            get => (int)this.Quantity(AIQuantityKey.TextOutput, AIPriceUnit.Token);
            init => this.SetQuantity(AIQuantityKey.TextOutput, AIPriceUnit.Token, value);
        }

        /// <summary>
        /// Gets the number of output tokens used by the AI call.
        /// </summary>
        [JsonIgnore]
        public int OutputTokens => this.OutputTokensReasoning + this.OutputTokensGeneration;

        /// <summary>
        /// Gets the total number of tokens used by the AI call.
        /// </summary>
        [JsonIgnore]
        public int TotalTokens => this.InputTokens + this.OutputTokens;

        /// <summary>
        /// Gets or init-sets the estimated number of input tokens (heuristic-based).
        /// Only populated when actual InputTokens are not provided by the AI provider.
        /// Uses character-length approximation (~3.5 chars/token).
        /// </summary>
        [JsonIgnore]
        public int EstimatedInputTokens
        {
            get => (int)this.Quantity(AIQuantityKey.EstimatedTextInput, AIPriceUnit.Token);
            init => this.SetQuantity(AIQuantityKey.EstimatedTextInput, AIPriceUnit.Token, value);
        }

        /// <summary>
        /// Gets or init-sets the estimated number of output tokens (heuristic-based).
        /// Only populated when actual OutputTokens are not provided by the AI provider.
        /// Uses character-length approximation (~3.5 chars/token).
        /// </summary>
        [JsonIgnore]
        public int EstimatedOutputTokens
        {
            get => (int)this.Quantity(AIQuantityKey.EstimatedTextOutput, AIPriceUnit.Token);
            init => this.SetQuantity(AIQuantityKey.EstimatedTextOutput, AIPriceUnit.Token, value);
        }

        /// <summary>
        /// Gets the total estimated tokens (input + output).
        /// </summary>
        [JsonIgnore]
        public int TotalEstimatedTokens => this.EstimatedInputTokens + this.EstimatedOutputTokens;

        /// <summary>
        /// Gets the effective total token count, using the maximum of actual and estimated tokens.
        /// This prevents undercounting when large tool payloads don't report metrics.
        /// </summary>
        [JsonIgnore]
        public int EffectiveTotalTokens => Math.Max(this.TotalTokens, this.TotalEstimatedTokens);

        /// <summary>
        /// Gets the context usage percentage (0.0 to 1.0) based on EffectiveTotalTokens and the model's context limit.
        /// Calculated using provider/model from this metrics instance.
        /// Returns null if context limit is unknown or not applicable.
        /// </summary>
        [JsonIgnore]
        public double? ContextUsagePercent
        {
            get
            {
                if (string.IsNullOrEmpty(this.Provider) || string.IsNullOrEmpty(this.Model))
                {
                    Logger.For<AIMetrics>().Warning("[AIMetrics.ContextUsagePercent] Missing provider or model");
                    return null;
                }

                var modelManager = AIModelCapabilityRegistry.Instance;
                var capabilities = modelManager?.GetCapabilities(this.Provider, this.Model);
                var contextLimit = capabilities?.ContextLimit;

                if (contextLimit == null || contextLimit.Value <= 0)
                {
                    Logger.For<AIMetrics>().Trace($"[AIMetrics.ContextUsagePercent] No context limit for {this.Provider}/{this.Model}");
                    return null;
                }

                var effectiveTokens = this.LastEffectiveTotalTokens > 0
                    ? this.LastEffectiveTotalTokens
                    : this.EffectiveTotalTokens;
                var usage = (double)effectiveTokens / contextLimit.Value;
                var roundedUsage = Math.Round(usage, 4);

                Logger.For<AIMetrics>().Trace($"[AIMetrics.ContextUsagePercent] Provider: {this.Provider}, Model: {this.Model}, EffectiveTokens: {effectiveTokens}, ContextLimit: {contextLimit.Value}, Usage: {roundedUsage}");

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
        /// Returns a new <see cref="AIMetrics"/> that combines the values of this
        /// metrics object with another one, without mutating either instance.
        /// Quantities sum per key; estimated and reported costs sum per currency.
        /// </summary>
        /// <param name="other">The <see cref="AIMetrics"/> to combine with.</param>
        /// <returns>A new combined <see cref="AIMetrics"/>.</returns>
        public AIMetrics WithCombined(AIMetrics other)
        {
            if (other == null)
            {
                return this;
            }

            var skipLog = IsDefault(this) && IsDefault(other);

            var otherLast = other.LastEffectiveTotalTokens > 0
                ? other.LastEffectiveTotalTokens
                : other.EffectiveTotalTokens;

            if (!skipLog)
            {
                Logger.For<AIMetrics>().Trace($"[AIMetrics] Combining metrics:\nProvider: {this.Provider} -> {other.Provider}\nModel: {this.Model} -> {other.Model}\nQuantities: {this.Quantities?.Count ?? 0} keys -> +{other.Quantities?.Count ?? 0} keys\nCompletionTime: {this.CompletionTime} -> {this.CompletionTime + other.CompletionTime}\nFinishReason: {this.FinishReason} -> {other.FinishReason}");
            }

            var dataCount = this.DataCount ?? other.DataCount;
            if (this.DataCount.HasValue || other.DataCount.HasValue)
            {
                dataCount = (this.DataCount ?? 0) + (other.DataCount ?? 0);
            }

            var iterationsCount = this.IterationsCount ?? other.IterationsCount;
            if (this.IterationsCount.HasValue || other.IterationsCount.HasValue)
            {
                iterationsCount = (this.IterationsCount ?? 0) + (other.IterationsCount ?? 0);
            }

            return this with
            {
                Quantities = MergeQuantities(this.Quantities, other.Quantities),
                EstimatedCosts = MergeCosts(this.EstimatedCosts, other.EstimatedCosts),
                ReportedCosts = MergeCosts(this.ReportedCosts, other.ReportedCosts),
                Provider = CombineCommaSeparated(this.Provider, other.Provider, "Unknown"),
                Model = CombineCommaSeparated(this.Model, other.Model),
                Role = CombineCommaSeparated(this.Role, other.Role),
                FinishReason = other.FinishReason ?? this.FinishReason,
                CompletionTime = this.CompletionTime + other.CompletionTime,
                CompletedAt = MaxCompletedAt(this.CompletedAt, other.CompletedAt),
                ExecutionId = null,
                LastEffectiveTotalTokens = otherLast > 0 ? otherLast : this.LastEffectiveTotalTokens,
                DataCount = dataCount,
                IterationsCount = iterationsCount,
            };
        }

        private static DateTimeOffset? MaxCompletedAt(DateTimeOffset? a, DateTimeOffset? b)
        {
            if (!a.HasValue) return b;
            if (!b.HasValue) return a;
            return a.Value >= b.Value ? a : b;
        }

        private static IReadOnlyDictionary<string, double> MergeQuantities(
            IReadOnlyDictionary<string, double> a,
            IReadOnlyDictionary<string, double> b)
        {
            if (a == null || a.Count == 0)
            {
                return b;
            }

            if (b == null || b.Count == 0)
            {
                return a;
            }

            var merged = new Dictionary<string, double>(a, StringComparer.Ordinal);
            foreach (var kv in b)
            {
                merged[kv.Key] = (merged.TryGetValue(kv.Key, out var cur) ? cur : 0d) + kv.Value;
            }

            return merged;
        }

        private static IReadOnlyDictionary<string, decimal> MergeCosts(
            IReadOnlyDictionary<string, decimal> a,
            IReadOnlyDictionary<string, decimal> b)
        {
            if (a == null || a.Count == 0)
            {
                return b;
            }

            if (b == null || b.Count == 0)
            {
                return a;
            }

            var merged = new Dictionary<string, decimal>(a, StringComparer.Ordinal);
            foreach (var kv in b)
            {
                merged[kv.Key] = (merged.TryGetValue(kv.Key, out var cur) ? cur : 0m) + kv.Value;
            }

            return merged;
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
            var quantitiesEmpty = metrics.Quantities == null || metrics.Quantities.Count == 0;
            var timeZero = metrics.CompletionTime == 0;
            var finishDefault = string.IsNullOrEmpty(metrics.FinishReason);

            return providerDefault && modelDefault && quantitiesEmpty && timeZero && finishDefault;
        }
    }
}
