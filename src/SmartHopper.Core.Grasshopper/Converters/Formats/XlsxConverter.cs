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

                        ProcessWorksheet(worksheetPart, workbookPart, markdown, options);
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

        private static void ProcessWorksheet(WorksheetPart worksheetPart, WorkbookPart workbookPart, StringBuilder markdown, FileConversionOptions options)
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
                    var cells = row.Elements<Cell>().Select(c => GetCellValue(c, workbookPart)).ToList();
                    markdown.AppendLine(string.Join("\t", cells));
                }

                return;
            }

            // Get max column count
            int maxCols = rows.Max(r => r.Elements<Cell>().Count());
            if (maxCols == 0)
            {
                return;
            }

            // Header row (first row or frozen row)
            var headerRow = rows[0];
            var headerCells = GetRowCells(headerRow, workbookPart, maxCols);

            markdown.Append("| ").AppendJoin(" | ", headerCells.Select(EscapeMarkdown)).AppendLine(" |");
            markdown.Append("|").AppendJoin("|", headerCells.Select(_ => "---")).AppendLine("|");

            // Data rows
            for (int i = 1; i < rows.Count; i++)
            {
                var cells = GetRowCells(rows[i], workbookPart, maxCols);
                markdown.Append("| ").AppendJoin(" | ", cells.Select(EscapeMarkdown)).AppendLine(" |");
            }
        }

        private static List<string> GetRowCells(Row row, WorkbookPart workbookPart, int maxCols)
        {
            var cells = new List<string>();
            var rowCells = row.Elements<Cell>().ToList();

            for (int i = 0; i < maxCols; i++)
            {
                if (i < rowCells.Count)
                {
                    cells.Add(GetCellValue(rowCells[i], workbookPart));
                }
                else
                {
                    cells.Add(string.Empty);
                }
            }

            return cells;
        }

        private static string GetCellValue(Cell cell, WorkbookPart workbookPart)
        {
            if (cell.CellValue == null)
            {
                return string.Empty;
            }

            string value = cell.CellValue.InnerText;
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            // If it's a shared string, look it up
            if (cell.DataType != null && cell.DataType.Value == CellValues.SharedString)
            {
                var stringTable = workbookPart.SharedStringTablePart?.SharedStringTable;
                if (stringTable != null && int.TryParse(value, out int index))
                {
                    var sharedString = stringTable.Elements<SharedStringItem>().ElementAtOrDefault(index);
                    return sharedString?.InnerText ?? value;
                }
            }

            return value;
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
