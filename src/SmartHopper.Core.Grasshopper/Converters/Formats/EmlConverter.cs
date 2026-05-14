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
using MimeKit;

namespace SmartHopper.Core.Grasshopper.Converters.Formats
{
    /// <summary>
    /// Converter for email files (.eml).
    /// Extracts email metadata and body content.
    /// </summary>
    public sealed class EmlConverter : IFileConverter
    {
        public IEnumerable<string> SupportedExtensions => new[] { ".eml" };

        public async Task<FileConversionResult> ConvertAsync(string filePath, FileConversionOptions options)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var message = MimeMessage.Load(filePath);
                    var markdown = new StringBuilder();
                    var result = FileConversionResult.Success(string.Empty, "eml");

                    // Extract metadata
                    if (!string.IsNullOrWhiteSpace(message.Subject))
                    {
                        result.Metadata["subject"] = message.Subject;
                        markdown.Append("# ").AppendLine(message.Subject);
                        markdown.AppendLine();
                    }

                    // Email headers
                    markdown.AppendLine("---");
                    if (message.From.Count > 0)
                    {
                        markdown.Append("**From:** ").AppendLine(message.From.ToString());
                        result.Metadata["from"] = message.From.ToString();
                    }

                    if (message.To.Count > 0)
                    {
                        markdown.Append("**To:** ").AppendLine(message.To.ToString());
                    }

                    if (message.Cc.Count > 0)
                    {
                        markdown.Append("**Cc:** ").AppendLine(message.Cc.ToString());
                    }

                    if (message.Date != DateTimeOffset.MinValue)
                    {
                        markdown.Append("**Date:** ").AppendLine(message.Date.ToString("yyyy-MM-dd HH:mm"));
                        result.Metadata["date"] = message.Date.ToString("yyyy-MM-dd");
                    }

                    markdown.AppendLine("---");
                    markdown.AppendLine();

                    // Extract body
                    string? bodyText = null;
                    if (message.HtmlBody != null)
                    {
                        // Convert HTML to plain text (simplified)
                        bodyText = ConvertHtmlToPlainText(message.HtmlBody);
                    }
                    else if (message.TextBody != null)
                    {
                        bodyText = message.TextBody;
                    }

                    if (!string.IsNullOrWhiteSpace(bodyText))
                    {
                        markdown.AppendLine(bodyText.Trim());
                    }

                    // List attachments
                    var attachments = new List<string>();
                    foreach (var attachment in message.Attachments)
                    {
                        if (attachment is MimePart mimePart && !string.IsNullOrWhiteSpace(mimePart.FileName))
                        {
                            attachments.Add(mimePart.FileName);
                        }
                    }

                    if (attachments.Count > 0)
                    {
                        markdown.AppendLine();
                        markdown.AppendLine("---");
                        markdown.AppendLine("**Attachments:**");
                        foreach (var attachment in attachments)
                        {
                            markdown.Append("- ").AppendLine(attachment);
                        }
                    }

                    result.MarkdownContent = markdown.ToString().Trim();
                    return result;
                }
                catch (Exception ex)
                {
                    return FileConversionResult.Failure("eml", $"Failed to convert EML: {ex.Message}");
                }
            }).ConfigureAwait(false);
        }

        private static string ConvertHtmlToPlainText(string html)
        {
            // Very basic HTML to text conversion
            var text = html;
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<script[^>]*>.*?</script>", string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<style[^>]*>.*?</style>", string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<[^>]+>", string.Empty);
            text = System.Net.WebUtility.HtmlDecode(text);
            return text.Trim();
        }
    }
}
