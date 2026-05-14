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
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SmartHopper.Core.Grasshopper.Converters.Formats
{
    /// <summary>
    /// Converter for RTF files (.rtf).
    /// On Windows, uses RichTextBox for accurate conversion.
    /// On macOS/Linux, uses regex-based RTF stripping (fallback).
    /// </summary>
    public sealed class RtfConverter : IFileConverter
    {
        public IEnumerable<string> SupportedExtensions => new[] { ".rtf" };

        public async Task<FileConversionResult> ConvertAsync(string filePath, FileConversionOptions options)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var rtfContent = File.ReadAllText(filePath, Encoding.UTF8);

                    string plainText;
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        plainText = ConvertRtfToTextWindows(rtfContent);
                    }
                    else
                    {
                        plainText = ConvertRtfToTextFallback(rtfContent);
                    }

                    return FileConversionResult.Success(plainText, "rtf");
                }
                catch (Exception ex)
                {
                    return FileConversionResult.Failure("rtf", $"Failed to convert RTF: {ex.Message}");
                }
            }).ConfigureAwait(false);
        }

        private static string ConvertRtfToTextWindows(string rtfContent)
        {
#if WINDOWS
            try
            {
                using var rtb = new System.Windows.Forms.RichTextBox();
                rtb.Rtf = rtfContent;
                return rtb.Text;
            }
            catch
            {
                return ConvertRtfToTextFallback(rtfContent);
            }

#else
            return ConvertRtfToTextFallback(rtfContent);
#endif
        }

        private static string ConvertRtfToTextFallback(string rtfContent)
        {
            // Remove RTF header
            var text = rtfContent;

            // Remove RTF control words and groups
            text = Regex.Replace(text, @"\\[a-z]+(-?\d+)?[ ]?", string.Empty);

            // Remove RTF groups
            text = Regex.Replace(text, @"[{}]", string.Empty);

            // Remove special characters
            text = text.Replace(@"\'", "'");
            text = text.Replace(@"\~", " ");
            text = text.Replace(@"\-", "-");
            text = text.Replace(@"\_", "_");

            // Clean up whitespace
            text = Regex.Replace(text, @"\s+", " ");

            return text.Trim();
        }
    }
}
