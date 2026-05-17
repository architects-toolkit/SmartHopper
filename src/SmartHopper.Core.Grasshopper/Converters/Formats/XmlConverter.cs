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
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace SmartHopper.Core.Grasshopper.Converters.Formats
{
    /// <summary>
    /// Converter for XML files (.xml).
    /// Formats XML as a fenced code block with syntax highlighting.
    /// </summary>
    public sealed class XmlConverter : IFileConverter
    {
        public IEnumerable<string> SupportedExtensions => new[] { ".xml" };

        public async Task<FileConversionResult> ConvertAsync(string filePath, FileConversionOptions options)
        {
            try
            {
                var xmlText = await File.ReadAllTextAsync(filePath, Encoding.UTF8).ConfigureAwait(false);

                // Try to parse and pretty-print the XML
                string formattedXml;
                try
                {
                    var doc = XDocument.Parse(xmlText);
                    formattedXml = doc.ToString();
                }
                catch
                {
                    // If parsing fails, use the original text
                    formattedXml = xmlText;
                }

                var markdown = new StringBuilder();
                markdown.AppendLine("```xml");
                markdown.AppendLine(formattedXml);
                markdown.AppendLine("```");

                return FileConversionResult.Success(markdown.ToString(), "xml");
            }
            catch (Exception ex)
            {
                return FileConversionResult.Failure("xml", $"Failed to convert XML: {ex.Message}");
            }
        }
    }
}
