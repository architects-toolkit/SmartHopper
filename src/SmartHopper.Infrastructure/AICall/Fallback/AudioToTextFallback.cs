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
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Metrics;
using SmartHopper.Infrastructure.AIModels;

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
            return ModelManager.Instance.ValidateCapabilities(
                providerName,
                ModelManager.Instance.SelectBestModel(providerName, null, this.RequiresCapability),
                this.RequiresCapability);
        }

        /// <inheritdoc/>
        public async Task<ModalityFallbackResult> ApplyAsync(AIBody body, string providerName, string modelName, CancellationToken ct)
        {
            var result = new ModalityFallbackResult();
            var newInteractions = new List<IAIInteraction>();
            var audioCount = 0;

            foreach (var interaction in body.Interactions)
            {
                ct.ThrowIfCancellationRequested();

                if (interaction is AIInteractionAudio audioInteraction)
                {
                    audioCount++;
                    Debug.WriteLine($"[AudioToTextFallback] Transcribing audio {audioCount} via {providerName}/{modelName}");

                    // Build a STT request for this audio
                    var audioBody = AIBodyBuilder.Create()
                        .Add(audioInteraction)
                        .Build();

                    var request = new AIRequestCall();
                    request.Initialize(
                        provider: providerName,
                        model: modelName,
                        body: audioBody,
                        endpoint: "fallback:stt",
                        capability: this.RequiresCapability);

                    var aiResult = await request.Exec().ConfigureAwait(false);

                    if (aiResult.Success && aiResult.Body != null)
                    {
                        var textInteraction = aiResult.Body.Interactions?
                            .OfType<AIInteractionText>()
                            .LastOrDefault();

                        var transcript = textInteraction?.Content ?? "[Audio transcription unavailable]";
                        var replacement = new AIInteractionText
                        {
                            Agent = audioInteraction.Agent,
                            Content = $"[Audio transcript: {transcript}]",
                        };
                        newInteractions.Add(replacement);
                    }
                    else
                    {
                        var replacement = new AIInteractionText
                        {
                            Agent = audioInteraction.Agent,
                            Content = "[Audio transcription unavailable — fallback AI call failed]",
                        };
                        newInteractions.Add(replacement);
                    }

                    if (aiResult.Metrics != null)
                    {
                        result.ExtraMetricsList.Add(aiResult.Metrics);
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

            Debug.WriteLine($"[AudioToTextFallback] Transcribed {audioCount} audio clips to text");
            return result;
        }
    }
}
