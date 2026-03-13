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
            double avgWords = lines.Average(l => l.Words.Count());
            if (avgWords < 1.5) return false;

            return FindConsistentColumnSeparators(lines).Count >= 1;
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
        private static List<double> FindConsistentColumnSeparators(List<TextLine> lines)
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
            double largeGapThreshold = Math.Max(medianGap * ColumnGapMultiplier, MinAbsoluteColumnGap);

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
        private static string RenderMarkdownTable(TextBlock block)
        {
            var lines = block.TextLines.ToList();
            var separators = FindConsistentColumnSeparators(lines);
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
                    string w = word.Text.Replace("|", "\\|");
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
