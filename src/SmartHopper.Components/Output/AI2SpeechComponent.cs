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
            : base("AI to Speech", "AI→Speech", "Generate speech audio from AI input", GH_Exposure.secondary)
        {
        }

        /// <summary>
        /// Gets the component GUID.
        /// </summary>
        public override Guid ComponentGuid => new Guid("1D862AD7-CB48-4A47-A22D-C05FD26FC866");

        /// <summary>
        /// Gets the component icon.
        /// </summary>
        protected override Bitmap Icon => Resources.aitospeech;

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
                    Description = "Generated audio file (path, URL, or base64)",
                    ParamType = typeof(VersatileAudioParameter),
                    Access = GH_ParamAccess.tree,
                    Extractor = OutputMapping.Single(aiReturn =>
                    {
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
            pManager.AddTextParameter("Voice", "V", "Voice ID (e.g., alloy, echo, fable, onyx, nova, shimmer)", GH_ParamAccess.tree, "nova");
            pManager.AddTextParameter("Speed", "Sp", "Speech speed (0.25 to 4.0)", GH_ParamAccess.tree, "1.0");
        }

        /// <summary>
        /// Overrides PrepareInputs to inject Voice and Speed into tool parameters.
        /// </summary>
        protected override void PrepareInputs(Dictionary<string, object> inputs, ProcessingUnitContext context)
        {
            base.PrepareInputs(inputs, context);

            // Read Voice and Speed from sliced inputs (per-unit)
            var voice = "nova";
            var speed = "1.0";

            if (inputs.TryGetValue("Voice", out var voiceObj) && voiceObj is GH_String voiceStr && !string.IsNullOrWhiteSpace(voiceStr.Value))
            {
                voice = voiceStr.Value;
            }

            if (inputs.TryGetValue("Speed", out var speedObj) && speedObj is GH_String speedStr && !string.IsNullOrWhiteSpace(speedStr.Value))
            {
                speed = speedStr.Value;
            }

            SetRequestParameterExtra(inputs, "voice", voice);
            if (double.TryParse(speed, NumberStyles.Float, CultureInfo.InvariantCulture, out var speedValue))
            {
                SetRequestParameterExtra(inputs, "speed", speedValue);
            }

            if (inputs.TryGetValue("_MergedBody", out var bodyObject) && bodyObject is AIBody body)
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
