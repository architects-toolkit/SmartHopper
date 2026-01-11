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
 * along with this library; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using System;
using System.Diagnostics;
using Newtonsoft.Json.Linq;

namespace SmartHopper.Core.Grasshopper.Utils
{
    public static class McNeelForumUtils
    {
        public static string FilterPostJson(string postJson)
        {
            if (string.IsNullOrWhiteSpace(postJson))
            {
                return postJson ?? string.Empty;
            }

            try
            {
                var postObject = JObject.Parse(postJson);

                int id = postObject["id"]?.Value<int?>() ?? 0;
                string username = postObject["username"]?.Value<string>() ?? string.Empty;
                int topicId = postObject["topic_id"]?.Value<int?>() ?? 0;

                string title = postObject["title"]?.Value<string>()
                    ?? postObject["topic_title"]?.Value<string>()
                    ?? string.Empty;

                string date = postObject["date"]?.Value<string>();
                if (string.IsNullOrWhiteSpace(date))
                {
                    date = postObject["created_at"]?.Value<string>() ?? string.Empty;
                }

                bool hasRaw = postObject["raw"] != null;
                string rawContent = hasRaw ? postObject["raw"]?.Value<string>() ?? string.Empty : string.Empty;

                int? readsValue = postObject["reads"]?.Value<int?>();
                bool hasReads = readsValue.HasValue && readsValue.Value > 0;

                var filteredObject = new JObject
                {
                    ["id"] = id,
                    ["username"] = username,
                    ["topic_id"] = topicId,
                    ["date"] = date,
                };

                if (!string.IsNullOrWhiteSpace(title))
                {
                    filteredObject["title"] = title;
                }

                if (hasRaw && !string.IsNullOrWhiteSpace(rawContent))
                {
                    filteredObject["raw"] = rawContent;
                }

                if (hasReads)
                {
                    filteredObject["reads"] = readsValue.Value;
                }

                if (postObject["post_number"] != null)
                {
                    filteredObject["post_number"] = postObject["post_number"];
                }

                int likes = 0;
                var actionsSummary = postObject["actions_summary"] as JArray;
                if (actionsSummary != null)
                {
                    foreach (var item in actionsSummary)
                    {
                        if (item is JObject action && action["id"]?.Value<int?>() == 2)
                        {
                            likes = action["count"]?.Value<int?>() ?? 0;
                            break;
                        }
                    }
                }

                filteredObject["likes"] = likes;

                return filteredObject.ToString(Newtonsoft.Json.Formatting.None);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[McNeelForumUtils] Failed to filter post JSON: {ex.Message}");
                return postJson;
            }
        }

        public static string FilterSuggestedTopicJson(string topicJson)
        {
            if (string.IsNullOrWhiteSpace(topicJson))
            {
                return topicJson ?? string.Empty;
            }

            try
            {
                var topicObject = JObject.Parse(topicJson);

                int id = topicObject["id"]?.Value<int?>() ?? 0;
                string title = topicObject["title"]?.Value<string>() ?? string.Empty;

                string createdAt = topicObject["created_at"]?.Value<string>() ?? string.Empty;
                string lastPostedAt = topicObject["last_posted_at"]?.Value<string>() ?? string.Empty;

                int postsCount = topicObject["posts_count"]?.Value<int?>() ?? 0;
                int views = topicObject["views"]?.Value<int?>() ?? 0;
                int likeCount = topicObject["like_count"]?.Value<int?>() ?? 0;

                var filteredObject = new JObject
                {
                    ["id"] = id,
                };

                if (!string.IsNullOrWhiteSpace(title))
                {
                    filteredObject["title"] = title;
                }

                if (!string.IsNullOrWhiteSpace(createdAt))
                {
                    filteredObject["created_at"] = createdAt;
                }

                if (!string.IsNullOrWhiteSpace(lastPostedAt))
                {
                    filteredObject["last_posted_at"] = lastPostedAt;
                }

                if (postsCount > 0)
                {
                    filteredObject["posts_count"] = postsCount;
                }

                if (views > 0)
                {
                    filteredObject["views"] = views;
                }

                if (likeCount > 0)
                {
                    filteredObject["like_count"] = likeCount;
                }

                var tags = topicObject["tags"] as JArray;
                if (tags != null && tags.Count > 0)
                {
                    filteredObject["tags"] = tags;
                }

                return filteredObject.ToString(Newtonsoft.Json.Formatting.None);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[McNeelForumUtils] Failed to filter suggested topic JSON: {ex.Message}");
                return topicJson;
            }
        }
    }
}
