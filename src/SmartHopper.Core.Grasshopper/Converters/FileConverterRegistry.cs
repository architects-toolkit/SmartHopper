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

/*
 * Portions of this code inspired by:
 * https://github.com/deanmalmgren/textract
 * MIT License
 * Copyright (c) Dean Malmgren
 *
 * Key concepts adapted:
 * - FileConverterRegistry dispatcher pattern for file format routing
 * - Extension-based converter lookup
 * - Unified ConvertAsync interface for all formats
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SmartHopper.Core.Grasshopper.Converters.Formats;

namespace SmartHopper.Core.Grasshopper.Converters
{
    /// <summary>
    /// Registry for file converters. Dispatches conversion requests to the appropriate converter based on file extension.
    /// </summary>
    public sealed class FileConverterRegistry
    {
        private readonly Dictionary<string, IFileConverter> convertersByExtension;
        private readonly List<IFileConverter> converters;

        /// <summary>
        /// Creates a new registry instance.
        /// </summary>
        public FileConverterRegistry()
        {
            this.convertersByExtension = new Dictionary<string, IFileConverter>(StringComparer.OrdinalIgnoreCase);
            this.converters = new List<IFileConverter>();
        }

        /// <summary>
        /// Registers a converter.
        /// </summary>
        /// <param name="converter">The converter to register.</param>
        public void Register(IFileConverter converter)
        {
            if (converter == null)
            {
                throw new ArgumentNullException(nameof(converter));
            }

            this.converters.Add(converter);

            foreach (var extension in converter.SupportedExtensions)
            {
                var normalizedExtension = NormalizeExtension(extension);
                this.convertersByExtension[normalizedExtension] = converter;
            }
        }

        /// <summary>
        /// Registers multiple converters.
        /// </summary>
        /// <param name="converters">The converters to register.</param>
        public void RegisterAll(IEnumerable<IFileConverter> converters)
        {
            if (converters == null)
            {
                throw new ArgumentNullException(nameof(converters));
            }

            foreach (var converter in converters)
            {
                this.Register(converter);
            }
        }

        /// <summary>
        /// Gets all supported file extensions.
        /// </summary>
        public IEnumerable<string> SupportedExtensions => this.convertersByExtension.Keys;

        /// <summary>
        /// Checks if a file extension is supported.
        /// </summary>
        /// <param name="extension">File extension (with or without leading dot).</param>
        public bool IsSupported(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
            {
                return false;
            }

            var normalizedExtension = NormalizeExtension(extension);
            return this.convertersByExtension.ContainsKey(normalizedExtension);
        }

        /// <summary>
        /// Converts a file to Markdown.
        /// </summary>
        /// <param name="filePath">Absolute path to the file.</param>
        /// <param name="options">Conversion options. If null, default options are used.</param>
        /// <returns>Conversion result.</returns>
        public async Task<FileConversionResult> ConvertAsync(string filePath, FileConversionOptions? options = null)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                return FileConversionResult.Failure("unknown", $"File not found: {filePath}");
            }

            var extension = Path.GetExtension(filePath);
            if (string.IsNullOrWhiteSpace(extension))
            {
                return FileConversionResult.Failure("unknown", $"File has no extension: {filePath}");
            }

            var normalizedExtension = NormalizeExtension(extension);
            if (!this.convertersByExtension.TryGetValue(normalizedExtension, out var converter))
            {
                // Fallback: when no specialised converter exists, attempt to read the file as
                // raw text. This lets users preview files such as .scn, .odt, or legacy formats
                // without needing a full converter, while still warning that the result may be
                // partial or unreadable for binary files.
                var rawResult = await TryReadRawTextAsync(filePath, normalizedExtension).ConfigureAwait(false);
                if (rawResult != null)
                {
                    return rawResult;
                }

                var supportedList = string.Join(", ", this.SupportedExtensions.OrderBy(e => e));
                return FileConversionResult.Failure(
                    normalizedExtension.TrimStart('.'),
                    $"Unsupported file format: {extension}. Supported formats: {supportedList}");
            }

            var conversionOptions = options ?? new FileConversionOptions();

            try
            {
                var result = await converter.ConvertAsync(filePath, conversionOptions).ConfigureAwait(false);

                // Final Markdown post-processing, applied to all file formats:
                // 1. Normalize ordered-list numbering (e.g. converters emitting repeated "1." markers
                //    for non-CommonMark list styles like lettered/Roman markers) so the raw Markdown
                //    reads correctly even without a renderer.
                // 2. General style cleanup: trailing whitespace, heading spacing, excess blank lines.
                if (result.IsSuccess)
                {
                    result.MarkdownContent = MarkdownListRenumberer.Renumber(result.MarkdownContent);
                    result.MarkdownContent = MarkdownStyleCleanup.Cleanup(result.MarkdownContent);
                }

                // Apply max content length if specified
                if (conversionOptions.MaxContentLength > 0 && result.MarkdownContent.Length > conversionOptions.MaxContentLength)
                {
                    result.MarkdownContent = result.MarkdownContent.Substring(0, conversionOptions.MaxContentLength);
                    result.Warnings.Add($"Content truncated to {conversionOptions.MaxContentLength} characters.");
                }

                return result;
            }
            catch (Exception ex)
            {
                return FileConversionResult.Failure(
                    normalizedExtension.TrimStart('.'),
                    $"Conversion failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Attempts to read an unrecognised file as raw text using the same encoding detection
        /// as <see cref="TxtConverter"/>. Returns <c>null</c> if the file cannot be read as text
        /// or produces only whitespace.
        /// </summary>
        private static async Task<FileConversionResult?> TryReadRawTextAsync(string filePath, string normalizedExtension)
        {
            try
            {
                var bytes = await File.ReadAllBytesAsync(filePath).ConfigureAwait(false);
                var content = TxtConverter.DecodeTextBytes(bytes);
                if (string.IsNullOrWhiteSpace(content))
                {
                    return null;
                }

                content = content.Replace("\r\n", "\n").Replace("\r", "\n");

                var result = FileConversionResult.Success(content, normalizedExtension.TrimStart('.'));
                result.Warnings.Add($"No specific converter for '{normalizedExtension}'; returning raw text. Binary files may produce unreadable output.");
                return result;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Normalizes a file extension to lowercase with leading dot.
        /// </summary>
        private static string NormalizeExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
            {
                return string.Empty;
            }

            extension = extension.Trim();
            if (!extension.StartsWith("."))
            {
                extension = "." + extension;
            }

            return extension.ToLowerInvariant();
        }

        /// <summary>
        /// Creates a default registry with all built-in converters registered.
        /// </summary>
        public static FileConverterRegistry CreateDefault()
        {
            var registry = new FileConverterRegistry();

            // Converters will be registered here as they are implemented
            return registry;
        }
    }
}
