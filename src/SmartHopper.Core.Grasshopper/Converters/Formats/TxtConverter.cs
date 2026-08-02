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
                var bytes = await File.ReadAllBytesAsync(filePath).ConfigureAwait(false);
                var content = DecodeTextBytes(bytes);

                // Normalize line endings
                content = content.Replace("\r\n", "\n").Replace("\r", "\n");

                return FileConversionResult.Success(content, "txt");
            }
            catch (Exception ex)
            {
                return FileConversionResult.Failure("txt", $"Failed to read text file: {ex.Message}");
            }
        }

        /// <summary>
        /// Decodes the raw bytes of a text file. UTF-8 (with or without BOM) is tried first;
        /// if the result contains replacement characters, a Latin-1 fallback is used so that
        /// legacy Windows-1252 / ISO-8859-1 files are read correctly.
        /// </summary>
        internal static string DecodeTextBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return string.Empty;
            }

            string content;
            int startIndex = 0;

            // UTF-8 BOM
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                startIndex = 3;
            }

            content = Encoding.UTF8.GetString(bytes, startIndex, bytes.Length - startIndex);

            // If UTF-8 decoding produced replacement characters, the file is likely Latin-1.
            if (content.Contains('\uFFFD'))
            {
                content = Encoding.Latin1.GetString(bytes, startIndex, bytes.Length - startIndex);
            }

            return content;
        }
    }
}
