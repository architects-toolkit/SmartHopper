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

using System.Linq;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.AIModels;
namespace SmartHopper.Infrastructure.AICall.Fallback
{
    /// <summary>
    /// Synthesizes speech audio from user text via a TTS-capable model.
    /// Used by output-adapter passthrough (strict TTS): each user
    /// <see cref="AIInteractionText"/> maps to one <see cref="AIInteractionAudio"/>.
    /// Not registered in <see cref="ModalityFallbackResolver"/> — output capabilities
    /// are not input-modality conversions, so resolving it as a chain step would be wrong.
    /// </summary>
    public sealed class TextToSpeechFallback : ModalityFallbackBase<AIInteractionText>
    {
        /// <inheritdoc/>
        public override string Name => "TextToSpeech";

        /// <inheritdoc/>
        public override AICapability Handles => AICapability.SpeechOutput;

        /// <inheritdoc/>
        public override AICapability RequiresCapability => AICapability.Text2Speech;

        /// <inheritdoc/>
        public override AICapability ResultsIn => AICapability.SpeechOutput;

        /// <inheritdoc/>
        public override string Description => "Text synthesized to speech via TTS model";

        /// <inheritdoc/>
        protected override string Origin => "fallback:tts";

        /// <inheritdoc/>
        protected override string ConvertedLabel => "Speech audio";

        /// <inheritdoc/>
        protected override string UnavailableContent => "[Speech synthesis unavailable]";

        /// <inheritdoc/>
        protected override string FailureContent => "[Speech synthesis failed — TTS AI call failed]";

        /// <inheritdoc/>
        protected override string ItemNoun => "text";

        /// <inheritdoc/>
        protected override IAIInteraction CreateTransformedInteraction(AIInteractionText source, AIReturn aiResult)
        {
            return ExtractAudio(aiResult)
                ?? (IAIInteraction)new AIInteractionText
                {
                    Agent = source.Agent,
                    Content = this.FailureContent,
                };
        }

        /// <inheritdoc/>
        protected override IAIInteraction CreateConvertedItem(AIReturn aiResult)
        {
            if (aiResult?.Success != true || aiResult.Body == null)
            {
                return null;
            }

            var audio = ExtractAudio(aiResult);
            if (audio != null)
            {
                return audio;
            }

            // Providers that return a path/URL/base64 payload as text instead of a typed audio interaction.
            var text = aiResult.Body.Interactions?
                .OfType<AIInteractionText>()
                .LastOrDefault(interaction => interaction.Agent == AIAgent.Assistant)
                ?? aiResult.Body.Interactions?.OfType<AIInteractionText>().LastOrDefault();

            return text != null
                ? new AIInteractionText { Agent = AIAgent.Assistant, Content = text.Content }
                : null;
        }

        /// <summary>
        /// Extracts the produced audio interaction, preferring assistant output.
        /// Mirrors the extraction used by the speech_generate tool.
        /// </summary>
        private static AIInteractionAudio ExtractAudio(AIReturn aiResult)
        {
            return aiResult?.Body?.Interactions?
                .OfType<AIInteractionAudio>()
                .LastOrDefault(interaction => interaction.Agent == AIAgent.Assistant)
                ?? aiResult?.Body?.Interactions?.OfType<AIInteractionAudio>().LastOrDefault();
        }
    }
}
