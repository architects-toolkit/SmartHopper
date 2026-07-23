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
    /// Controls how main-content extraction is performed for HTML sources.
    /// </summary>
    public enum ReadabilityMode
    {
        /// <summary>
        /// Try SmartReader (Mozilla Readability port) first; fall back to the
        /// built-in heuristic extractor when SmartReader is not confident.
        /// </summary>
        Auto = 0,

        /// <summary>Force SmartReader, regardless of confidence.</summary>
        SmartReader = 1,

        /// <summary>Force the built-in heuristic extractor (magic-html inspired).</summary>
        Heuristic = 2,

        /// <summary>Skip content extraction entirely and convert the full document body.</summary>
        Off = 3,
    }

    /// <summary>
    /// Helper methods for <see cref="ReadabilityMode"/>.
    /// </summary>
    public static class ReadabilityModeExtensions
    {
        /// <summary>
        /// Parses a string value into a <see cref="ReadabilityMode"/>, defaulting to <see cref="ReadabilityMode.Auto"/>.
        /// </summary>
        /// <param name="value">The input value to parse.</param>
        /// <returns>The parsed readability mode, or <see cref="ReadabilityMode.Auto"/> when the value is empty or unknown.</returns>
        public static ReadabilityMode FromString(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return ReadabilityMode.Auto;
            }

            return value.Trim().ToLowerInvariant() switch
            {
                "smartreader" => ReadabilityMode.SmartReader,
                "heuristic" => ReadabilityMode.Heuristic,
                "off" => ReadabilityMode.Off,
                _ => ReadabilityMode.Auto,
            };
        }
    }

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
        /// Gets or sets whether to preserve inline text formatting.
        /// When enabled, colored text is wrapped in an inline HTML span, highlighted text is wrapped in
        /// an inline HTML mark, and bold/italic/underlined/strikethrough text is emitted using Markdown or
        /// inline HTML. DOCX and ODF text documents preserve colors, highlights, bold, italic, underline,
        /// and strikethrough; XLSX, ODS, and PPTX preserve bold and italic.
        /// Default: true.
        /// </summary>
        public bool PreserveFormatting { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to preserve comments and their authors in DOCX files.
        /// When enabled, comments are appended as blockquotes after the paragraph that contains them.
        /// Applies to DOCX format.
        /// Default: true.
        /// </summary>
        public bool PreserveComments { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to preserve footnotes in DOCX files.
        /// When enabled, footnote references are expanded and footnotes are appended at the end of the document.
        /// Applies to DOCX format.
        /// Default: true.
        /// </summary>
        public bool PreserveFootnotes { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to preserve endnotes in DOCX files.
        /// When enabled, endnote references are expanded and endnotes are appended at the end of the document.
        /// Applies to DOCX format.
        /// Default: true.
        /// </summary>
        public bool PreserveEndnotes { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to preserve hyperlinks in DOCX and PPTX files.
        /// When enabled, link text is emitted as Markdown link syntax.
        /// Applies to DOCX and PPTX formats.
        /// Default: true.
        /// </summary>
        public bool PreserveHyperlinks { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to preserve Office Math (OMML) equations in DOCX and PPTX files.
        /// When enabled, equations are converted to LaTeX notation.
        /// Applies to DOCX and PPTX formats.
        /// Default: true.
        /// </summary>
        public bool PreserveMath { get; set; } = true;

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
        /// Gets or sets the HTML main-content extraction strategy. Default: <see cref="ReadabilityMode.Auto"/>.
        /// </summary>
        public ReadabilityMode HtmlReadabilityMode { get; set; } = ReadabilityMode.Auto;

        /// <summary>
        /// Gets or sets whether hyperlinks are preserved in the Markdown output for HTML sources.
        /// When false, link text is kept but URLs are dropped. Default: true.
        /// </summary>
        public bool IncludeLinks { get; set; } = true;

        /// <summary>
        /// Gets or sets whether inline <c>&lt;img&gt;</c> references in HTML sources are preserved in the Markdown output.
        /// When false, images are removed entirely. Note: this is independent of <see cref="ExtractImages"/>,
        /// which controls document-embedded image extraction for PDF/DOCX/PPTX. Default: true.
        /// </summary>
        public bool IncludeImages { get; set; } = true;

        /// <summary>
        /// Gets or sets an optional base URL used to resolve relative links and image sources in HTML into absolute URLs.
        /// Typically set by <see cref="Formats.UrlConverter"/> to the fetched page URL. Default: null.
        /// </summary>
        public string? BaseUrl { get; set; }

        /// <summary>
        /// Default value for <see cref="MaxDownloadBytes"/>: 10 MB.
        /// </summary>
        public const long DefaultMaxDownloadBytes = 10_000_000;

        /// <summary>
        /// Default value for <see cref="MinContentLength"/>: 40 characters.
        /// </summary>
        public const int DefaultMinContentLength = 40;

        /// <summary>
        /// Gets or sets the maximum number of raw bytes that will be downloaded from a remote resource
        /// (e.g. a web page) before conversion is aborted as too large. Applies to <see cref="Formats.UrlConverter"/>.
        /// This bounds network/memory usage independently of <see cref="MaxContentLength"/>, which only
        /// truncates the already-converted Markdown output. Default: <see cref="DefaultMaxDownloadBytes"/> (10 MB).
        /// </summary>
        public long MaxDownloadBytes { get; set; } = DefaultMaxDownloadBytes;

        /// <summary>
        /// Gets or sets the minimum number of trimmed characters a conversion must produce to be considered
        /// a genuine success. Conversions that produce less content than this (e.g. an empty or near-empty
        /// page) are reported as a failure with <see cref="FileConversionFailureReason.EmptyContent"/> instead
        /// of a false success with empty/thin Markdown. Default: <see cref="DefaultMinContentLength"/> (40 characters).
        /// </summary>
        public int MinContentLength { get; set; } = DefaultMinContentLength;

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
                PreserveFormatting = this.PreserveFormatting,
                PreserveComments = this.PreserveComments,
                PreserveFootnotes = this.PreserveFootnotes,
                PreserveEndnotes = this.PreserveEndnotes,
                PreserveHyperlinks = this.PreserveHyperlinks,
                PreserveMath = this.PreserveMath,
                MaxContentLength = this.MaxContentLength,
                ExtractImages = this.ExtractImages,
                HtmlReadabilityMode = this.HtmlReadabilityMode,
                IncludeLinks = this.IncludeLinks,
                IncludeImages = this.IncludeImages,
                BaseUrl = this.BaseUrl,
                MaxDownloadBytes = this.MaxDownloadBytes,
                MinContentLength = this.MinContentLength,
            };
        }
    }
}
