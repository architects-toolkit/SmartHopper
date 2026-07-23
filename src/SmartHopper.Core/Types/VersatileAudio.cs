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
using System.Net.Http;
using System.Text.RegularExpressions;

namespace SmartHopper.Core.Types
{
    /// <summary>
    /// Specifies the source kind of audio.
    /// </summary>
    public enum VersatileAudioKind
    {
        /// <summary>Local file path.</summary>
        LocalFile,

        /// <summary>HTTP(S) URL.</summary>
        Url,

        /// <summary>Base64-encoded audio data.</summary>
        Base64,

        /// <summary>Data URI with embedded base64 audio.</summary>
        DataUri,
    }

    /// <summary>
    /// Adapter that wraps heterogeneous audio inputs (file path, URL, base64, data-URI)
    /// and provides convenient access to audio data.
    /// </summary>
    public sealed class VersatileAudio
    {
        private static readonly HashSet<string> SupportedAudioExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3", ".wav", ".m4a", ".aac", ".flac", ".ogg", ".wma", ".opus",
        };

        /// <summary>
        /// Gets the kind of audio source.
        /// </summary>
        public VersatileAudioKind Kind { get; private set; }

        /// <summary>
        /// Gets the raw value (path, URL, base64, or data-URI string).
        /// </summary>
        public string RawValue { get; private set; }

        /// <summary>
        /// Gets the MIME type of the audio.
        /// </summary>
        public string MimeType { get; private set; }

        /// <summary>
        /// Gets the unique identifier for this audio within a document (e.g., "audio-1", "audio-2").
        /// Only populated when audio is extracted from a document.
        /// </summary>
        public string Id { get; private set; }

        /// <summary>
        /// Gets the contextual description of where the audio was found
        /// (e.g., "Page 3", "Slide 5", "Document body").
        /// Only populated when audio is extracted from a document.
        /// </summary>
        public string Context { get; private set; }

        /// <summary>
        /// Gets the page or slide number where the audio was found (1-based).
        /// A value of 0 means the location is unknown or not applicable.
        /// Only populated when audio is extracted from a document.
        /// </summary>
        public int PageOrSlide { get; private set; }

