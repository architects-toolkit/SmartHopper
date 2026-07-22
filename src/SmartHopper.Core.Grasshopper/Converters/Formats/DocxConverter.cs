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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SmartHopper.Core.Types;

namespace SmartHopper.Core.Grasshopper.Converters.Formats
{
    /// <summary>
    /// Converter for Word documents (.docx).
    /// Converts DOCX to Markdown preserving headings, lists, tables, formatting, hyperlinks,
    /// footnotes, endnotes, comments, and Office Math equations.
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
                    var mainPart = doc.MainDocumentPart;
                    var body = mainPart?.Document?.Body;
                    if (body == null)
                    {
                        return FileConversionResult.Failure("docx", "Document body is empty or invalid.");
                    }

                    var markdown = new StringBuilder();
                    var result = FileConversionResult.Success(string.Empty, "docx");

                    ExtractMetadata(doc, result);

                    var context = new ConversionContext(mainPart, options, result);

                    if (!options.RemoveHeadersFooters)
                    {
                        var headersFooters = ExtractHeadersFooters(mainPart);
                        if (!string.IsNullOrWhiteSpace(headersFooters))
                        {
                            markdown.Append(headersFooters);
                            markdown.AppendLine();
                        }
                    }

                    foreach (var element in body.Elements())
                    {
                        if (element is Paragraph paragraph)
                        {
                            ProcessParagraph(paragraph, markdown, context);
                        }
                        else if (element is Table table && options.PreserveTableStructure)
                        {
                            ProcessTable(table, markdown, context);
                        }
                    }

                    if (context.Footnotes.Count > 0 || context.Endnotes.Count > 0)
                    {
                        markdown.AppendLine();
                        AppendNotesSection(markdown, context.Footnotes, context.Endnotes);
                    }

                    // Some DOCX files keep part or all of their readable text inside grouped
                    // shapes / text boxes. Always extract that text alongside the regular body
                    // content so nothing is silently lost.
                    if (body != null)
                    {
                        AppendShapeText(body, markdown, context);
                    }

