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

using System.Collections.Generic;
using SmartHopper.ProviderSdk.AIModels;

namespace SmartHopper.ProviderSdk.AICall.Metrics
{
    /// <summary>
    /// Centralized, provider-agnostic cost estimator for <see cref="AIMetrics"/>.
    /// Joins each <see cref="AIPriceEntry"/> of the model's
    /// <see cref="AIModelPricing"/> against the measured
    /// <see cref="AIMetrics.Quantities"/> sharing its kind, converts the
    /// measured unit to the billed unit, applies the pricing discount, and
    /// accumulates the total under the pricing's currency — estimates never
    /// mix currencies. Provider-billed amounts are not this class's business:
    /// they live in <see cref="AIMetrics.ReportedCosts"/>.
    /// </summary>
    public static class AICostCalculator
    {
        /// <summary>
        /// Unit conversion factors into the billed unit. Only time units
        /// convert; everything else (token, item, credit, …) is
        /// non-convertible — a mismatch contributes zero, honestly.
        /// </summary>
        private static readonly Dictionary<(AIPriceUnit From, AIPriceUnit To), double> Conversions = new ()
        {
            [(AIPriceUnit.Second, AIPriceUnit.Minute)] = 1.0 / 60.0,
            [(AIPriceUnit.Minute, AIPriceUnit.Second)] = 60.0,
        };

        /// <summary>
        /// Calculates the estimated cost per currency for the supplied metrics.
        /// </summary>
        /// <param name="metrics">The metrics to estimate cost for.</param>
        /// <returns>
        /// Currency → estimated total. Empty when pricing is unknown, free,
        /// negative, or no billable quantities are present.
        /// </returns>
        public static IReadOnlyDictionary<string, decimal> Calculate(AIMetrics metrics)
        {
            var costs = new Dictionary<string, decimal>(System.StringComparer.Ordinal);
            if (metrics == null ||
                string.IsNullOrEmpty(metrics.Provider) ||
                string.IsNullOrEmpty(metrics.Model))
            {
                return costs;
            }

            var capabilities = AIModelCapabilityRegistry.Instance?.GetCapabilities(metrics.Provider, metrics.Model);
            var pricing = capabilities?.Pricing;
            if (pricing?.Entries == null || pricing.Entries.Count == 0)
            {
                return costs;
            }

            decimal total = 0m;
            foreach (var entry in pricing.Entries)
            {
                if (entry == null || entry.Amount <= 0m)
                {
                    continue;
                }

                var billed = BilledQuantity(metrics, entry);
                if (billed > 0d)
                {
                    total += (decimal)billed * entry.Amount;
                }
            }

            // Heuristic backstop: when the provider reported no usage at all on
            // a text side, the client-side token estimate stands in for it —
            // priced at the text rate, as before.
            var hasActualInput = metrics.Quantity(AIQuantityKey.TextInput, AIPriceUnit.Token) > 0d
                || metrics.Quantity(AIQuantityKey.CacheRead, AIPriceUnit.Token) > 0d
                || metrics.Quantity(AIQuantityKey.CacheWrite, AIPriceUnit.Token) > 0d;
            if (!hasActualInput)
            {
                var estimated = metrics.Quantity(AIQuantityKey.EstimatedTextInput, AIPriceUnit.Token);
                var entry = pricing.Find(AIQuantityKey.TextInput);
                if (estimated > 0d && entry != null && entry.Unit == AIPriceUnit.Token && entry.Amount > 0m)
                {
                    total += (decimal)estimated * entry.Amount;
                }
            }

            var hasActualOutput = metrics.Quantity(AIQuantityKey.TextOutput, AIPriceUnit.Token) > 0d
                || metrics.Quantity(AIQuantityKey.InternalReasoning, AIPriceUnit.Token) > 0d;
            if (!hasActualOutput)
            {
                var estimated = metrics.Quantity(AIQuantityKey.EstimatedTextOutput, AIPriceUnit.Token);
                var entry = pricing.Find(AIQuantityKey.TextOutput);
                if (estimated > 0d && entry != null && entry.Unit == AIPriceUnit.Token && entry.Amount > 0m)
                {
                    total += (decimal)estimated * entry.Amount;
                }
            }

            if (pricing.Discount.HasValue && pricing.Discount.Value > 0m)
            {
                total *= 1m - pricing.Discount.Value;
            }

            if (total > 0m && !string.IsNullOrEmpty(pricing.Currency))
            {
                costs[pricing.Currency] = total;
            }

            return costs;
        }

        /// <summary>
        /// Resolves how much of <paramref name="entry"/>'s kind was measured,
        /// converted into the entry's billed unit. <c>request</c> entries bill
        /// the call itself (one invocation per metrics record); every other
        /// kind needs a measured quantity under <c>kind/&lt;unit&gt;</c> whose
        /// unit converts to the billed one.
        /// </summary>
        private static double BilledQuantity(AIMetrics metrics, AIPriceEntry entry)
        {
            if (entry.Kind == AIQuantityKey.Request)
            {
                return metrics.Quantities?.Count > 0 ? 1d : 0d;
            }

            if (metrics.Quantities == null)
            {
                return 0d;
            }

            var billed = 0d;
            foreach (var kv in metrics.Quantities)
            {
                var sep = kv.Key.LastIndexOf('/');
                if (sep <= 0 || !string.Equals(kv.Key.Substring(0, sep), entry.Kind, System.StringComparison.Ordinal))
                {
                    continue;
                }

                var measuredUnit = AIPriceEntry.ParseUnit(kv.Key.Substring(sep + 1));
                if (measuredUnit == null || kv.Value <= 0d)
                {
                    continue;
                }

                if (measuredUnit.Value == entry.Unit)
                {
                    billed += kv.Value;
                }
                else if (Conversions.TryGetValue((measuredUnit.Value, entry.Unit), out var factor))
                {
                    billed += kv.Value * factor;
                }
            }

            return billed;
        }
    }
}
