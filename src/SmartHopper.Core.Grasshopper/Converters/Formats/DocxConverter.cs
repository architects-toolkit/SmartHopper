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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SmartHopper.Core.Types;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace SmartHopper.Core.Grasshopper.Converters.Formats
{
    /// <summary>
    /// Converter for Word documents (.docx).
    /// Converts DOCX to Markdown preserving headings, lists, tables, and formatting.
    /// </summary>
    public sealed class DocxConverter : IFileConverter
    {
        public IEnumerable<string> SupportedExtensions => new[] { ".docx" };

        public async Task<FileConversionResult> ConvertAsync(string filePath, FileConversionOptions options)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var doc = WordprocessingDocument.Open(filePath, false);
                    var body = doc.MainDocumentPart?.Document?.Body;
                    if (body == null)
                    {
                        return FileConversionResult.Failure("docx", "Document body is empty or invalid.");
                    }

                    var markdown = new StringBuilder();
                    var result = FileConversionResult.Success(string.Empty, "docx");

                    // Extract metadata
                    var coreProps = doc.PackageProperties;
                    if (!string.IsNullOrWhiteSpace(coreProps.Title))
                    {
                        result.Metadata["title"] = coreProps.Title;
                    }

                    if (!string.IsNullOrWhiteSpace(coreProps.Creator))
                    {
                        result.Metadata["author"] = coreProps.Creator;
                    }

                    if (coreProps.Created.HasValue)
                    {
                        result.Metadata["created"] = coreProps.Created.Value.ToString("yyyy-MM-dd");
                    }

                    // Process each element in the body.
                    // When ExtractImages is enabled, inline Drawing elements are detected during the
                    // paragraph walk so that [image N] placeholders appear at their exact document position.
                    int imageIndex = 0;
                    var mainPart = doc.MainDocumentPart;
                    foreach (var element in body.Elements())
                    {
                        if (element is Paragraph paragraph)
                        {
                            ProcessParagraph(paragraph, markdown, options, mainPart, result, ref imageIndex);
                        }
                        else if (element is Table table && options.PreserveTableStructure)
                        {
                            ProcessTable(table, markdown);
                        }
                    }

                    result.MarkdownContent = markdown.ToString().Trim();
                    return result;
                }
                catch (Exception ex)
                {
                    return FileConversionResult.Failure("docx", $"Failed to convert DOCX: {ex.Message}");
                }
            }).ConfigureAwait(false);
        }

        private static void ProcessParagraph(
            Paragraph paragraph,
            StringBuilder markdown,
            FileConversionOptions options,
            MainDocumentPart mainPart,
            FileConversionResult result,
            ref int imageIndex)
        {
            // Detect inline Drawing elements before checking text content.
            // A paragraph that contains only a Drawing (no text) would otherwise be silently skipped.
            if (options.ExtractImages && mainPart != null)
            {
                foreach (var run in paragraph.Elements<Run>())
                {
                    foreach (var drawing in run.Elements<Drawing>())
                    {
                        // Resolve the image relationship ID from the pic:blipFill/a:blip r:embed attribute.
                        var blip = drawing.Descendants<A.Blip>().FirstOrDefault();
                        if (blip == null) continue;

                        string embedId = blip.Embed?.Value;
                        if (string.IsNullOrEmpty(embedId)) continue;

                        try
                        {
                            var part = mainPart.GetPartById(embedId);
                            if (part is ImagePart imagePart)
                            {
                                imageIndex++;
                                using var stream = imagePart.GetStream();
                                using var ms = new MemoryStream();
                                stream.CopyTo(ms);
                                var bytes = ms.ToArray();
                                if (bytes.Length > 0)
                                {
                                    string mimeType = imagePart.ContentType ?? "image/png";
                                    string base64Data = Convert.ToBase64String(bytes);
                                    result.Images.Add(VersatileImage.FromExtractedDocument(
                                        base64Data: base64Data,
                                        mimeType: mimeType,
                                        id: $"img-{imageIndex}",
                                        context: "Document body",
                                        pageOrSlide: 0,
                                        sourceDocument: null));

                                    markdown.AppendLine();
                                    markdown.AppendLine($"[image {imageIndex}]");
                                    markdown.AppendLine();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            result.Warnings.Add($"⚠️ Image {imageIndex}: could not extract: {ex.Message}");
                        }
                    }
                }
            }

            var text = GetParagraphText(paragraph);
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            // Check if it's a heading
            var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
            if (options.DetectHeadings && !string.IsNullOrEmpty(styleId))
            {
                int headingLevel = GetHeadingLevel(styleId);
                if (headingLevel > 0)
                {
                    markdown.Append(new string('#', headingLevel)).Append(' ').AppendLine(text);
                    markdown.AppendLine();
                    return;
                }
            }

            // Check if it's a list item
            var numPr = paragraph.ParagraphProperties?.NumberingProperties;
            if (numPr != null)
            {
                var ilvl = numPr.NumberingLevelReference?.Val?.Value ?? 0;
                var indent = new string(' ', (int)ilvl * 2);

                // Check if it's ordered or unordered (simplified - just use bullet for all)
                markdown.Append(indent).Append("- ").AppendLine(text);
                return;
            }

            // Regular paragraph
            markdown.AppendLine(text);
            markdown.AppendLine();
        }

        private static string GetParagraphText(Paragraph paragraph)
        {
            var sb = new StringBuilder();
            foreach (var run in paragraph.Elements<Run>())
            {
                var text = run.InnerText;
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                // Check for bold
                var isBold = run.RunProperties?.Bold != null;
                var isItalic = run.RunProperties?.Italic != null;

                if (isBold && isItalic)
                {
                    sb.Append("***").Append(text).Append("***");
                }
                else if (isBold)
                {
                    sb.Append("**").Append(text).Append("**");
                }
                else if (isItalic)
                {
                    sb.Append("*").Append(text).Append("*");
                }
                else
                {
                    sb.Append(text);
                }
            }

            return sb.ToString().Trim();
        }

        private static int GetHeadingLevel(string styleId)
        {
            if (string.IsNullOrEmpty(styleId))
            {
                return 0;
            }

            styleId = styleId.ToLowerInvariant();
            if (styleId.Contains("heading1") || styleId.Contains("heading 1"))
            {
                return 1;
            }

            if (styleId.Contains("heading2") || styleId.Contains("heading 2"))
            {
                return 2;
            }

            if (styleId.Contains("heading3") || styleId.Contains("heading 3"))
            {
                return 3;
            }

            if (styleId.Contains("heading4") || styleId.Contains("heading 4"))
            {
                return 4;
            }

            if (styleId.Contains("heading5") || styleId.Contains("heading 5"))
            {
                return 5;
            }

            if (styleId.Contains("heading6") || styleId.Contains("heading 6"))
            {
                return 6;
            }

            return 0;
        }

        private static void ProcessTable(Table table, StringBuilder markdown)
        {
            var rows = table.Elements<TableRow>().ToList();
            if (rows.Count == 0)
            {
                return;
            }

            // Process header row
            var headerRow = rows[0];
            var headerCells = headerRow.Elements<TableCell>().Select(c => c.InnerText.Trim()).ToList();
            if (headerCells.Count == 0)
            {
                return;
            }

            markdown.Append("| ").AppendJoin(" | ", headerCells.Select(EscapeMarkdown)).AppendLine(" |");
            markdown.Append("|").AppendJoin("|", headerCells.Select(_ => "---")).AppendLine("|");

            // Process data rows
            for (int i = 1; i < rows.Count; i++)
            {
                var cells = rows[i].Elements<TableCell>().Select(c => c.InnerText.Trim()).ToList();
                while (cells.Count < headerCells.Count)
                {
                    cells.Add(string.Empty);
                }

                markdown.Append("| ").AppendJoin(" | ", cells.Select(EscapeMarkdown)).AppendLine(" |");
            }

            markdown.AppendLine();
        }

        private static string EscapeMarkdown(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            return text.Replace("|", "\\|").Replace("\n", " ").Replace("\r", " ");
        }
    }
}
