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
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using SmartHopper.Core.Types;

namespace SmartHopper.Core.Grasshopper.Converters.Formats
{
    /// <summary>
    /// Converter for OpenDocument formats (ODT, OTT, ODS, OTS, ODP, OTP, ODG).
    /// Extracts text, headings, lists, tables and images from the ODF <c>content.xml</c> package part,
    /// preserving inline formatting such as bold, italic, underline, strikethrough, font color and
    /// background/highlight for text documents.
    /// </summary>
    public sealed class OpenDocumentConverter : IFileConverter
    {
        // ODF 1.2 namespace URIs.
        private static readonly XNamespace OfficeNs = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
        private static readonly XNamespace TextNs = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
        private static readonly XNamespace TableNs = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
        private static readonly XNamespace DrawNs = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
        private static readonly XNamespace StyleNs = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
        private static readonly XNamespace FoNs = "urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0";
        private static readonly XNamespace XlinkNs = "http://www.w3.org/1999/xlink";
        private static readonly XNamespace MetaNs = "urn:oasis:names:tc:opendocument:xmlns:meta:1.0";
        private static readonly XNamespace DCNs = "http://purl.org/dc/elements/1.1/";

        /// <inheritdoc/>
        public IEnumerable<string> SupportedExtensions => new[]
        {
            ".odt", // OpenDocument Text
            ".ott", // OpenDocument Text Template
            ".ods", // OpenDocument Spreadsheet
            ".ots", // OpenDocument Spreadsheet Template
            ".odp", // OpenDocument Presentation
            ".otp", // OpenDocument Presentation Template
            ".odg", // OpenDocument Drawing
        };

        /// <inheritdoc/>
        public async Task<FileConversionResult> ConvertAsync(string filePath, FileConversionOptions options)
        {
            return await Task.Run(() => ConvertInternal(filePath, options)).ConfigureAwait(false);
        }

        private static FileConversionResult ConvertInternal(string filePath, FileConversionOptions options)
        {
            var extension = Path.GetExtension(filePath)?.ToLowerInvariant() ?? string.Empty;
            try
            {
                using var archive = ZipFile.OpenRead(filePath);
                var contentEntry = archive.GetEntry("content.xml");
                if (contentEntry == null)
                {
                    return FileConversionResult.Failure(extension.TrimStart('.'), "Missing content.xml in OpenDocument archive.");
                }

                var result = FileConversionResult.Success(string.Empty, extension.TrimStart('.'));
                ExtractMetadata(archive, result);

                using var stream = contentEntry.Open();
                var document = XDocument.Load(stream, LoadOptions.None);
                var body = document?.Root?.Element(OfficeNs + "body");
                if (body == null)
                {
                    return FileConversionResult.Failure(extension.TrimStart('.'), "OpenDocument body not found.");
                }

                var stylesDocument = LoadXmlEntry(archive, "styles.xml");
                var styles = new StyleResolver(
                    new[]
                    {
                        document?.Root?.Element(OfficeNs + "automatic-styles"),
                        stylesDocument?.Root?.Element(OfficeNs + "automatic-styles"),
                        stylesDocument?.Root?.Element(OfficeNs + "styles"),
                    }.Where(x => x != null)!);

                var markdown = new StringBuilder();
                var context = new ConversionContext(archive, options, result, styles, extension.TrimStart('.'))
                {
                    CurrentContext = "Document body",
                };

                foreach (var child in body.Elements())
                {
                    ConvertBodyContainer(child, markdown, context, 0);
                }

                result.MarkdownContent = markdown.ToString().Trim();
                return result;
            }
            catch (Exception ex)
            {
                return FileConversionResult.Failure(extension.TrimStart('.'), $"Failed to convert OpenDocument file: {ex.Message}");
            }
        }

        private static XDocument? LoadXmlEntry(ZipArchive archive, string entryName)
        {
            var entry = archive.GetEntry(entryName);
            if (entry == null)
            {
                return null;
            }

            try
            {
                using var stream = entry.Open();
                return XDocument.Load(stream, LoadOptions.None);
            }
            catch
            {
                return null;
            }
        }

