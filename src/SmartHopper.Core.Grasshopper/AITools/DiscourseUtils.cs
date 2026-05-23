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
using Newtonsoft.Json.Linq;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Utility methods for working with Discourse forum data.
    /// </summary>
    public static class DiscourseUtils
    {
        /// <summary>
        /// Filters a post JSON to include only essential fields.
        /// </summary>
        /// <param name="postJson">The raw post JSON string.</param>
        /// <returns>A filtered JSON string with essential fields only.</returns>
        public static string FilterPostJson(string postJson)
        {
            try
            {
                var post = JObject.Parse(postJson);
                var filtered = new JObject
                {
                    ["id"] = post["id"],
                    ["username"] = post["username"],
                    ["name"] = post["name"],
                    ["created_at"] = post["created_at"],
                    ["updated_at"] = post["updated_at"],
                    ["date"] = post["created_at"],
                    ["raw"] = post["raw"],
                    ["cooked"] = post["cooked"],
                    ["title"] = post["title"],
                    ["topic_id"] = post["topic_id"],
                    ["post_number"] = post["post_number"],
                };
                return filtered.ToString(Newtonsoft.Json.Formatting.None);
            }
            catch
            {
                return postJson;
            }
        }

        /// <summary>
        /// Extracts error messages from Discourse error response.
        /// </summary>
        /// <param name="content">The error response content.</param>
        /// <returns>The extracted error message or empty string if not found.</returns>
        public static string ExtractDiscourseErrorMessage(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return string.Empty;
            }

            try
            {
                var json = JObject.Parse(content);
                var errors = json["errors"] as JArray;
                if (errors != null && errors.Count > 0)
                {
                    var messages = errors.Values<string>().Where(e => !string.IsNullOrWhiteSpace(e));
                    return string.Join("; ", messages);
                }
            }
            catch
            {
            }

            return content;
        }

        /// <summary>
        /// Generates an excerpt from available post content fields.
        /// Priority: blurb > cooked > raw (truncated to 200 chars).
        /// </summary>
        /// <param name="post">The post JSON object.</param>
        /// <returns>A plain text excerpt or null if no content available.</returns>
        public static string GenerateExcerpt(JObject post)
        {
            if (post == null)
            {
                return null;
            }

            // Try blurb first (Discourse sometimes provides this in search results)
            string blurb = post.Value<string>("blurb");
            if (!string.IsNullOrWhiteSpace(blurb))
            {
                return TruncateAndClean(blurb, 200);
            }

            // Try cooked (HTML content) - strip HTML tags
            string cooked = post.Value<string>("cooked");
            if (!string.IsNullOrWhiteSpace(cooked))
            {
                string plainText = StripHtmlTags(cooked);
                return TruncateAndClean(plainText, 200);
            }

            // Try raw (markdown) - strip markdown syntax
            string raw = post.Value<string>("raw");
            if (!string.IsNullOrWhiteSpace(raw))
            {
                string plainText = StripMarkdownSyntax(raw);
                return TruncateAndClean(plainText, 200);
            }

            return null;
        }

        /// <summary>
        /// Strips HTML tags from content.
        /// </summary>
        /// <param name="html">The HTML content.</param>
        /// <returns>Plain text without HTML tags.</returns>
        public static string StripHtmlTags(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return string.Empty;
            }

            // Simple regex-free HTML tag stripping
            var result = new StringBuilder();
            bool inTag = false;
            foreach (char c in html)
            {
                if (c == '<')
                {
                    inTag = true;
                }
                else if (c == '>')
                {
                    inTag = false;
                }
                else if (!inTag)
                {
                    result.Append(c);
                }
            }

            return result.ToString().Trim();
        }

        /// <summary>
        /// Strips common markdown syntax for plain text excerpt generation.
        /// </summary>
        /// <param name="markdown">The markdown content.</param>
        /// <returns>Plain text without markdown syntax.</returns>
        public static string StripMarkdownSyntax(string markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown))
            {
                return string.Empty;
            }

            string result = markdown;

            // Remove code blocks
            result = System.Text.RegularExpressions.Regex.Replace(result, @"```[\s\S]*?```", " ");
            result = System.Text.RegularExpressions.Regex.Replace(result, @"`([^`]+)`", "$1");

            // Remove headers
            result = System.Text.RegularExpressions.Regex.Replace(result, @"^#{1,6}\s*", "", System.Text.RegularExpressions.RegexOptions.Multiline);

            // Remove bold/italic markers
            result = result.Replace("**", "").Replace("__", "").Replace("*", "").Replace("_", "");

            // Remove links but keep text [text](url) -> text
            result = System.Text.RegularExpressions.Regex.Replace(result, @"\[([^\]]+)\]\([^)]+\)", "$1");

            // Remove images ![alt](url)
            result = System.Text.RegularExpressions.Regex.Replace(result, @"!\[([^\]]*)\]\([^)]+\)", "$1");

            // Remove blockquotes
            result = System.Text.RegularExpressions.Regex.Replace(result, @"^>\s*", "", System.Text.RegularExpressions.RegexOptions.Multiline);

            return result.Trim();
        }

        /// <summary>
        /// Truncates text to max length and cleans up whitespace.
        /// </summary>
        /// <param name="text">The text to truncate.</param>
        /// <param name="maxLength">Maximum length of the output.</param>
        /// <returns>Truncated text or null if input is empty.</returns>
        public static string TruncateAndClean(string text, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            // Collapse whitespace
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();

            if (text.Length <= maxLength)
            {
                return text;
            }

            // Truncate at word boundary
            int truncateAt = text.LastIndexOf(' ', maxLength - 3);
            if (truncateAt < 0)
            {
                truncateAt = maxLength - 3;
            }

            return text.Substring(0, truncateAt) + "...";
        }
    }
}
