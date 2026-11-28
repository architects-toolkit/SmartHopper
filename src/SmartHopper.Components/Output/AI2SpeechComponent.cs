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
using System.Drawing;
using System.Globalization;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using SmartHopper.Components.Properties;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Core.Parameters;
using SmartHopper.Core.Types;
using SmartHopper.Infrastructure.AICall.Fallback;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;

namespace SmartHopper.Components.Output
{
    /// <summary>
    /// Generates speech audio from AI input payloads.
    /// </summary>
    public class AI2SpeechComponent : AIOutputAdapterBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AI2SpeechComponent"/> class.
        /// </summary>
        public AI2SpeechComponent()
            : base("AI to Speech", "AI2Speech", "Synthesize spoken audio from the connected AI input(s) using a text-to-speech model.", GH_Exposure.quarternary | GH_Exposure.obscure)
        {
        }

        /// <summary>
        /// Gets the component GUID.
        /// </summary>
        public override Guid ComponentGuid => new Guid("1D862AD7-CB48-4A47-A22D-C05FD26FC866");

        /// <summary>
        /// Gets the component icon.
        /// </summary>
        protected override Bitmap Icon => Resources.AI2Speech;

        /// <summary>
        /// Gets the AI tools used by this component.
        /// </summary>
        protected override IReadOnlyList<string> UsingAiTools => new[] { "speech_generate" };

        /// <summary>
        /// Gets the internal system prompt.
        /// </summary>
        protected override string GetInternalSystemPrompt()
        {
            return "You are a text-to-speech assistant. Generate clear, natural-sounding speech text based on user input.";
        }

        /// <inheritdoc/>
        protected override IModalityFallback GetPassthroughConversion(AIBody mergedBody)
        {
            // Strict TTS: every input item is user-authored text — each maps to one audio.
            // A System-agent instruction (e.g. from AI Prompt) counts as another input and
            // keeps the normal assistant path.
            var interactions = mergedBody?.Interactions;
            if (interactions == null || interactions.Count == 0)
            {
                return null;
            }

            return interactions.All(interaction => interaction is AIInteractionText && interaction.Agent == AIAgent.User)
                ? new TextToSpeechFallback()
                : null;
        }

        /// <inheritdoc/>
        protected override IGH_Goo ConvertPassthroughItem(IAIInteraction item)
        {
            try
            {
                if (item is AIInteractionAudio audio)
                {
                    return new GH_VersatileAudio(VersatileAudio.FromInteraction(audio));
                }

                // Providers that return a path/URL/base64 payload as text.
                if (item is AIInteractionText text && !string.IsNullOrWhiteSpace(text.Content))
                {
                    return new GH_VersatileAudio(VersatileAudio.FromString(text.Content));
                }
            }
            catch
            {
                // Conversion payload is not usable audio — drop the item.
            }

            return null;
        }

        /// <summary>
        /// Gets the output mappings.
        /// </summary>
        protected override IReadOnlyList<OutputMapping> GetOutputMappings()
        {
            return new[]
            {
                new OutputMapping
                {
                    ParamName = "Audio",
                    NickName = "A",
                    Description = "Synthesized speech audio (connect to Audio Viewer to play or save to disk).",
                    ParamType = typeof(VersatileAudioParameter),
                    Access = GH_ParamAccess.tree,
                    Extractor = OutputMapping.Single(aiReturn =>
                    {
                        // Prefer a typed audio interaction produced by TTS-capable providers.
                        var audioInteraction = aiReturn?.Body?.GetLastAssistantAudio();

                        if (audioInteraction != null)
                        {
                            try
                            {
                                return new GH_VersatileAudio(VersatileAudio.FromInteraction(audioInteraction));
                            }
                            catch
                            {
                                return null;
                            }
                        }

                        // Fallback: providers that return a path/URL/base64 payload as text.
                        if (aiReturn?.Body?.GetLastAssistantText() is string text && !string.IsNullOrWhiteSpace(text))
                        {
                            try
                            {
                                var audio = VersatileAudio.FromString(text);
                                return new GH_VersatileAudio(audio);
                            }
                            catch
                            {
                                return null;
                            }
                        }

                        return null;
                    })
                }
            };
        }

        /// <summary>
        /// Registers additional input parameters.
        /// </summary>
        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Voice", "V", "Optional provider-specific voice ID (e.g., 'alloy', 'echo', 'fable', 'onyx', 'nova', 'shimmer' on OpenAI, or 'Kore' on Gemini). Leave empty to use the provider's default voice.", GH_ParamAccess.tree, string.Empty);
            pManager.AddTextParameter("Speed", "Sp", "Playback speed multiplier (0.25 to 4.0, defaults to '1.0').", GH_ParamAccess.tree, "1.0");
        }

        /// <summary>
        /// Overrides PrepareInputs to inject Voice and Speed into tool parameters.
        /// </summary>
        protected override void PrepareInputs(Dictionary<string, object> inputs, ProcessingUnitContext context)
        {
            base.PrepareInputs(inputs, context);

            // Read Voice and Speed from sliced inputs (per-unit)
            var voice = string.Empty;
            var speed = "1.0";

            if (inputs.TryGetValue("Voice", out var voiceObj) && voiceObj is GH_String voiceStr && !string.IsNullOrWhiteSpace(voiceStr.Value))
            {
                voice = voiceStr.Value;
            }

            if (inputs.TryGetValue("Speed", out var speedObj) && speedObj is GH_String speedStr && !string.IsNullOrWhiteSpace(speedStr.Value))
            {
                speed = speedStr.Value;
            }

            if (!string.IsNullOrWhiteSpace(voice))
            {
                SetRequestParameterExtra(inputs, "voice", voice);
            }
            if (double.TryParse(speed, NumberStyles.Float, CultureInfo.InvariantCulture, out var speedValue))
            {
                SetRequestParameterExtra(inputs, "speed", speedValue);
            }

            // Passthrough bodies feed the TTS conversion directly — every user text must
            // be preserved, so the single-text rebuild only applies to the normal path.
            if (!inputs.ContainsKey("_PassthroughStep")
                && inputs.TryGetValue("_MergedBody", out var bodyObject) && bodyObject is AIBody body)
            {
                var text = body.Interactions
                    .OfType<AIInteractionText>()
                    .LastOrDefault(interaction => interaction.Agent == AIAgent.User)?.Content
                    ?? body.GetLastText();
                inputs["_MergedBody"] = AIBodyBuilder.Create().AddUser(text).Build();
            }
        }
    }
}
