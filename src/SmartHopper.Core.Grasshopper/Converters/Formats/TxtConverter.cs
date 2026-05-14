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

namespace SmartHopper.Core.Grasshopper.Converters.Formats
{
    /// <summary>
    /// Converter for plain text files (.txt).
    /// </summary>
    public sealed class TxtConverter : IFileConverter
    {
        public IEnumerable<string> SupportedExtensions => new[] { ".txt" };

        public async Task<FileConversionResult> ConvertAsync(string filePath, FileConversionOptions options)
        {
            try
            {
                var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8).ConfigureAwait(false);

                // Normalize line endings
                content = content.Replace("\r\n", "\n").Replace("\r", "\n");

                return FileConversionResult.Success(content, "txt");
            }
            catch (Exception ex)
            {
                return FileConversionResult.Failure("txt", $"Failed to read text file: {ex.Message}");
            }
        }
    }
}
