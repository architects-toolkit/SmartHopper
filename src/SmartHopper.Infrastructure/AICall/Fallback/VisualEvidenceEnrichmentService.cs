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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SmartHopper.Infrastructure.Settings;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AIModels;
using SmartHopper.ProviderSdk.Diagnostics;

namespace SmartHopper.Infrastructure.AICall.Fallback
{
    /// <summary>
    /// Shared explicit visual-evidence enrichment entry point. Unlike silent modality
    /// fallback, callers reach this only when the user explicitly requested AI visual
    /// comprehension (describeImages=true or consumerSupportsImages=false) — so the
    /// configured vision helper may run even when generic fallback is disabled. Uses the
    /// same <see cref="ImageToTextFallback"/> implementation, shared master instruction and
    /// helper resolution as request preparation; source text and images are preserved and
    /// helper provenance/metrics/diagnostics are reported.
    /// </summary>
    public static class VisualEvidenceEnrichmentService
    {
        /// <summary>
        /// Converts the model-visible images inside <paramref name="source"/> into
        /// contextual textual evidence via the registry-confirmed helper.
        /// </summary>
        /// <param name="source">Body carrying the source text and tool-result images.</param>
        /// <param name="preferredProvider">Preferred helper provider (tried first).</param>
        /// <param name="preferredModel">Preferred helper model, used directly when the
        /// registry confirms the conversion capability.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <param name="userGuidance">Optional caller guidance appended as a labeled user
        /// turn beside the shared master instruction; never replaces it.</param>
        /// <returns>The enrichment result — transformed body, helper metrics and
        /// diagnostics. Failure is reported via <see cref="ModalityFallbackResult.Messages"/>,
        /// never as a silent text-only success.</returns>
        public static Task<ModalityFallbackResult> EnrichAsync(
            AIBody source,
            string preferredProvider,
            string preferredModel,
            CancellationToken ct,
            string userGuidance = null)
        {
            return EnrichAsync(
                source, preferredProvider, preferredModel, ct,
                SmartHopperSettings.Load(), userGuidance);
        }

        /// <summary>
        /// Internal test seam: enrich against a caller-supplied settings snapshot
        /// instead of loading persisted user settings.
        /// </summary>
        internal static async Task<ModalityFallbackResult> EnrichAsync(
            AIBody source,
            string preferredProvider,
            string preferredModel,
            CancellationToken ct,
            SmartHopperSettings settings,
            string userGuidance = null)
        {
            var result = new ModalityFallbackResult { TransformedBody = source };
            if (source == null)
            {
                AddError(result, "Visual enrichment received no source content.");
                return result;
            }

            // Zero-cost contract: no model-visible images → nothing to convert, no
            // provider resolution, no call — the source is returned unchanged.
            var hasImages = source.Interactions?.Any(i =>
                (i is AIInteractionImage img && !string.IsNullOrEmpty(img.ImageData))
                || (i is AIInteractionToolResult tr && tr.Images != null && tr.Images.Any(t => t?.SendToModel == true && !string.IsNullOrEmpty(t.ImageData))))
                == true;
            if (!hasImages)
            {
                return result;
            }

            var chain = new ModalityFallbackResolver().ResolveConversion(
                AICapability.ImageInput,
                string.IsNullOrWhiteSpace(preferredProvider) ? settings?.DefaultAIProvider : preferredProvider,
                preferredModel,
                settings?.ModalityFallback ?? ModalityFallbackMode.Disabled,
                settings);

            if (chain == null || chain.Steps.Count == 0)
            {
                AddError(result,
                    "Visual evidence enrichment requested but no configured vision helper can perform the image conversion. Configure an image-capable provider or a fallback pin.");
                return result;
            }

            var step = chain.Steps[0];
            var enriched = await step.Fallback
                .ApplyAsync(source, step.Provider, step.Model, ct, context: new ModalityFallbackContext { UserGuidance = userGuidance })
                .ConfigureAwait(false);

            result.TransformedBody = enriched?.TransformedBody ?? source;
            if (enriched != null)
            {
                result.ExtraMetricsList.AddRange(enriched.ExtraMetricsList);
                result.Messages.AddRange(enriched.Messages);
            }

            return result;
        }

        private static void AddError(ModalityFallbackResult result, string message)
        {
            result.Messages.Add(new SHRuntimeMessage(
                SHRuntimeMessageSeverity.Error,
                SHRuntimeMessageOrigin.Provider,
                SHMessageCode.ConversionFailed,
                message));
        }
    }
}
