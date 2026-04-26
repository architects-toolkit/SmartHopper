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
using System.Threading.Tasks;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Metrics;
using SmartHopper.Infrastructure.Diagnostics;

namespace SmartHopper.Infrastructure.AICall.Policies.Response
{
    /// <summary>
    /// Normalizes the finish reason in response metrics to a consistent set of values
    /// after provider decode has run.
    /// </summary>
    public sealed class FinishReasonNormalizeResponsePolicy : IResponsePolicy
    {
        public Task ApplyAsync(PolicyContext context)
        {
            if (context == null || context.Response == null)
            {
                return Task.CompletedTask;
            }

            try
            {
                var response = context.Response;

                // Skip normalization when there is no body to update (e.g., HTTP error returns
                // produced by AIProvider.CallApi or streaming adapters with non-success status).
                // Without a body we cannot replace the last interaction; the error message has
                // already been surfaced via AIRuntimeMessage on the AIReturn.
                if (response.Body == null)
                {
                    return Task.CompletedTask;
                }

                var metrics = response.Metrics; // aggregated snapshot

                // Try to get existing finish reason or fallback to last interaction
                string original = metrics?.FinishReason;
                var lastInteraction = response.Body?.Interactions?.LastOrDefault();
                if (string.IsNullOrWhiteSpace(original))
                {
                    original = lastInteraction?.Metrics?.FinishReason;
                }

                // Normalize common values; default to "stop" if still missing
                bool usedDefault = false;
                string normalized = NormalizeFinishReason(original, out bool wasUnknown, out bool wasTransformed);
                if (string.IsNullOrWhiteSpace(normalized))
                {
                    normalized = "stop";
                    usedDefault = true;
                }

                // Replace only the finish reason in the last interaction if applicable
                if (lastInteraction != null && lastInteraction.Metrics != null)
                {
                    lastInteraction.Metrics.FinishReason = normalized;
                }
                else if (lastInteraction != null)
                {
                    lastInteraction.Metrics = new AIMetrics { FinishReason = normalized };
                }

                // Replace the last interaction in the response
                response.SetBody(AIBodyBuilder.FromImmutable(response.Body)
                        .ReplaceLast(lastInteraction)
                        .Build());

                // Surface an error when the provider stopped due to length/token limit
                if (string.Equals(normalized, "length", StringComparison.Ordinal))
                {
                    response.AddRuntimeMessage(
                        SHRuntimeMessageSeverity.Error,
                        SHRuntimeMessageOrigin.Return,
                        "AI response was cut off by the maximum token limit. You should increase the token limit for this provider in SmartHopper Settings.");
                }

                // Attach diagnostics when applicable
                if (usedDefault)
                {
                    response.AddRuntimeMessage(
                        SHRuntimeMessageSeverity.Warning,
                        SHRuntimeMessageOrigin.Return,
                        "Finish reason missing; defaulted to 'stop'.");
                }
                else if (wasUnknown)
                {
                    response.AddRuntimeMessage(
                        SHRuntimeMessageSeverity.Warning,
                        SHRuntimeMessageOrigin.Return,
                        $"Unrecognized finish reason '{original}'. Keeping original value.");
                }
                else if (wasTransformed && !string.Equals(original, normalized, StringComparison.Ordinal))
                {
                    response.AddRuntimeMessage(
                        SHRuntimeMessageSeverity.Info,
                        SHRuntimeMessageOrigin.Return,
                        $"Normalized finish reason '{original}' -> '{normalized}'.");
                }
            }
            catch (Exception ex)
            {
                // Non-fatal: attach as warning on the response
                context.Response?.AddRuntimeMessage(
                    SHRuntimeMessageSeverity.Warning,
                    SHRuntimeMessageOrigin.Return,
                    $"Finish reason normalization failed: {ex.Message}");
            }

            return Task.CompletedTask;
        }

        private static string NormalizeFinishReason(string value, out bool wasUnknown, out bool wasTransformed)
        {
            wasUnknown = false;
            wasTransformed = false;

            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var original = value.Trim();
            var key = original.Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_');

            // Common mappings across providers
            var map = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                // stop
                ["stop"] = "stop",
                ["stopped"] = "stop",
                ["completed"] = "stop",
                ["end"] = "stop",
                ["eos"] = "stop",
                ["stop_sequence"] = "stop",
                ["end_turn"] = "stop",

                // length
                ["length"] = "length",
                ["max_tokens"] = "length",
                ["max_token"] = "length",
                ["max_tokens_exceeded"] = "length",
                ["content_length"] = "length",
                ["length_finish"] = "length",

                // timeout
                ["timeout"] = "timeout",
                ["time_out"] = "timeout",
                ["deadline_exceeded"] = "timeout",
                ["pause_turn"] = "timeout",

                // cancelled
                ["cancelled"] = "cancelled",
                ["canceled"] = "cancelled",
                ["cancel"] = "cancelled",
                ["user_cancelled"] = "cancelled",
                ["aborted"] = "cancelled",
                ["abort"] = "cancelled",

                // tool calls
                ["tool_call"] = "tool_calls",
                ["tool_calls"] = "tool_calls",
                ["function_call"] = "tool_calls",
                ["function_calls"] = "tool_calls",
                ["tool_use"] = "tool_calls",

                // safety/content filter
                ["content_filter"] = "content_filter",
                ["safety"] = "content_filter",
                ["filtered"] = "content_filter",
                ["refusal"] = "content_filter",

                // provider reported error state
                ["error"] = "error",
                ["failed"] = "error",
            };

            if (map.TryGetValue(key, out var normalized))
            {
                wasTransformed = !string.Equals(original, normalized, StringComparison.Ordinal);
                return normalized;
            }

            // Unknown value: keep the original to avoid information loss, but mark as unknown
            wasUnknown = true;
            return original;
        }
    }
}
