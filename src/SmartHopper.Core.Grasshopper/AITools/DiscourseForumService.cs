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
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Shared service for fetching and formatting Discourse forum content.
    /// Used by both the Discourse AI tools and the generic URL-to-Markdown converter.
    /// </summary>
    public static class DiscourseForumService
    {
        /// <summary>
        /// Checks whether a URI looks like a Discourse forum URL.
        /// </summary>
        public static bool IsDiscourseUrl(Uri uri)
        {
            if (uri == null)
            {
                return false;
            }

            return uri.Host.Contains("discourse", StringComparison.OrdinalIgnoreCase)
                   || uri.AbsolutePath.StartsWith("/t/", StringComparison.OrdinalIgnoreCase)
                   || uri.AbsolutePath.StartsWith("/p/", StringComparison.OrdinalIgnoreCase)
                   || uri.AbsolutePath.StartsWith("/posts/", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Tries to parse a Discourse topic URL.
        /// Handles /t/{slug}/{topicId} and /t/{slug}/{topicId}/{postNumber}.
        /// </summary>
        public static bool TryParseTopicUrl(Uri uri, out int topicId, out int? postNumber)
        {
            topicId = 0;
            postNumber = null;

            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 2 || !segments[0].Equals("t", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            for (int i = 1; i < segments.Length; i++)
            {
                if (int.TryParse(segments[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
                {
                    topicId = id;

                    if (i + 1 < segments.Length
                        && int.TryParse(segments[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int postNum))
                    {
                        postNumber = postNum;
                    }

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Tries to parse a Discourse post URL.
        /// Handles /p/{postId} and /posts/{postId}.
        /// </summary>
        public static bool TryParsePostUrl(Uri uri, out int postId)
        {
            postId = 0;

            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 2)
            {
                return false;
            }

            if (segments[0].Equals("p", StringComparison.OrdinalIgnoreCase)
                || segments[0].Equals("posts", StringComparison.OrdinalIgnoreCase))
            {
                return int.TryParse(segments[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out postId);
            }

            return false;
        }

        /// <summary>
        /// Builds the Discourse topic JSON API URL.
        /// </summary>
        public static string BuildTopicJsonUrl(string baseUrl, int topicId, bool includeRaw = true)
        {
            var url = $"{baseUrl.TrimEnd('/')}/t/{topicId}.json";
            if (includeRaw)
            {
                url += "?include_raw=1";
            }

            return url;
        }

        /// <summary>
        /// Builds the Discourse post JSON API URL.
        /// </summary>
        public static string BuildPostJsonUrl(string baseUrl, int postId)
        {
            return $"{baseUrl.TrimEnd('/')}/posts/{postId}.json";
        }

        /// <summary>
        /// Fetches a single Discourse post by ID.
        /// </summary>
        public static async Task<JObject> FetchPostAsync(HttpClient httpClient, string baseUrl, int postId)
        {
            var url = BuildPostJsonUrl(baseUrl, postId);
            var response = await httpClient.GetAsync(new Uri(url)).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                string serverMessage = DiscourseUtils.ExtractDiscourseErrorMessage(content);
                string errorMessage = string.IsNullOrWhiteSpace(serverMessage)
                    ? $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}"
                    : $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}: {serverMessage}";

                throw new HttpRequestException(errorMessage);
            }

            return JsonConvert.DeserializeObject<JObject>(content, new JsonSerializerSettings { DateParseHandling = DateParseHandling.None });
        }

        /// <summary>
        /// Fetches a Discourse topic by ID, including raw markdown for each post by default.
        /// </summary>
        public static async Task<JObject> FetchTopicAsync(HttpClient httpClient, string baseUrl, int topicId, bool includeRaw = true)
        {
            var url = BuildTopicJsonUrl(baseUrl, topicId, includeRaw);
            var response = await httpClient.GetAsync(new Uri(url)).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                string serverMessage = DiscourseUtils.ExtractDiscourseErrorMessage(content);
                string errorMessage = string.IsNullOrWhiteSpace(serverMessage)
                    ? $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}"
                    : $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}: {serverMessage}";

                throw new HttpRequestException(errorMessage);
            }

            return JsonConvert.DeserializeObject<JObject>(content, new JsonSerializerSettings { DateParseHandling = DateParseHandling.None });
        }

        /// <summary>
        /// Formats a Discourse topic JSON object as Markdown.
        /// </summary>
        public static string FormatTopicAsMarkdown(JObject topicJson, string baseUrl, int? maxPosts = null)
        {
            var builder = new StringBuilder();

            string title = topicJson.Value<string>("title")?.Trim();
            if (!string.IsNullOrWhiteSpace(title))
            {
                builder.Append("# ").AppendLine(title).AppendLine();
            }

            int topicId = topicJson.Value<int>("id");
            string slug = topicJson.Value<string>("slug")?.Trim();
            string topicUrl = string.IsNullOrWhiteSpace(slug)
                ? $"{baseUrl.TrimEnd('/')}/t/{topicId}"
                : $"{baseUrl.TrimEnd('/')}/t/{slug}/{topicId}";

            builder.AppendLine($"source: {topicUrl}").AppendLine();

            var posts = topicJson.SelectToken("post_stream.posts") as JArray;
            if (posts == null)
            {
                return builder.ToString().Trim();
            }

            int count = 0;
            foreach (var post in posts.OfType<JObject>())
            {
                if (maxPosts.HasValue && count >= maxPosts.Value)
                {
                    break;
                }

                count++;

                string username = post.Value<string>("username") ?? "Unknown";
                string createdAt = post.Value<string>("created_at") ?? string.Empty;
                string raw = post.Value<string>("raw") ?? string.Empty;

                builder.Append("## ").Append(username);
                if (!string.IsNullOrWhiteSpace(createdAt))
                {
                    builder.Append(" – ").Append(createdAt);
                }

                builder.AppendLine().AppendLine();
                builder.AppendLine(raw.Trim());
                builder.AppendLine();
            }

            return builder.ToString().Trim();
        }

        /// <summary>
        /// Formats a Discourse post JSON object as Markdown.
        /// </summary>
        public static string FormatPostAsMarkdown(JObject postJson, string baseUrl)
        {
            var builder = new StringBuilder();

            string username = postJson.Value<string>("username") ?? "Unknown";
            string createdAt = postJson.Value<string>("created_at") ?? string.Empty;
            string raw = postJson.Value<string>("raw") ?? string.Empty;
            int topicId = postJson.Value<int>("topic_id");
            string topicSlug = postJson.Value<string>("topic_slug")?.Trim();
            int postNumber = postJson.Value<int>("post_number");

            string postUrl = topicId > 0
                ? (string.IsNullOrWhiteSpace(topicSlug)
                    ? $"{baseUrl.TrimEnd('/')}/t/{topicId}/{postNumber}"
                    : $"{baseUrl.TrimEnd('/')}/t/{topicSlug}/{topicId}/{postNumber}")
                : baseUrl;

            builder.AppendLine($"source: {postUrl}").AppendLine();
            builder.Append("## ").Append(username);
            if (!string.IsNullOrWhiteSpace(createdAt))
            {
                builder.Append(" – ").Append(createdAt);
            }

            builder.AppendLine().AppendLine();
            builder.AppendLine(raw.Trim());

            return builder.ToString().Trim();
        }
    }
}
