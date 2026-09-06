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
using System.IO;
using Newtonsoft.Json.Linq;
using SmartHopper.ProviderSdk.AICall.Core.Base;

namespace SmartHopper.ProviderSdk.AICall.Core.Interactions
{
    /// <summary>
    /// Encodes and decodes audio payloads for providers that target the
    /// OpenAI-compatible <c>/v1/chat/completions</c> audio shape:
    /// <c>input_audio</c> content parts on input, <c>modalities</c> +
    /// <c>audio</c> request parameters and a <c>message.audio</c> object on output.
    /// </summary>
    public static class OpenAICompatibleAudioCodec
    {
        /// <summary>The default voice used for chat audio output.</summary>
        public const string DefaultVoice = "alloy";

        /// <summary>The default audio format used for chat audio input and output.</summary>
        public const string DefaultFormat = "wav";

        /// <summary>
        /// Resolves the raw audio bytes and format token from an <see cref="AIInteractionAudio"/>.
        /// Bytes come from <see cref="AIInteractionAudio.Data"/> when present, otherwise the file
        /// referenced by <see cref="AIInteractionAudio.FilePath"/> is read from disk.
        /// </summary>
        /// <param name="audio">The audio interaction to resolve.</param>
        /// <param name="base64Data">The base64-encoded audio payload.</param>
        /// <param name="format">The OpenAI-compatible format token (wav, mp3, opus, ...).</param>
        /// <returns><c>true</c> when audio bytes could be resolved.</returns>
        public static bool TryResolveAudioData(AIInteractionAudio? audio, out string? base64Data, out string format)
        {
            base64Data = null;
            format = MapAudioFormat(audio?.MimeType, audio?.FilePath);

            if (audio == null)
            {
                return false;
            }

            byte[]? bytes = audio.Data;
            if ((bytes == null || bytes.Length == 0) && !string.IsNullOrWhiteSpace(audio.FilePath))
            {
                try
                {
                    bytes = File.ReadAllBytes(audio.FilePath);
                }
                catch (Exception)
                {
                    // The referenced file may not exist or be readable; treat as unresolvable.
                }
            }

            if (bytes == null || bytes.Length == 0)
            {
                return false;
            }

            base64Data = Convert.ToBase64String(bytes);
            return true;
        }

        /// <summary>
        /// Maps an audio MIME type (falling back to the file extension) to the
        /// OpenAI-compatible <c>input_audio.format</c> / <c>audio.format</c> token.
        /// </summary>
        /// <param name="mimeType">The MIME type (e.g. "audio/wav", "audio/mpeg").</param>
        /// <param name="filePath">Optional file path used to infer the format when the MIME type is missing.</param>
        /// <returns>A format token such as "wav", "mp3", "opus", "flac", "aac" or "pcm16".</returns>
        public static string MapAudioFormat(string? mimeType, string? filePath = null)
        {
            var mime = (mimeType ?? string.Empty).Trim().ToLowerInvariant();
            var semicolon = mime.IndexOf(';');
            if (semicolon >= 0)
            {
                mime = mime.Substring(0, semicolon).Trim();
            }

            switch (mime)
            {
                case "audio/wav":
                case "audio/x-wav":
                case "audio/wave":
                case "audio/vnd.wave":
                    return "wav";
                case "audio/mpeg":
                case "audio/mp3":
                case "audio/mpeg3":
                case "audio/x-mpeg":
                    return "mp3";
                case "audio/ogg":
                case "audio/opus":
                    return "opus";
                case "audio/flac":
                case "audio/x-flac":
                    return "flac";
                case "audio/aac":
                case "audio/aacp":
                case "audio/mp4":
                case "audio/m4a":
                case "audio/x-m4a":
                    return "aac";
                case "audio/pcm":
                case "audio/l16":
                    return "pcm16";
            }

            if (!string.IsNullOrWhiteSpace(filePath))
            {
                var extension = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant();
                switch (extension)
                {
                    case "mp3":
                        return "mp3";
                    case "ogg":
                        return "opus";
                    case "flac":
                        return "flac";
                    case "aac":
                    case "m4a":
                        return "aac";
                    case "pcm":
                        return "pcm16";
                }
            }

            return DefaultFormat;
        }

