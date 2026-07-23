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
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AIModels;

namespace SmartHopper.Core.Models
{
    /// <summary>
    /// Represents a payload of AI interactions that can be wired between Grasshopper components.
    /// Carries interactions, capability hints, and optional format hints for rendering.
    /// </summary>
    public sealed class AIInputPayload
    {
        /// <summary>
        /// Gets the list of AI interactions in this payload.
        /// </summary>
        public List<IAIInteraction> Interactions { get; }

        /// <summary>
        /// Gets the source capability that generated this payload.
        /// Used for capability validation and fallback chain detection.
        /// </summary>
        public AICapability InputCapabilityAtSource { get; }

        /// <summary>
        /// Gets an optional MIME type or format hint for the payload content.
        /// Examples: "image/png", "application/json", "text/plain".
        /// </summary>
        public string Hint { get; }

        /// <summary>
        /// Gets the type of payload for user-readable rendering in Grasshopper UI.
        /// </summary>
        public AIInputPayloadType PayloadType { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AIInputPayload"/> class.
        /// </summary>
        /// <param name="interactions">The list of interactions in this payload.</param>
        /// <param name="inputCapabilityAtSource">The source capability that generated this payload.</param>
        /// <param name="payloadType">The type of payload for UI rendering.</param>
        /// <param name="hint">Optional MIME type or format hint.</param>
        public AIInputPayload(
            List<IAIInteraction> interactions,
            AICapability inputCapabilityAtSource,
            AIInputPayloadType payloadType,
            string hint = null)
        {
            this.Interactions = interactions ?? new List<IAIInteraction>();
            this.InputCapabilityAtSource = inputCapabilityAtSource;
            this.PayloadType = payloadType;
            this.Hint = hint;
        }

        /// <summary>
        /// Creates a text payload from a single text interaction.
        /// </summary>
        /// <param name="content">The text content.</param>
        /// <param name="agent">The agent that originated this text (default: User).</param>
        /// <returns>A new AIInputPayload containing the text interaction.</returns>
        public static AIInputPayload FromText(string content, AIAgent agent = AIAgent.User)
        {
            var interaction = new AIInteractionText { Agent = agent, Content = content };
            return new AIInputPayload(
                new List<IAIInteraction> { interaction },
                AICapability.TextInput,
                AIInputPayloadType.Text);
        }

        /// <summary>
        /// Creates an image payload from a single image interaction.
        /// </summary>
        /// <param name="interaction">The image interaction.</param>
        /// <returns>A new AIInputPayload containing the image interaction.</returns>
        public static AIInputPayload FromImage(AIInteractionImage interaction)
        {
            return new AIInputPayload(
                new List<IAIInteraction> { interaction },
                AICapability.ImageInput,
                AIInputPayloadType.Image,
                interaction?.MimeType);
        }

        /// <summary>
        /// Creates an audio payload from a single audio interaction.
        /// </summary>
        /// <param name="interaction">The audio interaction.</param>
        /// <returns>A new AIInputPayload containing the audio interaction.</returns>
        public static AIInputPayload FromAudio(AIInteractionAudio interaction)
        {
            return new AIInputPayload(
                new List<IAIInteraction> { interaction },
                AICapability.AudioInput,
                AIInputPayloadType.Audio,
                interaction?.MimeType);
        }

        /// <summary>
        /// Creates a speech payload from a single audio interaction.
        /// </summary>
        /// <param name="interaction">The audio interaction containing speech data.</param>
        /// <returns>A new AIInputPayload containing the speech interaction.</returns>
        public static AIInputPayload FromSpeech(AIInteractionAudio interaction)
        {
            return new AIInputPayload(
                new List<IAIInteraction> { interaction },
                AICapability.SpeechInput,
                AIInputPayloadType.Speech,
                interaction?.MimeType);
        }

        /// <summary>
        /// Creates a context payload with a context filter string.
        /// </summary>
        /// <param name="contextFilter">The context provider filter (e.g., "time,file", "-time", "*", "-*").</param>
        /// <returns>A new AIInputPayload tagged as context type.</returns>
        public static AIInputPayload FromContextFilter(string contextFilter)
        {
            var interaction = new AIInteractionText
            {
                Agent = AIAgent.Context,
                Content = contextFilter ?? string.Empty
            };
            return new AIInputPayload(
                new List<IAIInteraction> { interaction },
                AICapability.None,
                AIInputPayloadType.Context);
        }
    }

    /// <summary>
    /// Specifies the type of AIInputPayload for user-readable rendering in Grasshopper UI.
    /// </summary>
    public enum AIInputPayloadType
    {
        /// <summary>Text content payload.</summary>
        Text,

        /// <summary>Image content payload.</summary>
        Image,

        /// <summary>Audio content payload (music, sound effects, general audio).</summary>
        Audio,

        /// <summary>Speech content payload (voice, TTS, STT).</summary>
        Speech,

        /// <summary>Context filter payload (not rendered as content).</summary>
        Context,

        /// <summary>Unknown or mixed payload type.</summary>
        Unknown,
    }
}
