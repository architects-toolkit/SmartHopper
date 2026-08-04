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

using SmartHopper.ProviderSdk.AIModels;

namespace SmartHopper.ProviderSdk.AICall.Metrics
{
    /// <summary>
    /// Centralized, provider-agnostic cost estimator for <see cref="AIMetrics"/>.
    /// Uses per-model pricing from <see cref="AIModelCapabilityRegistry"/> and the
    /// token buckets tracked by <see cref="AIMetrics"/>.
    /// </summary>
    public static class AICostCalculator
    {
        /// <summary>
        /// Calculates an estimated cost in USD for the supplied metrics.
        /// </summary>
        /// <param name="metrics">The metrics to estimate cost for.</param>
        /// <returns>Estimated cost in USD. Returns <c>0</c> when pricing is unknown, free, negative, or no tokens are present.</returns>
        public static decimal Calculate(AIMetrics metrics)
        {
            if (metrics == null ||
                string.IsNullOrEmpty(metrics.Provider) ||
                string.IsNullOrEmpty(metrics.Model))
            {
                return 0m;
            }

            var capabilities = AIModelCapabilityRegistry.Instance?.GetCapabilities(metrics.Provider, metrics.Model);
            var pricing = capabilities?.Pricing;
            if (pricing == null)
            {
                return 0m;
            }

            decimal cost = 0m;

            bool hasActualInput = metrics.InputTokens > 0;
            if (hasActualInput)
            {
                cost += metrics.InputTokensPrompt * GetPositivePrice(pricing.Prompt);
                cost += metrics.InputTokensCached * GetPositivePrice(pricing.InputCacheRead);
                cost += metrics.InputTokensCacheWrite * GetPositivePrice(pricing.InputCacheWrite);
            }
            else if (metrics.EstimatedInputTokens > 0)
            {
                cost += metrics.EstimatedInputTokens * GetPositivePrice(pricing.Prompt);
            }

            bool hasActualOutput = metrics.OutputTokens > 0;
            if (hasActualOutput)
            {
                cost += metrics.OutputTokensGeneration * GetPositivePrice(pricing.Completion);
                cost += metrics.OutputTokensReasoning * GetPositivePrice(pricing.InternalReasoning);
            }
            else if (metrics.EstimatedOutputTokens > 0)
            {
                cost += metrics.EstimatedOutputTokens * GetPositivePrice(pricing.Completion);
            }

            return cost;
        }

        /// <summary>
        /// Returns the price when it is a positive value; otherwise returns <c>0</c>.
        /// Negative or missing prices are treated as zero cost for that category.
        /// </summary>
        /// <param name="price">The optional price.</param>
        /// <returns>A non-negative price value.</returns>
        private static decimal GetPositivePrice(decimal? price)
        {
            return price.HasValue && price.Value > 0m ? price.Value : 0m;
        }
    }
}
