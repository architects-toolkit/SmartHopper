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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SmartHopper.ProviderSdk.AICall.Core;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Requests;
using SmartHopper.ProviderSdk.AICall.Metrics;
using SmartHopper.ProviderSdk.AIModels;
using SmartHopper.ProviderSdk.Diagnostics;
using SmartHopper.ProviderSdk.Hosting;
using SmartHopper.ProviderSdk.Logging;
namespace SmartHopper.Infrastructure.AICall.Fallback
{
    /// <summary>
    /// Converts audio inputs to text transcriptions via a speech-to-text capable model.
    /// Replaces <see cref="AIInteractionAudio"/> entries with <see cref="AIInteractionText"/>
    /// transcriptions so that a text-only model can process the body.
    /// </summary>
    public sealed class AudioToTextFallback : IModalityFallback
    {
        /// <inheritdoc/>
        public string Name => "AudioToText";

        /// <inheritdoc/>
        public AICapability Handles => AICapability.AudioInput;

        /// <inheritdoc/>
        public AICapability RequiresCapability => AICapability.Speech2Text;

        /// <inheritdoc/>
        public AICapability ResultsIn => AICapability.TextInput;

        /// <inheritdoc/>
        public string Description => "Audio transcribed to text via STT model";

        /// <inheritdoc/>
        public bool IsAvailable(string providerName)
        {
            // An unconfigured provider cannot execute the conversion call, so it
            // is never a usable fallback candidate regardless of catalogued models.
            var provider = ProviderSdkHost.ProviderRegistry.GetProvider(providerName);
            if (provider?.IsConfigured != true)
            {
                return false;
            }

            var model = AIModelCapabilityRegistry.Instance.SelectBestModel(providerName, null, this.RequiresCapability);
            return !string.IsNullOrWhiteSpace(model) &&
                AIModelCapabilityRegistry.Instance.ValidateCapabilities(providerName, model, this.RequiresCapability);
        }

        /// <inheritdoc/>
        public async Task<ModalityFallbackResult> ApplyAsync(AIBody body, string providerName, string modelName, CancellationToken ct, AIRequestParameters requestParameters = null, ModalityFallbackContext context = null)
        {
            var result = new ModalityFallbackResult();
            var newInteractions = new List<IAIInteraction>();
            var audioCount = 0;

            foreach (var interaction in body.Interactions)
            {
                if (ct.IsCancellationRequested)
                {
                    // Zero-work cancellation still throws; once helper calls were charged,
                    // stop with a blocking diagnostic instead of discarding paid metrics.
                    if (audioCount == 0 && result.ExtraMetricsList.Count == 0)
                    {
                        ct.ThrowIfCancellationRequested();
                    }

                    result.Messages.Add(new SHRuntimeMessage(
                        SHRuntimeMessageSeverity.Error,
                        SHRuntimeMessageOrigin.Provider,
                        SHMessageCode.ConversionFailed,
                        "Audio transcription was cancelled after paid helper work; the partial result is not submittable."));
                    break;
                }

                if (interaction is AIInteractionAudio audioInteraction)
                {
                    audioCount++;
                    Logger.For<AudioToTextFallback>().Trace($"[AudioToTextFallback] Transcribing audio {audioCount} via {providerName}/{modelName}");

                    // Build a STT request for this audio; modality fallback is disabled on the
                    // helper so an unsupported/malformed helper capability fails explicitly
                    // instead of recursively adapting its own audio input.
                    var audioBody = AIBodyBuilder.Create()
                        .Add(audioInteraction)
                        .Build();

                    var request = new AIRequestCall();
                    request.Initialize(
                        provider: providerName,
                        model: modelName,
                        body: audioBody,
                        origin: "fallback:stt",
                        capability: this.RequiresCapability);
                    request.Parameters = AIRequestParameters.Create()
                        .WithModel(modelName)
                        .WithExtra("modality_fallback", "disabled")
                        .Build();

                    var aiResult = await request.Exec(ct).ConfigureAwait(false);

                    if (aiResult?.Metrics != null)
                    {
                        // Charged helper usage is recorded once, even when conversion fails.
                        result.ExtraMetricsList.Add(aiResult.Metrics);
                    }

                    var transcript = aiResult != null && aiResult.Success && aiResult.Body != null
                        ? aiResult.Body.Interactions?
                            .OfType<AIInteractionText>()
                            .LastOrDefault(t => !string.IsNullOrWhiteSpace(t.Content))
                            ?.Content
                        : null;

                    if (!string.IsNullOrWhiteSpace(transcript))
                    {
                        // Provenance of the original audio interaction is preserved.
                        var replacement = new AIInteractionText
                        {
                            Agent = audioInteraction.Agent,
                            TurnId = audioInteraction.TurnId,
                            Time = audioInteraction.Time,
                            SourceReference = audioInteraction.SourceReference,
                            Content = $"[Audio transcript: {transcript}]",
                        };
                        newInteractions.Add(replacement);
                    }
                    else
                    {
                        // Unavailable transcription is a blocking error — never a placeholder
                        // reported as success. The original media stays so strict outbound
                        // validation also reports the leftover modality.
                        var cause = aiResult?.Messages?
                            .Where(m => m != null && m.Severity == SHRuntimeMessageSeverity.Error && !string.IsNullOrWhiteSpace(m.Message))
                            .Select(m => m.Message)
                            .FirstOrDefault()
                            ?? (aiResult == null ? "helper call returned no result" : "helper produced no transcript");
                        result.Messages.Add(new SHRuntimeMessage(
                            SHRuntimeMessageSeverity.Error,
                            SHRuntimeMessageOrigin.Provider,
                            SHMessageCode.ConversionFailed,
                            $"Audio transcription failed via '{providerName}' / '{modelName}': {cause}"));
                        newInteractions.Add(audioInteraction);
                    }
                }
                else
                {
                    newInteractions.Add(interaction);
                }
            }

            result.TransformedBody = new AIBody(
                newInteractions,
                body.ToolFilter,
                body.ContextFilter,
                body.JsonOutputSchema,
                body.InteractionsNew);

            Logger.For<AudioToTextFallback>().Trace($"[AudioToTextFallback] Transcribed {audioCount} audio clips to text");
            return result;
        }
    }
}
