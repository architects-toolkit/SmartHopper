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
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SmartHopper.Infrastructure.Settings;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Requests;
using SmartHopper.ProviderSdk.AIModels;
using SmartHopper.ProviderSdk.Diagnostics;
using SmartHopper.ProviderSdk.Logging;
namespace SmartHopper.Infrastructure.AICall.Fallback
{
    /// <summary>
    /// Central model-bound request preparation. Builds the outbound execution view of a
    /// request: the primary provider/model resolved for the requested task (never upgraded
    /// merely because the source carries images), strict capability validation of the
    /// media the body still requires, and — when the configured modality fallback permits —
    /// conversion of unsupported media through the existing fallback chain. Canonical
    /// session evidence is never mutated; conversion failures block submission.
    /// </summary>
    public static class RequestModalityPreparationService
    {
        /// <summary>
        /// Prepares <paramref name="request"/> for provider submission.
        /// </summary>
        /// <param name="request">The canonical request after request policies and approved-source materialization.</param>
        /// <param name="ct">Cancellation token; helper calls honor it.</param>
        /// <returns>
        /// The preparation result. <see cref="AIRequestPreparation.Request"/> is always a
        /// detached execution copy; <see cref="AIRequestPreparation.HasBlockingErrors"/>
        /// indicates the request must not be submitted.
        /// </returns>
        public static Task<AIRequestPreparation> PrepareAsync(AIRequestCall request, CancellationToken ct)
        {
            return PrepareAsync(request, null, null, ct);
        }

        /// <summary>
        /// Prepares <paramref name="request"/> for provider submission with explicit seams.
        /// </summary>
        /// <param name="request">The canonical request after request policies and approved-source materialization.</param>
        /// <param name="modeOverride">
        /// When non-null, this fallback mode is used instead of resolving
        /// 'modality_fallback' extras and the global setting. Deterministic tests use this
        /// so preparation never touches user settings.
        /// </param>
        /// <param name="resolveChain">
        /// When non-null, resolves the conversion chain for (provider, model, required,
        /// mode) instead of <see cref="ModalityFallbackResolver"/>. Returning null is a
        /// blocking error. Deterministic tests use this so preparation never loads the
        /// real <c>ProviderManager</c>.
        /// </param>
        /// <param name="ct">Cancellation token; helper calls honor it.</param>
        /// <returns>The preparation result.</returns>
        internal static async Task<AIRequestPreparation> PrepareAsync(
            AIRequestCall request,
            ModalityFallbackMode? modeOverride,
            Func<string, string, AICapability, ModalityFallbackMode, FallbackChain> resolveChain,
            CancellationToken ct)
        {
            var preparation = new AIRequestPreparation(request?.CreateExecutionCopy());
            if (request?.Body == null)
            {
                return preparation;
            }

            ct.ThrowIfCancellationRequested();

            var body = preparation.Request.Body;
            var required = AIRequestPreparation.RequiredInputCapabilities(preparation.Request);
            if (required == AICapability.None)
            {
                return preparation;
            }

            // The primary provider/model is the task-selected one, resolved without adding
            // inferred body-media requirements — a text-only primary is not upgraded merely
            // because the source contains images. The prepared copy holds that same primary.
            var provider = preparation.Request.Provider;
            var model = preparation.Request.Model;
            var capabilities = string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(model)
                ? null
                : AIModelCapabilityRegistry.Instance.GetCapabilities(provider, model);

            if (capabilities != null && capabilities.HasCapability(required))
            {
                // Registered target confirms the required modalities: retain original media.
                return preparation;
            }

            // Unknown capability is not confirmed support — adaptation is required when the
            // configured modality fallback allows it; otherwise the submission is blocked.
            var mode = modeOverride ?? ResolveFallbackMode(request);
            if (mode == ModalityFallbackMode.Disabled)
            {
                preparation.Messages.Add(new SHRuntimeMessage(
                    SHRuntimeMessageSeverity.Error,
                    SHRuntimeMessageOrigin.Validation,
                    SHMessageCode.CapabilityMismatch,
                    capabilities == null
                        ? $"Provider '{provider}' / model '{model}' has no registered capabilities; media input requirements ({required.ToDetailedString()}) cannot be confirmed and modality fallback is disabled."
                        : $"Provider '{provider}' / model '{model}' does not support {required.ToDetailedString()} and modality fallback is disabled."));
                return preparation;
            }

            var chain = resolveChain != null
                ? resolveChain(provider, model, required, mode)
                : new ModalityFallbackResolver().Resolve(provider, model, required, mode);
            if (chain == null)
            {
                preparation.Messages.Add(new SHRuntimeMessage(
                    SHRuntimeMessageSeverity.Error,
                    SHRuntimeMessageOrigin.Validation,
                    SHMessageCode.CapabilityMismatch,
                    $"Provider '{provider}' / model '{model}' does not support {required.ToDetailedString()} and no permitted modality fallback could be resolved (check configured provider and fallback pins)."));
                return preparation;
            }

            // Share the run's conversion cache so repeated preparations of the same evidence
            // inside one request run do not pay twice; bound to this request, never global.
            var cache = request.PreparationCache ??= new ConcurrentDictionary<string, string>();
            preparation.Request.PreparationCache = cache;

            var fallbackResult = await chain.ApplyAsync(
                body,
                ct,
                context: new ModalityFallbackContext { EvidenceCache = cache }).ConfigureAwait(false);

            // Charged helper usage is metered once, even when the run later fails or cancels.
            preparation.ExtraMetrics.AddRange(fallbackResult.ExtraMetricsList);
            preparation.Messages.AddRange(fallbackResult.Messages);
            if (fallbackResult.TransformedBody != null)
            {
                preparation.Request.Body = fallbackResult.TransformedBody;
            }

            Logger.For<PolicyPipelineHostLogger>().Trace($"[RequestModalityPreparation] {provider}/{model} required {required.ToDetailedString()}; chain '{chain.Description}' produced {preparation.ExtraMetrics.Count} extra metric(s)");

            // Cancellation arriving after helper work must still stop submission — but the
            // paid metrics/diagnostics recorded above are returned, never thrown away.
            if (ct.IsCancellationRequested)
            {
                preparation.Messages.Add(new SHRuntimeMessage(
                    SHRuntimeMessageSeverity.Error,
                    SHRuntimeMessageOrigin.Provider,
                    SHMessageCode.ConversionFailed,
                    "Request preparation was cancelled after helper work; submission is aborted."));
                return preparation;
            }

            // Final validation always checks the outbound view.
            preparation.Messages.AddRange(AIRequestPreparation.ValidateOutboundModalities(preparation.Request));
            return preparation;
        }

        /// <summary>
        /// Resolves the effective modality fallback mode from the request's own
        /// 'modality_fallback' extra (matching the component override contract) and the
        /// global setting. Pins are resolved inside <see cref="ModalityFallbackResolver"/>.
        /// </summary>
        private static ModalityFallbackMode ResolveFallbackMode(AIRequestCall request)
        {
            var extras = request?.Parameters?.Extras;
            if (extras != null && extras.TryGetValue("modality_fallback", out var modeToken))
            {
                var mode = modeToken?.ToString();
                switch (mode?.ToLowerInvariant())
                {
                    case "disabled": return ModalityFallbackMode.Disabled;
                    case "configured_provider": return ModalityFallbackMode.ConfiguredProvider;
                    case "any_provider": return ModalityFallbackMode.AnyProvider;
                }
            }

            return SmartHopperSettings.Load().ModalityFallback;
        }

        /// <summary>Logger category anchor for this service.</summary>
        private sealed class PolicyPipelineHostLogger { }
    }
}
