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

namespace SmartHopper.Core.Grasshopper.Tests.Converters
{
    using System;
    using System.IO;
    using System.IO.Compression;
    using System.Text;
    using System.Threading.Tasks;
    using System.Xml.Linq;
    using SmartHopper.Core.Grasshopper.Converters;
    using SmartHopper.Core.Grasshopper.Converters.Formats;
    using Xunit;

    /// <summary>
    /// Tests for the <see cref="OpenDocumentConverter"/> covering ODT, ODS, and ODP formatting
    /// and image extraction.
    /// </summary>
    public class OpenDocumentConverterTests : IDisposable
    {
        private readonly string _tempDir;

        public OpenDocumentConverterTests()
        {
            this._tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(this._tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(this._tempDir))
            {
                Directory.Delete(this._tempDir, true);
            }
        }

        #region ODT text formatting

        [Fact(DisplayName = "OdtConverter_BoldItalic_UniformSkip")]
        public async Task OdtConverter_BoldItalic_UniformSkip()
        {
            var filePath = Path.Combine(this._tempDir, "bold-italic.odt");
            var content = new XElement(
                OfficeNs + "document-content",
                new XAttribute(XNamespace.Xmlns + "office", OfficeNs.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "text", TextNs.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "style", StyleNs.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "fo", FoNs.NamespaceName),
                new XElement(
                    OfficeNs + "automatic-styles",
                    ParagraphStyle("P_Bold", bold: true),
                    TextStyle("T_Italic", italic: true)),
                new XElement(
                    OfficeNs + "body",
                    new XElement(
                        OfficeNs + "text",
                        Paragraph("P_Bold", "All bold paragraph"),
                        new XElement(
                            TextNs + "p",
                            new XText("normal "),
                            TextSpan("T_Italic", "italic"),
                            new XText(" text")))));

            CreateOdfPackage(filePath, content);

            var result = await new OpenDocumentConverter().ConvertAsync(filePath, new FileConversionOptions());

            Assert.True(result.IsSuccess);
            Assert.Contains("All bold paragraph", result.MarkdownContent);
            Assert.DoesNotContain("**All bold paragraph**", result.MarkdownContent);
            Assert.Contains("normal *italic* text", result.MarkdownContent);
        }

        [Fact(DisplayName = "OdtConverter_ColorsAndHighlight")]
        public async Task OdtConverter_ColorsAndHighlight()
        {
            var filePath = Path.Combine(this._tempDir, "colors.odt");
            var content = new XElement(
                OfficeNs + "document-content",
                new XAttribute(XNamespace.Xmlns + "office", OfficeNs.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "text", TextNs.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "style", StyleNs.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "fo", FoNs.NamespaceName),
                new XElement(
                    OfficeNs + "automatic-styles",
                    TextStyle("T_Red", color: "#FF0000"),
                    TextStyle("T_Yellow", backgroundColor: "#FFFF00")),
                new XElement(
                    OfficeNs + "body",
                    new XElement(
                        OfficeNs + "text",
                        new XElement(
                            TextNs + "p",
                            new XText("Some "),
                            TextSpan("T_Red", "red"),
                            new XText(" and "),
                            TextSpan("T_Yellow", "highlight"),
                            new XText(" text.")))));

            CreateOdfPackage(filePath, content);

            var result = await new OpenDocumentConverter().ConvertAsync(filePath, new FileConversionOptions());

            Assert.True(result.IsSuccess);
            Assert.Contains("Some <span style=\"color: #FF0000;\">red</span>", result.MarkdownContent);
            Assert.Contains("<mark style=\"background-color: #FFFF00;\">highlight</mark>", result.MarkdownContent);
        }

        [Fact(DisplayName = "OdtConverter_UnderlineAndStrikethrough")]
        public async Task OdtConverter_UnderlineAndStrikethrough()
        {
            var filePath = Path.Combine(this._tempDir, "underline-strike.odt");
            var content = new XElement(
                OfficeNs + "document-content",
                new XAttribute(XNamespace.Xmlns + "office", OfficeNs.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "text", TextNs.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "style", StyleNs.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "fo", FoNs.NamespaceName),
                new XElement(
                    OfficeNs + "automatic-styles",
                    TextStyle("T_Underline", underline: true),
                    TextStyle("T_Strike", strikethrough: true)),
                new XElement(
                    OfficeNs + "body",
                    new XElement(
                        OfficeNs + "text",
                        new XElement(
                            TextNs + "p",
                            TextSpan("T_Underline", "underlined"),
                            new XText(" and "),
                            TextSpan("T_Strike", "struck")))));

            CreateOdfPackage(filePath, content);

            var result = await new OpenDocumentConverter().ConvertAsync(filePath, new FileConversionOptions());

            Assert.True(result.IsSuccess);
            Assert.Contains("<u>underlined</u>", result.MarkdownContent);
            Assert.Contains("<s>struck</s>", result.MarkdownContent);
        }

