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
using System.Linq;
using System.Text;
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
        /// Returns a human-readable summary with a stable checksum in brackets.
        /// Changes whenever payload content or internal structure changes.
        /// Used by StatefulComponentBase for deterministic input hashing.
        /// </summary>
        public override string ToString()
        {
            var count = this.Interactions?.Count ?? 0;

            var typeCounts = this.Interactions
                ?.GroupBy(i => i.GetType().Name)
                .ToDictionary(g => g.Key, g => g.Count())
                ?? new Dictionary<string, int>();

            var overview = typeCounts.Count > 0
                ? string.Join(", ", typeCounts.Select(kv => $"{kv.Value} {FriendlyTypeName(kv.Key)}"))
                : "empty";

            var human = $"AIInputPayload ({overview})";

            var canonical = new StringBuilder();
            canonical.Append(this.PayloadType.ToString());
            canonical.Append(';');
            canonical.Append((int)this.InputCapabilityAtSource);
            canonical.Append(';');
            canonical.Append(this.Hint ?? string.Empty);
            canonical.Append(';');
            canonical.Append(count);
            canonical.Append(';');

            foreach (var interaction in this.Interactions)
            {
                canonical.Append(interaction.GetType().Name);
                canonical.Append(':');
                canonical.Append((int)interaction.Agent);
                canonical.Append(':');
                canonical.Append(interaction.TurnId ?? string.Empty);
                canonical.Append(':');
                canonical.Append(interaction.Time.Ticks);
                canonical.Append(':');
                canonical.Append(DescribeSource(interaction.SourceReference));
                canonical.Append(':');

                switch (interaction)
                {
                    case AIInteractionText text:
                        canonical.Append(text.Content ?? string.Empty);
                        canonical.Append(':');
                        canonical.Append(text.Reasoning ?? string.Empty);
                        break;
                    case AIInteractionImage img:
                        canonical.Append(img.ImageUrl?.ToString() ?? string.Empty);
                        canonical.Append(':');
                        canonical.Append(img.ImageData ?? string.Empty);
                        canonical.Append(':');
                        canonical.Append(img.OriginalPrompt ?? string.Empty);
                        canonical.Append(':');
                        canonical.Append(img.MimeType ?? string.Empty);
                        break;
                    case AIInteractionAudio aud:
                        canonical.Append(aud.FilePath ?? string.Empty);
                        canonical.Append(':');
                        canonical.Append(aud.MimeType ?? string.Empty);
                        canonical.Append(':');
                        canonical.Append(aud.Data?.Length ?? 0);
                        break;
                    case AIInteractionToolResult tr:
                        canonical.Append(tr.Id ?? string.Empty);
                        canonical.Append(':');
                        canonical.Append(tr.Name ?? string.Empty);
                        canonical.Append(':');
                        canonical.Append(tr.Result?.ToString() ?? string.Empty);
                        canonical.Append(':');
                        canonical.Append(tr.Reasoning ?? string.Empty);
                        canonical.Append(':');
                        if (tr.Images != null)
                        {
                            foreach (var image in tr.Images)
                            {
                                canonical.Append(image?.ImageData ?? string.Empty);
                                canonical.Append('|');
                                canonical.Append(image?.MimeType ?? string.Empty);
                                canonical.Append('|');
                                canonical.Append(image?.SendToModel == true ? '1' : '0');
                                canonical.Append('|');
                                canonical.Append(DescribeSource(image?.SourceReference));
                                canonical.Append('|');
                            }
                        }

                        break;
                    case AIInteractionToolCall tc:
                        canonical.Append(tc.Id ?? string.Empty);
                        canonical.Append(':');
                        canonical.Append(tc.Name ?? string.Empty);
                        canonical.Append(':');
                        canonical.Append(tc.Arguments?.ToString() ?? string.Empty);
                        canonical.Append(':');
                        canonical.Append(tc.Reasoning ?? string.Empty);
                        break;
                    case AIInteractionRuntimeMessage msg:
                        canonical.Append((int)msg.Severity);
                        canonical.Append(':');
                        canonical.Append(msg.Content ?? string.Empty);
                        break;
                }

                canonical.Append(';');
            }

            var checksum = ComputeChecksum(canonical.ToString());
            return $"{human} [{checksum}]";
        }

        private static string DescribeSource(SourceContentReference source)
        {
            if (source == null)
            {
                return string.Empty;
            }

            return string.Join(
                ",",
                source.DocumentId ?? string.Empty,
                source.SourceName ?? string.Empty,
                source.PageNumber?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                source.AssetId ?? string.Empty,
                source.FigureLabel ?? string.Empty,
                source.Caption ?? string.Empty,
                source.ContextText ?? string.Empty,
                ((int)source.Kind).ToString(System.Globalization.CultureInfo.InvariantCulture),
                source.Region == null
                    ? string.Empty
                    : string.Join(
                        ",",
                        source.Region.X.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        source.Region.Y.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        source.Region.Width.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        source.Region.Height.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        private static string FriendlyTypeName(string typeName)
        {
            return typeName switch
            {
                "AIInteractionText" => "text",
                "AIInteractionImage" => "image",
                "AIInteractionAudio" => "audio",
                "AIInteractionToolCall" => "tool call",
                "AIInteractionToolResult" => "tool result",
                "AIInteractionRuntimeMessage" => "message",
                _ => typeName.Replace("AIInteraction", string.Empty).ToLowerInvariant(),
            };
        }

        private static string ComputeChecksum(string input)
        {
            unchecked
            {
                const int offsetBasis = unchecked((int)2166136261);
                const int prime = 16777619;
                int hash = offsetBasis;

                for (int i = 0; i < input.Length; i++)
                {
                    hash ^= input[i];
                    hash *= prime;
                }

                return hash.ToString("x8");
            }
        }

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
        /// Creates an audio payload from a single speech/audio interaction.
        /// </summary>
        /// <param name="interaction">The audio interaction containing speech data.</param>
        /// <returns>A new AIInputPayload containing the speech/audio interaction.</returns>
        public static AIInputPayload FromSpeech(AIInteractionAudio interaction)
        {
            return new AIInputPayload(
                new List<IAIInteraction> { interaction },
                AICapability.SpeechInput,
                AIInputPayloadType.Audio,
                interaction?.MimeType);
        }

        /// <summary>
        /// Creates a payload from an ordered set of interactions (one document: text, images,
        /// audio and tool results in document order). The payload type is derived from the
        /// modalities present: a single modality maps to its type, mixed content maps to
        /// <see cref="AIInputPayloadType.Multimodal"/>; required input capabilities are
        /// always derived from the interactions themselves.
        /// </summary>
        /// <param name="interactions">The ordered interactions composing the payload.</param>
        /// <returns>A new AIInputPayload wrapping the given interactions.</returns>
        public static AIInputPayload FromInteractions(IEnumerable<IAIInteraction> interactions)
        {
            var list = interactions?.Where(i => i != null).ToList() ?? new List<IAIInteraction>();
            var required = list.RequiredInputCapabilities();

            var hasText = list.Any(i => i is AIInteractionText || i is AIInteractionToolCall || i is AIInteractionToolResult || i is IAIModelProjectedInteraction);
            if (hasText)
            {
                // The modality hint deliberately excludes text: add it so the factory never
                // claims a text-bearing payload has no source modality.
                required |= AICapability.TextInput;
            }
            var hasImage = list.Any(i => i is AIInteractionImage || (i is AIInteractionToolResult tr && tr.Images != null && tr.Images.Count > 0));
            var hasAudio = list.Any(i => i is AIInteractionAudio);

            var modalities = (hasText ? 1 : 0) + (hasImage ? 1 : 0) + (hasAudio ? 1 : 0);
            var payloadType = modalities > 1
                ? AIInputPayloadType.Multimodal
                : hasImage
                    ? AIInputPayloadType.Image
                    : hasAudio
                        ? AIInputPayloadType.Audio
                        : AIInputPayloadType.Text;

            return new AIInputPayload(list, required, payloadType);
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

        /// <summary>Audio content payload (music, sound effects, speech, TTS, STT).</summary>
        Audio,

        /// <summary>Context filter payload (not rendered as content).</summary>
        Context,

        /// <summary>Unknown or unclassified payload type.</summary>
        Unknown,

        /// <summary>
        /// Mixed-modality document payload (ordered text, image and/or audio interactions).
        /// A presentation classification only — required modalities are derived from the
        /// interactions, not this label.
        /// </summary>
        Multimodal,
    }
}
