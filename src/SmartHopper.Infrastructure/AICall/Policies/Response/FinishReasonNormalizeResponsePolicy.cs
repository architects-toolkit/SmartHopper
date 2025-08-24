/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Metrics;

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
                var metrics = response.Metrics; // aggregated snapshot

                // Try to get existing finish reason or fallback to last assistant interaction
                string original = metrics?.FinishReason;
                if (string.IsNullOrWhiteSpace(original))
                {
                    var lastAssistant = response.Body?.GetLastInteraction(AIAgent.Assistant);
                    original = lastAssistant?.Metrics?.FinishReason;
                }

                // Normalize common values; default to "stop" if still missing
                bool usedDefault = false;
                string normalized = NormalizeFinishReason(original, out bool wasUnknown, out bool wasTransformed);
                if (string.IsNullOrWhiteSpace(normalized))
                {
                    normalized = "stop";
                    usedDefault = true;
                }

                // Persist normalized finish reason into global metrics without disturbing other fields
                response.Metrics = new AIMetrics { FinishReason = normalized };

                // Attach diagnostics when applicable
                if (usedDefault)
                {
                    response.AddRuntimeMessage(
                        AIRuntimeMessageSeverity.Warning,
                        AIRuntimeMessageOrigin.Return,
                        "Finish reason missing; defaulted to 'stop'.");
                }
                else if (wasUnknown)
                {
                    response.AddRuntimeMessage(
                        AIRuntimeMessageSeverity.Warning,
                        AIRuntimeMessageOrigin.Return,
                        $"Unrecognized finish reason '{original}'. Keeping original value.");
                }
                else if (wasTransformed && !string.Equals(original, normalized, StringComparison.Ordinal))
                {
                    response.AddRuntimeMessage(
                        AIRuntimeMessageSeverity.Info,
                        AIRuntimeMessageOrigin.Return,
                        $"Normalized finish reason '{original}' -> '{normalized}'.");
                }
            }
            catch (Exception ex)
            {
                // Non-fatal: attach as warning on the response
                context.Response?.AddRuntimeMessage(
                    AIRuntimeMessageSeverity.Warning,
                    AIRuntimeMessageOrigin.Return,
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

                // safety/content filter
                ["content_filter"] = "content_filter",
                ["safety"] = "content_filter",
                ["filtered"] = "content_filter",

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
