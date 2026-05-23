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

namespace SmartHopper.Core.Grasshopper.Converters.Formats
{
    /// <summary>
    /// Converter for CSV files (.csv).
    /// Converts CSV data to Markdown tables.
    /// </summary>
    public sealed class CsvConverter : IFileConverter
    {
        public IEnumerable<string> SupportedExtensions => new[] { ".csv" };

        public async Task<FileConversionResult> ConvertAsync(string filePath, FileConversionOptions options)
        {
            try
            {
                var lines = await File.ReadAllLinesAsync(filePath, Encoding.UTF8).ConfigureAwait(false);

                if (lines.Length == 0)
                {
                    return FileConversionResult.Success(string.Empty, "csv");
                }

                // Return as plain text if table structure is explicitly not preserved
                if (options?.PreserveTableStructure == false)
                {
                    return FileConversionResult.Success(string.Join("\n", lines), "csv");
                }

                var markdown = new StringBuilder();
                var rows = lines.Select(ParseCsvLine).ToList();

                if (rows.Count == 0)
                {
                    return FileConversionResult.Success(string.Empty, "csv");
                }

                // Header row
                var headerRow = rows[0];
                markdown.AppendLine("| " + string.Join(" | ", headerRow.Select(EscapeMarkdown)) + " |");
                markdown.AppendLine("|" + string.Join("|", headerRow.Select(_ => "---")) + "|");

                // Data rows
                for (int i = 1; i < rows.Count; i++)
                {
                    var row = rows[i];

                    // Pad row to match header column count
                    while (row.Count < headerRow.Count)
                    {
                        row.Add(string.Empty);
                    }

                    markdown.AppendLine("| " + string.Join(" | ", row.Select(EscapeMarkdown)) + " |");
                }

                return FileConversionResult.Success(markdown.ToString(), "csv");
            }
            catch (Exception ex)
            {
                return FileConversionResult.Failure("csv", $"Failed to convert CSV: {ex.Message}");
            }
        }

        private static List<string> ParseCsvLine(string line)
        {
            var fields = new List<string>();
            var currentField = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        // Escaped quote
                        currentField.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    fields.Add(currentField.ToString());
                    currentField.Clear();
                }
                else
                {
                    currentField.Append(c);
                }
            }

            fields.Add(currentField.ToString());
            return fields;
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
