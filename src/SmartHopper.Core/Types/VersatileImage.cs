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
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;

namespace SmartHopper.Core.Types
{
    /// <summary>
    /// Specifies the source kind of an image.
    /// </summary>
    public enum VersatileImageKind
    {
        /// <summary>In-memory Bitmap object.</summary>
        Bitmap,

        /// <summary>Local file path.</summary>
        LocalFile,

        /// <summary>HTTP(S) URL.</summary>
        Url,

        /// <summary>Base64-encoded image data.</summary>
        Base64,

        /// <summary>Data URI with embedded base64 image.</summary>
        DataUri,
    }

    /// <summary>
    /// Adapter that wraps heterogeneous image inputs (Bitmap, file path, URL, base64, data-URI)
    /// and converts them to AIInteractionImage for AI processing.
    /// </summary>
    public sealed class VersatileImage
    {
        private static readonly HashSet<string> SupportedImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tiff", ".tif", ".ico", ".svg",
        };

        /// <summary>
        /// Gets the kind of image source.
        /// </summary>
        public VersatileImageKind Kind { get; private set; }

        /// <summary>
        /// Gets the in-memory Bitmap (when Kind == Bitmap).
        /// </summary>
        public Bitmap Bitmap { get; private set; }

        /// <summary>
        /// Gets the raw value (path, URL, base64, or data-URI string).
        /// </summary>
        public string RawValue { get; private set; }

        /// <summary>
        /// Gets the unique identifier for this image within a document (e.g., "img-1", "img-2").
        /// Only populated when image is extracted from a document.
        /// </summary>
        public string Id { get; private set; }

        /// <summary>
        /// Gets the contextual description of where the image was found
        /// (e.g., "Page 3", "Slide 5", "Document body").
        /// Only populated when image is extracted from a document.
        /// </summary>
        public string Context { get; private set; }

        /// <summary>
        /// Gets the page or slide number where the image was found (1-based).
        /// A value of 0 means the location is unknown or not applicable.
        /// Only populated when image is extracted from a document.
        /// </summary>
        public int PageOrSlide { get; private set; }

        /// <summary>
        /// Gets the source document path or identifier.
        /// Only populated when image is extracted from a document.
        /// </summary>
        public string SourceDocument { get; private set; }

