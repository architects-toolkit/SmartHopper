/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
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
    }
}
