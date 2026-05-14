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
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SmartHopper.Core.Grasshopper.Converters.Formats
{
    /// <summary>
    /// Converter for JSON files (.json).
    /// Formats JSON as a fenced code block with syntax highlighting.
    /// </summary>
    public sealed class JsonConverter : IFileConverter
    {
        public IEnumerable<string> SupportedExtensions => new[] { ".json" };

        public async Task<FileConversionResult> ConvertAsync(string filePath, FileConversionOptions options)
        {
            try
            {
                var jsonText = await File.ReadAllTextAsync(filePath, Encoding.UTF8).ConfigureAwait(false);

                // Try to parse and pretty-print the JSON
                string formattedJson;
                try
                {
                    using var jsonDoc = JsonDocument.Parse(jsonText);
                    formattedJson = JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
                }
                catch
                {
                    // If parsing fails, use the original text
                    formattedJson = jsonText;
                }

                var markdown = new StringBuilder();
                markdown.AppendLine("```json");
                markdown.AppendLine(formattedJson);
                markdown.AppendLine("```");

                return FileConversionResult.Success(markdown.ToString(), "json");
            }
            catch (Exception ex)
            {
                return FileConversionResult.Failure("json", $"Failed to convert JSON: {ex.Message}");
            }
        }
    }
}