        /// <summary>
        /// Gets the source document path or identifier.
        /// Only populated when audio is extracted from a document.
        /// </summary>
        public string SourceDocument { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="VersatileAudio"/> class from a string.
        /// Auto-detects the source kind based on input pattern.
        /// </summary>
        /// <param name="input">The input string (URL, file path, base64, or data-URI).</param>
        /// <returns>A new VersatileAudio.</returns>
        public static VersatileAudio FromString(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                throw new ArgumentException("Input cannot be null or whitespace.", nameof(input));
            }

            var trimmedInput = input.Trim();
            var kind = DetectSourceKind(trimmedInput);

            return new VersatileAudio
            {
                Kind = kind,
                RawValue = trimmedInput,
                MimeType = GetMimeTypeFromSource(trimmedInput, kind),
                Id = null,
                Context = null,
                PageOrSlide = 0,
                SourceDocument = null,
            };
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="VersatileAudio"/> class from extracted document data.
        /// Use this when creating audio sources from PDF/DOCX/PPTX file extraction.
        /// </summary>
        /// <param name="base64Data">The base64-encoded audio data.</param>
        /// <param name="mimeType">The MIME type of the audio.</param>
        /// <param name="id">Unique identifier for this audio within the document (e.g., "audio-1").</param>
        /// <param name="context">Contextual description of where the audio was found (e.g., "Page 3").</param>
        /// <param name="pageOrSlide">Page or slide number (1-based), or 0 if unknown.</param>
        /// <param name="sourceDocument">Source document path or identifier.</param>
        /// <returns>A new VersatileAudio with document metadata.</returns>
        public static VersatileAudio FromExtractedDocument(
            string base64Data,
            string mimeType,
            string id,
            string context,
            int pageOrSlide,
            string sourceDocument)
        {
            if (string.IsNullOrWhiteSpace(base64Data))
            {
                throw new ArgumentException("Base64 data cannot be null or whitespace.", nameof(base64Data));
            }

            return new VersatileAudio
            {
                Kind = VersatileAudioKind.Base64,
                RawValue = base64Data,
                MimeType = mimeType ?? "audio/mpeg",
                Id = id ?? "audio",
                Context = context ?? string.Empty,
                PageOrSlide = pageOrSlide,
                SourceDocument = sourceDocument ?? string.Empty,
            };
        }

        /// <summary>
        /// Reconstructs a <see cref="VersatileAudio"/> from a previously serialized representation.
        /// </summary>
        /// <param name="kind">The source kind.</param>
        /// <param name="rawValue">The raw value (path, URL, base64, or data-URI).</param>
        /// <param name="mimeType">MIME type of the audio.</param>
        /// <param name="id">Unique identifier within a document.</param>
        /// <param name="context">Contextual description of where the audio was found.</param>
        /// <param name="pageOrSlide">Page or slide number, or 0 if unknown.</param>
        /// <param name="sourceDocument">Source document path or identifier.</param>
        /// <returns>A new <see cref="VersatileAudio"/>.</returns>
        public static VersatileAudio FromDeserialized(
            VersatileAudioKind kind,
            string rawValue,
            string mimeType,
            string id,
            string context,
            int pageOrSlide,
            string sourceDocument)
        {
            return new VersatileAudio
            {
                Kind = kind,
                RawValue = rawValue,
                MimeType = mimeType ?? "audio/mpeg",
                Id = id ?? "audio",
                Context = context ?? string.Empty,
                PageOrSlide = pageOrSlide,
                SourceDocument = sourceDocument ?? string.Empty,
            };
        }

        /// <summary>
        /// Converts this audio source to a byte array.
        /// For URLs and base64, downloads/decodes lazily.
        /// For local files, loads from disk.
        /// </summary>
        /// <returns>A byte array containing the audio data.</returns>
        public byte[] ToByteArray()
        {
            if (this.Kind == VersatileAudioKind.LocalFile)
            {
                return File.ReadAllBytes(this.RawValue);
            }

            if (this.Kind == VersatileAudioKind.Url)
            {
                using (var client = new HttpClient())
                {
                    var response = client.GetAsync(this.RawValue).Result;
                    response.EnsureSuccessStatusCode();
                    return response.Content.ReadAsByteArrayAsync().Result;
                }
            }

            if (this.Kind == VersatileAudioKind.Base64)
            {
                return Convert.FromBase64String(this.RawValue);
            }

            if (this.Kind == VersatileAudioKind.DataUri)
            {
                var base64Data = ExtractBase64FromDataUri(this.RawValue);
                return Convert.FromBase64String(base64Data);
            }

            throw new InvalidOperationException($"Cannot convert {this.Kind} to byte array.");
        }

        /// <summary>
        /// Detects the source kind from an input string.
        /// </summary>
        private static VersatileAudioKind DetectSourceKind(string input)
        {
            // Check for HTTP(S) URL
            if (input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return VersatileAudioKind.Url;
            }

            // Check for data-URI (data:audio/...;base64,...)
            if (input.StartsWith("data:audio/", StringComparison.OrdinalIgnoreCase) &&
                input.Contains(";base64,"))
            {
                return VersatileAudioKind.DataUri;
            }

            // Check for local file path with valid audio extension
            if (IsValidLocalFilePath(input))
            {
                return VersatileAudioKind.LocalFile;
            }

            // Check for base64 (long string, no spaces, no path separators)
            if (IsValidBase64String(input))
            {
                return VersatileAudioKind.Base64;
            }

            // Default to local file if it looks like a path
            return VersatileAudioKind.LocalFile;
        }

        /// <summary>
        /// Checks if the input is a valid local file path with a supported audio extension.
        /// </summary>
        private static bool IsValidLocalFilePath(string input)
        {
            try
            {
                // Check if it contains path separators or looks like a file path
                if (!input.Contains("\\") && !input.Contains("/"))
                {
                    return false;
                }

                // Check for supported audio extension
                var extension = Path.GetExtension(input);
                if (!SupportedAudioExtensions.Contains(extension))
                {
                    return false;
                }

                // Check if file exists
                return File.Exists(input);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if the input is a valid base64 string.
        /// </summary>
        private static bool IsValidBase64String(string input)
        {
            // Base64 strings should not contain spaces or path separators
            if (input.Contains(" ") || input.Contains("\\") || input.Contains("/"))
            {
                return false;
            }

            // Try to decode as base64
            try
            {
                Convert.FromBase64String(input);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the MIME type from a file path.
        /// </summary>
        private static string GetMimeTypeFromPath(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".mp3" => "audio/mpeg",
                ".wav" => "audio/wav",
                ".m4a" => "audio/mp4",
                ".aac" => "audio/aac",
                ".flac" => "audio/flac",
                ".ogg" => "audio/ogg",
                ".wma" => "audio/x-ms-wma",
                ".opus" => "audio/opus",
                _ => "audio/mpeg",
            };
        }

        /// <summary>
        /// Gets the MIME type from an audio source.
        /// </summary>
        private static string GetMimeTypeFromSource(string source, VersatileAudioKind kind)
        {
            return kind switch
            {
                VersatileAudioKind.LocalFile => GetMimeTypeFromPath(source),
                VersatileAudioKind.Url => GetMimeTypeFromPath(source),
                VersatileAudioKind.Base64 => "audio/mpeg",
                VersatileAudioKind.DataUri => ExtractMimeTypeFromDataUri(source),
                _ => "audio/mpeg",
            };
        }

        /// <summary>
        /// Extracts the MIME type from a data-URI.
        /// </summary>
        private static string ExtractMimeTypeFromDataUri(string dataUri)
        {
            var match = Regex.Match(dataUri, @"data:([^;]+);");
            return match.Success ? match.Groups[1].Value : "audio/mpeg";
        }

        /// <summary>
        /// Extracts the base64 data from a data-URI.
        /// </summary>
        private static string ExtractBase64FromDataUri(string dataUri)
        {
            var match = Regex.Match(dataUri, @";base64,(.+)$");
            return match.Success ? match.Groups[1].Value : string.Empty;
        }
    }
}
