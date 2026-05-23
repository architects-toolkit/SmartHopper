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
 * https://github.com/ariesdevil/markdown-clipper
 * Apache License 2.0
 * Copyright (c) ariesdevil
 *
 * Key concepts adapted:
 * - Readability-based main-content extraction with heuristic fallback
 * - Two-stage pipeline: extract clean article HTML, then convert to Markdown
 */

namespace SmartHopper.Core.Grasshopper.Converters.Formats
{
    /// <summary>
    /// Result of a Readability-style main-content extraction pass.
    /// </summary>
    public sealed class ReadabilityResult
    {
        /// <summary>Gets or sets the cleaned article HTML fragment (no document/head wrapper needed).</summary>
        public string CleanHtml { get; set; } = string.Empty;

        /// <summary>Gets or sets the detected article title, if any.</summary>
        public string? Title { get; set; }

        /// <summary>Gets or sets the detected byline/author, if any.</summary>
        public string? Byline { get; set; }

        /// <summary>Gets or sets a short excerpt/summary when available.</summary>
        public string? Excerpt { get; set; }

        /// <summary>Gets or sets a confidence score in the range [0, 1]. Extractors may leave this 0 when unknown.</summary>
        public double Confidence { get; set; }

        /// <summary>Gets or sets a short identifier of the extractor that produced this result (e.g. "smartreader", "heuristic").</summary>
        public string ExtractorName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Extracts the main readable content from an HTML document.
    /// </summary>
    public interface IReadabilityExtractor
    {
        /// <summary>Gets a short identifier for this extractor.</summary>
        string Name { get; }

        /// <summary>
        /// Attempts to extract the main article content from the given HTML.
        /// Returns null when extraction fails or the extractor is not confident.
        /// </summary>
        /// <param name="html">Raw HTML source document.</param>
        /// <param name="baseUrl">Optional base URL used to resolve relative links/images.</param>
        ReadabilityResult? Extract(string html, string? baseUrl = null);
    }
}
