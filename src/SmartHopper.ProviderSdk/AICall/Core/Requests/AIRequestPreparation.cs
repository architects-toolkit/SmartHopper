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
using SmartHopper.ProviderSdk.AICall.Metrics;
using SmartHopper.ProviderSdk.AICall.Utilities;
using SmartHopper.ProviderSdk.AIModels;
using SmartHopper.ProviderSdk.Diagnostics;
using SmartHopper.ProviderSdk.Hosting;

namespace SmartHopper.ProviderSdk.AICall.Core.Requests
{
    /// <summary>
    /// Result of model-bound request preparation: an outbound execution copy of the
    /// request plus any helper-call metrics and diagnostic messages produced while
    /// adapting media to the resolved model's capabilities. The canonical request and
    /// its session evidence are never mutated by preparation.
    /// </summary>
    public sealed class AIRequestPreparation
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AIRequestPreparation"/> class.
        /// </summary>
        /// <param name="request">The prepared outbound request to submit to the provider.</param>
        public AIRequestPreparation(AIRequestCall request)
        {
            this.Request = request;
        }

        /// <summary>
        /// Gets the outbound request to submit. Always an execution copy detached from the
        /// canonical request (see <see cref="AIRequestCall.CreateExecutionCopy"/>); when no
        /// adaptation was required it still shares the canonical immutable body and parameters.
        /// </summary>
        public AIRequestCall Request { get; }

        /// <summary>
        /// Gets the metrics produced by helper calls performed during preparation
        /// (for example, vision-helper conversions). Each entry carries its own
        /// provider/model and must be metered once, separately from the primary call.
        /// </summary>
        public List<AIMetrics> ExtraMetrics { get; } = new List<AIMetrics>();

        /// <summary>
        /// Gets the diagnostic messages produced during preparation. Error-severity entries
        /// indicate the request must not be submitted (missing helper, failed conversion,
        /// unconfirmed capability).
        /// </summary>
        public List<SHRuntimeMessage> Messages { get; } = new List<SHRuntimeMessage>();

        /// <summary>
        /// Gets a value indicating whether any message blocks submission of the prepared request.
        /// </summary>
        public bool HasBlockingErrors =>
            this.Messages.Exists(m => m != null && m.Severity == SHRuntimeMessageSeverity.Error);

        /// <summary>
        /// Computes the input-modality capabilities the request's body requires, honoring the
        /// request's declared modality kind: on a dedicated speech request
        /// (<see cref="AICapability.SpeechInput"/> declared), audio interactions require
        /// <see cref="AICapability.SpeechInput"/> rather than the generic
        /// <see cref="AICapability.AudioInput"/> — a speech-to-text helper is a dedicated
        /// speech model, not a generic audio-input model.
        /// </summary>
        /// <param name="request">The request whose body and declared capability are inspected.</param>
        /// <returns>The required input-modality capability flags.</returns>
        public static AICapability RequiredInputCapabilities(AIRequestCall request)
        {
            var required = request?.Body?.Interactions.RequiredInputCapabilities() ?? AICapability.None;
            if ((required & AICapability.AudioInput) != 0
                && request != null
                && (request.Capability & AICapability.SpeechInput) != 0)
            {
                required = (required & ~AICapability.AudioInput) | AICapability.SpeechInput;
            }

            return required;
        }

        /// <summary>
        /// Full outbound gate for a prepared request: modality-capability validation,
        /// structural <see cref="AIRequestCall.IsValid"/>, and the provider trust boundary.
        /// Callers submitting without going through <see cref="AIRequestCall.Exec"/> (streaming,
        /// special turns, batch queues) must run this exact same gate.
        /// </summary>
        /// <param name="request">The prepared outbound request.</param>
        /// <returns>All diagnostics produced; error-severity entries block submission.</returns>
        public static List<SHRuntimeMessage> ValidateOutboundRequest(AIRequestCall request)
        {
            var messages = ValidateOutboundModalities(request);
            if (request == null)
            {
                return messages;
            }

            var (ok, errors) = request.IsValid();
            if (!ok && errors != null)
            {
                messages.AddRange(errors);
            }

            var trust = Validation.ProviderTrustPolicy.Evaluate(request);
            if (trust?.Messages != null)
            {
                messages.AddRange(trust.Messages);
            }

            if (trust?.Verdict == Validation.ProviderTrustVerdict.Block)
            {
                messages.Add(new SHRuntimeMessage(
                    SHRuntimeMessageSeverity.Error,
                    SHRuntimeMessageOrigin.Validation,
                    SHMessageCode.ProviderTrustBlocked,
                    $"Provider '{request.Provider}' was blocked by the trust policy."));
            }

            return messages;
        }

        /// <summary>
        /// Strictly validates the outbound (prepared) view of a request: the resolved model's
        /// registered capabilities must confirm every input modality the body still requires.
        /// An unregistered model means the capability is unknown, and unknown image/audio
        /// capability is not confirmed support — the request must not be submitted.
        /// </summary>
        /// <param name="request">The prepared outbound request.</param>
        /// <returns>Blocking error messages; empty when every required modality is confirmed.</returns>
        public static List<SHRuntimeMessage> ValidateOutboundModalities(AIRequestCall request)
        {
            var errors = new List<SHRuntimeMessage>();
            if (request == null)
            {
                return errors;
            }

            var required = RequiredInputCapabilities(request);
            if (required == AICapability.None)
            {
                return errors;
            }

            var provider = request.Provider;
            var model = request.Model;
            var capabilities = string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(model)
                ? null
                : AIModelCapabilityRegistry.Instance.GetCapabilities(provider, model);

            if (capabilities == null)
            {
                errors.Add(new SHRuntimeMessage(
                    SHRuntimeMessageSeverity.Error,
                    SHRuntimeMessageOrigin.Validation,
                    SHMessageCode.CapabilityMismatch,
                    $"Outbound request still requires {required.ToDetailedString()} but '{provider}' / '{model}' has no registered capabilities; unknown media capability is not confirmed support."));
            }
            else if (!capabilities.HasCapability(required))
            {
                errors.Add(new SHRuntimeMessage(
                    SHRuntimeMessageSeverity.Error,
                    SHRuntimeMessageOrigin.Validation,
                    SHMessageCode.CapabilityMismatch,
                    $"Outbound request still requires {required.ToDetailedString()} but '{provider}' / '{model}' does not support it."));
            }

            return errors;
        }
    }
}
