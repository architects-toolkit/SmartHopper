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
using SmartHopper.Infrastructure.AICall.Core.Interactions;

namespace SmartHopper.Infrastructure.AICall.Utilities
{
    /// <summary>
    /// Shared utility for coalescing streaming text deltas.
    /// Handles both cumulative (OpenAI-style) and incremental streaming patterns.
    /// </summary>
    public static class TextStreamCoalescer
    {
        /// <summary>
        /// Coalesces a new text delta into an accumulated text interaction.
        /// Handles both cumulative and incremental streaming patterns while avoiding regressions.
        /// </summary>
        /// <param name="accumulated">The currently accumulated text, or null if this is the first chunk.</param>
        /// <param name="incoming">The new text delta from the provider.</param>
        /// <param name="turnId">The turn ID to assign if creating a new accumulated text.</param>
        /// <param name="preserveMetrics">When true, preserves metrics from accumulated; when false, updates from incoming.</param>
        /// <returns>The updated accumulated text interaction.</returns>
        /// <remarks>
        /// Coalescing rules:
        /// - First chunk: initialize accumulator with incoming values
        /// - Subsequent chunks for content and reasoning:
        ///   - If incoming starts with current (and is longer) → cumulative: replace with incoming
        ///   - Else if current starts with incoming → regression/noise: ignore to prevent trimming
        ///   - Else → incremental delta: append incoming to current
        /// </remarks>
        public static AIInteractionText Coalesce(
            AIInteractionText accumulated,
            AIInteractionText incoming,
            string turnId,
            bool preserveMetrics = false)
        {
            if (incoming == null)
            {
                return accumulated;
            }

            // First chunk: initialize accumulator
            if (accumulated == null)
            {
                return new AIInteractionText
                {
                    Agent = incoming.Agent,
                    Content = incoming.Content ?? string.Empty,
                    Reasoning = incoming.Reasoning ?? string.Empty,
                    TurnId = turnId,
                    Time = incoming.Time != default ? incoming.Time : DateTime.UtcNow,
                    Metrics = incoming.Metrics,
                };
            }

            // Coalesce content (cumulative vs incremental detection)
            var currentContent = accumulated.Content ?? string.Empty;
            var incomingContent = incoming.Content ?? string.Empty;

            if (!string.IsNullOrEmpty(incomingContent))
            {
                // Cumulative: incoming contains full text so far (e.g., OpenAI)
                if (incomingContent.Length >= currentContent.Length &&
                    incomingContent.StartsWith(currentContent, StringComparison.Ordinal))
                {
                    accumulated.Content = incomingContent;
                }
                // Regression/noise: ignore to avoid trimming
                else if (currentContent.StartsWith(incomingContent, StringComparison.Ordinal))
                {
                    // Keep existing, ignore incoming
                }
                // Incremental: append delta
                else
                {
                    accumulated.Content = currentContent + incomingContent;
                }
            }

            // Coalesce reasoning similarly
            var currentReasoning = accumulated.Reasoning ?? string.Empty;
            var incomingReasoning = incoming.Reasoning ?? string.Empty;

            if (!string.IsNullOrEmpty(incomingReasoning))
            {
                if (incomingReasoning.Length >= currentReasoning.Length &&
                    incomingReasoning.StartsWith(currentReasoning, StringComparison.Ordinal))
                {
                    accumulated.Reasoning = incomingReasoning;
                }
                else if (currentReasoning.StartsWith(incomingReasoning, StringComparison.Ordinal))
                {
                    // Keep existing
                }
                else
                {
                    accumulated.Reasoning = currentReasoning + incomingReasoning;
                }
            }

            // Update metrics and time based on preserveMetrics flag
            if (!preserveMetrics)
            {
                if (incoming.Metrics != null)
                {
                    accumulated.Metrics = incoming.Metrics;
                }

                if (incoming.Time != default)
                {
                    accumulated.Time = incoming.Time;
                }
            }

            return accumulated;
        }
    }
}