        [Fact(DisplayName = "OdtConverter_Hyperlink")]
        public async Task OdtConverter_Hyperlink()
        {
            var filePath = Path.Combine(this._tempDir, "hyperlink.odt");
            var content = new XElement(
                OfficeNs + "document-content",
                new XAttribute(XNamespace.Xmlns + "office", OfficeNs.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "text", TextNs.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "xlink", XlinkNs.NamespaceName),
                new XElement(
                    OfficeNs + "body",
                    new XElement(
                        OfficeNs + "text",
                        new XElement(
                            TextNs + "p",
                            new XElement(
                                TextNs + "a",
                                new XAttribute(XlinkNs + "href", "https://example.com"),
                                new XText("click here"))))));

            CreateOdfPackage(filePath, content);

            var result = await new OpenDocumentConverter().ConvertAsync(filePath, new FileConversionOptions());

            Assert.True(result.IsSuccess);
            Assert.Contains("[click here](https://example.com)", result.MarkdownContent);
        }

        [Fact(DisplayName = "OdtConverter_PreserveFormattingOff")]
        public async Task OdtConverter_PreserveFormattingOff()
        {
            var filePath = Path.Combine(this._tempDir, "no-format.odt");
            var content = new XElement(
                OfficeNs + "document-content",
                new XAttribute(XNamespace.Xmlns + "office", OfficeNs.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "text", TextNs.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "style", StyleNs.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "fo", FoNs.NamespaceName),
                new XElement(
                    OfficeNs + "automatic-styles",
                    TextStyle("T_Bold", bold: true)),
                new XElement(
                    OfficeNs + "body",
                    new XElement(
                        OfficeNs + "text",
                        new XElement(
                            TextNs + "p",
                            TextSpan("T_Bold", "plain")))));

            CreateOdfPackage(filePath, content);

            var result = await new OpenDocumentConverter().ConvertAsync(filePath, new FileConversionOptions { PreserveFormatting = false });

            Assert.True(result.IsSuccess);
            Assert.Contains("plain", result.MarkdownContent);
            Assert.DoesNotContain("**", result.MarkdownContent);
        }

        #endregion

        #region ODS table formatting

        [Fact(DisplayName = "OdsConverter_TableCellFormatting_UniformSkip")]
        public async Task OdsConverter_TableCellFormatting_UniformSkip()
        {
            var filePath = Path.Combine(this._tempDir, "table.ods");
            var content = new XElement(
                OfficeNs + "document-content",
                new XAttribute(XNamespace.Xmlns + "office", OfficeNs.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "table", TableNs.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "text", TextNs.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "style", StyleNs.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "fo", FoNs.NamespaceName),
                new XElement(
                    OfficeNs + "automatic-styles",
                    TableCellStyle("T_Bold", bold: true)),
                new XElement(
                    OfficeNs + "body",
                    new XElement(
                        OfficeNs + "spreadsheet",
                        new XElement(
                            TableNs + "table",
                            new XElement(
                                TableNs + "table-row",
                                TableCell("Header A", styleName: "T_Bold"),
                                TableCell("Header B", styleName: "T_Bold")),
                            new XElement(
                                TableNs + "table-row",
                                TableCell("data1"),
                                TableCell("data2", styleName: "T_Bold"))))));

            CreateOdfPackage(filePath, content);

            var result = await new OpenDocumentConverter().ConvertAsync(filePath, new FileConversionOptions());

            Assert.True(result.IsSuccess);
            Assert.Contains("Header A", result.MarkdownContent);
            Assert.Contains("Header B", result.MarkdownContent);
            Assert.DoesNotContain("**Header A**", result.MarkdownContent);
            Assert.Contains("**data2**", result.MarkdownContent);
        }

        #endregion

        #region Image extraction

        [Fact(DisplayName = "OdtConverter_ImageExtraction")]
        public async Task OdtConverter_ImageExtraction()
        {
            var filePath = Path.Combine(this._tempDir, "image.odt");
            var imageBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00 };
            var content = new XElement(
                OfficeNs + "document-content",
                new XAttribute(XNamespace.Xmlns + "office", OfficeNs.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "text", TextNs.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "draw", DrawNs.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "xlink", XlinkNs.NamespaceName),
                new XElement(
                    OfficeNs + "body",
                    new XElement(
                        OfficeNs + "text",
                        new XElement(
                            TextNs + "p",
                            new XText("Before image."),
                            new XElement(
                                DrawNs + "frame",
                                new XElement(
                                    DrawNs + "image",
                                    new XAttribute(XlinkNs + "href", "Pictures/test.png"))),
                            new XText("After image.")))));

            CreateOdfPackage(filePath, content, new[] { ("Pictures/test.png", imageBytes) });

            var result = await new OpenDocumentConverter().ConvertAsync(filePath, new FileConversionOptions { ExtractImages = true });

            Assert.True(result.IsSuccess);
            Assert.Single(result.Images);
            Assert.Contains("[image 1]", result.MarkdownContent);
        }

        #endregion

        #region Failure paths

