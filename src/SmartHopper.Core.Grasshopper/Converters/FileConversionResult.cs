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
 * - FileConversionResult container pattern for file conversion results
 * - Success/failure factory method pattern
 */

using System.Collections.Generic;

namespace SmartHopper.Core.Grasshopper.Converters
{
    /// <summary>
    /// Result of a file-to-markdown conversion.
    /// </summary>
    public sealed class FileConversionResult
    {
        /// <summary>
        /// Gets or sets the Markdown content extracted from the file.
        /// </summary>
        public string MarkdownContent { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the detected original format (e.g., "pdf", "docx", "html").
        /// </summary>
        public string DetectedFormat { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets metadata extracted from the file (e.g., title, author, creation date).
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Gets or sets warnings generated during conversion (e.g., "Page 3 appears to be scanned").
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets images extracted from the document.
        /// Only populated when <see cref="FileConversionOptions.ExtractImages"/> is enabled.
        /// </summary>
        public List<ExtractedImage> Images { get; set; } = new List<ExtractedImage>();

        /// <summary>
        /// Gets whether the conversion was successful.
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Creates a new instance with default values.
        /// </summary>
        public FileConversionResult()
        {
        }

        /// <summary>
        /// Creates a successful conversion result.
        /// </summary>
        public static FileConversionResult Success(string markdownContent, string detectedFormat)
        {
            return new FileConversionResult
            {
                MarkdownContent = markdownContent ?? string.Empty,
                DetectedFormat = detectedFormat,
                IsSuccess = true
            };
        }

        /// <summary>
        /// Creates a failed conversion result with a warning.
        /// </summary>
        public static FileConversionResult Failure(string detectedFormat, string warningMessage)
        {
            return new FileConversionResult
            {
                MarkdownContent = string.Empty,
                DetectedFormat = detectedFormat,
                Warnings = new List<string> { warningMessage },
                IsSuccess = false
            };
        }
    }
}
