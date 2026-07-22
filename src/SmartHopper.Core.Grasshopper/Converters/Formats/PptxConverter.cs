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
using A = DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using SmartHopper.Core.Types;

namespace SmartHopper.Core.Grasshopper.Converters.Formats
{
    /// <summary>
    /// Converter for PowerPoint presentations (.pptx).
    /// Converts each slide to a Markdown section with title, body, tables, and notes.
    /// </summary>
    public sealed class PptxConverter : IFileConverter
    {
        public IEnumerable<string> SupportedExtensions => new[] { ".pptx" };

        public async Task<FileConversionResult> ConvertAsync(string filePath, FileConversionOptions options)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var doc = PresentationDocument.Open(filePath, false);
                    var presentationPart = doc.PresentationPart;
                    if (presentationPart == null)
                    {
                        return FileConversionResult.Failure("pptx", "Presentation is empty or invalid.");
                    }

                    var markdown = new StringBuilder();
                    var result = FileConversionResult.Success(string.Empty, "pptx");

                    // Extract metadata
                    var coreProps = doc.PackageProperties;
                    if (!string.IsNullOrWhiteSpace(coreProps.Title))
                    {
                        result.Metadata["title"] = coreProps.Title;
                        markdown.Append("# ").AppendLine(coreProps.Title);
                        markdown.AppendLine();
                    }

                    if (!string.IsNullOrWhiteSpace(coreProps.Creator))
                    {
                        result.Metadata["author"] = coreProps.Creator;
                    }

                    // Get slide IDs
                    var slideIds = presentationPart.Presentation.SlideIdList?.Elements<SlideId>().ToList();
                    if (slideIds == null || slideIds.Count == 0)
                    {
                        return FileConversionResult.Failure("pptx", "No slides found in presentation.");
                    }

                    // Process each slide
                    int slideNumber = 1;
                    int imageIndex = 0;
                    foreach (var slideId in slideIds)
                    {
                        var slidePart = (SlidePart)presentationPart.GetPartById(slideId.RelationshipId!);
                        ProcessSlide(slidePart, slideNumber, markdown, options);

                        // Extract images from the slide when enabled, then emit [image N] placeholders
                        // after the slide's text content (PPTX does not expose inline image positions).
                        if (options.ExtractImages)
                        {
                            int slideImageStart = result.Images.Count;
                            imageIndex = ExtractSlideImages(slidePart, slideNumber, imageIndex, result);
                            for (int imgIdx = slideImageStart; imgIdx < result.Images.Count; imgIdx++)
                            {
                                markdown.AppendLine($"[image {imgIdx + 1}]");
                                markdown.AppendLine();
                            }
                        }

                        slideNumber++;
                    }

