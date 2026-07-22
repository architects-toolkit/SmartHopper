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
using ExcelDataReader;
using SmartHopper.Core.Grasshopper.Converters;

namespace SmartHopper.Core.Grasshopper.Converters.Formats
{
    /// <summary>
    /// Converter for legacy Excel spreadsheets (.xls).
    /// Converts each sheet to a Markdown table.
    /// </summary>
    public sealed class XlsConverter : IFileConverter
    {
        static XlsConverter()
        {
            // ExcelDataReader relies on legacy Windows code pages for BIFF2-5 documents.
            // This provider is not registered by default on .NET Core / .NET 5+.
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        }

        public IEnumerable<string> SupportedExtensions => new[] { ".xls" };

        public async Task<FileConversionResult> ConvertAsync(string filePath, FileConversionOptions options)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var reader = ExcelReaderFactory.CreateReader(stream);

                    var markdown = new StringBuilder();
                    var result = FileConversionResult.Success(string.Empty, "xls");

                    do
                    {
                        if (markdown.Length > 0)
                        {
                            markdown.AppendLine().AppendLine();
                        }

                        if (!string.IsNullOrWhiteSpace(reader.Name))
                        {
                            markdown.Append("## ").AppendLine(reader.Name).AppendLine();
                        }

                        ProcessSheet(reader, markdown, options);
                    }
                    while (reader.NextResult());

                    result.MarkdownContent = markdown.ToString().Trim();
                    return result;
                }
                catch (Exception ex)
                {
                    return FileConversionResult.Failure("xls", $"Failed to convert XLS: {ex.Message}");
                }
            }).ConfigureAwait(false);
        }

        private static void ProcessSheet(IExcelDataReader reader, StringBuilder markdown, FileConversionOptions options)
        {
            var rows = new List<string[]>();
            int maxColumns = 0;

            while (reader.Read())
            {
                var row = new string[reader.FieldCount];
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[i] = FormatValue(reader.GetValue(i));
                }

                rows.Add(row);
                if (reader.FieldCount > maxColumns)
                {
                    maxColumns = reader.FieldCount;
                }
            }

            if (rows.Count == 0 || maxColumns == 0)
            {
                return;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row.Length < maxColumns)
                {
                    Array.Resize(ref row, maxColumns);
                    rows[i] = row;
                }
            }

            if (!options.PreserveTableStructure)
            {
                foreach (var row in rows)
                {
                    markdown.AppendLine(string.Join("\t", row));
                }

                return;
            }

            // Header row
            markdown.Append("| ");
            for (int i = 0; i < maxColumns; i++)
            {
                if (i > 0)
                {
                    markdown.Append(" | ");
                }

                markdown.Append(EscapeMarkdownCell(rows[0][i] ?? string.Empty));
            }

            markdown.AppendLine(" |");

            markdown.Append("|");
            for (int i = 0; i < maxColumns; i++)
            {
                markdown.Append(" --- |");
            }

            markdown.AppendLine();

            // Data rows
            for (int i = 1; i < rows.Count; i++)
            {
                markdown.Append("| ");
                for (int j = 0; j < maxColumns; j++)
                {
                    if (j > 0)
                    {
                        markdown.Append(" | ");
                    }

                    markdown.Append(EscapeMarkdownCell(rows[i][j] ?? string.Empty));
                }

                markdown.AppendLine(" |");
            }
        }

        private static string FormatValue(object value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            if (value is DateTime dt)
            {
                return dt.ToString("u", CultureInfo.InvariantCulture);
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private static string EscapeMarkdownCell(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return " ";
            }

            return value
                .Replace("|", "\\|")
                .Replace("\r\n", " ")
                .Replace("\n", " ")
                .Replace("\r", " ")
                .Trim();
        }
    }
}
