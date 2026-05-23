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
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Utilities;

namespace SmartHopper.Infrastructure.AICall.Core.Interactions
{
    /// <summary>
    /// Represents an audio interaction for speech-to-text or text-to-speech operations.
    /// </summary>
    public class AIInteractionAudio : AIInteractionBase, IAIKeyedInteraction
    {
        /// <summary>
        /// Gets or sets the audio data as a byte array.
        /// Either Data or FilePath should be set, not both.
        /// </summary>
        public byte[] Data { get; set; }

        /// <summary>
        /// Gets or sets the file path to the audio file.
        /// Either Data or FilePath should be set, not both.
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// Gets or sets the MIME type of the audio (e.g., "audio/wav", "audio/mp3", "audio/mpeg").
        /// </summary>
        public string MimeType { get; set; }

        /// <summary>
        /// Gets or sets an optional language hint for speech-to-text operations.
        /// Format: ISO 639-1 language code (e.g., "en", "es", "fr").
        /// </summary>
        public string LanguageHint { get; set; }

        /// <summary>
        /// Gets the size of the audio data in bytes.
        /// </summary>
        public long GetAudioSize()
        {
            if (this.Data != null)
            {
                return this.Data.Length;
            }

            if (!string.IsNullOrWhiteSpace(this.FilePath))
            {
                try
                {
                    var fileInfo = new System.IO.FileInfo(this.FilePath);
                    return fileInfo.Length;
                }
                catch
                {
                    return 0;
                }
            }

            return 0;
        }

        /// <summary>
        /// Returns a stable stream grouping key for this audio interaction.
        /// Uses file path when available; otherwise a short hash of audio data.
        /// </summary>
        /// <returns>Stream group key.</returns>
        public string GetStreamKey()
        {
            var id = !string.IsNullOrWhiteSpace(this.FilePath)
                ? this.FilePath
                : (this.Data != null && this.Data.Length > 0
                    ? HashUtility.ComputeShortHash(Convert.ToBase64String(this.Data))
                    : "empty");

            if (!string.IsNullOrWhiteSpace(this.TurnId))
            {
                return $"turn:{this.TurnId}:audio:{id}";
            }

            return $"audio:{id}";
        }

        /// <summary>
        /// Returns a stable de-duplication key for this audio interaction.
        /// Includes stream key and MIME type to distinguish similar audio files.
        /// </summary>
        /// <returns>De-duplication key.</returns>
        public string GetDedupKey()
        {
            var mimeType = this.MimeType ?? "unknown";
            return $"{this.GetStreamKey()}:{mimeType}";
        }

        /// <summary>
        /// Returns a string representation of the audio interaction.
        /// </summary>
        /// <returns>A formatted string containing audio metadata.</returns>
        public override string ToString()
        {
            var size = this.GetAudioSize();
            var sizeStr = size > 0 ? $" ({size} bytes)" : string.Empty;
            var source = this.Data != null ? "in-memory" : this.FilePath ?? "unknown";
            var lang = !string.IsNullOrWhiteSpace(this.LanguageHint) ? $" [{this.LanguageHint}]" : string.Empty;

            return $"Audio({this.MimeType}, {source}{sizeStr}){lang}";
        }
    }
}
