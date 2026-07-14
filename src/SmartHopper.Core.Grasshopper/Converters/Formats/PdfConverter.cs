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
 * This code uses PdfPig for PDF text extraction and layout analysis:
 * https://github.com/UglyToad/PdfPig
 * Apache License 2.0
 *
 * Layout analysis improvements inspired by:
 * - MinerU (AGPL-3.0, https://github.com/opendatalab/MinerU):
 *   xy-cut reading order, header/footer removal, heading detection by font size
 * - Camelot (MIT, https://github.com/camelot-dev/camelot):
 *   Stream-mode table detection via whitespace column-alignment clustering
 * - Tabula (MIT, https://github.com/tabulapdf/tabula):
 *   Column centroid clustering for table cell extraction
 * - PyMuPDF / MuPDF (AGPL-3.0, https://pymupdf.readthedocs.io/):
 *   Lattice-mode table detection via PDF vector graphics (page.Paths) as primary signal
 * All algorithms are independently reimplemented in C#; no source code was copied.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SmartHopper.Core.Types;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.ReadingOrderDetector;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace SmartHopper.Core.Grasshopper.Converters.Formats
{
    /// <summary>
    /// Converter for PDF files (.pdf).
    /// Uses PdfPig DocumentLayoutAnalysis for:
    /// - NearestNeighbourWordExtractor for accurate word grouping in academic PDFs
    /// - RecursiveXYCut page segmentation for multi-column layout detection
    /// - UnsupervisedReadingOrderDetector (RowWise, no rendering order) for correct top-to-bottom, left-to-right order
    /// - Header/footer removal by cross-page text frequency analysis
    /// - Heading detection by font size ratio
    /// - Stream-mode table detection (whitespace column-alignment clustering)
    /// - Markdown table rendering with pipe escaping
    /// - Scanned page detection
    /// </summary>
    public sealed class PdfConverter : IFileConverter
    {
        /// <summary>Minimum number of data rows required to classify a block as a table.</summary>
        private const int MinTableRows = 3;

        /// <summary>Minimum page count for a repeated text to be removed as header/footer.</summary>
        private const int MinHeaderFooterRepeat = 3;

        /// <summary>
        /// A column separator gap must be at least this multiple of the block's
        /// median inter-word spacing to be considered a table column boundary.
        /// Body text has roughly uniform spacing, so no gap exceeds this threshold.
        /// </summary>
        private const double ColumnGapMultiplier = 2.5;

        /// <summary>Absolute minimum width (PDF units ≈ 1/72 in) for a column separator gap, regardless of median spacing.</summary>
        private const double MinAbsoluteColumnGap = 10.0;

        /// <summary>Cluster radius (PDF units) when grouping separator midpoints across lines.</summary>
        private const double ColumnSeparatorTolerance = 20.0;

        /// <summary>Maximum fraction of cells allowed to be empty before the block is rejected as a table.</summary>
        private const double MaxEmptyCellFraction = 0.5;

        /// <summary>
        /// When a TABLE caption immediately precedes a block, use this lower gap multiplier
        /// for forced table detection (table presence is already confirmed by the caption).
        /// </summary>
        private const double ColumnGapMultiplierForced = 1.5;

        /// <summary>
        /// Maximum word count for a block to be tracked as a candidate running header/footer
        /// anywhere on the page (not only in the top/bottom margin zones).
        /// </summary>
        private const int MaxShortRepeatBlockWords = 5;

        /// <summary>Matches section-number artifacts some PDF exporters emit on their own line (e.g. "# 7").</summary>
        private static readonly Regex SectionArtifactPattern =
            new Regex(@"^#\s*\d+$", RegexOptions.Compiled);

        /// <summary>
        /// Matches TABLE captions: "TABLE 1", "Table A", "TABLE XI", etc.
        /// Supports digits, single letters (A-Z or a-z), and roman numerals (I-XII).
        /// </summary>
        private static readonly Regex TableCaptionPattern =
            new Regex(@"^(?:TABLE|Table)\s+(?:\d+|[A-Z]|[a-z]|[IVXivx]+)", RegexOptions.Compiled);

        /// <summary>
        /// Matches FIGURE captions: "FIGURE 1", "Figure A", "Fig. XI", etc.
        /// Supports digits, single letters (A-Z or a-z), and roman numerals (I-XII).
        /// </summary>
        private static readonly Regex FigureCaptionPattern =
            new Regex(@"^(?:FIGURE|Figure|Fig\.)\s+(?:\d+|[A-Z]|[a-z]|[IVXivx]+)", RegexOptions.Compiled);

        /// <summary>Matches blocks whose entire text is a 1–4 digit number (used to detect large-font page-number glyphs).</summary>
        private static readonly Regex NumericOnlyPattern = new Regex(@"^\d{1,4}$", RegexOptions.Compiled);

        /// <summary>Matches bullet list items: "• Item", "- Item", "→ Item", etc.</summary>
        private static readonly Regex BulletListPattern =
            new Regex(@"^(?:[•‣◦○▪▫►→▸▹›»]|\s*[\-–—])\s+", RegexOptions.Compiled);

        /// <summary>Matches numbered list items: "1. Item", "a) Item", "i. Item", etc.</summary>
        private static readonly Regex NumberedListPattern =
            new Regex(@"^(?:(\d+)[.)]\s+|[a-zA-Z][.)]\s+|[ivxIVX]+[.)]\s+)", RegexOptions.Compiled);

        /// <inheritdoc/>
        public IEnumerable<string> SupportedExtensions => new[] { ".pdf" };

        /// <inheritdoc/>
        public async Task<FileConversionResult> ConvertAsync(string filePath, FileConversionOptions options)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var document = PdfDocument.Open(filePath);
                    var result = FileConversionResult.Success(string.Empty, "pdf");

                    ExtractMetadata(document, result);

                    var allPageData = CollectPageData(document);

                    var headersFooters = options.RemoveHeadersFooters
                        ? DetectHeadersFooters(allPageData)
                        : new HashSet<string>();

                    var markdown = new StringBuilder();
                    int pageNumber = 1;
                    int imageIndex = 0;

                    foreach (var (page, blocks) in allPageData)
                    {
                        if (blocks.Count == 0 || blocks.Sum(b => GetBlockText(b).Length) < 5)
                        {
                            result.Warnings.Add($"⚠️ Page {pageNumber} appears to be scanned; text may be missing.");
                        }

                        // Extract images from the page when enabled.
                        int pageImageStart = result.Images.Count;
                        if (options.ExtractImages)
                        {
                            imageIndex = ExtractPageImages(page, pageNumber, imageIndex, result);
                        }

                        var pageImages = options.ExtractImages
                            ? GetPageImagePositions(page, pageImageStart, result)
                            : new List<ImagePosition>();

                        if (blocks.Count == 0 || blocks.Sum(b => GetBlockText(b).Length) < 5)
                        {
                            // No text on this page — emit image placeholders inline for any images found
                            foreach (var img in pageImages)
                            {
                                markdown.AppendLine($"[image {img.ImageNumber}]");
                                markdown.AppendLine();
                            }

                            pageNumber++;
                            continue;
                        }

                        var ordered = new UnsupervisedReadingOrderDetector(
                            spatialReasoningRule: UnsupervisedReadingOrderDetector.SpatialReasoningRules.RowWise,
                            useRenderingOrder: false).Get(blocks);
                        var content = ordered
                            .Where(b => !headersFooters.Contains(GetBlockText(b)))
                            .ToList();

                        var hyperlinks = options.PreserveHyperlinks
                            ? page.GetHyperlinks()
                            : Array.Empty<Hyperlink>();

                        double medianFontSize = GetMedianFontSize(content);
                        double headingThreshold = medianFontSize * 1.3;

                        bool nextBlockForceTable = false;
                        var listLeftMarginLevels = new List<double>();
                        int imagePtr = 0;

                        for (int bi = 0; bi < content.Count; bi++)
                        {
                            var block = content[bi];

                            // ---- Inline image placement ----
                            // Emit images whose vertical top is above the current text block.
                            double blockTopY = block.BoundingBox.Top;
                            while (imagePtr < pageImages.Count && pageImages[imagePtr].TopY >= blockTopY)
                            {
                                markdown.AppendLine($"[image {pageImages[imagePtr].ImageNumber}]");
                                markdown.AppendLine();
                                imagePtr++;
                            }

                            // Use unstyled text for pattern matching and caption detection
                            string text = GetBlockText(block);

                            if (string.IsNullOrWhiteSpace(text))
                            {
                                nextBlockForceTable = false;
                                continue;
                            }

                            // ---- Boilerplate filters ----
                            // PDF section-number artifacts (e.g. "# 7")
                            if (SectionArtifactPattern.IsMatch(text)) continue;

                            // Continuation markers
                            if (text.IndexOf("continued on next page", StringComparison.OrdinalIgnoreCase) >= 0) continue;

                            // Large-font standalone page numbers (e.g. "8", "9") at page margins:
                            // suppressed only when the block's font would trigger heading detection
                            // AND the block sits in the top or bottom 12 % of the page.
                            if (NumericOnlyPattern.IsMatch(text)
                                && GetBlockFontSize(block) > headingThreshold
                                && (block.BoundingBox.Bottom > page.Height * 0.88
                                    || block.BoundingBox.Top < page.Height * 0.12))
                            {
                                continue;
                            }

                            bool isTableCaption = TableCaptionPattern.IsMatch(text);
                            int wordCount = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;
                            bool isFigureCaption = !isTableCaption
                                && FigureCaptionPattern.IsMatch(text)
                                && wordCount <= 25;

                            // Read and immediately reset the force flag so it applies only once
                            bool forceTable = nextBlockForceTable;
                            nextBlockForceTable = isTableCaption;

                            // ---- Table rendering ----
                            if (options.PreserveTableStructure && !isTableCaption && !isFigureCaption)
                            {
                                bool isLattice = HasLatticeLines(page, block);
                                bool forcedRender = forceTable || isLattice;

                                if (forcedRender || IsTableBlock(block))
                                {
                                    string table = RenderMarkdownTable(block, forcedRender);
                                    if (!string.IsNullOrWhiteSpace(table))
                                    {
                                        markdown.AppendLine(table);
                                        listLeftMarginLevels.Clear();
                                        continue;
                                    }

                                    // Code-block fallback: when a TABLE caption explicitly forced
                                    // table rendering but the structure is too irregular for pipe
                                    // syntax, preserve content as preformatted text to avoid data loss.
                                    if (forceTable)
                                    {
                                        markdown.AppendLine("```text");
                                        markdown.AppendLine(GetBlockText(block));
                                        markdown.AppendLine("```");
                                        listLeftMarginLevels.Clear();
                                        continue;
                                    }
                                }
                            }

                            // ---- Caption and body text rendering ----
                            double fontSize = GetBlockFontSize(block);

                            if (isTableCaption || isFigureCaption)
                            {
                                listLeftMarginLevels.Clear();

                                // Captions already bold via markdown wrapper; use styled text for inner content
                                string styledCaption = GetBlockTextWithStylingAndLinks(block, hyperlinks).Replace("|", "\\|");

                                // If the whole caption text is already wrapped in bold markers (e.g. the caption
                                // font itself is bold), strip the inner markers to avoid producing "****text****".
                                if (styledCaption.Length >= 4 && styledCaption.StartsWith("**", StringComparison.Ordinal) && styledCaption.EndsWith("**", StringComparison.Ordinal))
                                {
                                    styledCaption = styledCaption.Substring(2, styledCaption.Length - 4);
                                }

                                markdown.Append("**").Append(styledCaption).AppendLine("**");
                            }
                            else if (options.DetectHeadings && fontSize > headingThreshold)
                            {
                                listLeftMarginLevels.Clear();

                                // Headings use plain text (markdown heading syntax doesn't support inline styling)
                                int level = GetHeadingLevel(fontSize, medianFontSize);
                                markdown.Append(new string('#', level)).Append(' ').AppendLine(text);
                            }
                            else if (TryDetectListItem(text, out string listMarker, out string listContent))
                            {
                                int indentLevel = GetListIndentLevel(block.BoundingBox.Left, listLeftMarginLevels);
                                markdown.Append(new string(' ', indentLevel * 2))
                                    .Append(listMarker).Append(' ').AppendLine(listContent);
                            }
                            else
                            {
                                listLeftMarginLevels.Clear();

                                // Body paragraphs: apply inline bold/italic styling and hyperlink syntax
                                string styledText = GetBlockTextWithStylingAndLinks(block, hyperlinks);
                                markdown.AppendLine(styledText);
                            }

                            markdown.AppendLine();
                        }

                        // Emit any remaining images for this page
                        while (imagePtr < pageImages.Count)
                        {
                            markdown.AppendLine($"[image {pageImages[imagePtr].ImageNumber}]");
                            markdown.AppendLine();
                            imagePtr++;
                        }

                        pageNumber++;
                    }

                    result.MarkdownContent = markdown.ToString().Trim();
                    return result;
                }
                catch (Exception ex)
                {
                    return FileConversionResult.Failure("pdf", $"Failed to convert PDF: {ex.Message}");
                }
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Extracts images from a PDF page using PdfPig's GetImages() API.
        /// Tries TryGetPng first for decoded bitmap data; falls back to RawBytes for embedded JPEGs.
        /// </summary>
        /// <param name="page">The PDF page to extract images from.</param>
        /// <param name="pageNumber">Current page number (1-based).</param>
        /// <param name="imageIndex">Running image counter across all pages.</param>
        /// <param name="result">The conversion result to add extracted images to.</param>
        /// <returns>Updated image index after processing this page.</returns>
        private static int ExtractPageImages(Page page, int pageNumber, int imageIndex, FileConversionResult result)
        {
            try
            {
                foreach (var image in page.GetImages())
                {
                    imageIndex++;
                    string base64Data = null;
                    string mimeType = "image/png";

                    if (image.TryGetPng(out var pngBytes))
                    {
                        base64Data = System.Convert.ToBase64String(pngBytes);
                        mimeType = "image/png";
                    }
                    else
                    {
                        // RawBytes is often a valid JPEG for embedded images
                        var rawBytes = image.RawBytes.ToArray();
                        if (rawBytes.Length > 0)
                        {
                            // Detect JPEG by magic bytes (FF D8 FF)
                            if (rawBytes.Length >= 3
                                && rawBytes[0] == 0xFF
                                && rawBytes[1] == 0xD8
                                && rawBytes[2] == 0xFF)
                            {
                                base64Data = System.Convert.ToBase64String(rawBytes);
                                mimeType = "image/jpeg";
                            }
                            else
                            {
                                // Unknown format; store as generic octet-stream
                                base64Data = System.Convert.ToBase64String(rawBytes);
                                mimeType = "application/octet-stream";
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(base64Data))
                    {
                        var extracted = VersatileImage.FromExtractedDocument(
                            base64Data: base64Data,
                            mimeType: mimeType,
                            id: $"img-{imageIndex}",
                            context: $"Page {pageNumber}",
                            pageOrSlide: pageNumber,
                            sourceDocument: null);
                        result.Images.Add(extracted);
                    }
                    else
                    {
                        result.Warnings.Add($"⚠️ Page {pageNumber}, image {imageIndex}: could not extract image data.");
                    }
                }
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"⚠️ Page {pageNumber}: image extraction failed: {ex.Message}");
            }

            return imageIndex;
        }

        /// <summary>
        /// Holds the vertical position of an extracted image for inline placement.
        /// </summary>
        private readonly struct ImagePosition
        {
            public readonly int ImageNumber;
            public readonly double TopY;

            public ImagePosition(int imageNumber, double topY)
            {
                ImageNumber = imageNumber;
                TopY = topY;
            }
        }

        /// <summary>
        /// Returns the vertical positions of images extracted from a page so they can be
        /// interleaved with text blocks in reading order.
        /// </summary>
        private static List<ImagePosition> GetPageImagePositions(Page page, int pageImageStart, FileConversionResult result)
        {
            var positions = new List<ImagePosition>();
            var pageImages = page.GetImages().ToList();
            if (pageImages.Count == 0)
            {
                return positions;
            }

            int idx = pageImageStart;
            foreach (var image in pageImages)
            {
                idx++;
                if (idx <= result.Images.Count)
                {
                    positions.Add(new ImagePosition(idx, image.Bounds.Top));
                }
            }

            // Highest Y first = top of page first (PDF coordinates: Y increases upward)
            positions.Sort((a, b) => b.TopY.CompareTo(a.TopY));
            return positions;
        }

        private static void ExtractMetadata(PdfDocument document, FileConversionResult result)
        {
            var info = document.Information;
            if (!string.IsNullOrWhiteSpace(info.Title)) result.Metadata["title"] = info.Title;
            if (!string.IsNullOrWhiteSpace(info.Author)) result.Metadata["author"] = info.Author;
            if (!string.IsNullOrWhiteSpace(info.CreationDate)) result.Metadata["created"] = info.CreationDate;
        }

        private static List<(Page Page, List<TextBlock> Blocks)> CollectPageData(PdfDocument document)
        {
            var pageData = new List<(Page, List<TextBlock>)>();

            foreach (var page in document.GetPages())
            {
                var letters = page.Letters;
                if (letters.Count == 0)
                {
                    pageData.Add((page, new List<TextBlock>()));
                    continue;
                }

                // NearestNeighbourWordExtractor handles kerning and ligatures better than default
                var words = NearestNeighbourWordExtractor.Instance.GetWords(letters).ToList();
                if (words.Count == 0)
                {
                    pageData.Add((page, new List<TextBlock>()));
                    continue;
                }

                // RecursiveXYCut detects multi-column layouts; MinimumWidth = page.Width/3
                // prevents narrow marginal annotations from being treated as columns
                var segmenter = new RecursiveXYCut(new RecursiveXYCut.RecursiveXYCutOptions
                {
                    MinimumWidth = page.Width / 3.0,
                });

                var blocks = segmenter.GetBlocks(words).ToList();
                pageData.Add((page, blocks));
            }

            return pageData;
        }

        private static HashSet<string> DetectHeadersFooters(
            List<(Page Page, List<TextBlock> Blocks)> allPageData)
        {
            if (allPageData.Count < MinHeaderFooterRepeat)
            {
                return new HashSet<string>();
            }

            var headerCounts = new Dictionary<string, int>();
            var footerCounts = new Dictionary<string, int>();

            // Running headers in multi-column academic PDFs often appear at the top of
            // each column rather than at the absolute page top, so they fall outside the
            // position-based zone.  Track all short blocks (<= MaxShortRepeatBlockWords)
            // regardless of position and filter by repeat count separately.
            var shortTextCounts = new Dictionary<string, int>();

            foreach (var (page, blocks) in allPageData)
            {
                // Widened from 8 %/92 % to 12 %/88 % to catch journal running headers
                // that sit just inside the content area.
                double headerLine = page.Height * 0.88;
                double footerLine = page.Height * 0.12;

                foreach (var block in blocks)
                {
                    string text = GetBlockText(block);
                    if (string.IsNullOrWhiteSpace(text)) continue;

                    var bb = block.BoundingBox;
                    if (bb.Bottom > headerLine)
                    {
                        headerCounts.TryGetValue(text, out int c);
                        headerCounts[text] = c + 1;
                    }
                    else if (bb.Top < footerLine)
                    {
                        footerCounts.TryGetValue(text, out int c);
                        footerCounts[text] = c + 1;
                    }

                    // Track short blocks from anywhere on the page
                    int words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;
                    if (words <= MaxShortRepeatBlockWords)
                    {
                        shortTextCounts.TryGetValue(text, out int sc);
                        shortTextCounts[text] = sc + 1;
                    }
                }
            }

            var result = new HashSet<string>();
            foreach (var kvp in headerCounts.Where(k => k.Value >= MinHeaderFooterRepeat))
            {
                result.Add(kvp.Key);
            }

            foreach (var kvp in footerCounts.Where(k => k.Value >= MinHeaderFooterRepeat))
            {
                result.Add(kvp.Key);
            }

            foreach (var kvp in shortTextCounts.Where(k => k.Value >= MinHeaderFooterRepeat))
            {
                result.Add(kvp.Key);
            }

            return result;
        }

        private static string GetBlockText(TextBlock block)
        {
            return string.Join(" ", block.TextLines
                .Select(l => string.Join(" ", l.Words.Select(w => w.Text)))).Trim();
        }

        /// <summary>
        /// Extracts block text with inline Markdown styling (bold, italic) based on font names.
        /// PDFs encode styling via font family names (e.g., "Arial-Bold", "Times-Italic").
        /// Consecutive words sharing the same bold/italic state are merged into a single style run
        /// so output reads "**word1 word2 word3**" rather than "**word1** **word2** **word3**".
        /// Underline is not supported because PDFs render underlines as separate vector graphics.
        /// </summary>
        private static string GetBlockTextWithStyling(TextBlock block)
        {
            return string.Join(" ", block.TextLines.Select(GetStyledLineText)).Trim();
        }

        /// <summary>
        /// Finds the hyperlink (if any) whose bounding box intersects with the given word.
        /// </summary>
        private static Hyperlink? FindLinkForWord(Word word, IReadOnlyList<Hyperlink> hyperlinks)
        {
            var wb = word.BoundingBox;
            foreach (var link in hyperlinks)
            {
                var lb = link.Bounds;
                if (wb.Left < lb.Right && wb.Right > lb.Left
                    && wb.Bottom < lb.Top && wb.Top > lb.Bottom)
                {
                    return link;
                }
            }

            return null;
        }

        /// <summary>
        /// Extracts block text with inline Markdown styling (bold, italic) and wraps
        /// words that fall inside hyperlink annotation regions in <c>[text](url)</c> syntax.
        /// </summary>
        private static string GetBlockTextWithStylingAndLinks(TextBlock block, IReadOnlyList<Hyperlink> hyperlinks)
        {
            var lines = block.TextLines.ToList();
            if (lines.Count == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            for (int li = 0; li < lines.Count; li++)
            {
                var line = lines[li];
                if (li > 0)
                {
                    sb.Append(' ');
                }

                var words = line.Words.ToList();
                int i = 0;
                while (i < words.Count)
                {
                    var word = words[i];
                    var link = FindLinkForWord(word, hyperlinks);

                    if (link != null)
                    {
                        int j = i + 1;
                        while (j < words.Count && FindLinkForWord(words[j], hyperlinks) == link)
                        {
                            j++;
                        }

                        if (sb.Length > 0 && sb[^1] != ' ')
                        {
                            sb.Append(' ');
                        }

                        string linkText = string.Join(" ", words.GetRange(i, j - i).Select(w => EscapeMarkdownChars(w.Text)));
                        string linkUrl = string.IsNullOrWhiteSpace(link.Uri) ? string.Empty : EscapeMarkdownUrl(link.Uri);
                        if (string.IsNullOrEmpty(linkUrl))
                        {
                            sb.Append(linkText);
                        }
                        else
                        {
                            sb.Append($"[{linkText}]({linkUrl})");
                        }

                        i = j;
                    }
                    else
                    {
                        var (isBold, isItalic) = GetWordStyle(word);

                        // Extend run while the style is unchanged and next word is not linked
                        int j = i + 1;
                        while (j < words.Count)
                        {
                            var (nb, ni) = GetWordStyle(words[j]);
                            if (nb != isBold || ni != isItalic || FindLinkForWord(words[j], hyperlinks) != null)
                            {
                                break;
                            }

                            j++;
                        }

                        string runText = string.Join(" ", words.GetRange(i, j - i).Select(w => EscapeMarkdownChars(w.Text)));
                        if (sb.Length > 0 && sb[^1] != ' ')
                        {
                            sb.Append(' ');
                        }

                        if (isBold && isItalic)
                        {
                            sb.Append("***").Append(runText).Append("***");
                        }
                        else if (isBold)
                        {
                            sb.Append("**").Append(runText).Append("**");
                        }
                        else if (isItalic)
                        {
                            sb.Append('*').Append(runText).Append('*');
                        }
                        else
                        {
                            sb.Append(runText);
                        }

                        i = j;
                    }
                }
            }

            return sb.ToString().Trim();
        }

        /// <summary>
        /// Returns a single line of Markdown with style runs: consecutive words with identical
        /// bold/italic state are wrapped in one marker instead of one per word.
        /// </summary>
        private static string GetStyledLineText(TextLine line)
        {
            var words = line.Words.ToList();
            if (words.Count == 0) return string.Empty;

            var sb = new StringBuilder();
            int i = 0;
            while (i < words.Count)
            {
                var (isBold, isItalic) = GetWordStyle(words[i]);

                // Extend run while the style is unchanged
                int j = i + 1;
                while (j < words.Count)
                {
                    var (nb, ni) = GetWordStyle(words[j]);
                    if (nb != isBold || ni != isItalic) break;
                    j++;
                }

                string runText = string.Join(" ", words.GetRange(i, j - i).Select(w => EscapeMarkdownChars(w.Text)));
                if (sb.Length > 0) sb.Append(' ');
                if (isBold && isItalic) sb.Append("***").Append(runText).Append("***");
                else if (isBold) sb.Append("**").Append(runText).Append("**");
                else if (isItalic) sb.Append('*').Append(runText).Append('*');
                else sb.Append(runText);
                i = j;
            }

            return sb.ToString();
        }

        /// <summary>
        /// Returns the bold/italic state of a word by inspecting its first letter's font name.
        /// PDF words are typically typeset in a single font, so sampling the first letter is reliable.
        /// </summary>
        private static (bool IsBold, bool IsItalic) GetWordStyle(Word word)
        {
            if (!word.Letters.Any()) return (false, false);

            string fontName = word.Letters.First().FontName ?? string.Empty;

            bool isBold = fontName.IndexOf("Bold", StringComparison.OrdinalIgnoreCase) >= 0
                       || fontName.IndexOf("-B", StringComparison.Ordinal) >= 0
                       || fontName.IndexOf(",Bold", StringComparison.OrdinalIgnoreCase) >= 0;

            bool isItalic = fontName.IndexOf("Italic", StringComparison.OrdinalIgnoreCase) >= 0
                         || fontName.IndexOf("Oblique", StringComparison.OrdinalIgnoreCase) >= 0
                         || fontName.IndexOf("-I", StringComparison.Ordinal) >= 0
                         || fontName.IndexOf(",Italic", StringComparison.OrdinalIgnoreCase) >= 0;

            return (isBold, isItalic);
        }

        /// <summary>Escapes Markdown reserved characters inside plain word text before wrapping in style markers.</summary>
        private static string EscapeMarkdownChars(string text)
        {
            return text.Replace("*", "\\*").Replace("_", "\\_");
        }

        /// <summary>
        /// Escapes characters that would break Markdown link destination syntax (parentheses and
        /// whitespace). Does not escape '*' or '_' since those have no special meaning inside a
        /// link destination and escaping them would corrupt the URL.
        /// </summary>
        private static string EscapeMarkdownUrl(string url)
        {
            return url.Replace("(", "%28").Replace(")", "%29").Replace(" ", "%20");
        }

        /// <summary>
        /// Detects whether a text block starts with a bullet or numbered list marker.
        /// When true, <paramref name="marker"/> receives the Markdown list marker ("-" or "1." etc.)
        /// and <paramref name="content"/> receives the text after the original marker.
        /// </summary>
        private static bool TryDetectListItem(string text, out string marker, out string content)
        {
            var bulletMatch = BulletListPattern.Match(text);
            if (bulletMatch.Success)
            {
                marker = "-";
                content = text.Substring(bulletMatch.Length).TrimStart();
                return true;
            }

            var numMatch = NumberedListPattern.Match(text);
            if (numMatch.Success)
            {
                // Only digit markers ('1)', '2.') have a capturing group and can preserve their
                // original number. Letter and Roman-numeral markers ('a)', 'i.') have no CommonMark
                // equivalent, so they are normalized to a plain ordered-list marker; Markdown viewers
                // renumber consecutive '1.' items automatically.
                string? number = numMatch.Groups[1].Success ? numMatch.Groups[1].Value : null;
                marker = string.IsNullOrEmpty(number) ? "1." : $"{number}.";
                content = text.Substring(numMatch.Length).TrimStart();
                return true;
            }

            marker = string.Empty;
            content = string.Empty;
            return false;
        }

        /// <summary>
        /// Computes the indentation level for a list item based on its left margin relative
        /// to previously-seen list-item left margins on the same page.
        /// </summary>
        private static int GetListIndentLevel(double leftMargin, List<double> leftMarginLevels, double tolerance = 18.0)
        {
            for (int i = 0; i < leftMarginLevels.Count; i++)
            {
                if (Math.Abs(leftMargin - leftMarginLevels[i]) < tolerance)
                {
                    return i;
                }
            }

            leftMarginLevels.Add(leftMargin);
            leftMarginLevels.Sort();
            return leftMarginLevels.Count - 1;
        }

        private static double GetBlockFontSize(TextBlock block)
        {
            var letters = block.TextLines.SelectMany(l => l.Words).SelectMany(w => w.Letters);
            return letters.Any() ? letters.Average(l => l.FontSize) : 12.0;
        }

        private static double GetMedianFontSize(IEnumerable<TextBlock> blocks)
        {
            var sizes = blocks.Select(GetBlockFontSize).OrderBy(s => s).ToList();
            if (sizes.Count == 0) return 12.0;
            int mid = sizes.Count / 2;
            return sizes.Count % 2 == 0 ? (sizes[mid - 1] + sizes[mid]) / 2.0 : sizes[mid];
        }

        private static int GetHeadingLevel(double fontSize, double medianFontSize)
        {
            double ratio = fontSize / medianFontSize;
            if (ratio >= 2.0) return 1;
            if (ratio >= 1.7) return 2;
            if (ratio >= 1.5) return 3;
            if (ratio >= 1.4) return 4;
            if (ratio >= 1.3) return 5;
            return 6;
        }

        // -------------------------------------------------------------------------
        // Table detection — gap-based stream mode (Camelot/Tabula-inspired)
        //
        // Core insight: body text has roughly uniform inter-word spacing throughout
        // a block, so NO gap exceeds ColumnGapMultiplier × median.  A real table
        // always has one or more column-separator gaps that are much wider than the
        // within-cell word spacing, and those gaps appear at consistent X positions
        // across most rows.  Left-edge clustering (old approach) failed because word
        // starts are distributed across the full line width in paragraph text.
        // -------------------------------------------------------------------------

        /// <summary>
        /// Returns true only when the block contains at least one consistent
        /// large-gap column separator, unambiguously indicating tabular content.
        /// </summary>
        private static bool IsTableBlock(TextBlock block)
        {
            var lines = block.TextLines.ToList();
            if (lines.Count < MinTableRows) return false;

            // Blocks with very few words per line (e.g. single-word lines) are
            // equations or captions, not tables.
            double avgWords = lines.Average(l => l.Words.Count);
            if (avgWords < 1.5) return false;

            return FindConsistentColumnSeparators(lines).Count >= 1;
        }

        /// <summary>
        /// Detects whether a text block is surrounded by drawn line segments (lattice-mode table).
        /// Checks the PDF page's stroked paths: a block qualifies when at least three horizontal
        /// and one vertical stroked path segment overlap its bounding region.
        /// Inspired by PyMuPDF's lattice strategy — more reliable than whitespace-gap detection
        /// for tables with explicit cell borders. Fails gracefully if path data is unavailable.
        /// </summary>
        private static bool HasLatticeLines(Page page, TextBlock block)
        {
            if (page == null || block == null) return false;
            try
            {
                var bbox = block.BoundingBox;
                const double tolerance = 5.0;
                const double maxLineThickness = 3.0;
                const double minLineLength = 15.0;

                int hLineCount = 0;
                int vLineCount = 0;

                foreach (var path in page.Paths)
                {
                    if (!path.IsStroked) continue;

                    var rect = path.GetBoundingRectangle();
                    if (rect == null) continue;

                    var r = rect.Value;

                    // Must overlap the block's bounding box (with tolerance)
                    if (r.Right < bbox.Left - tolerance || r.Left > bbox.Right + tolerance) continue;
                    if (r.Top < bbox.Bottom - tolerance || r.Bottom > bbox.Top + tolerance) continue;

                    double w = r.Width;
                    double h = r.Height;

                    if (h <= maxLineThickness && w >= minLineLength) hLineCount++;
                    else if (w <= maxLineThickness && h >= minLineLength) vLineCount++;
                }

                return hLineCount >= 3 && vLineCount >= 1;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Finds X-positions where a consistently large inter-word gap exists across
        /// at least half of the block's lines.  These positions are the column
        /// separators of a table.
        /// <para>
        /// Algorithm: for each line compute all inter-word gaps.  Determine the
        /// block-wide median gap (= normal word spacing).  Any gap ≥
        /// <see cref="ColumnGapMultiplier"/> × median AND ≥
        /// <see cref="MinAbsoluteColumnGap"/> is a candidate separator.  Candidate
        /// midpoints are clustered with <see cref="ColumnSeparatorTolerance"/>;
        /// only clusters present in ≥ half the lines are kept.
        /// </para>
        /// </summary>
        private static List<double> FindConsistentColumnSeparators(List<TextLine> lines, double gapMultiplier = ColumnGapMultiplier)
        {
            var allGapSizes = new List<double>();
            var lineGapData = new List<List<(double Mid, double Size)>>();

            foreach (var line in lines)
            {
                var words = line.Words.OrderBy(w => w.BoundingBox.Left).ToList();
                var lineGaps = new List<(double Mid, double Size)>();

                for (int i = 1; i < words.Count; i++)
                {
                    double gap = words[i].BoundingBox.Left - words[i - 1].BoundingBox.Right;
                    if (gap > 0)
                    {
                        double mid = (words[i - 1].BoundingBox.Right + words[i].BoundingBox.Left) / 2.0;
                        lineGaps.Add((mid, gap));
                        allGapSizes.Add(gap);
                    }
                }

                lineGapData.Add(lineGaps);
            }

            if (allGapSizes.Count == 0) return new List<double>();

            allGapSizes.Sort();
            double medianGap = allGapSizes[allGapSizes.Count / 2];
            double largeGapThreshold = Math.Max(medianGap * gapMultiplier, MinAbsoluteColumnGap);

            // Collect midpoints of all large gaps across all lines
            var largeMids = lineGapData
                .SelectMany(lg => lg.Where(g => g.Size >= largeGapThreshold).Select(g => g.Mid))
                .OrderBy(x => x)
                .ToList();

            if (largeMids.Count == 0) return new List<double>();

            // Cluster separator midpoints with running-mean
            var clusters = new List<(double Centroid, int Count)> { (largeMids[0], 1) };
            foreach (var x in largeMids.Skip(1))
            {
                int nearest = -1;
                double nearestDist = ColumnSeparatorTolerance;
                for (int i = 0; i < clusters.Count; i++)
                {
                    double d = Math.Abs(x - clusters[i].Centroid);
                    if (d < nearestDist)
                    {
                        nearestDist = d;
                        nearest = i;
                    }
                }

                if (nearest >= 0)
                {
                    var (c, n) = clusters[nearest];
                    clusters[nearest] = ((c * n + x) / (n + 1), n + 1);
                }
                else
                {
                    clusters.Add((x, 1));
                }
            }

            // Keep only separators that appear in at least half the lines
            int minAppearances = Math.Max(2, lines.Count / 2);
            return clusters
                .Where(cl => lineGapData.Count(lg =>
                    lg.Any(g => g.Size >= largeGapThreshold &&
                                Math.Abs(g.Mid - cl.Centroid) <= ColumnSeparatorTolerance))
                    >= minAppearances)
                .OrderBy(cl => cl.Centroid)
                .Select(cl => cl.Centroid)
                .ToList();
        }

        /// <summary>
        /// Splits each line at the detected column separator positions and renders
        /// a GitHub-Flavoured Markdown pipe table.  The first row becomes the header.
        /// Returns an empty string if the quality checks fail (too many empty cells).
        /// </summary>
        private static string RenderMarkdownTable(TextBlock block, bool forcedByCaption = false)
        {
            var lines = block.TextLines.ToList();
            double gapMult = forcedByCaption ? ColumnGapMultiplierForced : ColumnGapMultiplier;
            var separators = FindConsistentColumnSeparators(lines, gapMult);
            if (separators.Count < 1) return string.Empty;

            int columnCount = separators.Count + 1;
            var rows = new List<string[]>();

            foreach (var line in lines)
            {
                var cells = new string[columnCount];
                for (int i = 0; i < cells.Length; i++) cells[i] = string.Empty;

                foreach (var word in line.Words)
                {
                    int col = GetCellIndex(word.BoundingBox.Left, separators);
                    string w = word.Text.Replace("|", "\\|").Replace("*", "\\*").Replace("_", "\\_");
                    cells[col] = cells[col].Length == 0 ? w : cells[col] + " " + w;
                }

                rows.Add(cells);
            }

            if (rows.Count == 0) return string.Empty;

            // Quality gate: reject if more than half of all cells are empty
            int totalCells = rows.Count * columnCount;
            int emptyCells = rows.Sum(r => r.Count(string.IsNullOrWhiteSpace));
            if (emptyCells > totalCells * MaxEmptyCellFraction) return string.Empty;

            var sb = new StringBuilder();
            sb.Append("| ").Append(string.Join(" | ", rows[0])).AppendLine(" |");
            sb.Append("| ").Append(string.Join(" | ", Enumerable.Repeat("---", columnCount))).AppendLine(" |");
            foreach (var row in rows.Skip(1))
            {
                sb.Append("| ").Append(string.Join(" | ", row)).AppendLine(" |");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Returns the cell index (0-based) for a word at <paramref name="wordLeft"/>
        /// given a list of separator X-positions.
        /// </summary>
        private static int GetCellIndex(double wordLeft, List<double> separators)
        {
            for (int i = 0; i < separators.Count; i++)
            {
                if (wordLeft < separators[i]) return i;
            }

            return separators.Count;
        }
    }
}