        private static void ExtractMetadata(ZipArchive archive, FileConversionResult result)
        {
            try
            {
                var metaEntry = archive.GetEntry("meta.xml");
                if (metaEntry == null)
                {
                    return;
                }

                using var stream = metaEntry.Open();
                var meta = XDocument.Load(stream, LoadOptions.None);
                var root = meta?.Root;
                if (root == null)
                {
                    return;
                }

                var title = root.Descendants(DCNs + "title").FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(title?.Value))
                {
                    result.Metadata["title"] = title.Value.Trim();
                }

                var creator = root.Descendants(DCNs + "creator").FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(creator?.Value))
                {
                    result.Metadata["author"] = creator.Value.Trim();
                }

                var created = root.Descendants(MetaNs + "creation-date").FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(created?.Value) && DateTime.TryParse(created.Value, out var createdDate))
                {
                    result.Metadata["created"] = createdDate.ToString("yyyy-MM-dd");
                }
            }
            catch
            {
                // Metadata extraction is best-effort.
            }
        }

        private static void ConvertBodyContainer(XElement container, StringBuilder markdown, ConversionContext context, int listDepth)
        {
            foreach (var element in container.Elements())
            {
                ConvertElement(element, markdown, context, listDepth);
            }
        }

        private static void ConvertElement(XElement element, StringBuilder markdown, ConversionContext context, int listDepth)
        {
            if (element.Name == TextNs + "h")
            {
                var levelAttr = element.Attribute(TextNs + "outline-level");
                var level = 1;
                if (levelAttr != null && int.TryParse(levelAttr.Value, out var parsedLevel) && parsedLevel > 0)
                {
                    level = parsedLevel;
                }

                var style = context.Styles.Resolve(element.Attribute(TextNs + "style-name")?.Value, "paragraph");
                var text = GetInlineText(element, context.Options, context.Styles, style);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    markdown.Append(new string('#', Math.Min(level, 6))).Append(' ').AppendLine(text);
                    markdown.AppendLine();
                }

                return;
            }

            if (element.Name == TextNs + "p")
            {
                AppendParagraph(element, markdown, context, listDepth);
                return;
            }

            if (element.Name == TextNs + "list")
            {
                ConvertList(element, markdown, context, listDepth);
                return;
            }

            if (element.Name == TextNs + "list-item")
            {
                // list-item should normally be handled inside ConvertList, but keep this for safety.
                ConvertBodyContainer(element, markdown, context, listDepth);
                return;
            }

            if (element.Name == TableNs + "table")
            {
                ConvertTable(element, markdown, context);
                return;
            }

            if (element.Name == DrawNs + "page")
            {
                var pageName = element.Attribute(DrawNs + "name")?.Value;
                if (!string.IsNullOrWhiteSpace(pageName))
                {
                    markdown.Append("## ").AppendLine(pageName.Trim());
                }

                context.PageOrSlide++;
                ConvertBodyContainer(element, markdown, context, listDepth);
                if (context.Options.ExtractImages)
                {
                    AppendImages(element, context, markdown);
                }

                markdown.AppendLine();
                return;
            }

            if (element.Name == DrawNs + "frame" || element.Name == DrawNs + "text-box")
            {
                ConvertBodyContainer(element, markdown, context, listDepth);
                return;
            }

            // For any other container, recurse to find text content.
            ConvertBodyContainer(element, markdown, context, listDepth);
        }

        private static void AppendParagraph(XElement paragraph, StringBuilder markdown, ConversionContext context, int listDepth)
        {
            var style = context.Styles.Resolve(paragraph.Attribute(TextNs + "style-name")?.Value, "paragraph");
            var text = GetInlineText(paragraph, context.Options, context.Styles, style);
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            if (listDepth > 0)
            {
                markdown.Append(new string(' ', listDepth * 2)).Append("- ").AppendLine(text);
            }
            else
            {
                markdown.AppendLine(text);
            }

            markdown.AppendLine();

            if (context.Options.ExtractImages)
            {
                AppendImages(paragraph, context, markdown);
            }
        }

        private static void ConvertList(XElement list, StringBuilder markdown, ConversionContext context, int listDepth)
        {
            foreach (var item in list.Elements(TextNs + "list-item"))
            {
                foreach (var child in item.Elements())
                {
                    if (child.Name == TextNs + "list")
                    {
                        ConvertList(child, markdown, context, listDepth + 1);
                    }
                    else
                    {
                        ConvertElement(child, markdown, context, listDepth + 1);
                    }
                }
            }

            if (listDepth == 0)
            {
                markdown.AppendLine();
            }
        }

        private static void ConvertTable(XElement table, StringBuilder markdown, ConversionContext context)
        {
            var rows = table.Elements(TableNs + "table-row").ToList();
            if (rows.Count == 0)
            {
                return;
            }

            var renderedRows = rows.Select(r => RenderRow(r, context)).Where(r => r.Count > 0).ToList();
            if (renderedRows.Count == 0)
            {
                return;
            }

            var columnCount = renderedRows.Max(r => r.Count);
            if (columnCount == 0)
            {
                return;
            }

            var rowUniformBold = new bool[renderedRows.Count];
            var rowUniformItalic = new bool[renderedRows.Count];
            for (int i = 0; i < renderedRows.Count; i++)
            {
                var nonEmpty = renderedRows[i].Where(c => !c.IsEmpty).ToList();
                rowUniformBold[i] = nonEmpty.Count > 0 && nonEmpty.All(c => c.AllBold);
                rowUniformItalic[i] = nonEmpty.Count > 0 && nonEmpty.All(c => c.AllItalic);
            }

            for (int i = 0; i < renderedRows.Count; i++)
            {
                var row = renderedRows[i];
                markdown.Append("| ");
                for (int j = 0; j < columnCount; j++)
                {
                    if (j > 0)
                    {
                        markdown.Append(" | ");
                    }

                    var cell = j < row.Count ? row[j] : new CellInfo();
                    var cellText = FormatCell(cell, rowUniformBold[i], rowUniformItalic[i], context);
                    markdown.Append(OpenXmlMarkdownHelper.EscapeMarkdownTableCell(cellText));
                }

                markdown.AppendLine(" |");

                if (i == 0)
                {
                    markdown.Append("|");
                    for (int j = 0; j < columnCount; j++)
                    {
                        markdown.Append(" --- |");
                    }

                    markdown.AppendLine();
                }
            }

            markdown.AppendLine();
        }

        private static List<CellInfo> RenderRow(XElement row, ConversionContext context)
        {
            return row.Elements(TableNs + "table-cell").Select(c => RenderCell(c, context)).ToList();
        }

        private static CellInfo RenderCell(XElement cell, ConversionContext context)
        {
            var cellStyleName = cell.Attribute(TableNs + "style-name")?.Value;
            var baseStyle = context.Styles.Resolve(cellStyleName, "table-cell");
            var segments = new List<TextSegment>();
            GetInlineTextCore(cell, context.Options, context.Styles, baseStyle, segments);
            return new CellInfo(MergeSegments(segments));
        }

        private static string FormatCell(CellInfo cell, bool rowUniformBold, bool rowUniformItalic, ConversionContext context)
        {
            if (cell.Segments.Count == 0)
            {
                return string.Empty;
            }

            var skipBold = rowUniformBold;
            var skipItalic = rowUniformItalic;
            return FormatSegments(
                cell.Segments,
                context.Options.PreserveFormatting,
                skipBold,
                skipItalic,
                context.IsTextDocument);
        }

        /// <summary>
        /// Recursively extracts inline text from an element, handling spans, tabs, line breaks
        /// and spaces in a way that keeps the resulting Markdown readable.
        /// </summary>
        private static string GetInlineText(XElement element, FileConversionOptions options, StyleResolver styles, TextStyle inheritedStyle = default)
        {
            var segments = new List<TextSegment>();
            GetInlineTextCore(element, options, styles, inheritedStyle, segments);
            var merged = MergeSegments(segments);
            var allBold = merged.Any() && merged.All(s => s.Style.IsBoldValue);
            var allItalic = merged.Any() && merged.All(s => s.Style.IsItalicValue);
            return FormatSegments(merged, options.PreserveFormatting, allBold, allItalic, enableRichFormatting: true);
        }

        private static void GetInlineTextCore(XNode node, FileConversionOptions options, StyleResolver styles, TextStyle inheritedStyle, List<TextSegment> segments)
        {
            if (node is XText textNode)
            {
                segments.Add(new TextSegment(textNode.Value, inheritedStyle));
                return;
            }

            if (node is not XElement element)
            {
                return;
            }

            if (element.Name == TextNs + "tab")
            {
                segments.Add(new TextSegment("\t", inheritedStyle));
                return;
            }

            if (element.Name == TextNs + "line-break")
            {
                segments.Add(new TextSegment("\n", inheritedStyle));
                return;
            }

            if (element.Name == TextNs + "s")
            {
                var countAttr = element.Attribute(TextNs + "c");
                var count = countAttr != null && int.TryParse(countAttr.Value, out var c) ? c : 1;
                segments.Add(new TextSegment(new string(' ', Math.Max(count, 1)), inheritedStyle));
                return;
            }

            if (element.Name == TextNs + "a")
            {
                if (options.PreserveHyperlinks)
                {
                    var href = element.Attribute(XlinkNs + "href")?.Value;
                    var linkTextSegments = new List<TextSegment>();
                    foreach (var child in element.Nodes())
                    {
                        GetInlineTextCore(child, options, styles, inheritedStyle, linkTextSegments);
                    }

                    var linkText = string.Concat(linkTextSegments.Select(s => s.Text)).Trim();
                    if (!string.IsNullOrEmpty(href) && !string.IsNullOrWhiteSpace(linkText))
                    {
                        segments.Add(new TextSegment($"[{linkText}]({href})", inheritedStyle));
                    }
                    else if (!string.IsNullOrWhiteSpace(linkText))
                    {
                        segments.Add(new TextSegment(linkText, inheritedStyle));
                    }

                    return;
                }
            }

            if (element.Name == TextNs + "span")
            {
                var spanStyleName = element.Attribute(TextNs + "style-name")?.Value;
                var spanStyle = styles.Resolve(spanStyleName, "text").Merge(inheritedStyle);
                foreach (var child in element.Nodes())
                {
                    GetInlineTextCore(child, options, styles, spanStyle, segments);
                }

                return;
            }

            if (element.Name == TextNs + "p" || element.Name == TextNs + "h")
            {
                var paraStyleName = element.Attribute(TextNs + "style-name")?.Value;
                var paraStyle = styles.Resolve(paraStyleName, "paragraph").Merge(inheritedStyle);
                foreach (var child in element.Nodes())
                {
                    GetInlineTextCore(child, options, styles, paraStyle, segments);
                }

                return;
            }

            // For any other container, recurse with the inherited style.
            foreach (var child in element.Nodes())
            {
                GetInlineTextCore(child, options, styles, inheritedStyle, segments);
            }
        }

        private static List<TextSegment> MergeSegments(List<TextSegment> segments)
        {
            var result = new List<TextSegment>();
            if (segments.Count == 0)
            {
                return result;
            }

            var current = segments[0];
            for (int i = 1; i < segments.Count; i++)
            {
                var segment = segments[i];
                if (current.Style.Equals(segment.Style))
                {
                    current = new TextSegment(current.Text + segment.Text, current.Style);
                }
                else
                {
                    result.Add(current);
                    current = segment;
                }
            }

            result.Add(current);
            return result;
        }

        private static string FormatSegments(
            List<TextSegment> segments,
            bool preserveFormatting,
            bool skipBold,
            bool skipItalic,
            bool enableRichFormatting)
        {
            var sb = new StringBuilder();
            foreach (var segment in segments)
            {
                var text = segment.Text;
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                var style = enableRichFormatting
                    ? segment.Style
                    : new TextStyle(segment.Style.IsBold, segment.Style.IsItalic, null, null, null, null);

                var isBold = preserveFormatting && style.IsBoldValue && !skipBold;
                var isItalic = preserveFormatting && style.IsItalicValue && !skipItalic;
                var isUnderline = preserveFormatting && style.IsUnderlineValue;
                var isStrikethrough = preserveFormatting && style.IsStrikethroughValue;
                var color = preserveFormatting ? style.Color : null;
                var backgroundColor = preserveFormatting ? style.BackgroundColor : null;

                if (!string.IsNullOrEmpty(backgroundColor))
                {
                    sb.Append($"<mark style=\"background-color: #{backgroundColor};\">");
                }

                if (!string.IsNullOrEmpty(color))
                {
                    sb.Append($"<span style=\"color: #{color};\">");
                }

                if (isUnderline)
                {
                    sb.Append("<u>");
                }

                if (isStrikethrough)
                {
                    sb.Append("<s>");
                }

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

                if (isStrikethrough)
                {
                    sb.Append("</s>");
                }

                if (isUnderline)
                {
                    sb.Append("</u>");
                }

                if (!string.IsNullOrEmpty(color))
                {
                    sb.Append("</span>");
                }

                if (!string.IsNullOrEmpty(backgroundColor))
                {
                    sb.Append("</mark>");
                }
            }

            return sb.ToString().Trim();
        }

        private static void AppendImages(XElement container, ConversionContext context, StringBuilder markdown)
        {
            foreach (var image in container.Descendants(DrawNs + "image"))
            {
                var href = image.Attribute(XlinkNs + "href")?.Value;
                if (string.IsNullOrEmpty(href))
                {
                    continue;
                }

                try
                {
                    var bytes = ReadArchiveEntry(context.Archive, href);
                    if (bytes == null || bytes.Length == 0)
                    {
                        continue;
                    }

                    context.ImageIndex++;
                    var mimeType = InferImageMimeType(href, bytes);
                    var base64Data = Convert.ToBase64String(bytes);
                    context.Result.Images.Add(VersatileImage.FromExtractedDocument(
                        base64Data: base64Data,
                        mimeType: mimeType,
                        id: $"img-{context.ImageIndex}",
                        context: context.CurrentContext,
                        pageOrSlide: context.PageOrSlide,
                        sourceDocument: null));

                    markdown.AppendLine();
                    markdown.AppendLine($"[image {context.ImageIndex}]");
                }
                catch (Exception ex)
                {
                    context.Result.Warnings.Add($"Could not extract image {href}: {ex.Message}");
                }
            }
        }

        private static byte[]? ReadArchiveEntry(ZipArchive archive, string path)
        {
            var entry = archive.GetEntry(path.Replace('\\', '/'));
            if (entry == null)
            {
                return null;
            }

            using var stream = entry.Open();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }

        private static string InferImageMimeType(string href, byte[] bytes)
        {
            if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
            {
                return "image/png";
            }

            if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            {
                return "image/jpeg";
            }

            if (bytes.Length >= 3 && bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46)
            {
                return "image/gif";
            }

            if (bytes.Length >= 2 && bytes[0] == 0x42 && bytes[1] == 0x4D)
            {
                return "image/bmp";
            }

            var ext = Path.GetExtension(href).ToLowerInvariant();
            return ext switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                ".tiff" or ".tif" => "image/tiff",
                ".svg" => "image/svg+xml",
                _ => "image/png",
            };
        }

        /// <summary>
        /// Holds mutable state for one OpenDocument conversion.
        /// </summary>
        private sealed class ConversionContext
        {
            public ConversionContext(
                ZipArchive archive,
                FileConversionOptions options,
                FileConversionResult result,
                StyleResolver styles,
                string fileType)
            {
                this.Archive = archive;
                this.Options = options;
                this.Result = result;
                this.Styles = styles;
                this.FileType = fileType;
            }

            public ZipArchive Archive { get; }

            public FileConversionOptions Options { get; }

            public FileConversionResult Result { get; }

            public StyleResolver Styles { get; }

            public string FileType { get; }

            public bool IsTextDocument => this.FileType is "odt" or "ott";

            public string CurrentContext { get; set; } = "Document body";

            public int PageOrSlide { get; set; } = 0;

            public int ImageIndex { get; set; } = 0;
        }

        /// <summary>
        /// Resolves ODF paragraph, text and table-cell styles including parent-style inheritance.
        /// </summary>
        private sealed class StyleResolver
        {
            private readonly Dictionary<(string Name, string Family), XElement> styles;
            private readonly Dictionary<(string Name, string Family), TextStyle> cache;

            public StyleResolver(IEnumerable<XElement> styleContainers)
            {
                this.styles = new Dictionary<(string, string), XElement>();
                this.cache = new Dictionary<(string, string), TextStyle>();
                foreach (var container in styleContainers)
                {
                    foreach (var style in container.Elements(StyleNs + "style"))
                    {
                        var name = style.Attribute(StyleNs + "name")?.Value;
                        var family = style.Attribute(StyleNs + "family")?.Value ?? "paragraph";
                        if (!string.IsNullOrEmpty(name))
                        {
                            this.styles[(name, family)] = style;
                        }
                    }
                }
            }

            public TextStyle Resolve(string? styleName, string family = "paragraph")
            {
                if (string.IsNullOrEmpty(styleName))
                {
                    return TextStyle.Default;
                }

                var key = (styleName, family);
                if (this.cache.TryGetValue(key, out var cached))
                {
                    return cached;
                }

                if (this.styles.TryGetValue(key, out var style))
                {
                    var resolved = this.ResolveStyle(style, new HashSet<(string, string)>());
                    this.cache[key] = resolved;
                    return resolved;
                }

                return TextStyle.Default;
            }

            private TextStyle ResolveStyle(XElement style, HashSet<(string, string)> visiting)
            {
                var name = style.Attribute(StyleNs + "name")?.Value ?? string.Empty;
                var family = style.Attribute(StyleNs + "family")?.Value ?? "paragraph";
                var key = (name, family);
                if (this.cache.TryGetValue(key, out var cached))
                {
                    return cached;
                }

                if (!visiting.Add(key))
                {
                    // Cycle detected; return default to avoid infinite recursion.
                    return TextStyle.Default;
                }

                var parentName = style.Attribute(StyleNs + "parent-style-name")?.Value;
                var parent = string.IsNullOrEmpty(parentName)
                    ? TextStyle.Default
                    : this.Resolve(parentName, family);

                var props = style.Element(StyleNs + "text-properties");
                var result = TextStyle.FromProperties(props).Merge(parent);
                this.cache[key] = result;
                return result;
            }
        }

        /// <summary>
        /// Represents the resolved inline formatting of an ODF style.
        /// Nullable booleans distinguish between unset and explicitly disabled values.
        /// </summary>
        private readonly record struct TextStyle(
            bool? IsBold,
            bool? IsItalic,
            bool? IsUnderline,
            bool? IsStrikethrough,
            string? Color,
            string? BackgroundColor)
        {
            public static readonly TextStyle Default = new(null, null, null, null, null, null);

            public bool IsBoldValue => this.IsBold ?? false;

            public bool IsItalicValue => this.IsItalic ?? false;

            public bool IsUnderlineValue => this.IsUnderline ?? false;

            public bool IsStrikethroughValue => this.IsStrikethrough ?? false;

            public static TextStyle FromProperties(XElement? properties)
            {
                if (properties == null)
                {
                    return Default;
                }

                var fontWeight = properties.Attribute(FoNs + "font-weight")?.Value;
                var fontStyle = properties.Attribute(FoNs + "font-style")?.Value;
                var underlineStyle = properties.Attribute(StyleNs + "text-underline-style")?.Value;
                var underlineType = properties.Attribute(StyleNs + "text-underline-type")?.Value;
                var lineThroughStyle = properties.Attribute(StyleNs + "text-line-through-style")?.Value;
                var color = properties.Attribute(FoNs + "color")?.Value;
                var backgroundColor = properties.Attribute(FoNs + "background-color")?.Value;

                return new TextStyle(
                    IsBold: ParseFontWeight(fontWeight),
                    IsItalic: ParseFontStyle(fontStyle),
                    IsUnderline: ParseUnderline(underlineStyle, underlineType),
                    IsStrikethrough: ParseStrikethrough(lineThroughStyle),
                    Color: NormalizeColor(color),
                    BackgroundColor: NormalizeColor(backgroundColor));
            }

            /// <summary>
            /// Merges another style over this one. Values present in <paramref name="other"/> take precedence.
            /// </summary>
            public TextStyle Merge(TextStyle other)
            {
                return new TextStyle(
                    other.IsBold ?? this.IsBold,
                    other.IsItalic ?? this.IsItalic,
                    other.IsUnderline ?? this.IsUnderline,
                    other.IsStrikethrough ?? this.IsStrikethrough,
                    other.Color ?? this.Color,
                    other.BackgroundColor ?? this.BackgroundColor);
            }

            private static bool? ParseFontWeight(string? value)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return null;
                }

                if (string.Equals(value, "bold", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, "bolder", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (string.Equals(value, "normal", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (int.TryParse(value, out var numeric) && numeric >= 600)
                {
                    return true;
                }

                return null;
            }

            private static bool? ParseFontStyle(string? value)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return null;
                }

                if (string.Equals(value, "italic", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, "oblique", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (string.Equals(value, "normal", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                return null;
            }

            private static bool? ParseUnderline(string? styleValue, string? typeValue)
            {
                var styleSet = !string.IsNullOrWhiteSpace(styleValue);
                var typeSet = !string.IsNullOrWhiteSpace(typeValue);
                if (!styleSet && !typeSet)
                {
                    return null;
                }

                if (styleSet && !string.Equals(styleValue, "none", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (typeSet && !string.Equals(typeValue, "none", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (styleSet && string.Equals(styleValue, "none", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (typeSet && string.Equals(typeValue, "none", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                return null;
            }

            private static bool? ParseStrikethrough(string? value)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return null;
                }

                return !string.Equals(value, "none", StringComparison.OrdinalIgnoreCase);
            }

            private static string? NormalizeColor(string? value)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return null;
                }

                var trimmed = value.Trim();
                if (string.Equals(trimmed, "auto", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(trimmed, "transparent", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                if (trimmed.StartsWith("#", StringComparison.Ordinal))
                {
                    trimmed = trimmed.Substring(1);
                }

                if (trimmed.Length != 6)
                {
                    return null;
                }

                if (trimmed.Equals("000000", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.Equals("FFFFFF", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                return trimmed.ToUpperInvariant();
            }
        }

        /// <summary>
        /// A contiguous run of text with an associated ODF text style.
        /// </summary>
        private readonly record struct TextSegment(string Text, TextStyle Style);

        /// <summary>
        /// Rendering information for a single table cell.
        /// </summary>
        private sealed class CellInfo
        {
            public CellInfo()
                : this(new List<TextSegment>())
            {
            }

            public CellInfo(List<TextSegment> segments)
            {
                this.Segments = segments;
            }

            public List<TextSegment> Segments { get; }

            public bool IsEmpty => this.Segments.Count == 0 ||
                string.IsNullOrWhiteSpace(string.Concat(this.Segments.Select(s => s.Text)));

            public bool AllBold => this.Segments.Count > 0 && this.Segments.All(s => s.Style.IsBoldValue);

            public bool AllItalic => this.Segments.Count > 0 && this.Segments.All(s => s.Style.IsItalicValue);
        }
    }
}