        /// <summary>
        /// Gets the MIME type of the image.
        /// Only populated when image is extracted from a document.
        /// </summary>
        public string MimeType { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="VersatileImage"/> class from a Bitmap.
        /// </summary>
        /// <param name="bitmap">The bitmap to wrap.</param>
        /// <returns>A new VersatileImage.</returns>
        public static VersatileImage FromBitmap(Bitmap bitmap)
        {
            if (bitmap == null)
            {
                throw new ArgumentNullException(nameof(bitmap));
            }

            return new VersatileImage
            {
                Kind = VersatileImageKind.Bitmap,
                Bitmap = bitmap,
                RawValue = null,
                Id = null,
                Context = null,
                PageOrSlide = 0,
                SourceDocument = null,
            };
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="VersatileImage"/> class from a string.
        /// Auto-detects the source kind based on input pattern.
        /// </summary>
        /// <param name="input">The input string (URL, file path, base64, or data-URI).</param>
        /// <returns>A new VersatileImage.</returns>
        public static VersatileImage FromString(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                throw new ArgumentException("Input cannot be null or whitespace.", nameof(input));
            }

            var trimmedInput = input.Trim();
            var kind = DetectSourceKind(trimmedInput);

            return new VersatileImage
            {
                Kind = kind,
                Bitmap = null,
                RawValue = trimmedInput,
                Id = null,
                Context = null,
                PageOrSlide = 0,
                SourceDocument = null,
            };
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="VersatileImage"/> class from extracted document data.
        /// Use this when creating image sources from PDF/DOCX/PPTX file extraction.
        /// </summary>
        /// <param name="base64Data">The base64-encoded image data.</param>
        /// <param name="mimeType">The MIME type of the image.</param>
        /// <param name="id">Unique identifier for this image within the document (e.g., "img-1").</param>
        /// <param name="context">Contextual description of where the image was found (e.g., "Page 3").</param>
        /// <param name="pageOrSlide">Page or slide number (1-based), or 0 if unknown.</param>
        /// <param name="sourceDocument">Source document path or identifier.</param>
        /// <returns>A new VersatileImage with document metadata.</returns>
        public static VersatileImage FromExtractedDocument(
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

            return new VersatileImage
            {
                Kind = VersatileImageKind.Base64,
                Bitmap = null,
                RawValue = base64Data,
                Id = id ?? "img",
                Context = context ?? string.Empty,
                PageOrSlide = pageOrSlide,
                SourceDocument = sourceDocument ?? string.Empty,
                MimeType = mimeType ?? "image/png",
            };
        }

        /// <summary>
        /// Converts this image source to a Bitmap.
        /// For URLs and base64, downloads/decodes lazily.
        /// For local files, loads from disk.
        /// </summary>
        /// <returns>A Bitmap object.</returns>
        public Bitmap ToBitmap()
        {
            if (this.Kind == VersatileImageKind.Bitmap && this.Bitmap != null)
            {
                return this.Bitmap;
            }

            if (this.Kind == VersatileImageKind.LocalFile)
            {
                return new Bitmap(this.RawValue);
            }

            if (this.Kind == VersatileImageKind.Url)
            {
                using (var client = new HttpClient())
                {
                    var response = client.GetAsync(this.RawValue).Result;
                    response.EnsureSuccessStatusCode();
                    using (var stream = response.Content.ReadAsStreamAsync().Result)
                    {
                        return new Bitmap(stream);
                    }
                }
            }

            if (this.Kind == VersatileImageKind.Base64)
            {
                var bytes = Convert.FromBase64String(this.RawValue);
                using (var stream = new MemoryStream(bytes))
                {
                    return new Bitmap(stream);
                }
            }

            if (this.Kind == VersatileImageKind.DataUri)
            {
                var base64Data = ExtractBase64FromDataUri(this.RawValue);
                var bytes = Convert.FromBase64String(base64Data);
                using (var stream = new MemoryStream(bytes))
                {
                    return new Bitmap(stream);
                }
            }

            throw new InvalidOperationException($"Cannot convert {this.Kind} to Bitmap.");
        }

        /// <summary>
        /// Converts this image source to an AIInteractionImage for AI processing.
        /// </summary>
        /// <param name="agent">The agent originating this image (default: User).</param>
        /// <returns>An AIInteractionImage ready for AI processing.</returns>
        public AIInteractionImage ToInteraction(AIAgent agent = AIAgent.User)
        {
            var interaction = new AIInteractionImage { Agent = agent };

            switch (this.Kind)
            {
                case VersatileImageKind.Bitmap:
                    // Convert Bitmap to base64
                    using (var ms = new MemoryStream())
                    {
                        this.Bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                        var base64 = Convert.ToBase64String(ms.ToArray());
                        interaction.CreateVisionInputFromBase64(base64, "image/png");
                    }

                    break;

                case VersatileImageKind.LocalFile:
                    // Load file and convert to base64
                    var fileBytes = File.ReadAllBytes(this.RawValue);
                    var base64File = Convert.ToBase64String(fileBytes);
                    var mimeType = GetMimeTypeFromPath(this.RawValue);
                    interaction.CreateVisionInputFromBase64(base64File, mimeType);
                    break;

                case VersatileImageKind.Url:
                    // Use URL directly
                    interaction.CreateVisionInput(this.RawValue);
                    break;

                case VersatileImageKind.Base64:
                    // Use base64 directly (assume PNG if no hint)
                    interaction.CreateVisionInputFromBase64(this.RawValue, "image/png");
                    break;

                case VersatileImageKind.DataUri:
                    // Extract base64 from data-URI
                    var base64DataUri = ExtractBase64FromDataUri(this.RawValue);
                    var mimeTypeDataUri = ExtractMimeTypeFromDataUri(this.RawValue);
                    interaction.CreateVisionInputFromBase64(base64DataUri, mimeTypeDataUri);
                    break;

                default:
                    throw new InvalidOperationException($"Unknown image source kind: {this.Kind}");
            }

            return interaction;
        }

        /// <summary>
        /// Detects the source kind from an input string.
        /// </summary>
        private static VersatileImageKind DetectSourceKind(string input)
        {
            // Check for HTTP(S) URL
            if (input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return VersatileImageKind.Url;
            }

            // Check for data-URI (data:image/...;base64,...)
            if (input.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase) &&
                input.Contains(";base64,"))
            {
                return VersatileImageKind.DataUri;
            }

            // Check for local file path with a supported image extension
            if (IsValidLocalFilePath(input))
            {
                return VersatileImageKind.LocalFile;
            }

            // Check for base64 (long string, no spaces, no path separators)
            if (IsValidBase64String(input))
            {
                return VersatileImageKind.Base64;
            }

            // The input is not a recognized image source
            ThrowUnsupportedImageSource(input);
            return default;
        }

        /// <summary>
        /// Throws a descriptive exception when the input cannot be interpreted as a supported image source.
        /// </summary>
        private static void ThrowUnsupportedImageSource(string input)
        {
            try
            {
                var extension = Path.GetExtension(input);
                bool hasExtension = !string.IsNullOrEmpty(extension);
                bool looksLikePath = input.Contains("\\") || input.Contains("/") || hasExtension;

                if (hasExtension && !SupportedImageExtensions.Contains(extension))
                {
                    throw new NotSupportedException(
                        $"Unsupported image file extension '{extension}'. Supported extensions are: {string.Join(", ", SupportedImageExtensions.OrderBy(e => e))}.");
                }

                if (looksLikePath)
                {
                    if (!File.Exists(input))
                    {
                        throw new FileNotFoundException($"Image file not found: {input}", input);
                    }

                    throw new NotSupportedException(
                        $"The file does not appear to be a supported image. Supported extensions are: {string.Join(", ", SupportedImageExtensions.OrderBy(e => e))}.");
                }
            }
            catch (NotSupportedException)
            {
                throw;
            }
            catch (FileNotFoundException)
            {
                throw;
            }
            catch
            {
                // Fall through to the generic message if path inspection fails.
            }

            throw new ArgumentException(
                "Input is not a recognized image source. Expected a URL, a data URI, a base64 string, or a local file path with a supported image extension.",
                nameof(input));
        }

        /// <summary>
        /// Checks if the input is a valid local file path with a supported image extension.
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

                // Check for supported image extension
                var extension = Path.GetExtension(input);
                if (!SupportedImageExtensions.Contains(extension))
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
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                ".tiff" or ".tif" => "image/tiff",
                ".ico" => "image/x-icon",
                ".svg" => "image/svg+xml",
                _ => "image/png",
            };
        }

        /// <summary>
        /// Extracts the MIME type from a data-URI.
        /// </summary>
        private static string ExtractMimeTypeFromDataUri(string dataUri)
        {
            var match = Regex.Match(dataUri, @"data:([^;]+);");
            return match.Success ? match.Groups[1].Value : "image/png";
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
