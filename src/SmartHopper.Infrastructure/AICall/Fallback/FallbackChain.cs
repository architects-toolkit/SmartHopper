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
using System.Threading;
using System.Threading.Tasks;
using SmartHopper.ProviderSdk.AICall.Core;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AIModels;
using SmartHopper.ProviderSdk.Diagnostics;
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
        /// <param name="body">The canonical body to transform.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <param name="requestParameters">Optional request parameters propagated to each step's conversion calls.</param>
        /// <param name="context">Optional execution-scoped context shared across steps.</param>
        public async Task<ModalityFallbackResult> ApplyAsync(AIBody body, CancellationToken ct, AIRequestParameters requestParameters = null, ModalityFallbackContext context = null)
        {
            var combinedResult = new ModalityFallbackResult { TransformedBody = body };

            foreach (var step in this.Steps)
            {
                ModalityFallbackResult stepResult;
                try
                {
                    stepResult = await step.Fallback.ApplyAsync(
                        combinedResult.TransformedBody,
                        step.Provider,
                        step.Model,
                        ct,
                        requestParameters,
                        context).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // A later step must not discard metrics and messages already charged by
                    // earlier steps/images: surface them on the returned result with a
                    // blocking cancellation diagnostic instead of propagating bare.
                    if (combinedResult.ExtraMetricsList.Count == 0 && combinedResult.Messages.Count == 0)
                    {
                        throw;
                    }

                    combinedResult.Messages.Add(new SHRuntimeMessage(
                        SHRuntimeMessageSeverity.Error,
                        SHRuntimeMessageOrigin.Provider,
                        SHMessageCode.ConversionFailed,
                        "Modality fallback was cancelled after paid helper work; the partial result is not submittable."));
                    return combinedResult;
                }

                combinedResult.TransformedBody = stepResult.TransformedBody;
                combinedResult.ExtraMetricsList.AddRange(stepResult.ExtraMetricsList);
                combinedResult.Messages.AddRange(stepResult.Messages);
            }

            return combinedResult;
        }
    }
}