        /// <summary>
        /// Builds an OpenAI-compatible <c>input_audio</c> content part for a chat or
        /// Responses-API message.
        /// </summary>
        /// <param name="audio">The audio interaction to encode.</param>
        /// <returns>
        /// A JObject shaped like <c>{ "type": "input_audio", "input_audio": { "data": ..., "format": ... } }</c>,
        /// or <c>null</c> when no audio bytes could be resolved.
        /// </returns>
        public static JObject? ToInputAudioContentPart(AIInteractionAudio? audio)
        {
            if (!TryResolveAudioData(audio, out var base64Data, out var format) || base64Data == null)
            {
                return null;
            }

            return new JObject
            {
                ["type"] = "input_audio",
                ["input_audio"] = new JObject
                {
                    ["data"] = base64Data,
                    ["format"] = format,
                },
            };
        }

        /// <summary>
        /// Adds the <c>modalities</c> and <c>audio</c> parameters required to request
        /// audio output on OpenAI-compatible chat completions endpoints. Voice and format
        /// are read from request extras (<c>voice</c>, <c>format</c> or <c>response_format</c>),
        /// falling back to <see cref="DefaultVoice"/> and <see cref="DefaultFormat"/>.
        /// </summary>
        /// <param name="body">The request body under construction.</param>
        /// <param name="extras">Optional per-request extra parameters.</param>
        public static void ApplyAudioOutputParameters(JObject body, IReadOnlyDictionary<string, JToken>? extras)
        {
            var voice = DefaultVoice;
            var format = DefaultFormat;

            if (extras != null)
            {
                if (extras.TryGetValue("voice", out var voiceToken) && !string.IsNullOrWhiteSpace(voiceToken?.ToString()))
                {
                    voice = voiceToken!.ToString();
                }

                if (extras.TryGetValue("format", out var formatToken) && !string.IsNullOrWhiteSpace(formatToken?.ToString()))
                {
                    format = formatToken!.ToString();
                }
                else if (extras.TryGetValue("response_format", out var responseFormatToken) && !string.IsNullOrWhiteSpace(responseFormatToken?.ToString()))
                {
                    format = responseFormatToken!.ToString();
                }
            }

            body["modalities"] = new JArray { "text", "audio" };
            body["audio"] = new JObject
            {
                ["voice"] = voice,
                ["format"] = format,
            };
        }

        /// <summary>
        /// Extracts an <see cref="AIInteractionAudio"/> from a chat completions response
        /// <c>message.audio</c> object ({ id, data, transcript, expires_at }).
        /// </summary>
        /// <param name="message">The response <c>message</c> object.</param>
        /// <param name="transcript">The audio transcript, when present.</param>
        /// <returns>An assistant audio interaction, or <c>null</c> when the response carries no audio data.</returns>
        public static AIInteractionAudio? ExtractAudioOutput(JObject? message, out string? transcript)
        {
            transcript = null;

            var audioObj = message?["audio"] as JObject;
            if (audioObj == null)
            {
                return null;
            }

            transcript = audioObj["transcript"]?.ToString();

            var data = audioObj["data"]?.ToString();
            if (string.IsNullOrWhiteSpace(data))
            {
                return null;
            }

            byte[]? bytes = null;
            try
            {
                bytes = Convert.FromBase64String(data);
            }
            catch (FormatException)
            {
                // Malformed base64 payload; still surface the interaction without data.
            }

            return new AIInteractionAudio
            {
                Agent = AIAgent.Assistant,
                Data = bytes,
                MimeType = $"audio/{DefaultFormat}",
            };
        }
    }
}
