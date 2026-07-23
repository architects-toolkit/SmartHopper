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
using System.Threading;
using System.Threading.Tasks;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AIModels;

namespace SmartHopper.Infrastructure.AICall.Fallback
{
    /// <summary>
    /// An ordered list of fallback steps that convert unsupported modalities
    /// into supported ones. For v1, chains are always single-step.
    /// </summary>
    public sealed class FallbackChain
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FallbackChain"/> class.
        /// </summary>
        public FallbackChain(
            IReadOnlyList<IModalityFallback> steps,
            string description,
            string actualProvider,
            string actualModel,
            AICapability effectiveCapability)
        {
            this.Steps = steps;
            this.Description = description;
            this.ActualProvider = actualProvider;
            this.ActualModel = actualModel;
            this.EffectiveCapability = effectiveCapability;
        }

        /// <summary>Ordered fallback steps.</summary>
        public IReadOnlyList<IModalityFallback> Steps { get; }

        /// <summary>Joined step descriptions for the warning message.</summary>
        public string Description { get; }

        /// <summary>Provider executing the fallback call(s). Equals the component's
        /// configured provider unless mode is AnyProvider and a different one was chosen.</summary>
        public string ActualProvider { get; }

        /// <summary>Model executing the fallback (resolved, never null when chain is non-null).</summary>
        public string ActualModel { get; }

        /// <summary>True when a different provider from the component's configured one is used.</summary>
        public bool UsesAltProvider { get; init; }

        /// <summary>Capability after all steps applied.</summary>
        public AICapability EffectiveCapability { get; }

        /// <summary>
        /// Applies all steps in order, transforming the body and collecting metrics.
        /// </summary>
        public async Task<ModalityFallbackResult> ApplyAsync(AIBody body, CancellationToken ct)
        {
            var combinedResult = new ModalityFallbackResult { TransformedBody = body };

            foreach (var step in this.Steps)
            {
                var stepResult = await step.ApplyAsync(
                    combinedResult.TransformedBody,
                    this.ActualProvider,
                    this.ActualModel,
                    ct).ConfigureAwait(false);

                combinedResult.TransformedBody = stepResult.TransformedBody;
                combinedResult.ExtraMetricsList.AddRange(stepResult.ExtraMetricsList);
                combinedResult.Messages.AddRange(stepResult.Messages);
            }

            return combinedResult;
        }
    }
}
