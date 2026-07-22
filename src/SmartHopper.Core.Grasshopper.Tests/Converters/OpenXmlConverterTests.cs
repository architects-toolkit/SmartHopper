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
    using System.Linq;
    using System.Threading.Tasks;
    using DocumentFormat.OpenXml;
    using DocumentFormat.OpenXml.Packaging;
    using SmartHopper.Core.Grasshopper.Converters;
    using SmartHopper.Core.Grasshopper.Converters.Formats;
    using A = DocumentFormat.OpenXml.Drawing;
    using M = DocumentFormat.OpenXml.Math;
    using P = DocumentFormat.OpenXml.Presentation;
    using S = DocumentFormat.OpenXml.Spreadsheet;
    using V = DocumentFormat.OpenXml.Vml;
    using W = DocumentFormat.OpenXml.Wordprocessing;
    using Xunit;

    /// <summary>
    /// Tests for DOCX, PPTX, and XLSX converters and the shared <see cref="OpenXmlMarkdownHelper"/>.
    /// </summary>
    public class OpenXmlConverterTests : IDisposable
    {
        private readonly string _tempDir;

        public OpenXmlConverterTests()
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

        #region DOCX

        [Fact(DisplayName = "DocxConverter_BoldItalic_UniformSkip")]
        public async Task DocxConverter_BoldItalic_UniformSkip()
        {
            var filePath = Path.Combine(this._tempDir, "uniform-bold.docx");
            using (var doc = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document))
            {
                var mainPart = doc.AddMainDocumentPart();
                mainPart.Document = new W.Document(new W.Body(
                    new W.Paragraph(new W.Run(
                        new W.RunProperties(new W.Bold()),
                        new W.Text("All bold paragraph"))),
                    new W.Paragraph(new W.Run(
                        new W.RunProperties(new W.Italic()),
                        new W.Text("All italic paragraph"))),
                    new W.Paragraph(
                        new W.Run(new W.Text("normal ") { Space = SpaceProcessingModeValues.Preserve }),
                        new W.Run(
                            new W.RunProperties(new W.Bold()),
                            new W.Text("bold")),
                        new W.Run(new W.Text(" text") { Space = SpaceProcessingModeValues.Preserve }))));
            }

            var result = await new DocxConverter().ConvertAsync(filePath, new FileConversionOptions());
            Assert.True(result.IsSuccess);
            Assert.DoesNotContain("**All bold paragraph**", result.MarkdownContent);
            Assert.Contains("All bold paragraph", result.MarkdownContent);
            Assert.DoesNotContain("*All italic paragraph*", result.MarkdownContent);
            Assert.Contains("All italic paragraph", result.MarkdownContent);
            Assert.Contains("normal **bold** text", result.MarkdownContent);
        }

        [Fact(DisplayName = "DocxConverter_BoldItalic_ExplicitOff")]
        public async Task DocxConverter_BoldItalic_ExplicitOff()
        {
            var filePath = Path.Combine(this._tempDir, "bold-off.docx");
            using (var doc = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document))
            {
                var mainPart = doc.AddMainDocumentPart();
                mainPart.Document = new W.Document(new W.Body(
                    new W.Paragraph(new W.Run(
                        new W.RunProperties(new W.Bold { Val = false }),
                        new W.Text("Not bold")))));
            }

            var result = await new DocxConverter().ConvertAsync(filePath, new FileConversionOptions());
            Assert.True(result.IsSuccess);
            Assert.DoesNotContain("**", result.MarkdownContent);
        }

        [Fact(DisplayName = "DocxConverter_Hyperlink")]
        public async Task DocxConverter_Hyperlink()
        {
            var filePath = Path.Combine(this._tempDir, "hyperlink.docx");
            using (var doc = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document))
            {
                var mainPart = doc.AddMainDocumentPart();
                mainPart.Document = new W.Document(new W.Body());
                var rel = mainPart.AddHyperlinkRelationship(new Uri("https://example.com"), true);
                mainPart.Document.Body.Append(new W.Paragraph(new W.Hyperlink(
                    new W.Run(new W.Text("click here")))
                {
                    Id = rel.Id,
                }));
            }

            var result = await new DocxConverter().ConvertAsync(filePath, new FileConversionOptions());
            Assert.True(result.IsSuccess);
            Assert.Contains("[click here](https://example.com/)", result.MarkdownContent);
        }

        [Fact(DisplayName = "DocxConverter_Footnote")]
        public async Task DocxConverter_Footnote()
        {
            var filePath = Path.Combine(this._tempDir, "footnote.docx");
            using (var doc = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document))
            {
                var mainPart = doc.AddMainDocumentPart();
                mainPart.Document = new W.Document(new W.Body(
                    new W.Paragraph(
                        new W.Run(new W.Text("Hello") { Space = SpaceProcessingModeValues.Preserve }),
                        new W.Run(new W.FootnoteReference { Id = 2 }),
                        new W.Run(new W.Text(".") { Space = SpaceProcessingModeValues.Preserve }))));

                var footnotesPart = mainPart.AddNewPart<FootnotesPart>();
                footnotesPart.Footnotes = new W.Footnotes(
                    new W.Footnote(new W.Paragraph(new W.Run(new W.Text("Footnote text.")))) { Id = 2 });
            }

            var result = await new DocxConverter().ConvertAsync(filePath, new FileConversionOptions());
            Assert.True(result.IsSuccess);
            Assert.Contains("Hello[^fn2].", result.MarkdownContent);
            Assert.Contains("[^fn2]: Footnote text.", result.MarkdownContent);
        }

        [Fact(DisplayName = "DocxConverter_Endnote")]
        public async Task DocxConverter_Endnote()
        {
            var filePath = Path.Combine(this._tempDir, "endnote.docx");
            using (var doc = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document))
            {
                var mainPart = doc.AddMainDocumentPart();
                mainPart.Document = new W.Document(new W.Body(
                    new W.Paragraph(
                        new W.Run(new W.Text("Text") { Space = SpaceProcessingModeValues.Preserve }),
                        new W.Run(new W.EndnoteReference { Id = 2 }))));

                var endnotesPart = mainPart.AddNewPart<EndnotesPart>();
                endnotesPart.Endnotes = new W.Endnotes(
                    new W.Endnote(new W.Paragraph(new W.Run(new W.Text("Endnote text.")))) { Id = 2 });
            }

            var result = await new DocxConverter().ConvertAsync(filePath, new FileConversionOptions());
            Assert.True(result.IsSuccess);
            Assert.Contains("Text[^en2]", result.MarkdownContent);
            Assert.Contains("[^en2]: Endnote text.", result.MarkdownContent);
        }

        [Fact(DisplayName = "DocxConverter_RemoveHeadersFooters")]
        public async Task DocxConverter_RemoveHeadersFooters()
        {
            var filePath = Path.Combine(this._tempDir, "headers.docx");
            using (var doc = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document))
            {
                var mainPart = doc.AddMainDocumentPart();
                var headerPart = mainPart.AddNewPart<HeaderPart>();
                headerPart.Header = new W.Header(new W.Paragraph(new W.Run(new W.Text("Header text"))));
                mainPart.Document = new W.Document(new W.Body(
                    new W.Paragraph(new W.Run(new W.Text("Body text"))),
                    new W.SectionProperties(new W.HeaderReference { Type = W.HeaderFooterValues.Default, Id = mainPart.GetIdOfPart(headerPart) })));
            }

            var withHeaders = await new DocxConverter().ConvertAsync(filePath, new FileConversionOptions { RemoveHeadersFooters = false });
            Assert.True(withHeaders.IsSuccess);
            Assert.Contains("Header text", withHeaders.MarkdownContent);
            Assert.Contains("Body text", withHeaders.MarkdownContent);

            var withoutHeaders = await new DocxConverter().ConvertAsync(filePath, new FileConversionOptions { RemoveHeadersFooters = true });
            Assert.True(withoutHeaders.IsSuccess);
            Assert.DoesNotContain("Header text", withoutHeaders.MarkdownContent);
            Assert.Contains("Body text", withoutHeaders.MarkdownContent);
        }

        [Fact(DisplayName = "DocxConverter_OrderedList")]
        public async Task DocxConverter_OrderedList()
        {
            var filePath = Path.Combine(this._tempDir, "ordered.docx");
            using (var doc = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document))
            {
                var mainPart = doc.AddMainDocumentPart();
                var numberingPart = mainPart.AddNewPart<NumberingDefinitionsPart>();
                numberingPart.Numbering = new W.Numbering(
                    new W.AbstractNum(
                        new W.Level(
                            new W.NumberingFormat { Val = W.NumberFormatValues.Decimal },
                            new W.LevelText { Val = "%1." })
                        { LevelIndex = 0 })
                    { AbstractNumberId = 1 },
                    new W.NumberingInstance(new W.AbstractNumId { Val = 1 }) { NumberID = 1 });

                mainPart.Document = new W.Document(new W.Body(
                    new W.Paragraph(
                        new W.ParagraphProperties(new W.NumberingProperties(
                            new W.NumberingId { Val = 1 },
                            new W.NumberingLevelReference { Val = 0 })),
                        new W.Run(new W.Text("First"))),
                    new W.Paragraph(
                        new W.ParagraphProperties(new W.NumberingProperties(
                            new W.NumberingId { Val = 1 },
                            new W.NumberingLevelReference { Val = 0 })),
                        new W.Run(new W.Text("Second")))));
            }

            var result = await new DocxConverter().ConvertAsync(filePath, new FileConversionOptions());
            Assert.True(result.IsSuccess);
            Assert.Contains("1. First", result.MarkdownContent);
            Assert.Contains("2. Second", result.MarkdownContent);
        }

        [Fact(DisplayName = "DocxConverter_TableCellFormatting")]
        public async Task DocxConverter_TableCellFormatting()
        {
            var filePath = Path.Combine(this._tempDir, "table.docx");
            using (var doc = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document))
            {
                var mainPart = doc.AddMainDocumentPart();
                mainPart.Document = new W.Document(new W.Body(
                    new W.Table(
                        new W.TableRow(
                            new W.TableCell(new W.Paragraph(new W.Run(new W.Text("Header")))),
                            new W.TableCell(new W.Paragraph(new W.Run(new W.Text("Bold"))))),
                        new W.TableRow(
                            new W.TableCell(new W.Paragraph(new W.Run(new W.Text("A")))),
                            new W.TableCell(new W.Paragraph(
                                new W.Run(new W.Text { Text = "plain " }),
                                new W.Run(
                                    new W.RunProperties(new W.Bold()),
                                    new W.Text { Text = "B" })))))));
            }

            var result = await new DocxConverter().ConvertAsync(filePath, new FileConversionOptions());
            Assert.True(result.IsSuccess);
            Assert.Contains("| Bold |", result.MarkdownContent);
            Assert.Contains("| plain **B** |", result.MarkdownContent);
        }

        [Fact(DisplayName = "DocxConverter_Math")]
        public async Task DocxConverter_Math()
        {
            var filePath = Path.Combine(this._tempDir, "math.docx");
            using (var doc = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document))
            {
                var mainPart = doc.AddMainDocumentPart();
                var math = new M.OfficeMath(
                    new M.Run(new M.Text("x")),
                    new M.Superscript(
                        new M.Run(new M.Text("2")),
                        new M.Base(new M.Run(new M.Text("y")))));
                mainPart.Document = new W.Document(new W.Body(
                    new W.Paragraph(new W.Run(new W.Text("Equation: ") { Space = SpaceProcessingModeValues.Preserve }),
                    math)));
            }

            var result = await new DocxConverter().ConvertAsync(filePath, new FileConversionOptions { PreserveMath = true });
            Assert.True(result.IsSuccess);
            Assert.Contains("$", result.MarkdownContent);
            Assert.Contains("x", result.MarkdownContent);
            Assert.Contains("y", result.MarkdownContent);
        }

        [Fact(DisplayName = "DocxConverter_TextBoxOnly")]
        public async Task DocxConverter_TextBoxOnly()
        {
            var filePath = Path.Combine(this._tempDir, "textbox-only.docx");
            using (var doc = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document))
            {
                var mainPart = doc.AddMainDocumentPart();
                mainPart.Document = new W.Document(new W.Body(
                    CreateTextBoxParagraph("Shape text inside a text box.")));
            }

            var result = await new DocxConverter().ConvertAsync(filePath, new FileConversionOptions());
            Assert.True(result.IsSuccess);
            Assert.Contains("> **Text extracted from shapes/text boxes**", result.MarkdownContent);
            Assert.Contains("> Shape text inside a text box.", result.MarkdownContent);
        }

        [Fact(DisplayName = "DocxConverter_TextBoxAndBodyText")]
        public async Task DocxConverter_TextBoxAndBodyText()
        {
            var filePath = Path.Combine(this._tempDir, "textbox-and-body.docx");
            using (var doc = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document))
            {
                var mainPart = doc.AddMainDocumentPart();
                mainPart.Document = new W.Document(new W.Body(
                    new W.Paragraph(new W.Run(new W.Text("Regular body text."))),
                    CreateTextBoxParagraph("Shape text inside a text box.")));
            }

            var result = await new DocxConverter().ConvertAsync(filePath, new FileConversionOptions());
            Assert.True(result.IsSuccess);
            Assert.Contains("Regular body text.", result.MarkdownContent);
            Assert.Contains("> **Text extracted from shapes/text boxes**", result.MarkdownContent);
            Assert.Contains("> Shape text inside a text box.", result.MarkdownContent);
        }

        private static W.Paragraph CreateTextBoxParagraph(string text)
        {
            return new W.Paragraph(
                new W.Run(
                    new W.Picture(
                        new V.Shape(
                            new V.TextBox(
                                new W.TextBoxContent(
                                    new W.Paragraph(
                                        new W.Run(new W.Text(text))))))
                        {
                            Id = "TextBox1",
                            Style = "width:100pt;height:50pt",
                        })));
        }

        #endregion

        #region PPTX

        [Fact(DisplayName = "PptxConverter_BoldItalic")]
        public async Task PptxConverter_BoldItalic()
        {
            var filePath = Path.Combine(this._tempDir, "formatting.pptx");
            using (var doc = PresentationDocument.Create(filePath, PresentationDocumentType.Presentation))
            {
                var presentationPart = doc.AddPresentationPart();
                presentationPart.Presentation = new P.Presentation(new P.SlideIdList(), new P.SlideSize());
                var slidePart = presentationPart.AddNewPart<SlidePart>();
                slidePart.Slide = new P.Slide(
                    new P.CommonSlideData(
                        new P.ShapeTree(
                            new P.Shape(
                                new P.NonVisualShapeProperties(
                                    new A.NonVisualDrawingProperties { Id = 2, Name = "Content" },
                                    new P.ApplicationNonVisualDrawingProperties(),
                                    new P.ShapeStyle()),
                                new P.ShapeProperties(),
                                new P.TextBody(
                                    new A.BodyProperties(),
                                    new A.ListStyle(),
                                    new A.Paragraph(
                                        new A.Run(
                                            new A.Text { Text = "Plain " }),
                                        new A.Run(
                                            new A.RunProperties { Bold = true },
                                            new A.Text { Text = "Bold" })),
                                    new A.Paragraph(
                                        new A.Run(
                                            new A.Text { Text = "plain" }),
                                        new A.Run(
                                            new A.RunProperties { Bold = true },
                                            new A.Text { Text = "bold" })),
                                    new A.Paragraph(
                                        new A.Run(
                                            new A.RunProperties { Italic = true },
                                            new A.Text { Text = "Italic" }),
                                        new A.Run(
                                            new A.Text { Text = " plain" })))))));

                var slideId = new P.SlideId { RelationshipId = presentationPart.GetIdOfPart(slidePart) };
                presentationPart.Presentation.SlideIdList.Append(slideId);
            }

            var result = await new PptxConverter().ConvertAsync(filePath, new FileConversionOptions());
            Assert.True(result.IsSuccess);
            Assert.Contains("**Bold**", result.MarkdownContent);
            Assert.Contains("plain**bold**", result.MarkdownContent);
            Assert.Contains("*Italic*", result.MarkdownContent);
        }

        [Fact(DisplayName = "PptxConverter_BoldItalic_Disabled_WhenPreserveFormattingFalse")]
        public async Task PptxConverter_BoldItalic_Disabled_WhenPreserveFormattingFalse()
        {
            var filePath = Path.Combine(this._tempDir, "no-formatting.pptx");
            using (var doc = PresentationDocument.Create(filePath, PresentationDocumentType.Presentation))
            {
                var presentationPart = doc.AddPresentationPart();
                presentationPart.Presentation = new P.Presentation(new P.SlideIdList(), new P.SlideSize());
                var slidePart = presentationPart.AddNewPart<SlidePart>();
                slidePart.Slide = new P.Slide(
                    new P.CommonSlideData(
                        new P.ShapeTree(
                            new P.Shape(
                                new P.NonVisualShapeProperties(
                                    new A.NonVisualDrawingProperties { Id = 2, Name = "Content" },
                                    new P.ApplicationNonVisualDrawingProperties(),
                                    new P.ShapeStyle()),
                                new P.ShapeProperties(),
                                new P.TextBody(
                                    new A.BodyProperties(),
                                    new A.ListStyle(),
                                    new A.Paragraph(
                                        new A.Run(
                                            new A.Text { Text = "Plain " }),
                                        new A.Run(
                                            new A.RunProperties { Bold = true },
                                            new A.Text { Text = "Bold" })),
                                    new A.Paragraph(
                                        new A.Run(
                                            new A.RunProperties { Italic = true },
                                            new A.Text { Text = "Italic" })))))));

                var slideId = new P.SlideId { RelationshipId = presentationPart.GetIdOfPart(slidePart) };
                presentationPart.Presentation.SlideIdList.Append(slideId);
            }

            var result = await new PptxConverter().ConvertAsync(filePath, new FileConversionOptions { PreserveFormatting = false });
            Assert.True(result.IsSuccess);
            Assert.Contains("Plain Bold", result.MarkdownContent);
            Assert.Contains("Italic", result.MarkdownContent);
            Assert.DoesNotContain("**", result.MarkdownContent);
            Assert.DoesNotContain("*Italic*", result.MarkdownContent);
        }

        [Fact(DisplayName = "PptxConverter_Table")]
        public async Task PptxConverter_Table()
        {
            var filePath = Path.Combine(this._tempDir, "table.pptx");
            using (var doc = PresentationDocument.Create(filePath, PresentationDocumentType.Presentation))
            {
                var presentationPart = doc.AddPresentationPart();
                presentationPart.Presentation = new P.Presentation(new P.SlideIdList(), new P.SlideSize());
                var slidePart = presentationPart.AddNewPart<SlidePart>();
                var table = new A.Table(
                    new A.TableProperties(),
                    new A.TableGrid(new A.GridColumn(), new A.GridColumn()),
                    new A.TableRow(
                        new A.TableCell(
                            new A.TextBody(new A.BodyProperties(), new A.ListStyle(), new A.Paragraph(new A.Run(new A.Text { Text = "H1" })))),
                        new A.TableCell(
                            new A.TextBody(new A.BodyProperties(), new A.ListStyle(), new A.Paragraph(new A.Run(new A.Text { Text = "H2" }))))),
                    new A.TableRow(
                        new A.TableCell(
                            new A.TextBody(new A.BodyProperties(), new A.ListStyle(), new A.Paragraph(new A.Run(new A.Text { Text = "A" })))),
                        new A.TableCell(
                            new A.TextBody(new A.BodyProperties(), new A.ListStyle(), new A.Paragraph(new A.Run(new A.Text { Text = "B" }))))));

                var graphicData = new A.GraphicData { Uri = "http://schemas.openxmlformats.org/drawingml/2006/table" };
                graphicData.Append(table.CloneNode(true));
                var graphicFrame = new P.GraphicFrame(
                    new P.NonVisualGraphicFrameProperties(new A.NonVisualDrawingProperties { Id = 3, Name = "Table" }),
                    new A.Graphic(graphicData));

                slidePart.Slide = new P.Slide(
                    new P.CommonSlideData(new P.ShapeTree(graphicFrame)));
                var slideId = new P.SlideId { RelationshipId = presentationPart.GetIdOfPart(slidePart) };
                presentationPart.Presentation.SlideIdList.Append(slideId);
            }

            var result = await new PptxConverter().ConvertAsync(filePath, new FileConversionOptions());
            Assert.True(result.IsSuccess);
            Assert.Contains("| H1 | H2 |", result.MarkdownContent);
            Assert.Contains("| A | B |", result.MarkdownContent);
        }

        #endregion

        #region XLSX

        [Fact(DisplayName = "XlsxConverter_ColumnAlignment")]
        public async Task XlsxConverter_ColumnAlignment()
        {
            var filePath = Path.Combine(this._tempDir, "sparse.xlsx");
            using (var doc = SpreadsheetDocument.Create(filePath, SpreadsheetDocumentType.Workbook))
            {
                var workbookPart = doc.AddWorkbookPart();
                workbookPart.Workbook = new S.Workbook(new S.Sheets(new S.Sheet { Name = "Sheet1", SheetId = 1, Id = null }));
                var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                worksheetPart.Worksheet = new S.Worksheet(new S.SheetData(
                    new S.Row(
                        new S.Cell { CellReference = "A1", CellValue = new S.CellValue("A") },
                        new S.Cell { CellReference = "C1", CellValue = new S.CellValue("C") }),
                    new S.Row(
                        new S.Cell { CellReference = "A2", CellValue = new S.CellValue("1") },
                        new S.Cell { CellReference = "C2", CellValue = new S.CellValue("3") })));

                var sheet = workbookPart.Workbook.Sheets.GetFirstChild<S.Sheet>();
                sheet.Id = workbookPart.GetIdOfPart(worksheetPart);
            }

            var result = await new XlsxConverter().ConvertAsync(filePath, new FileConversionOptions());
            Assert.True(result.IsSuccess);
            var lines = result.MarkdownContent.Split('\n').Select(l => l.Trim()).ToArray();
            var header = lines.FirstOrDefault(l => l.StartsWith("|", StringComparison.Ordinal));
            Assert.NotNull(header);
            Assert.Contains("A", header);
            Assert.Contains("C", header);
            Assert.Contains("|", header);
            var cells = header.Split('|', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(3, cells.Length);
        }

        [Fact(DisplayName = "XlsxConverter_InlineString")]
        public async Task XlsxConverter_InlineString()
        {
            var filePath = Path.Combine(this._tempDir, "inline.xlsx");
            using (var doc = SpreadsheetDocument.Create(filePath, SpreadsheetDocumentType.Workbook))
            {
                var workbookPart = doc.AddWorkbookPart();
                workbookPart.Workbook = new S.Workbook(new S.Sheets(new S.Sheet { Name = "Sheet1", SheetId = 1, Id = null }));
                var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                worksheetPart.Worksheet = new S.Worksheet(new S.SheetData(
                    new S.Row(new S.Cell { CellReference = "A1", DataType = S.CellValues.InlineString, InlineString = new S.InlineString(new S.Text("Inline")) })));

                var sheet = workbookPart.Workbook.Sheets.GetFirstChild<S.Sheet>();
                sheet.Id = workbookPart.GetIdOfPart(worksheetPart);
            }

            var result = await new XlsxConverter().ConvertAsync(filePath, new FileConversionOptions());
            Assert.True(result.IsSuccess);
            Assert.Contains("Inline", result.MarkdownContent);
        }

        [Fact(DisplayName = "XlsxConverter_CellFormatting")]
        public async Task XlsxConverter_CellFormatting()
        {
            var filePath = Path.Combine(this._tempDir, "cell-format.xlsx");
            using (var doc = SpreadsheetDocument.Create(filePath, SpreadsheetDocumentType.Workbook))
            {
                var workbookPart = doc.AddWorkbookPart();
                workbookPart.Workbook = new S.Workbook(new S.Sheets(new S.Sheet { Name = "Sheet1", SheetId = 1, Id = null }));

                var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
                stylesPart.Stylesheet = new S.Stylesheet(
                    new S.NumberingFormats(),
                    new S.Fonts(new S.Font(new S.Bold()), new S.Font()),
                    new S.Fills(
                        new S.Fill(new S.PatternFill { PatternType = S.PatternValues.None }),
                        new S.Fill(new S.PatternFill { PatternType = S.PatternValues.Gray125 })),
                    new S.Borders(new S.Border()),
                    new S.CellStyleFormats(new S.CellFormat { NumberFormatId = 0, FontId = 0, FillId = 0, BorderId = 0 }),
                    new S.CellFormats(
                        new S.CellFormat { NumberFormatId = 0, FontId = 0, FillId = 0, BorderId = 0, ApplyFont = true },
                        new S.CellFormat { NumberFormatId = 0, FontId = 1, FillId = 0, BorderId = 0, ApplyFont = true }),
                    new S.CellStyles(new S.CellStyle { Name = "Normal", FormatId = 0, BuiltinId = 0 }),
                    new S.DifferentialFormats(),
                    new S.TableStyles());

                var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                worksheetPart.Worksheet = new S.Worksheet(new S.SheetData(
                    new S.Row(
                        new S.Cell { CellReference = "A1", CellValue = new S.CellValue("Bold"), StyleIndex = 0 },
                        new S.Cell { CellReference = "B1", CellValue = new S.CellValue("Plain"), StyleIndex = 1 }),
                    new S.Row(
                        new S.Cell { CellReference = "A2", CellValue = new S.CellValue("Data"), StyleIndex = 1 })));

                var sheet = workbookPart.Workbook.Sheets.GetFirstChild<S.Sheet>();
                sheet.Id = workbookPart.GetIdOfPart(worksheetPart);
            }

            var result = await new XlsxConverter().ConvertAsync(filePath, new FileConversionOptions());
            Assert.True(result.IsSuccess);
            Assert.Contains("| **Bold** | Plain |", result.MarkdownContent);
            Assert.Contains("| Data |", result.MarkdownContent);
        }

        [Fact(DisplayName = "XlsxConverter_CellFormatting_Disabled_WhenPreserveFormattingFalse")]
        public async Task XlsxConverter_CellFormatting_Disabled_WhenPreserveFormattingFalse()
        {
            var filePath = Path.Combine(this._tempDir, "no-cell-format.xlsx");
            using (var doc = SpreadsheetDocument.Create(filePath, SpreadsheetDocumentType.Workbook))
            {
                var workbookPart = doc.AddWorkbookPart();
                workbookPart.Workbook = new S.Workbook(new S.Sheets(new S.Sheet { Name = "Sheet1", SheetId = 1, Id = null }));

                var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
                stylesPart.Stylesheet = new S.Stylesheet(
                    new S.NumberingFormats(),
                    new S.Fonts(new S.Font(new S.Bold()), new S.Font(new S.Italic()), new S.Font()),
                    new S.Fills(
                        new S.Fill(new S.PatternFill { PatternType = S.PatternValues.None }),
                        new S.Fill(new S.PatternFill { PatternType = S.PatternValues.Gray125 })),
                    new S.Borders(new S.Border()),
                    new S.CellStyleFormats(new S.CellFormat { NumberFormatId = 0, FontId = 0, FillId = 0, BorderId = 0 }),
                    new S.CellFormats(
                        new S.CellFormat { NumberFormatId = 0, FontId = 0, FillId = 0, BorderId = 0, ApplyFont = true },
                        new S.CellFormat { NumberFormatId = 0, FontId = 1, FillId = 0, BorderId = 0, ApplyFont = true },
                        new S.CellFormat { NumberFormatId = 0, FontId = 2, FillId = 0, BorderId = 0, ApplyFont = true }),
                    new S.CellStyles(new S.CellStyle { Name = "Normal", FormatId = 0, BuiltinId = 0 }),
                    new S.DifferentialFormats(),
                    new S.TableStyles());

                var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                worksheetPart.Worksheet = new S.Worksheet(new S.SheetData(
                    new S.Row(
                        new S.Cell { CellReference = "A1", CellValue = new S.CellValue("Bold"), StyleIndex = 0 },
                        new S.Cell { CellReference = "B1", CellValue = new S.CellValue("Italic"), StyleIndex = 1 },
                        new S.Cell { CellReference = "C1", CellValue = new S.CellValue("Plain"), StyleIndex = 2 })));

                var sheet = workbookPart.Workbook.Sheets.GetFirstChild<S.Sheet>();
                sheet.Id = workbookPart.GetIdOfPart(worksheetPart);
            }

            var result = await new XlsxConverter().ConvertAsync(filePath, new FileConversionOptions { PreserveFormatting = false });
            Assert.True(result.IsSuccess);
            Assert.Contains("| Bold | Italic | Plain |", result.MarkdownContent);
            Assert.DoesNotContain("**", result.MarkdownContent);
            Assert.DoesNotContain("*Italic*", result.MarkdownContent);
        }

        #endregion

        #region Shared helper

        [Fact(DisplayName = "OpenXmlMarkdownHelper_IsToggleOn_BoldValFalse")]
        public void OpenXmlMarkdownHelper_IsToggleOn_BoldValFalse()
        {
            var run = new W.Run(new W.RunProperties(new W.Bold { Val = false }), new W.Text("text"));
            Assert.False(OpenXmlMarkdownHelper.IsBold(run));
        }

        [Fact(DisplayName = "OpenXmlMarkdownHelper_ColumnIndex")]
        public void OpenXmlMarkdownHelper_ColumnIndex()
        {
            Assert.Equal(0, OpenXmlMarkdownHelper.GetSpreadsheetColumnIndex("A1"));
            Assert.Equal(1, OpenXmlMarkdownHelper.GetSpreadsheetColumnIndex("B2"));
            Assert.Equal(25, OpenXmlMarkdownHelper.GetSpreadsheetColumnIndex("Z99"));
            Assert.Equal(26, OpenXmlMarkdownHelper.GetSpreadsheetColumnIndex("AA1"));
        }

        #endregion
    }
}
