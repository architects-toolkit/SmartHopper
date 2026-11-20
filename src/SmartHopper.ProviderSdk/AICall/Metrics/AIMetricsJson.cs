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
using Newtonsoft.Json.Linq;

namespace SmartHopper.ProviderSdk.AICall.Metrics
{
    /// <summary>
    /// Shared serialization for <see cref="AIMetrics"/> entries. One codec is used
    /// by component metrics output, unit-result provenance persistence, and the
    /// metrics compose/decompose components so every consumer agrees on field
    /// names — including provenance fields added later (completed_at,
    /// execution_id, reused).
    /// </summary>
    public static class AIMetricsJson
    {
        /// <summary>Serializes a metric entry to JSON.</summary>
        /// <param name="metrics">The metric to serialize.</param>
        /// <param name="dataCount">Optional override for data_count; when null the metric's own value is used.</param>
        /// <param name="reused">Marks the entry as replayed provenance (no work executed this run).</param>
        /// <returns>A JSON object, or null when the metric is null.</returns>
        public static JObject ToJson(AIMetrics metrics, int? dataCount = null, bool reused = false)
        {
            if (metrics == null)
            {
                return null;
            }

            var obj = new JObject
            {
                ["ai_provider"] = metrics.Provider,
                ["ai_model"] = metrics.Model,
                ["quantities"] = metrics.Quantities != null
                    ? JObject.FromObject(metrics.Quantities)
                    : new JObject(),
                ["costs"] = new JObject
                {
                    ["estimated"] = metrics.EstimatedCosts != null ? JObject.FromObject(metrics.EstimatedCosts) : new JObject(),
                    ["reported"] = metrics.ReportedCosts != null ? JObject.FromObject(metrics.ReportedCosts) : new JObject(),
                },
                ["finish_reason"] = metrics.FinishReason,
                ["completion_time"] = metrics.CompletionTime,
                ["context_usage_percent"] = metrics.ContextUsagePercent,
            };

            if (metrics.CompletedAt.HasValue)
            {
                obj["completed_at"] = metrics.CompletedAt.Value.UtcDateTime.ToString("o");
            }

            if (!string.IsNullOrEmpty(metrics.ExecutionId))
            {
                obj["execution_id"] = metrics.ExecutionId;
            }

            // Batch entries are priced off batch_pricing — the flag must
            // round-trip so restored metrics keep estimating correctly.
            if (metrics.IsBatch == true)
            {
                obj["batch"] = true;
            }

            if (!string.IsNullOrEmpty(metrics.Role))
            {
                obj["role"] = metrics.Role;
            }

            var effectiveDataCount = dataCount ?? metrics.DataCount;
            if (effectiveDataCount.HasValue)
            {
                obj["data_count"] = effectiveDataCount.Value;
            }

            obj["iterations_count"] = metrics.IterationsCount ?? 0;

            if (reused)
            {
                obj["reused"] = true;
            }

            return obj;
        }

        /// <summary>Parses a JSON metric entry produced by <see cref="ToJson"/>.</summary>
        /// <param name="token">The JSON token to parse.</param>
        /// <returns>An <see cref="AIMetrics"/> instance, or null when the token is invalid.</returns>
        public static AIMetrics FromJson(JToken token)
        {
            var obj = token as JObject;
            if (obj == null)
            {
                return null;
            }

            try
            {
                var quantities = (obj["quantities"] as JObject)?
                    .Properties()
                    .ToDictionary(p => p.Name, p => (double)p.Value) as IReadOnlyDictionary<string, double>;

                var costs = obj["costs"] as JObject;
                var estimated = (costs?["estimated"] as JObject)?
                    .Properties()
                    .ToDictionary(p => p.Name, p => (decimal)p.Value);
                var reported = (costs?["reported"] as JObject)?
                    .Properties()
                    .ToDictionary(p => p.Name, p => (decimal)p.Value);

                var completedAt = ReadCompletedAt(obj["completed_at"]);

                return new AIMetrics
                {
                    Provider = obj["ai_provider"]?.ToString(),
                    Model = obj["ai_model"]?.ToString(),
                    Quantities = quantities,
                    EstimatedCosts = estimated,
                    ReportedCosts = reported,
                    FinishReason = obj["finish_reason"]?.ToString(),
                    CompletionTime = obj["completion_time"]?.Value<double>() ?? 0.0,
                    CompletedAt = completedAt,
                    ExecutionId = obj["execution_id"]?.ToString(),
                    IsBatch = obj["batch"]?.Value<bool>() == true ? true : (bool?)null,
                    DataCount = obj["data_count"]?.Value<int>(),
                    IterationsCount = obj["iterations_count"]?.Value<int>(),
                    Role = obj["role"]?.ToString(),
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Reads <c>completed_at</c> from either a string token (canonical ISO 8601)
        /// or a <see cref="JTokenType.Date"/> token. The Date case matters whenever the
        /// metrics JSON passed through <see cref="JObject.Parse(string)"/> first —
        /// default <c>DateParseHandling.DateTime</c> converts the ISO string into a
        /// <see cref="System.DateTime"/> token, whose <c>ToString()</c> yields a
        /// culture string that loses both the UTC offset and sub-second precision.
        /// </summary>
        /// <param name="token">The raw <c>completed_at</c> token, or null.</param>
        /// <returns>The parsed timestamp, or null when absent/invalid.</returns>
        public static DateTimeOffset? ReadCompletedAt(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return null;
            }

            if (token.Type == JTokenType.Date)
            {
                var dt = token.Value<System.DateTime>();
                return dt == default ? (DateTimeOffset?)null : new DateTimeOffset(dt);
            }

            var text = token.ToString();
            return !string.IsNullOrEmpty(text)
                && DateTimeOffset.TryParse(text, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
                    ? parsed
                    : (DateTimeOffset?)null;
        }
    }
}
