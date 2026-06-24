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
using System.Linq;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using M = DocumentFormat.OpenXml.Math;
using S = DocumentFormat.OpenXml.Spreadsheet;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace SmartHopper.Core.Grasshopper.Converters.Formats
{
    /// <summary>
    /// Shared helpers for converting OpenXML (DOCX, PPTX, XLSX) documents to Markdown.
    /// Centralizes formatting detection, table cell escaping, spreadsheet indexing, and OMML math conversion.
    /// </summary>
    public static class OpenXmlMarkdownHelper
    {
        #region Formatting detection

        /// <summary>
        /// Returns whether a Wordprocessing run is bold.
        /// A toggle is enabled when the element is present and its value is absent or true/1.
        /// </summary>
        public static bool IsBold(W.Run run)
        {
            var bold = run.RunProperties?.Bold;
            return bold is not null && (bold.Val is null || bold.Val.Value);
        }

        /// <summary>
        /// Returns whether a Wordprocessing run is italic.
        /// </summary>
        public static bool IsItalic(W.Run run)
        {
            var italic = run.RunProperties?.Italic;
            return italic is not null && (italic.Val is null || italic.Val.Value);
        }

        /// <summary>
        /// Returns whether a DrawingML run is bold.
        /// A toggle is enabled when the element is present and its value is absent or true.
        /// </summary>
        public static bool IsBold(A.Run run)
        {
            var bold = run.RunProperties?.Bold;
            return bold is not null && (!bold.HasValue || bold.Value);
        }

        /// <summary>
        /// Returns whether a DrawingML run is italic.
        /// A toggle is enabled when the element is present and its value is absent or true.
        /// </summary>
        public static bool IsItalic(A.Run run)
        {
            var italic = run.RunProperties?.Italic;
            return italic is not null && (!italic.HasValue || italic.Value);
        }

        /// <summary>
        /// Returns whether a Spreadsheet font is bold.
        /// </summary>
        public static bool IsBold(S.Font font)
        {
            var bold = font?.Bold;
            return bold is not null && (bold.Val is null || bold.Val.Value);
        }

        /// <summary>
        /// Returns whether a Spreadsheet font is italic.
        /// </summary>
        public static bool IsItalic(S.Font font)
        {
            var italic = font?.Italic;
            return italic is not null && (italic.Val is null || italic.Val.Value);
        }

        /// <summary>
        /// Returns whether all runs in the sequence are bold, requiring at least one run.
        /// </summary>
        public static bool AllRunsBold(IEnumerable<W.Run> runs) => runs.Any() && runs.All(IsBold);

        /// <summary>
        /// Returns whether all runs in the sequence are italic, requiring at least one run.
        /// </summary>
        public static bool AllRunsItalic(IEnumerable<W.Run> runs) => runs.Any() && runs.All(IsItalic);

        /// <summary>
        /// Returns whether all DrawingML runs in the sequence are bold, requiring at least one run.
        /// </summary>
        public static bool AllRunsBold(IEnumerable<A.Run> runs) => runs.Any() && runs.All(IsBold);

        /// <summary>
        /// Returns whether all DrawingML runs in the sequence are italic, requiring at least one run.
        /// </summary>
        public static bool AllRunsItalic(IEnumerable<A.Run> runs) => runs.Any() && runs.All(IsItalic);

        #endregion

        #region Markdown escaping

        /// <summary>
        /// Escapes pipe characters and normalizes line breaks for a Markdown table cell.
        /// </summary>
        public static string EscapeMarkdownTableCell(string? text)
        {
            return (text ?? string.Empty)
                .Replace("|", "\\|", StringComparison.Ordinal)
                .Replace("\r", " ", StringComparison.Ordinal)
                .Replace("\n", " ", StringComparison.Ordinal);
        }

        /// <summary>
        /// Escapes pipe characters and normalizes line breaks for a Markdown table cell,
        /// joining multiple paragraphs with the given separator.
        /// </summary>
        public static string EscapeMarkdownTableCell(IEnumerable<string?> paragraphs, string separator = " ")
        {
            return EscapeMarkdownTableCell(string.Join(separator, paragraphs.Where(p => !string.IsNullOrWhiteSpace(p))));
        }

        #endregion

        #region Spreadsheet helpers

        /// <summary>
        /// Converts a cell reference such as "A1" or "AA10" into a zero-based column index.
        /// </summary>
        public static int GetSpreadsheetColumnIndex(string? cellReference)
        {
            if (string.IsNullOrEmpty(cellReference))
            {
                return 0;
            }

            var column = 0;
            foreach (var c in cellReference)
            {
                if (!char.IsLetter(c))
                {
                    break;
                }

                column = (column * 26) + (char.ToUpper(c, CultureInfo.InvariantCulture) - 'A' + 1);
            }

            return column - 1;
        }

        /// <summary>
        /// Resolves the display value of a spreadsheet cell, handling shared strings and inline strings.
        /// </summary>
        public static string GetSpreadsheetCellValue(Cell cell, string[] sharedStrings)
        {
            var value = cell.CellValue?.InnerText;
            if (value is null)
            {
                return cell.InnerText ?? string.Empty;
            }

            if (cell.DataType?.Value == CellValues.SharedString &&
                int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) &&
                index >= 0 && index < sharedStrings.Length)
            {
                return sharedStrings[index];
            }

            if (cell.DataType?.Value == CellValues.InlineString)
            {
                return cell.InnerText ?? string.Empty;
            }

            return value;
        }

        /// <summary>
        /// Looks up the <see cref="S.Font"/> referenced by a cell's style.
        /// </summary>
        public static S.Font? GetCellFont(Cell cell, Stylesheet? stylesheet)
        {
            if (stylesheet is null)
            {
                return null;
            }

            var cellFormats = stylesheet.CellFormats;
            var fonts = stylesheet.Fonts;
            if (cellFormats is null || fonts is null)
            {
                return null;
            }

            var styleIndex = cell.StyleIndex?.Value ?? 0;
            if (styleIndex >= cellFormats.Elements<CellFormat>().Count())
            {
                return null;
            }

            if (cellFormats.ElementAt((int)styleIndex) is not CellFormat cellFormat)
            {
                return null;
            }

            var fontId = cellFormat.FontId?.Value ?? 0;
            if (fontId >= fonts.Elements<S.Font>().Count())
            {
                return null;
            }

            if (fonts.ElementAt((int)fontId) is not S.Font font)
            {
                return null;
            }

            return font;
        }

        #endregion

        #region DOCX hyperlink resolution

        /// <summary>
        /// Resolves a Wordprocessing hyperlink to its absolute URL, or null if it cannot be resolved.
        /// </summary>
        public static string? ResolveDocxHyperlink(W.Hyperlink hyperlink, MainDocumentPart? mainPart)
        {
            if (hyperlink.Id is null || mainPart is null)
            {
                return null;
            }

            var rel = mainPart.HyperlinkRelationships.FirstOrDefault(r => r.Id == hyperlink.Id);
            return rel?.Uri?.ToString();
        }

        #endregion

        #region OMML Math → LaTeX

        /// <summary>
        /// Converts an OOXML Math element (m:oMath or m:oMathPara) to a LaTeX string.
        /// Returns null if the element is not a recognized math element.
        /// </summary>
        public static string? ConvertMathToLaTeX(OpenXmlElement element)
        {
            if (element is null)
            {
                return null;
            }

            var ns = element.NamespaceUri;
            var localName = element.LocalName;
            if (ns != "http://schemas.openxmlformats.org/officeDocument/2006/math" ||
                (localName != "oMath" && localName != "oMathPara"))
            {
                return null;
            }

            var sb = new StringBuilder();
            ConvertMathElement(element, sb);
            return sb.ToString().Trim();
        }

        private static void ConvertMathElement(OpenXmlElement element, StringBuilder sb)
        {
            switch (element)
            {
                case M.Fraction f:
                    sb.Append(@"\frac{");
                    ConvertMathChildren(f.Numerator, sb);
                    sb.Append("}{");
                    ConvertMathChildren(f.Denominator, sb);
                    sb.Append('}');
                    break;

                case M.Subscript sub:
                    ConvertMathChildren(sub.Base, sb);
                    sb.Append("_{");
                    ConvertMathChildren(sub.SubArgument, sb);
                    sb.Append('}');
                    break;

                case M.Superscript sup:
                    ConvertMathChildren(sup.Base, sb);
                    sb.Append("^{");
                    ConvertMathChildren(sup.SuperArgument, sb);
                    sb.Append('}');
                    break;

                case M.SubSuperscript subSup:
                    ConvertMathChildren(subSup.Base, sb);
                    sb.Append("_{");
                    ConvertMathChildren(subSup.SubArgument, sb);
                    sb.Append("}^{");
                    ConvertMathChildren(subSup.SuperArgument, sb);
                    sb.Append('}');
                    break;

                case M.Delimiter d:
                    var beginChar = d.DelimiterProperties?.GetFirstChild<M.BeginChar>()?.Val?.Value ?? "(";
                    var endChar = d.DelimiterProperties?.GetFirstChild<M.EndChar>()?.Val?.Value ?? ")";
                    sb.Append($@"\left{beginChar}");
                    foreach (var baseEl in d.Elements<M.Base>())
                    {
                        ConvertMathChildren(baseEl, sb);
                    }

                    sb.Append($@"\right{endChar}");
                    break;

                case M.Nary nary:
                    var chr = nary.NaryProperties?.GetFirstChild<M.AccentChar>()?.Val?.Value;
                    var op = chr switch
                    {
                        "∑" => @"\sum",
                        "∏" => @"\prod",
                        "∫" => @"\int",
                        "∮" => @"\oint",
                        _ => chr ?? @"\sum"
                    };

                    sb.Append(op);
                    if (nary.SubArgument is not null && nary.SubArgument.HasChildren)
                    {
                        sb.Append("_{");
                        ConvertMathChildren(nary.SubArgument, sb);
                        sb.Append('}');
                    }

                    if (nary.SuperArgument is not null && nary.SuperArgument.HasChildren)
                    {
                        sb.Append("^{");
                        ConvertMathChildren(nary.SuperArgument, sb);
                        sb.Append('}');
                    }

                    sb.Append(' ');
                    if (nary.Base is not null)
                    {
                        ConvertMathChildren(nary.Base, sb);
                    }

                    break;

                case M.Run run:
                    foreach (var t in run.Elements<M.Text>())
                    {
                        sb.Append(t.Text);
                    }

                    break;

                case M.OfficeMath:
                case M.Paragraph:
                case M.Base:
                    ConvertMathChildren(element, sb);
                    break;

                case M.FractionProperties:
                case M.SubscriptProperties:
                case M.SuperscriptProperties:
                case M.SubSuperscriptProperties:
                case M.DelimiterProperties:
                case M.NaryProperties:
                case M.RunProperties:
                case M.MathProperties:
                case M.ControlProperties:
                    break;

                default:
                    ConvertMathChildren(element, sb);
                    break;
            }
        }

        private static void ConvertMathChildren(OpenXmlElement? parent, StringBuilder sb)
        {
            if (parent is null)
            {
                return;
            }

            foreach (var child in parent.ChildElements)
            {
                ConvertMathElement(child, sb);
            }
        }

        #endregion
    }
}
