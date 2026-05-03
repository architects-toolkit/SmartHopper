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
        /// Filters a search result post to include only essential fields.
        /// </summary>
        /// <param name="post">The search result post JObject.</param>
        /// <returns>A filtered JObject with essential fields.</returns>
        public static JObject FilterSearchResultPost(JObject post)
        {
            if (post == null)
            {
                return null;
            }

            try
            {
                return new JObject
                {
                    ["id"] = post["id"],
                    ["username"] = post["username"],
                    ["name"] = post["name"],
                    ["created_at"] = post["created_at"],
                    ["excerpt"] = post["excerpt"],
                    ["title"] = post["title"],
                    ["topic_id"] = post["topic_id"],
                    ["post_number"] = post["post_number"],
                };
            }
            catch
            {
                return post;
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
    }
}
