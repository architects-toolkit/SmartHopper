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
 * - FileConversionOptions for configurable conversion behavior
 * - Options-based configuration pattern
 */

namespace SmartHopper.Core.Grasshopper.Converters
{
    /// <summary>
    /// Options for file-to-markdown conversion.
    /// </summary>
    public sealed class FileConversionOptions
    {
        /// <summary>
        /// Gets or sets whether to preserve table structure as Markdown tables.
        /// If false, tables may be rendered as plain text.
        /// Default: true.
        /// </summary>
        public bool PreserveTableStructure { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to attempt to remove headers and footers from documents.
        /// Applies primarily to PDF and DOCX formats.
        /// Default: true.
        /// </summary>
        public bool RemoveHeadersFooters { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to detect and convert headings to Markdown heading syntax.
        /// Default: true.
        /// </summary>
        public bool DetectHeadings { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to extract embedded images from the document.
        /// When enabled, images are extracted as base64 data and stored in the conversion result.
        /// Applies to PDF, DOCX, and PPTX formats.
        /// Default: false.
        /// </summary>
        public bool ExtractImages { get; set; } = false;

        /// <summary>
        /// Gets or sets the maximum content length in characters.
        /// If the converted content exceeds this length, it will be truncated.
        /// A value of 0 or less means no limit.
        /// Default: 0 (no limit).
        /// </summary>
        public int MaxContentLength { get; set; } = 0;

        /// <summary>
        /// Creates a new instance with default options.
        /// </summary>
        public FileConversionOptions()
        {
        }

        /// <summary>
        /// Creates a copy of this options instance.
        /// </summary>
        public FileConversionOptions Clone()
        {
            return new FileConversionOptions
            {
                PreserveTableStructure = this.PreserveTableStructure,
                RemoveHeadersFooters = this.RemoveHeadersFooters,
                DetectHeadings = this.DetectHeadings,
                MaxContentLength = this.MaxContentLength,
                ExtractImages = this.ExtractImages,
            };
        }
    }
}
