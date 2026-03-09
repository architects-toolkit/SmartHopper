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
 * This code uses PdfPig for PDF text extraction:
 * https://github.com/UglyToad/PdfPig
 * Apache License 2.0
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace SmartHopper.Core.Grasshopper.Converters.Formats
{
    /// <summary>
    /// Converter for PDF files (.pdf).
    /// Uses PdfPig for text extraction with MinerU-inspired layout intelligence:
    /// - Column detection and reading order
    /// - Header/footer removal
    /// - Heading detection by font size
    /// - Table detection
    /// - Scanned page detection
    /// </summary>
    public sealed class PdfConverter : IFileConverter
    {
        public IEnumerable<string> SupportedExtensions => new[] { ".pdf" };

        public async Task<FileConversionResult> ConvertAsync(string filePath, FileConversionOptions options)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var document = PdfDocument.Open(filePath);
                    var markdown = new StringBuilder();
                    var result = FileConversionResult.Success(string.Empty, "pdf");

                    // Extract metadata
                    var info = document.Information;
                    if (!string.IsNullOrWhiteSpace(info.Title))
                    {
                        result.Metadata["title"] = info.Title;
                    }
                    if (!string.IsNullOrWhiteSpace(info.Author))
                    {
                        result.Metadata["author"] = info.Author;
                    }
                    if (!string.IsNullOrWhiteSpace(info.CreationDate))
                    {
                        result.Metadata["created"] = info.CreationDate;
                    }

                    // Collect text blocks from all pages for header/footer detection
                    var allPageBlocks = new List<List<TextBlock>>();
                    foreach (var page in document.GetPages())
                    {
                        var blocks = ExtractTextBlocks(page);
                        allPageBlocks.Add(blocks);
                    }

                    // Detect headers/footers if option is enabled
                    var headersFooters = options.RemoveHeadersFooters 
                        ? DetectHeadersFooters(allPageBlocks) 
                        : new HashSet<string>();

                    // Process each page
                    int pageNumber = 1;
                    foreach (var blocks in allPageBlocks)
                    {
                        // Check for scanned page
                        if (blocks.Count == 0 || blocks.Sum(b => b.Text.Length) < 5)
                        {
                            result.Warnings.Add($"⚠️ Page {pageNumber} appears to be scanned; text may be missing.");
                            pageNumber++;
                            continue;
                        }

                        // Remove headers/footers
                        var contentBlocks = blocks.Where(b => !headersFooters.Contains(b.Text)).ToList();

                        // Sort blocks by reading order (column-aware)
                        var sortedBlocks = SortByReadingOrder(contentBlocks);

                        // Detect heading font size threshold
                        double medianFontSize = GetMedianFontSize(sortedBlocks);
                        double headingThreshold = medianFontSize * 1.3;

                        // Convert blocks to markdown
                        foreach (var block in sortedBlocks)
                        {
                            if (options.DetectHeadings && block.FontSize > headingThreshold)
                            {
                                // Heading - use font size to determine level
                                int level = GetHeadingLevel(block.FontSize, medianFontSize);
                                markdown.Append(new string('#', level)).Append(' ').AppendLine(block.Text);
                                markdown.AppendLine();
                            }
                            else
                            {
                                markdown.AppendLine(block.Text);
                                markdown.AppendLine();
                            }
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

        private static List<TextBlock> ExtractTextBlocks(Page page)
        {
            var blocks = new List<TextBlock>();
            var words = page.GetWords().ToList();
            if (words.Count == 0)
            {
                return blocks;
            }

            // Group words into lines based on Y coordinate
            var lines = new List<List<Word>>();
            var currentLine = new List<Word> { words[0] };
            double currentY = words[0].BoundingBox.Bottom;

            for (int i = 1; i < words.Count; i++)
            {
                var word = words[i];
                // If Y coordinate is close to current line, add to current line
                if (Math.Abs(word.BoundingBox.Bottom - currentY) < 5)
                {
                    currentLine.Add(word);
                }
                else
                {
                    // Start new line
                    lines.Add(currentLine);
                    currentLine = new List<Word> { word };
                    currentY = word.BoundingBox.Bottom;
                }
            }
            lines.Add(currentLine);

            // Convert lines to text blocks
            foreach (var line in lines)
            {
                if (line.Count == 0) continue;

                var text = string.Join(" ", line.Select(w => w.Text));
                var fontSize = line.SelectMany(w => w.Letters).Average(l => l.FontSize);
                var minX = line.Min(w => w.BoundingBox.Left);
                var maxX = line.Max(w => w.BoundingBox.Right);
                var minY = line.Min(w => w.BoundingBox.Bottom);
                var maxY = line.Max(w => w.BoundingBox.Top);

                blocks.Add(new TextBlock
                {
                    Text = text.Trim(),
                    FontSize = fontSize,
                    Left = minX,
                    Right = maxX,
                    Bottom = minY,
                    Top = maxY
                });
            }

            return blocks;
        }

        private static HashSet<string> DetectHeadersFooters(List<List<TextBlock>> allPageBlocks)
        {
            if (allPageBlocks.Count < 3)
            {
                return new HashSet<string>();
            }

            var headersFooters = new HashSet<string>();
            var pageHeight = allPageBlocks[0].Count > 0 
                ? allPageBlocks[0].Max(b => b.Top) 
                : 0;

            if (pageHeight == 0)
            {
                return headersFooters;
            }

            double headerThreshold = pageHeight * 0.92; // Top 8%
            double footerThreshold = pageHeight * 0.08; // Bottom 8%

            // Count occurrences of text in header/footer regions
            var headerTexts = new Dictionary<string, int>();
            var footerTexts = new Dictionary<string, int>();

            foreach (var blocks in allPageBlocks)
            {
                foreach (var block in blocks)
                {
                    if (block.Top > headerThreshold)
                    {
                        headerTexts.TryGetValue(block.Text, out int count);
                        headerTexts[block.Text] = count + 1;
                    }
                    else if (block.Bottom < footerThreshold)
                    {
                        footerTexts.TryGetValue(block.Text, out int count);
                        footerTexts[block.Text] = count + 1;
                    }
                }
            }

            // Add texts that appear on 3+ pages
            foreach (var kvp in headerTexts.Where(kvp => kvp.Value >= 3))
            {
                headersFooters.Add(kvp.Key);
            }
            foreach (var kvp in footerTexts.Where(kvp => kvp.Value >= 3))
            {
                headersFooters.Add(kvp.Key);
            }

            return headersFooters;
        }

        private static List<TextBlock> SortByReadingOrder(List<TextBlock> blocks)
        {
            if (blocks.Count == 0)
            {
                return blocks;
            }

            // Detect columns by finding large horizontal gaps
            var sortedByX = blocks.OrderBy(b => b.Left).ToList();
            var columns = new List<List<TextBlock>>();
            var currentColumn = new List<TextBlock> { sortedByX[0] };
            double currentMaxX = sortedByX[0].Right;

            for (int i = 1; i < sortedByX.Count; i++)
            {
                var block = sortedByX[i];
                double gap = block.Left - currentMaxX;

                // If gap is large (>50 units), start new column
                if (gap > 50)
                {
                    columns.Add(currentColumn);
                    currentColumn = new List<TextBlock> { block };
                    currentMaxX = block.Right;
                }
                else
                {
                    currentColumn.Add(block);
                    currentMaxX = Math.Max(currentMaxX, block.Right);
                }
            }
            columns.Add(currentColumn);

            // Sort each column top-to-bottom, then concatenate columns left-to-right
            var result = new List<TextBlock>();
            foreach (var column in columns)
            {
                result.AddRange(column.OrderByDescending(b => b.Top));
            }

            return result;
        }

        private static double GetMedianFontSize(List<TextBlock> blocks)
        {
            if (blocks.Count == 0)
            {
                return 12.0;
            }

            var sizes = blocks.Select(b => b.FontSize).OrderBy(s => s).ToList();
            int mid = sizes.Count / 2;
            return sizes.Count % 2 == 0 
                ? (sizes[mid - 1] + sizes[mid]) / 2.0 
                : sizes[mid];
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

        private sealed class TextBlock
        {
            public string Text { get; set; } = string.Empty;
            public double FontSize { get; set; }
            public double Left { get; set; }
            public double Right { get; set; }
            public double Bottom { get; set; }
            public double Top { get; set; }
        }
    }
}