        [Fact(DisplayName = "OdtConverter_MissingContentXml_ReturnsFailure")]
        public async Task OdtConverter_MissingContentXml_ReturnsFailure()
        {
            var filePath = Path.Combine(this._tempDir, "broken.odt");
            using (var archive = ZipFile.Open(filePath, ZipArchiveMode.Create))
            {
                var meta = archive.CreateEntry("meta.xml");
                using (var stream = meta.Open())
                {
                    var bytes = Encoding.UTF8.GetBytes("<office:document-meta xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\"/>");
                    stream.Write(bytes, 0, bytes.Length);
                }
            }

            var result = await new OpenDocumentConverter().ConvertAsync(filePath, new FileConversionOptions());

            Assert.False(result.IsSuccess);
            Assert.Contains("Missing content.xml", result.Warnings[0]);
        }

        #endregion

        #region Test helpers

        private static readonly XNamespace OfficeNs = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
        private static readonly XNamespace TextNs = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
        private static readonly XNamespace TableNs = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
        private static readonly XNamespace DrawNs = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
        private static readonly XNamespace StyleNs = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
        private static readonly XNamespace FoNs = "urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0";
        private static readonly XNamespace XlinkNs = "http://www.w3.org/1999/xlink";

        private static void CreateOdfPackage(
            string filePath,
            XElement contentXml,
            (string path, byte[] data)[]? embeddedFiles = null)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            using var archive = ZipFile.Open(filePath, ZipArchiveMode.Create);

            // Required by the ODF package format.
            var mimetype = archive.CreateEntry("mimetype", CompressionLevel.NoCompression);
            using (var stream = mimetype.Open())
            {
                var bytes = Encoding.UTF8.GetBytes("application/vnd.oasis.opendocument.text");
                stream.Write(bytes, 0, bytes.Length);
            }

            AddXmlEntry(archive, "content.xml", contentXml);

            if (embeddedFiles != null)
            {
                foreach (var (path, data) in embeddedFiles)
                {
                    var entry = archive.CreateEntry(path, CompressionLevel.NoCompression);
                    using var stream = entry.Open();
                    stream.Write(data, 0, data.Length);
                }
            }
        }

        private static void AddXmlEntry(ZipArchive archive, string entryName, XElement element)
        {
            var entry = archive.CreateEntry(entryName);
            using var stream = entry.Open();
            element.Save(stream, SaveOptions.OmitDuplicateNamespaces);
        }

        private static XElement ParagraphStyle(string name, bool bold = false, bool italic = false)
        {
            return new XElement(
                StyleNs + "style",
                new XAttribute(StyleNs + "name", name),
                new XAttribute(StyleNs + "family", "paragraph"),
                TextProperties(bold, italic, null, null));
        }

        private static XElement TextStyle(
            string name,
            bool bold = false,
            bool italic = false,
            string? color = null,
            string? backgroundColor = null,
            bool underline = false,
            bool strikethrough = false)
        {
            return new XElement(
                StyleNs + "style",
                new XAttribute(StyleNs + "name", name),
                new XAttribute(StyleNs + "family", "text"),
                TextProperties(bold, italic, color, backgroundColor, underline, strikethrough));
        }

        private static XElement TableCellStyle(string name, bool bold = false, bool italic = false)
        {
            return new XElement(
                StyleNs + "style",
                new XAttribute(StyleNs + "name", name),
                new XAttribute(StyleNs + "family", "table-cell"),
                TextProperties(bold, italic, null, null));
        }

        private static XElement TextProperties(
            bool bold,
            bool italic,
            string? color,
            string? backgroundColor,
            bool underline = false,
            bool strikethrough = false)
        {
            var props = new XElement(StyleNs + "text-properties");
            if (bold)
            {
                props.SetAttributeValue(FoNs + "font-weight", "bold");
            }

            if (italic)
            {
                props.SetAttributeValue(FoNs + "font-style", "italic");
            }

            if (underline)
            {
                props.SetAttributeValue(StyleNs + "text-underline-style", "solid");
            }

            if (strikethrough)
            {
                props.SetAttributeValue(StyleNs + "text-line-through-style", "solid");
            }

            if (!string.IsNullOrEmpty(color))
            {
                props.SetAttributeValue(FoNs + "color", color);
            }

            if (!string.IsNullOrEmpty(backgroundColor))
            {
                props.SetAttributeValue(FoNs + "background-color", backgroundColor);
            }

            return props;
        }

        private static XElement Paragraph(string styleName, string text)
        {
            return new XElement(
                TextNs + "p",
                new XAttribute(TextNs + "style-name", styleName),
                new XText(text));
        }

        private static XElement TextSpan(string styleName, string text)
        {
            return new XElement(
                TextNs + "span",
                new XAttribute(TextNs + "style-name", styleName),
                new XText(text));
        }

        private static XElement TableCell(string text, string? styleName = null)
        {
            var cell = new XElement(
                TableNs + "table-cell",
                new XElement(
                    TextNs + "p",
                    new XText(text)));
            if (!string.IsNullOrEmpty(styleName))
            {
                cell.SetAttributeValue(TableNs + "style-name", styleName);
            }

            return cell;
        }

        #endregion
    }
}