                    result.MarkdownContent = markdown.ToString().Trim();
                    return result;
                }
                catch (Exception ex)
                {
                    return FileConversionResult.Failure("pptx", $"Failed to convert PPTX: {ex.Message}");
                }
            }).ConfigureAwait(false);
        }

        private static void ProcessSlide(SlidePart slidePart, int slideNumber, StringBuilder markdown, FileConversionOptions options)
        {
            var slide = slidePart.Slide;
            if (slide == null)
            {
                return;
            }

            // Extract title
            string? title = null;
            var shapeTree = slide.CommonSlideData?.ShapeTree;
            if (shapeTree != null)
            {
                foreach (var shape in shapeTree.Elements<Shape>())
                {
                    var placeholder = shape.NonVisualShapeProperties?.ApplicationNonVisualDrawingProperties?.PlaceholderShape;
                    if (placeholder?.Type?.Value == PlaceholderValues.Title ||
                        placeholder?.Type?.Value == PlaceholderValues.CenteredTitle)
                    {
                        title = GetShapeText(shape, slidePart, options);
                        break;
                    }
                }
            }

            // Write slide heading
            if (!string.IsNullOrWhiteSpace(title))
            {
                markdown.Append("## Slide ").Append(slideNumber).Append(": ").AppendLine(title);
            }
            else
            {
                markdown.Append("## Slide ").AppendLine(slideNumber.ToString());
            }

            markdown.AppendLine();

            // Extract body text and tables from the shape tree
            if (shapeTree != null)
            {
                foreach (var element in shapeTree.ChildElements)
                {
                    ProcessShapeTreeElement(element, markdown, slidePart, options);
                }
            }

            // Extract speaker notes
            var notesPart = slidePart.NotesSlidePart;
            if (notesPart != null)
            {
                var notesText = GetNotesText(notesPart, options);
                if (!string.IsNullOrWhiteSpace(notesText))
                {
                    markdown.AppendLine("> **Note:** " + notesText);
                    markdown.AppendLine();
                }
            }
        }

        private static void ProcessShapeTreeElement(
            OpenXmlElement element,
            StringBuilder markdown,
            OpenXmlPart part,
            FileConversionOptions options)
        {
            if (element is Shape shape)
            {
                var placeholder = shape.NonVisualShapeProperties?.ApplicationNonVisualDrawingProperties?.PlaceholderShape;
                if (placeholder?.Type?.Value == PlaceholderValues.Title ||
                    placeholder?.Type?.Value == PlaceholderValues.CenteredTitle)
                {
                    return;
                }

                var textBody = shape.TextBody;
                if (textBody == null)
                {
                    return;
                }

                if (HasBullets(textBody))
                {
                    foreach (var paragraph in textBody.Elements<A.Paragraph>())
                    {
                        var text = GetParagraphText(paragraph, part, options);
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            var level = paragraph.ParagraphProperties?.Level?.Value ?? 0;
                            markdown.Append(new string(' ', (int)level * 2)).Append("- ").AppendLine(text);
                        }
                    }
                }
                else
                {
                    var text = GetShapeText(shape, part, options);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        markdown.AppendLine(text);
                        markdown.AppendLine();
                    }
                }
            }
            else if (element is GraphicFrame graphicFrame)
            {
                var table = graphicFrame.Descendants<A.Table>().FirstOrDefault();
                if (table != null)
                {
                    var tableText = ConvertTableToMarkdown(table, part, options);
                    if (!string.IsNullOrWhiteSpace(tableText))
                    {
                        markdown.AppendLine();
                        markdown.AppendLine(tableText);
                        markdown.AppendLine();
                    }
                }
            }
            else if (element is GroupShape groupShape)
            {
                foreach (var child in groupShape.ChildElements)
                {
                    ProcessShapeTreeElement(child, markdown, part, options);
                }
            }
        }

        private static string GetShapeText(Shape shape, OpenXmlPart part, FileConversionOptions options)
        {
            var textBody = shape.TextBody;
            if (textBody == null)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            foreach (var paragraph in textBody.Elements<A.Paragraph>())
            {
                var text = GetParagraphText(paragraph, part, options);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    if (sb.Length > 0)
                    {
                        sb.AppendLine();
                    }

                    sb.Append(text);
                }
            }

            return sb.ToString().Trim();
        }

        private static string GetParagraphText(A.Paragraph paragraph, OpenXmlPart part, FileConversionOptions options)
        {
            var sb = new StringBuilder();
            var runs = paragraph.Elements<A.Run>().ToList();
            var allRunsBold = OpenXmlMarkdownHelper.AllRunsBold(runs);
            var allRunsItalic = OpenXmlMarkdownHelper.AllRunsItalic(runs);

            foreach (var child in paragraph.ChildElements)
            {
                if (child is A.Run run)
                {
                    var text = run.Text?.Text;
                    if (string.IsNullOrEmpty(text))
                    {
                        continue;
                    }

                    var isBold = options.PreserveFormatting && OpenXmlMarkdownHelper.IsBold(run) && !allRunsBold;
                    var isItalic = options.PreserveFormatting && OpenXmlMarkdownHelper.IsItalic(run) && !allRunsItalic;
                    var runStart = sb.Length;

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

                    if (options.PreserveHyperlinks)
                    {
                        var hyperlink = run.RunProperties?.GetFirstChild<A.HyperlinkOnClick>();
                        if (hyperlink != null && !string.IsNullOrWhiteSpace(hyperlink.Id))
                        {
                            var linkRel = part.HyperlinkRelationships.FirstOrDefault(r => r.Id == hyperlink.Id);
                            if (linkRel?.Uri != null)
                            {
                                var linkText = sb.ToString(runStart, sb.Length - runStart);
                                sb.Remove(runStart, sb.Length - runStart);
                                sb.Append('[').Append(linkText).Append("](").Append(linkRel.Uri).Append(')');
                            }
                        }
                    }
                }
                else if (child is A.Field field)
                {
                    var text = field.Text?.Text;
                    if (!string.IsNullOrEmpty(text))
                    {
                        sb.Append(text);
                    }
                }
                else if (child.NamespaceUri == "http://schemas.openxmlformats.org/officeDocument/2006/math" &&
                         options.PreserveMath &&
                         (child.LocalName == "oMath" || child.LocalName == "oMathPara"))
                {
                    var latex = OpenXmlMarkdownHelper.ConvertMathToLaTeX(child);
                    if (!string.IsNullOrEmpty(latex))
                    {
                        sb.Append($"${latex}$");
                    }
                }
            }

            return sb.ToString().Trim();
        }

        private static bool HasBullets(DocumentFormat.OpenXml.Presentation.TextBody textBody)
        {
            foreach (var paragraph in textBody.Elements<A.Paragraph>())
            {
                var pPr = paragraph.ParagraphProperties;
                if (pPr != null)
                {
                    foreach (var child in pPr.ChildElements)
                    {
                        if (child.LocalName.StartsWith("bu", StringComparison.Ordinal) && child.LocalName != "buNone")
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static string ConvertTableToMarkdown(A.Table table, OpenXmlPart part, FileConversionOptions options)
        {
            var rows = table.Elements<A.TableRow>().ToList();
            if (rows.Count == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            var isFirstRow = true;
            var maxCols = 0;

            foreach (var row in rows)
            {
                var cells = row.Elements<A.TableCell>().ToList();
                if (cells.Count > maxCols)
                {
                    maxCols = cells.Count;
                }
            }

            if (maxCols == 0)
            {
                return string.Empty;
            }

            foreach (var row in rows)
            {
                sb.Append('|');
                foreach (var cell in row.Elements<A.TableCell>())
                {
                    var cellText = new StringBuilder();
                    if (cell.TextBody != null)
                    {
                        foreach (var p in cell.TextBody.Elements<A.Paragraph>())
                        {
                            var pText = GetParagraphText(p, part, options);
                            if (!string.IsNullOrEmpty(pText))
                            {
                                if (cellText.Length > 0)
                                {
                                    cellText.Append("<br>");
                                }

                                cellText.Append(pText);
                            }
                        }
                    }

                    sb.Append(' ')
                      .Append(OpenXmlMarkdownHelper.EscapeMarkdownTableCell(cellText.ToString().Trim()))
                      .Append(" |");
                }

                // Pad missing cells
                var rowCells = row.Elements<A.TableCell>().Count();
                for (int i = rowCells; i < maxCols; i++)
                {
                    sb.Append("  |");
                }

                sb.AppendLine();

                if (isFirstRow)
                {
                    sb.Append('|');
                    for (int i = 0; i < maxCols; i++)
                    {
                        sb.Append(" --- |");
                    }

                    sb.AppendLine();
                    isFirstRow = false;
                }
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Extracts embedded images from a PPTX slide part.
        /// </summary>
        private static int ExtractSlideImages(SlidePart slidePart, int slideNumber, int imageIndex, FileConversionResult result)
        {
            try
            {
                foreach (var imagePart in slidePart.ImageParts)
                {
                    imageIndex++;
                    try
                    {
                        using var stream = imagePart.GetStream();
                        using var ms = new MemoryStream();
                        stream.CopyTo(ms);
                        var bytes = ms.ToArray();

                        if (bytes.Length == 0)
                        {
                            continue;
                        }

                        string mimeType = imagePart.ContentType ?? "image/png";
                        string base64Data = System.Convert.ToBase64String(bytes);

                        var extracted = VersatileImage.FromExtractedDocument(
                            base64Data: base64Data,
                            mimeType: mimeType,
                            id: $"img-{imageIndex}",
                            context: $"Slide {slideNumber}",
                            pageOrSlide: slideNumber,
                            sourceDocument: null);
                        result.Images.Add(extracted);
                    }
                    catch (Exception ex)
                    {
                        result.Warnings.Add($"\u26a0\ufe0f Slide {slideNumber}, image {imageIndex}: could not extract: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"\u26a0\ufe0f Slide {slideNumber}: image extraction failed: {ex.Message}");
            }

            return imageIndex;
        }

        private static string GetNotesText(NotesSlidePart notesPart, FileConversionOptions options)
        {
            var notesSlide = notesPart.NotesSlide;
            if (notesSlide == null)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            var shapes = notesSlide.CommonSlideData?.ShapeTree?.Elements<Shape>().ToList();
            if (shapes != null)
            {
                foreach (var shape in shapes)
                {
                    var placeholder = shape.NonVisualShapeProperties?.ApplicationNonVisualDrawingProperties?.PlaceholderShape;
                    if (placeholder?.Type?.Value == PlaceholderValues.Body)
                    {
                        var text = GetShapeText(shape, notesPart, options);
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            sb.Append(text);
                        }
                    }
                }
            }

            return sb.ToString().Trim();
        }
    }
}