                    var markdownText = markdown.ToString().Trim();
                    result.MarkdownContent = markdownText;
                    return result;
                }
                catch (Exception ex)
                {
                    return FileConversionResult.Failure("docx", $"Failed to convert DOCX: {ex.Message}");
                }
            }).ConfigureAwait(false);
        }

        private static void ExtractMetadata(WordprocessingDocument doc, FileConversionResult result)
        {
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
        }

        private static void ProcessParagraph(
            Paragraph paragraph,
            StringBuilder markdown,
            ConversionContext context)
        {
            if (context.Options.ExtractImages && context.MainPart != null)
            {
                ExtractParagraphImages(paragraph, markdown, context);
            }

            var text = GetParagraphText(paragraph, context);
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
            if (context.Options.DetectHeadings && !string.IsNullOrEmpty(styleId))
            {
                int headingLevel = GetHeadingLevel(styleId, context.Styles);
                if (headingLevel > 0)
                {
                    markdown.Append(new string('#', headingLevel)).Append(' ').AppendLine(text);
                    markdown.AppendLine();
                    AppendComments(paragraph, context.Comments, markdown);
                    return;
                }
            }

            var numPr = paragraph.ParagraphProperties?.NumberingProperties;
            if (numPr != null)
            {
                ProcessListItem(paragraph, text, numPr, markdown, context);
                AppendComments(paragraph, context.Comments, markdown);
                return;
            }

            markdown.AppendLine(text);
            markdown.AppendLine();
            AppendComments(paragraph, context.Comments, markdown);
        }

        private static void ExtractParagraphImages(Paragraph paragraph, StringBuilder markdown, ConversionContext context)
        {
            foreach (var run in paragraph.Elements<Run>())
            {
                foreach (var drawing in run.Elements<Drawing>())
                {
                    var blip = drawing.Descendants<A.Blip>().FirstOrDefault();
                    if (blip == null)
                    {
                        continue;
                    }

                    string embedId = blip.Embed?.Value;
                    if (string.IsNullOrEmpty(embedId))
                    {
                        continue;
                    }

                    try
                    {
                        var part = context.MainPart.GetPartById(embedId);
                        if (part is ImagePart imagePart)
                        {
                            context.ImageIndex++;
                            using var stream = imagePart.GetStream();
                            using var ms = new MemoryStream();
                            stream.CopyTo(ms);
                            var bytes = ms.ToArray();
                            if (bytes.Length > 0)
                            {
                                string mimeType = imagePart.ContentType ?? "image/png";
                                string base64Data = Convert.ToBase64String(bytes);
                                context.Result.Images.Add(VersatileImage.FromExtractedDocument(
                                    base64Data: base64Data,
                                    mimeType: mimeType,
                                    id: $"img-{context.ImageIndex}",
                                    context: "Document body",
                                    pageOrSlide: 0,
                                    sourceDocument: null));

                                markdown.AppendLine();
                                markdown.AppendLine($"[image {context.ImageIndex}]");
                                markdown.AppendLine();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        context.Result.Warnings.Add($"⚠️ Image {context.ImageIndex}: could not extract: {ex.Message}");
                    }
                }
            }
        }

        private static string GetParagraphText(Paragraph paragraph, ConversionContext context)
        {
            var sb = new StringBuilder();
            var runs = paragraph.Elements<Run>().ToList();
            var allRunsBold = OpenXmlMarkdownHelper.AllRunsBold(runs);
            var allRunsItalic = OpenXmlMarkdownHelper.AllRunsItalic(runs);

            foreach (var child in paragraph.ChildElements)
            {
                if (child is Run run)
                {
                    AppendRunText(run, sb, context, allRunsBold, allRunsItalic);
                }
                else if (child is Hyperlink hyperlink && context.Options.PreserveHyperlinks)
                {
                    AppendHyperlink(hyperlink, sb, context);
                }
                else if (child is FootnoteReference fnRef && context.Options.PreserveFootnotes)
                {
                    var id = fnRef.Id?.Value;
                    if (id.HasValue)
                    {
                        sb.Append($"[^fn{(int)id.Value}]");
                    }
                }
                else if (child is EndnoteReference enRef && context.Options.PreserveEndnotes)
                {
                    var id = enRef.Id?.Value;
                    if (id.HasValue)
                    {
                        sb.Append($"[^en{(int)id.Value}]");
                    }
                }
                else if (child.NamespaceUri == "http://schemas.openxmlformats.org/officeDocument/2006/math" &&
                         context.Options.PreserveMath &&
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

        private static void AppendRunText(
            Run run,
            StringBuilder sb,
            ConversionContext context,
            bool allRunsBold,
            bool allRunsItalic)
        {
            var rPr = run.RunProperties;
            var preserveFormatting = context.Options.PreserveFormatting;
            var isBold = preserveFormatting && OpenXmlMarkdownHelper.IsBold(run) && !allRunsBold;
            var isItalic = preserveFormatting && OpenXmlMarkdownHelper.IsItalic(run) && !allRunsItalic;

            var colorOpen = preserveFormatting ? ResolveColorOpen(rPr) : null;
            var highlightOpen = preserveFormatting ? ResolveHighlightOpen(rPr) : null;
            var highlightClose = highlightOpen != null ? "</mark>" : null;
            var colorClose = colorOpen != null ? "</span>" : null;

            foreach (var child in run.ChildElements)
            {
                if (child is Text textElement)
                {
                    var text = textElement.Text;
                    if (string.IsNullOrEmpty(text))
                    {
                        continue;
                    }

                    sb.Append(colorOpen);
                    sb.Append(highlightOpen);

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

                    sb.Append(highlightClose);
                    sb.Append(colorClose);
                }
                else if (child is FootnoteReference fnRef && context.Options.PreserveFootnotes)
                {
                    var id = fnRef.Id?.Value;
                    if (id.HasValue)
                    {
                        sb.Append($"[^fn{(int)id.Value}]");
                    }
                }
                else if (child is EndnoteReference enRef && context.Options.PreserveEndnotes)
                {
                    var id = enRef.Id?.Value;
                    if (id.HasValue)
                    {
                        sb.Append($"[^en{(int)id.Value}]");
                    }
                }
            }
        }

        private static void AppendHyperlink(Hyperlink hyperlink, StringBuilder sb, ConversionContext context)
        {
            var url = OpenXmlMarkdownHelper.ResolveDocxHyperlink(hyperlink, context.MainPart);
            var text = GetHyperlinkText(hyperlink, context);
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            if (string.IsNullOrEmpty(url))
            {
                sb.Append(text);
            }
            else
            {
                sb.Append('[').Append(text).Append("](").Append(url).Append(')');
            }
        }

        private static string GetHyperlinkText(Hyperlink hyperlink, ConversionContext context)
        {
            var sb = new StringBuilder();
            var runs = hyperlink.Elements<Run>().ToList();
            var allRunsBold = OpenXmlMarkdownHelper.AllRunsBold(runs);
            var allRunsItalic = OpenXmlMarkdownHelper.AllRunsItalic(runs);

            foreach (var run in runs)
            {
                AppendRunText(run, sb, context, allRunsBold, allRunsItalic);
            }

            return sb.ToString().Trim();
        }

        private static void ProcessListItem(
            Paragraph paragraph,
            string text,
            NumberingProperties numPr,
            StringBuilder markdown,
            ConversionContext context)
        {
            var ilvl = (int)(numPr.NumberingLevelReference?.Val?.Value ?? 0);
            var numId = (int)(numPr.NumberingId?.Val?.Value ?? 0);
            var indent = new string(' ', ilvl * 2);
            var marker = context.NumberingState.GetMarker(numId, ilvl, context.NumberingDefinitions);

            markdown.Append(indent).Append(marker).Append(' ').AppendLine(text);
        }

        private static void ProcessTable(
            Table table,
            StringBuilder markdown,
            ConversionContext context)
        {
            var rows = table.Elements<TableRow>().ToList();
            if (rows.Count == 0)
            {
                return;
            }

            var headerRow = rows[0];
            var headerCells = headerRow.Elements<TableCell>().Select(c => GetCellText(c, context)).ToList();
            if (headerCells.Count == 0)
            {
                return;
            }

            markdown.Append("| ").AppendJoin(" | ", headerCells.Select(OpenXmlMarkdownHelper.EscapeMarkdownTableCell)).AppendLine(" |");
            markdown.Append("|").AppendJoin("|", headerCells.Select(_ => " --- ")).AppendLine("|");

            for (int i = 1; i < rows.Count; i++)
            {
                var cells = rows[i].Elements<TableCell>().Select(c => GetCellText(c, context)).ToList();
                while (cells.Count < headerCells.Count)
                {
                    cells.Add(string.Empty);
                }

                markdown.Append("| ").AppendJoin(" | ", cells.Select(OpenXmlMarkdownHelper.EscapeMarkdownTableCell)).AppendLine(" |");
            }

            markdown.AppendLine();
        }

        private static string GetCellText(TableCell cell, ConversionContext context)
        {
            var sb = new StringBuilder();
            var paragraphs = cell.Elements<Paragraph>().ToList();
            for (int i = 0; i < paragraphs.Count; i++)
            {
                var text = GetParagraphText(paragraphs[i], context);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    if (sb.Length > 0)
                    {
                        sb.Append("<br>");
                    }

                    sb.Append(text);
                }
            }

            return sb.ToString().Trim();
        }

        /// <summary>
        /// Extracts text from paragraphs that live inside DrawingML or VML shapes / text boxes
        /// (i.e. paragraphs whose ancestors include a <c>&lt;w:drawing&gt;</c> or <c>&lt;w:pict&gt;</c>
        /// element) and appends it to the Markdown output as a labeled blockquote. This ensures
        /// DOCX files whose readable content is stored in grouped shapes still produce usable text.
        /// </summary>
        private static void AppendShapeText(Body body, StringBuilder markdown, ConversionContext context)
        {
            var shapeParagraphs = body.Descendants<Paragraph>()
                .Where(p => (p.Ancestors<Drawing>().Any() || p.Ancestors<Picture>().Any()) &&
                            !p.Ancestors<Table>().Any())
                .ToList();

            if (shapeParagraphs.Count == 0)
            {
                return;
            }

            var extracted = new StringBuilder();
            foreach (var paragraph in shapeParagraphs)
            {
                var text = GetParagraphText(paragraph, context);
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                if (extracted.Length > 0)
                {
                    extracted.AppendLine();
                    extracted.AppendLine();
                }

                extracted.Append(text);
            }

                        var extracted = VersatileImage.FromExtractedDocument(
                            base64Data: base64Data,
                            mimeType: mimeType,
                            id: $"img-{imageIndex}",
                            context: "Document body",
                            pageOrSlide: 0,
                            sourceDocument: null);
                        result.Images.Add(extracted);
                    }
                    catch (Exception ex)
                    {
                        result.Warnings.Add($"\u26a0\ufe0f Image {imageIndex}: could not extract: {ex.Message}");
                    }
                }
            }

            context.Result.Warnings.Add("Additional text was extracted from shapes/text boxes.");
        }

        private static int GetHeadingLevel(string styleId, Styles? styles)
        {
            if (string.IsNullOrEmpty(styleId))
            {
                return 0;
            }

            var normalized = styleId.ToLowerInvariant();
            if (normalized.Contains("heading1") || normalized.Contains("heading 1"))
            {
                return 1;
            }

            if (normalized.Contains("heading2") || normalized.Contains("heading 2"))
            {
                return 2;
            }

            if (normalized.Contains("heading3") || normalized.Contains("heading 3"))
            {
                return 3;
            }

            if (normalized.Contains("heading4") || normalized.Contains("heading 4"))
            {
                return 4;
            }

            if (normalized.Contains("heading5") || normalized.Contains("heading 5"))
            {
                return 5;
            }

            if (normalized.Contains("heading6") || normalized.Contains("heading 6"))
            {
                return 6;
            }

            // Fallback to outline level from style definitions.
            if (styles != null)
            {
                var style = styles.Elements<Style>().FirstOrDefault(s => s.StyleId?.Value == styleId);
                var outlineLevel = style?.StyleParagraphProperties?.GetFirstChild<OutlineLevel>()?.Val?.Value;
                if (outlineLevel.HasValue && outlineLevel.Value >= 1 && outlineLevel.Value <= 6)
                {
                    return (int)outlineLevel.Value;
                }
            }

            return 0;
        }

        private static IReadOnlyDictionary<int, CommentInfo> LoadComments(MainDocumentPart? mainPart, bool preserveComments)
        {
            if (!preserveComments || mainPart?.WordprocessingCommentsPart == null)
            {
                return new Dictionary<int, CommentInfo>();
            }

            var comments = new Dictionary<int, CommentInfo>();
            foreach (var comment in mainPart.WordprocessingCommentsPart.Comments.Elements<Comment>())
            {
                var id = comment.Id?.Value;
                if (string.IsNullOrEmpty(id) || !int.TryParse(id, out var idInt))
                {
                    continue;
                }

                var text = string.Join(
                    " ",
                    comment.Elements<Paragraph>()
                            .Select(p => p.InnerText)
                            .Where(t => !string.IsNullOrWhiteSpace(t)));

                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                comments[idInt] = new CommentInfo(
                    comment.Author?.Value,
                    text);
            }

            return comments;
        }

        private static void AppendComments(
            Paragraph paragraph,
            IReadOnlyDictionary<int, CommentInfo> comments,
            StringBuilder markdown)
        {
            if (comments.Count == 0)
            {
                return;
            }

            foreach (var commentRef in paragraph.Descendants<CommentReference>())
            {
                var id = commentRef.Id?.Value;
                if (string.IsNullOrEmpty(id) || !int.TryParse(id, out var idInt))
                {
                    continue;
                }

                if (!comments.TryGetValue(idInt, out var comment))
                {
                    continue;
                }

                markdown.AppendLine();
                markdown.Append("> **");
                if (!string.IsNullOrEmpty(comment.Author))
                {
                    markdown.Append("[Comment — ").Append(comment.Author).Append("]:** ");
                }
                else
                {
                    markdown.Append("[Comment]:** ");
                }

                markdown.AppendLine(comment.Text);
            }
        }

        private static IReadOnlyDictionary<int, string> LoadFootnotes(MainDocumentPart? mainPart)
        {
            if (mainPart?.FootnotesPart == null)
            {
                return new Dictionary<int, string>();
            }

            var footnotes = new Dictionary<int, string>();
            foreach (var footnote in mainPart.FootnotesPart.Footnotes.Elements<Footnote>())
            {
                if (footnote.Id?.Value is not { } idLong)
                {
                    continue;
                }

                var id = (int)idLong;
                if (id <= 1)
                {
                    continue; // Skip separator (0) and continuation separator (1)
                }

                var text = string.Join(
                    " ",
                    footnote.Elements<Paragraph>()
                            .Select(p => p.InnerText)
                            .Where(t => !string.IsNullOrWhiteSpace(t)));

                if (!string.IsNullOrWhiteSpace(text))
                {
                    footnotes[id] = text;
                }
            }

            return footnotes;
        }

        private static IReadOnlyDictionary<int, string> LoadEndnotes(MainDocumentPart? mainPart)
        {
            if (mainPart?.EndnotesPart == null)
            {
                return new Dictionary<int, string>();
            }

            var endnotes = new Dictionary<int, string>();
            foreach (var endnote in mainPart.EndnotesPart.Endnotes.Elements<Endnote>())
            {
                if (endnote.Id?.Value is not { } idLong)
                {
                    continue;
                }

                var id = (int)idLong;
                if (id <= 1)
                {
                    continue; // Skip separator (0) and continuation separator (1)
                }

                var text = string.Join(
                    " ",
                    endnote.Elements<Paragraph>()
                            .Select(p => p.InnerText)
                            .Where(t => !string.IsNullOrWhiteSpace(t)));

                if (!string.IsNullOrWhiteSpace(text))
                {
                    endnotes[id] = text;
                }
            }

            return endnotes;
        }

        private static void AppendNotesSection(
            StringBuilder markdown,
            IReadOnlyDictionary<int, string> footnotes,
            IReadOnlyDictionary<int, string> endnotes)
        {
            if (footnotes.Count > 0)
            {
                markdown.AppendLine("---");
                markdown.AppendLine();
                foreach (var kvp in footnotes.OrderBy(k => k.Key))
                {
                    markdown.AppendLine($"[^fn{kvp.Key}]: {kvp.Value}");
                }
            }

            if (endnotes.Count > 0)
            {
                if (footnotes.Count > 0)
                {
                    markdown.AppendLine();
                }

                markdown.AppendLine("---");
                markdown.AppendLine();
                foreach (var kvp in endnotes.OrderBy(k => k.Key))
                {
                    markdown.AppendLine($"[^en{kvp.Key}]: {kvp.Value}");
                }
            }
        }

        private static string ExtractHeadersFooters(MainDocumentPart? mainPart)
        {
            if (mainPart?.Document?.Body == null)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            var sectionProps = mainPart.Document.Body.Descendants<SectionProperties>();
            foreach (var secProps in sectionProps)
            {
                foreach (var headerRef in secProps.Elements<HeaderReference>())
                {
                    if (mainPart.GetPartById(headerRef.Id!) is HeaderPart headerPart)
                    {
                        var text = string.Join(
                            " ",
                            headerPart.Header.Elements<Paragraph>()
                                .Select(p => p.InnerText)
                                .Where(t => !string.IsNullOrWhiteSpace(t)));
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            var type = headerRef.Type?.Value;
                            var label = type == HeaderFooterValues.First ? "Header — First Page"
                                : type == HeaderFooterValues.Even ? "Header — Even"
                                : "Header";
                            sb.AppendLine($"> **[{label}]:** {text}");
                        }
                    }
                }

                foreach (var footerRef in secProps.Elements<FooterReference>())
                {
                    if (mainPart.GetPartById(footerRef.Id!) is FooterPart footerPart)
                    {
                        var text = string.Join(
                            " ",
                            footerPart.Footer.Elements<Paragraph>()
                                .Select(p => p.InnerText)
                                .Where(t => !string.IsNullOrWhiteSpace(t)));
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            var type = footerRef.Type?.Value;
                            var label = type == HeaderFooterValues.First ? "Footer — First Page"
                                : type == HeaderFooterValues.Even ? "Footer — Even"
                                : "Footer";
                            sb.AppendLine($"> **[{label}]:** {text}");
                        }
                    }
                }
            }

            return sb.ToString().TrimEnd();
        }

        private static IReadOnlyDictionary<int, IReadOnlyDictionary<int, Level>> LoadNumbering(MainDocumentPart? mainPart)
        {
            if (mainPart?.NumberingDefinitionsPart?.Numbering == null)
            {
                return new Dictionary<int, IReadOnlyDictionary<int, Level>>();
            }

            var numbering = mainPart.NumberingDefinitionsPart.Numbering;
            var abstractNums = numbering.Elements<AbstractNum>().ToDictionary(
                a => (int)(a.AbstractNumberId?.Value ?? 0),
                a => (IReadOnlyDictionary<int, Level>)a.Elements<Level>().ToDictionary(l => (int)(l.LevelIndex?.Value ?? 0)));

            var result = new Dictionary<int, IReadOnlyDictionary<int, Level>>();
            foreach (var num in numbering.Elements<DocumentFormat.OpenXml.Wordprocessing.NumberingInstance>())
            {
                var numId = (int)(num.NumberID?.Value ?? 0);
                var abstractNumId = (int)(num.AbstractNumId?.Val?.Value ?? 0);
                if (abstractNums.TryGetValue(abstractNumId, out var levels))
                {
                    result[numId] = levels;
                }
            }

            return result;
        }

        private static string ResolveColorOpen(RunProperties? rPr)
        {
            var color = rPr?.GetFirstChild<Color>();
            if (color == null)
            {
                return null;
            }

            var val = color.Val?.Value;
            if (string.IsNullOrEmpty(val) || val.Equals("auto", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (val.Equals("000000", StringComparison.OrdinalIgnoreCase) ||
                val.Equals("FFFFFF", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return $"<span style=\"color: #{val};\">";
        }

        private static string ResolveHighlightOpen(RunProperties? rPr)
        {
            var highlight = rPr?.GetFirstChild<Highlight>();
            var highlightValue = highlight?.Val?.ToString();
            if (!string.IsNullOrEmpty(highlightValue) &&
                !string.Equals(highlightValue, "none", StringComparison.OrdinalIgnoreCase))
            {
                var rgb = HighlightColorToRgb(highlightValue);
                if (!string.IsNullOrEmpty(rgb))
                {
                    return $"<mark style=\"background-color: #{rgb};\">";
                }
            }

            var shading = rPr?.GetFirstChild<Shading>();
            var fill = shading?.Fill?.Value;
            if (!string.IsNullOrEmpty(fill))
            {
                return $"<mark style=\"background-color: #{fill};\">";
            }

            return null;
        }

        private static string HighlightColorToRgb(string highlight)
        {
            return (highlight ?? string.Empty).ToLowerInvariant() switch
            {
                "black" => "000000",
                "blue" => "0000FF",
                "cyan" => "00FFFF",
                "darkblue" => "000080",
                "darkcyan" => "008080",
                "darkgray" => "808080",
                "darkgreen" => "008000",
                "darkmagenta" => "800080",
                "darkred" => "800000",
                "darkyellow" => "808000",
                "green" => "00FF00",
                "lightgray" => "C0C0C0",
                "magenta" => "FF00FF",
                "red" => "FF0000",
                "white" => "FFFFFF",
                "yellow" => "FFFF00",
                _ => null,
            };
        }

        /// <summary>
        /// Lightweight container for a single DOCX comment.
        /// </summary>
        private readonly struct CommentInfo
        {
            public CommentInfo(string author, string text)
            {
                this.Author = author;
                this.Text = text;
            }

            public string Author { get; }

            public string Text { get; }
        }

        /// <summary>
        /// Holds mutable state for one DOCX conversion.
        /// </summary>
        private sealed class ConversionContext
        {
            public ConversionContext(MainDocumentPart? mainPart, FileConversionOptions options, FileConversionResult result)
            {
                this.MainPart = mainPart;
                this.Options = options;
                this.Result = result;
                this.Comments = LoadComments(mainPart, options.PreserveComments);
                this.Footnotes = options.PreserveFootnotes ? LoadFootnotes(mainPart) : new Dictionary<int, string>();
                this.Endnotes = options.PreserveEndnotes ? LoadEndnotes(mainPart) : new Dictionary<int, string>();
                this.NumberingDefinitions = LoadNumbering(mainPart);
                this.Styles = mainPart?.StyleDefinitionsPart?.Styles;
                this.NumberingState = new ListNumberingState();
            }

            public MainDocumentPart? MainPart { get; }

            public FileConversionOptions Options { get; }

            public FileConversionResult Result { get; }

            public IReadOnlyDictionary<int, CommentInfo> Comments { get; }

            public IReadOnlyDictionary<int, string> Footnotes { get; }

            public IReadOnlyDictionary<int, string> Endnotes { get; }

            public IReadOnlyDictionary<int, IReadOnlyDictionary<int, Level>> NumberingDefinitions { get; }

            public Styles? Styles { get; }

            public ListNumberingState NumberingState { get; }

            public int ImageIndex { get; set; }
        }

        /// <summary>
        /// Tracks numbering state for DOCX lists so ordered lists can emit incrementing numbers.
        /// </summary>
        private sealed class ListNumberingState
        {
            private readonly Dictionary<(int numId, int ilvl), int> counters = new();
            private (int numId, int ilvl)? last;

            public string GetMarker(
                int numId,
                int ilvl,
                IReadOnlyDictionary<int, IReadOnlyDictionary<int, Level>> numberingDefinitions)
            {
                var key = (numId, ilvl);
                bool isOrdered = IsOrdered(numId, ilvl, numberingDefinitions);

                if (last.HasValue)
                {
                    var lastKey = last.Value;
                    if (lastKey.numId == numId && lastKey.ilvl == ilvl)
                    {
                        counters[key]++;
                    }
                    else if (lastKey.numId != numId)
                    {
                        counters.Clear();
                        counters[key] = 1;
                    }
                    else if (lastKey.ilvl < ilvl)
                    {
                        counters[key] = 1;
                    }
                    else
                    {
                        counters[key] = counters.TryGetValue(key, out var existing) ? existing + 1 : 1;
                    }
                }
                else
                {
                    counters[key] = 1;
                }

                last = key;

                if (isOrdered)
                {
                    return counters[key].ToString(CultureInfo.InvariantCulture) + ".";
                }

                return "-";
            }

            private static bool IsOrdered(
                int numId,
                int ilvl,
                IReadOnlyDictionary<int, IReadOnlyDictionary<int, Level>> numberingDefinitions)
            {
                if (numberingDefinitions.TryGetValue(numId, out var levels) &&
                    levels.TryGetValue(ilvl, out var level))
                {
                    return level.NumberingFormat?.Val?.Value == NumberFormatValues.Decimal;
                }

                return false;
            }
        }
    }
}
