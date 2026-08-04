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
using System.Linq;
using System.Text;
using SmartHopper.Core.Models;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;

namespace SmartHopper.Core.Types
{
    /// <summary>
    /// Provides user-readable rendering of AIInputPayload content based on payload type.
    /// Formats interactions for display in Grasshopper UI.
    /// </summary>
    public static class AIInputPayloadRenderer
    {
        /// <summary>
        /// Renders an AIInputPayload to user-readable text for Grasshopper UI display.
        /// Format varies based on payload type.
        /// </summary>
        /// <param name="payload">The payload to render.</param>
        /// <returns>User-readable text representation.</returns>
        public static string RenderToUserText(AIInputPayload payload)
        {
            if (payload == null)
            {
                return "[Empty Payload]";
            }

            return payload.PayloadType switch
            {
                AIInputPayloadType.Text => RenderTextPayload(payload),
                AIInputPayloadType.Image => RenderImagePayload(payload),
                AIInputPayloadType.Audio => RenderAudioPayload(payload),
                AIInputPayloadType.Speech => RenderSpeechPayload(payload),
                AIInputPayloadType.Context => RenderContextPayload(payload),
                _ => RenderUnknownPayload(payload),
            };
        }

        /// <summary>
        /// Renders a text payload to user-readable format.
        /// Shows agent and content preview.
        /// </summary>
        private static string RenderTextPayload(AIInputPayload payload)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[Text Payload]");

            foreach (var interaction in payload.Interactions)
            {
                if (interaction is AIInteractionText textInteraction)
                {
                    var agent = textInteraction.Agent.ToDescription();
                    var content = textInteraction.Content ?? string.Empty;

                    // Truncate long content for preview
                    var preview = content.Length > 100
                        ? content.Substring(0, 100) + "..."
                        : content;

                    sb.AppendLine($"{agent}: {preview}");
                }
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Renders an image payload to user-readable format.
        /// Shows image source information.
        /// </summary>
        private static string RenderImagePayload(AIInputPayload payload)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[Image Payload]");

            var imageCount = 0;
            foreach (var interaction in payload.Interactions)
            {
                if (interaction is AIInteractionImage imageInteraction)
                {
                    imageCount++;
                    var source = imageInteraction.ImageUrl != null
                        ? $"URL: {imageInteraction.ImageUrl}"
                        : !string.IsNullOrWhiteSpace(imageInteraction.ImageData)
                        ? $"Base64 ({imageInteraction.ImageData.Length} chars)"
                        : "Unknown source";

                    var mime = imageInteraction.MimeType ?? "unknown";
                    sb.AppendLine($"Image {imageCount}: {source} ({mime})");
                }
            }

            if (imageCount == 0)
            {
                sb.AppendLine("(No images found)");
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Renders an audio payload to user-readable format.
        /// Shows audio file information.
        /// </summary>
        private static string RenderAudioPayload(AIInputPayload payload)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[Audio Payload]");

            var audioCount = 0;
            foreach (var interaction in payload.Interactions)
            {
                if (interaction is AIInteractionAudio audioInteraction)
                {
                    audioCount++;
                    var source = !string.IsNullOrWhiteSpace(audioInteraction.FilePath)
                        ? $"File: {audioInteraction.FilePath}"
                        : audioInteraction.Data != null
                        ? $"In-memory ({audioInteraction.Data.Length} bytes)"
                        : "Unknown source";

                    var mime = audioInteraction.MimeType ?? "unknown";
                    var lang = !string.IsNullOrWhiteSpace(audioInteraction.LanguageHint)
                        ? $" [{audioInteraction.LanguageHint}]"
                        : string.Empty;

                    sb.AppendLine($"Audio {audioCount}: {source} ({mime}){lang}");
                }
            }

            if (audioCount == 0)
            {
                sb.AppendLine("(No audio found)");
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Renders a speech payload to user-readable format.
        /// Shows speech/voice file information.
        /// </summary>
        private static string RenderSpeechPayload(AIInputPayload payload)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[Speech Payload]");

            var speechCount = 0;
            foreach (var interaction in payload.Interactions)
            {
                if (interaction is AIInteractionAudio speechInteraction)
                {
                    speechCount++;
                    var source = !string.IsNullOrWhiteSpace(speechInteraction.FilePath)
                        ? $"File: {speechInteraction.FilePath}"
                        : speechInteraction.Data != null
                        ? $"In-memory ({speechInteraction.Data.Length} bytes)"
                        : "Unknown source";

                    var mime = speechInteraction.MimeType ?? "unknown";
                    var lang = !string.IsNullOrWhiteSpace(speechInteraction.LanguageHint)
                        ? $" [{speechInteraction.LanguageHint}]"
                        : string.Empty;

                    sb.AppendLine($"Speech {speechCount}: {source} ({mime}){lang}");
                }
            }

            if (speechCount == 0)
            {
                sb.AppendLine("(No speech data found)");
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Renders a context payload to user-readable format.
        /// Shows context provider filter.
        /// </summary>
        private static string RenderContextPayload(AIInputPayload payload)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[Context Payload]");

            var contextInteraction = payload.Interactions.FirstOrDefault(i => i.Agent == AIAgent.Context);
            if (contextInteraction is AIInteractionText contextText)
            {
                var filter = contextText.Content ?? string.Empty;
                sb.AppendLine($"Provider filter: {filter}");
            }
            else
            {
                sb.AppendLine("(No context filter found)");
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Renders an unknown payload type to user-readable format.
        /// Shows interaction count and types.
        /// </summary>
        private static string RenderUnknownPayload(AIInputPayload payload)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[Unknown Payload]");

            if (payload.Interactions == null || payload.Interactions.Count == 0)
            {
                sb.AppendLine("(No interactions)");
                return sb.ToString().TrimEnd();
            }

            var interactionTypes = new Dictionary<string, int>();
            foreach (var interaction in payload.Interactions)
            {
                var typeName = interaction?.GetType().Name ?? "Unknown";
                if (!interactionTypes.ContainsKey(typeName))
                {
                    interactionTypes[typeName] = 0;
                }

                interactionTypes[typeName]++;
            }

            foreach (var kvp in interactionTypes)
            {
                sb.AppendLine($"{kvp.Key}: {kvp.Value}");
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Gets a short summary of the payload for tooltips or badges.
        /// </summary>
        /// <param name="payload">The payload to summarize.</param>
        /// <returns>A short summary string.</returns>
        public static string GetSummary(AIInputPayload payload)
        {
            if (payload == null)
            {
                return "Empty";
            }

            var interactionCount = payload.Interactions?.Count ?? 0;
            return payload.PayloadType switch
            {
                AIInputPayloadType.Text => $"Text ({interactionCount})",
                AIInputPayloadType.Image => $"Image ({interactionCount})",
                AIInputPayloadType.Audio => $"Audio ({interactionCount})",
                AIInputPayloadType.Speech => $"Speech ({interactionCount})",
                AIInputPayloadType.Context => "Context",
                _ => $"Payload ({interactionCount})",
            };
        }
    }
}
