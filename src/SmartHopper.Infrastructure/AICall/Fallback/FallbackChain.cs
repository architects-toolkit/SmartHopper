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
    /// into supported ones. Each step carries the provider and model resolved
    /// for it, so a multi-modality chain may span providers.
    /// </summary>
    public sealed class FallbackChain
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FallbackChain"/> class.
        /// </summary>
        public FallbackChain(
            IReadOnlyList<FallbackStep> steps,
            string description,
            AICapability effectiveCapability)
        {
            this.Steps = steps;
            this.Description = description;
            this.EffectiveCapability = effectiveCapability;
        }

        /// <summary>Ordered fallback steps, each with its own resolved provider/model.</summary>
        public IReadOnlyList<FallbackStep> Steps { get; }

        /// <summary>Joined step descriptions for the warning message.</summary>
        public string Description { get; }

        /// <summary>True when at least one step runs on a provider different from the
        /// component's configured one.</summary>
        public bool UsesAltProvider { get; init; }

        /// <summary>Capability after all steps applied.</summary>
        public AICapability EffectiveCapability { get; }

        /// <summary>
        /// Applies all steps in order, transforming the body and collecting metrics.
        /// Each step runs on its own resolved provider/model.
        /// </summary>
        public async Task<ModalityFallbackResult> ApplyAsync(AIBody body, CancellationToken ct)
        {
            var combinedResult = new ModalityFallbackResult { TransformedBody = body };

            foreach (var step in this.Steps)
            {
                var stepResult = await step.Fallback.ApplyAsync(
                    combinedResult.TransformedBody,
                    step.Provider,
                    step.Model,
                    ct).ConfigureAwait(false);

                combinedResult.TransformedBody = stepResult.TransformedBody;
                combinedResult.ExtraMetricsList.AddRange(stepResult.ExtraMetricsList);
                combinedResult.Messages.AddRange(stepResult.Messages);
            }

            return combinedResult;
        }
    }
}
