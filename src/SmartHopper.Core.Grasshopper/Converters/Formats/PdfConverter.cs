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
 * All algorithms are independently reimplemented in C#; no source code was copied.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
    /// - DefaultReadingOrderDetector for correct top-to-bottom, left-to-right order
    /// - Header/footer removal by cross-page text frequency analysis
    /// - Heading detection by font size ratio
    /// - Stream-mode table detection (whitespace column-alignment clustering)
    /// - Markdown table rendering with pipe escaping
    /// - Scanned page detection
    /// </summary>
    public sealed class PdfConverter : IFileConverter
    {
        /// <summary>X-position tolerance (PDF units) for clustering word left-edges into columns.</summary>
        private const double ColumnTolerance = 10.0;

        /// <summary>Minimum number of data rows required to classify a block as a table.</summary>
        private const int MinTableRows = 3;

        /// <summary>Minimum number of detected columns required to classify a block as a table.</summary>
        private const int MinTableColumns = 2;

        /// <summary>Minimum page count for a repeated text to be removed as header/footer.</summary>
        private const int MinHeaderFooterRepeat = 3;

        /// <summary>Maximum average words-per-line before a block is considered body text, not a table.</summary>
        private const double MaxWordsPerLineForTable = 12.0;

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

                    foreach (var (page, blocks) in allPageData)
                    {
                        if (blocks.Count == 0 || blocks.Sum(b => GetBlockText(b).Length) < 5)
                        {
                            result.Warnings.Add($"⚠️ Page {pageNumber} appears to be scanned; text may be missing.");
                            pageNumber++;
                            continue;
                        }

                        var ordered = DefaultReadingOrderDetector.Instance.Get(blocks);
                        var content = ordered
                            .Where(b => !headersFooters.Contains(GetBlockText(b)))
                            .ToList();

                        double medianFontSize = GetMedianFontSize(content);
                        double headingThreshold = medianFontSize * 1.3;

                        foreach (var block in content)
                        {
                            if (options.PreserveTableStructure && IsTableBlock(block))
                            {
                                string table = RenderMarkdownTable(block);
                                if (!string.IsNullOrWhiteSpace(table))
                                {
                                    markdown.AppendLine(table);
                                    continue;
                                }
                            }

                            string text = GetBlockText(block);
                            if (string.IsNullOrWhiteSpace(text))
                            {
                                continue;
                            }

                            double fontSize = GetBlockFontSize(block);
                            if (options.DetectHeadings && fontSize > headingThreshold)
                            {
                                int level = GetHeadingLevel(fontSize, medianFontSize);
                                markdown.Append(new string('#', level)).Append(' ').AppendLine(text);
                            }
                            else
                            {
                                markdown.AppendLine(text);
                            }

                            markdown.AppendLine();
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

            foreach (var (page, blocks) in allPageData)
            {
                double headerLine = page.Height * 0.92;
                double footerLine = page.Height * 0.08;

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

            return result;
        }

        private static string GetBlockText(TextBlock block)
        {
            return string.Join(" ", block.TextLines
                .Select(l => string.Join(" ", l.Words.Select(w => w.Text)))).Trim();
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
        // Table detection — stream mode inspired by Camelot and Tabula
        // -------------------------------------------------------------------------

        /// <summary>
        /// Returns true when a block's lines exhibit consistent multi-column alignment,
        /// indicating tabular data rather than body text.
        /// </summary>
        private static bool IsTableBlock(TextBlock block)
        {
            var lines = block.TextLines.ToList();
            if (lines.Count < MinTableRows) return false;

            // Body-text paragraphs have many words per line; tables typically do not
            double avgWords = lines.Average(l => l.Words.Count());
            if (avgWords > MaxWordsPerLineForTable) return false;

            return DetectColumnPositions(lines).Count >= MinTableColumns;
        }

        /// <summary>
        /// Clusters word left-edges across all lines into stable column positions.
        /// Uses a running-mean merge with <see cref="ColumnTolerance"/> as radius.
        /// Only columns that appear in at least half the lines are retained.
        /// </summary>
        private static List<double> DetectColumnPositions(List<TextLine> lines)
        {
            var allLeft = lines
                .SelectMany(l => l.Words)
                .Select(w => w.BoundingBox.Left)
                .OrderBy(x => x)
                .ToList();

            if (allLeft.Count == 0) return new List<double>();

            // Running-mean clustering
            var clusters = new List<(double Centroid, int Count)> { (allLeft[0], 1) };

            foreach (var x in allLeft.Skip(1))
            {
                int nearest = -1;
                double nearestDist = ColumnTolerance;

                for (int i = 0; i < clusters.Count; i++)
                {
                    double d = Math.Abs(x - clusters[i].Centroid);
                    if (d < nearestDist) { nearestDist = d; nearest = i; }
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

            // Discard columns that appear in fewer than half of all lines
            int minAppearances = Math.Max(2, lines.Count / 2);
            return clusters
                .Where(cl => lines.Count(line =>
                    line.Words.Any(w => Math.Abs(w.BoundingBox.Left - cl.Centroid) <= ColumnTolerance))
                    >= minAppearances)
                .OrderBy(cl => cl.Centroid)
                .Select(cl => cl.Centroid)
                .ToList();
        }

        /// <summary>
        /// Assigns each word in every line to its nearest column and renders a
        /// GitHub-Flavoured Markdown pipe table.  The first row becomes the header.
        /// </summary>
        private static string RenderMarkdownTable(TextBlock block)
        {
            var lines = block.TextLines.ToList();
            var columns = DetectColumnPositions(lines);
            if (columns.Count < MinTableColumns) return string.Empty;

            var rows = new List<string[]>();

            foreach (var line in lines)
            {
                var cells = new string[columns.Count];
                for (int i = 0; i < cells.Length; i++) cells[i] = string.Empty;

                foreach (var word in line.Words)
                {
                    int col = FindNearestColumn(word.BoundingBox.Left, columns);
                    if (col < 0) continue;
                    string w = word.Text.Replace("|", "\\|");
                    cells[col] = cells[col].Length == 0 ? w : cells[col] + " " + w;
                }

                rows.Add(cells);
            }

            if (rows.Count == 0) return string.Empty;

            var sb = new StringBuilder();

            // Header row
            sb.Append("| ").Append(string.Join(" | ", rows[0])).AppendLine(" |");

            // Separator
            sb.Append("| ").Append(string.Join(" | ", Enumerable.Repeat("---", columns.Count))).AppendLine(" |");

            // Data rows
            foreach (var row in rows.Skip(1))
            {
                sb.Append("| ").Append(string.Join(" | ", row)).AppendLine(" |");
            }

            return sb.ToString();
        }

        private static int FindNearestColumn(double x, List<double> columns)
        {
            int best = -1;
            double bestDist = ColumnTolerance * 2;
            for (int i = 0; i < columns.Count; i++)
            {
                double d = Math.Abs(x - columns[i]);
                if (d < bestDist) { bestDist = d; best = i; }
            }

            return best;
        }
    }
}
