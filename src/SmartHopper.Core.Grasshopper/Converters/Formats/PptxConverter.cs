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
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using SmartHopper.Core.Types;
using A = DocumentFormat.OpenXml.Drawing;

namespace SmartHopper.Core.Grasshopper.Converters.Formats
{
    /// <summary>
    /// Converter for PowerPoint presentations (.pptx).
    /// Converts each slide to a Markdown section with title, body, and notes.
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
                        ProcessSlide(slidePart, slideNumber, markdown);

                        // Extract images from the slide when enabled
                        if (options.ExtractImages)
                        {
                            imageIndex = ExtractSlideImages(slidePart, slideNumber, imageIndex, result);
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

        private static void ProcessSlide(SlidePart slidePart, int slideNumber, StringBuilder markdown)
        {
            var slide = slidePart.Slide;
            if (slide == null)
            {
                return;
            }

            // Extract title
            string? title = null;
            var shapes = slide.CommonSlideData?.ShapeTree?.Elements<Shape>().ToList();
            if (shapes != null)
            {
                foreach (var shape in shapes)
                {
                    var placeholder = shape.NonVisualShapeProperties?.ApplicationNonVisualDrawingProperties?.PlaceholderShape;
                    if (placeholder?.Type?.Value == PlaceholderValues.Title ||
                        placeholder?.Type?.Value == PlaceholderValues.CenteredTitle)
                    {
                        title = GetShapeText(shape);
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

            // Extract body text
            if (shapes != null)
            {
                foreach (var shape in shapes)
                {
                    var placeholder = shape.NonVisualShapeProperties?.ApplicationNonVisualDrawingProperties?.PlaceholderShape;

                    // Skip title placeholder (already processed)
                    if (placeholder?.Type?.Value == PlaceholderValues.Title ||
                        placeholder?.Type?.Value == PlaceholderValues.CenteredTitle)
                    {
                        continue;
                    }

                    var text = GetShapeText(shape);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        // Check if it's a list
                        var textBody = shape.TextBody;
                        if (textBody != null && HasBullets(textBody))
                        {
                            var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var line in lines)
                            {
                                markdown.Append("- ").AppendLine(line.Trim());
                            }
                        }
                        else
                        {
                            markdown.AppendLine(text);
                        }

                        markdown.AppendLine();
                    }
                }
            }

            // Extract speaker notes
            var notesPart = slidePart.NotesSlidePart;
            if (notesPart != null)
            {
                var notesText = GetNotesText(notesPart);
                if (!string.IsNullOrWhiteSpace(notesText))
                {
                    markdown.AppendLine("> **Note:** " + notesText);
                    markdown.AppendLine();
                }
            }
        }

        private static string GetShapeText(Shape shape)
        {
            var textBody = shape.TextBody;
            if (textBody == null)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            foreach (var paragraph in textBody.Elements<A.Paragraph>())
            {
                foreach (var run in paragraph.Elements<A.Run>())
                {
                    var text = run.Text?.Text;
                    if (!string.IsNullOrEmpty(text))
                    {
                        sb.Append(text);
                    }
                }

                sb.AppendLine();
            }

            return sb.ToString().Trim();
        }

        private static bool HasBullets(DocumentFormat.OpenXml.Presentation.TextBody textBody)
        {
            foreach (var paragraph in textBody.Elements<A.Paragraph>())
            {
                var bulletProperties = paragraph.ParagraphProperties?.GetFirstChild<A.BulletFont>();
                if (bulletProperties != null)
                {
                    return true;
                }
            }

            return false;
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

        private static string GetNotesText(NotesSlidePart notesPart)
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
                        var text = GetShapeText(shape);
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
