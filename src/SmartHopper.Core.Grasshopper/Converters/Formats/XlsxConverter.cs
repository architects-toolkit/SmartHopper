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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace SmartHopper.Core.Grasshopper.Converters.Formats
{
    /// <summary>
    /// Converter for Excel spreadsheets (.xlsx).
    /// Converts each sheet to a Markdown table.
    /// </summary>
    public sealed class XlsxConverter : IFileConverter
    {
        public IEnumerable<string> SupportedExtensions => new[] { ".xlsx" };

        public async Task<FileConversionResult> ConvertAsync(string filePath, FileConversionOptions options)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var doc = SpreadsheetDocument.Open(filePath, false);
                    var workbookPart = doc.WorkbookPart;
                    if (workbookPart == null)
                    {
                        return FileConversionResult.Failure("xlsx", "Workbook is empty or invalid.");
                    }

                    var markdown = new StringBuilder();
                    var result = FileConversionResult.Success(string.Empty, "xlsx");

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

                    // Load shared strings once
                    var sst = workbookPart.SharedStringTablePart?.SharedStringTable;
                    var sharedStrings = sst?.Elements<SharedStringItem>()
                        .Select(s => s.InnerText)
                        .ToArray() ?? Array.Empty<string>();

                    var stylesheet = workbookPart.WorkbookStylesPart?.Stylesheet;

                    // Process each sheet
                    var sheets = workbookPart.Workbook.Sheets?.Elements<Sheet>().ToList();
                    if (sheets == null || sheets.Count == 0)
                    {
                        return FileConversionResult.Failure("xlsx", "No sheets found in workbook.");
                    }

                    foreach (var sheet in sheets)
                    {
                        var sheetName = sheet.Name?.Value ?? "Unnamed Sheet";
                        var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id!);

                        if (sheets.Count > 1)
                        {
                            markdown.Append("## ").AppendLine(sheetName);
                            markdown.AppendLine();
                        }

                        ProcessWorksheet(worksheetPart, sharedStrings, stylesheet, markdown, options);
                        markdown.AppendLine();
                    }

                    result.MarkdownContent = markdown.ToString().Trim();
                    return result;
                }
                catch (Exception ex)
                {
                    return FileConversionResult.Failure("xlsx", $"Failed to convert XLSX: {ex.Message}");
                }
            }).ConfigureAwait(false);
        }

        private static void ProcessWorksheet(
            WorksheetPart worksheetPart,
            string[] sharedStrings,
            Stylesheet? stylesheet,
            StringBuilder markdown,
            FileConversionOptions options)
        {
            var sheetData = worksheetPart.Worksheet.Elements<SheetData>().FirstOrDefault();
            if (sheetData == null)
            {
                return;
            }

            var rows = sheetData.Elements<Row>().ToList();
            if (rows.Count == 0)
            {
                return;
            }

            if (!options.PreserveTableStructure)
            {
                // Just output as plain text
                foreach (var row in rows)
                {
                    var cells = row.Elements<Cell>().Select(c => OpenXmlMarkdownHelper.GetSpreadsheetCellValue(c, sharedStrings)).ToList();
                    markdown.AppendLine(string.Join("\t", cells));
                }

                return;
            }

            // Determine the maximum column index across all rows
            var maxCol = 0;
            foreach (var row in rows)
            {
                foreach (var cell in row.Elements<Cell>())
                {
                    var colIndex = OpenXmlMarkdownHelper.GetSpreadsheetColumnIndex(cell.CellReference?.Value);
                    if (colIndex > maxCol)
                    {
                        maxCol = colIndex;
                    }
                }
            }

            if (maxCol < 0)
            {
                return;
            }

            var columnCount = maxCol + 1;

            // Build rows as string arrays aligned by column index
            var tableData = new List<string[]>();
            var formatting = new List<(bool Bold, bool Italic)[]>();

            foreach (var row in rows)
            {
                var rowValues = new string[columnCount];
                var rowFormatting = new (bool Bold, bool Italic)[columnCount];

                foreach (var cell in row.Elements<Cell>())
                {
                    var colIndex = OpenXmlMarkdownHelper.GetSpreadsheetColumnIndex(cell.CellReference?.Value);
                    if (colIndex < 0 || colIndex >= columnCount)
                    {
                        continue;
                    }

                    rowValues[colIndex] = OpenXmlMarkdownHelper.GetSpreadsheetCellValue(cell, sharedStrings);

                    var font = OpenXmlMarkdownHelper.GetCellFont(cell, stylesheet);
                    if (font != null)
                    {
                        rowFormatting[colIndex] = (OpenXmlMarkdownHelper.IsBold(font), OpenXmlMarkdownHelper.IsItalic(font));
                    }
                }

                tableData.Add(rowValues);
                formatting.Add(rowFormatting);
            }

            // Determine uniform formatting per row
            var rowUniformBold = new bool[tableData.Count];
            var rowUniformItalic = new bool[tableData.Count];
            for (int i = 0; i < tableData.Count; i++)
            {
                var rowFmt = formatting[i];
                var nonEmptyCells = tableData[i]
                    .Select((value, idx) => (value, idx))
                    .Where(x => !string.IsNullOrWhiteSpace(x.value))
                    .Select(x => rowFmt[x.idx])
                    .ToList();

                rowUniformBold[i] = nonEmptyCells.Count > 0 && nonEmptyCells.All(f => f.Bold);
                rowUniformItalic[i] = nonEmptyCells.Count > 0 && nonEmptyCells.All(f => f.Italic);
            }

            // Header row
            var headerRow = tableData[0];
            var headerRowFormatting = formatting[0];
            markdown.Append("| ");
            for (int i = 0; i < columnCount; i++)
            {
                if (i > 0)
                {
                    markdown.Append(" | ");
                }

                markdown.Append(FormatCell(headerRow[i], headerRowFormatting[i], rowUniformBold[0], rowUniformItalic[0], options.PreserveFormatting));
            }

            markdown.AppendLine(" |");
            markdown.Append("|");
            for (int i = 0; i < columnCount; i++)
            {
                markdown.Append(" --- |");
            }

            markdown.AppendLine();

            // Data rows
            for (int i = 1; i < tableData.Count; i++)
            {
                var rowValues = tableData[i];
                var rowFmt = formatting[i];
                markdown.Append("| ");
                for (int j = 0; j < columnCount; j++)
                {
                    if (j > 0)
                    {
                        markdown.Append(" | ");
                    }

                    markdown.Append(FormatCell(rowValues[j], rowFmt[j], rowUniformBold[i], rowUniformItalic[i], options.PreserveFormatting));
                }

                markdown.AppendLine(" |");
            }
        }

        private static string FormatCell(string? value, (bool Bold, bool Italic) formatting, bool rowUniformBold, bool rowUniformItalic, bool preserveFormatting)
        {
            var text = OpenXmlMarkdownHelper.EscapeMarkdownTableCell(value);
            var isBold = preserveFormatting && formatting.Bold && !rowUniformBold;
            var isItalic = preserveFormatting && formatting.Italic && !rowUniformItalic;

            if (isBold && isItalic)
            {
                return $"***{text}***";
            }

            if (isBold)
            {
                return $"**{text}**";
            }

            if (isItalic)
            {
                return $"*{text}*";
            }

            return text;
        }
    }
}
