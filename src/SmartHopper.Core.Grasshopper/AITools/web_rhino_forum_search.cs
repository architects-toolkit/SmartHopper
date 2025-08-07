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
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AITools;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Provides AI tools for fetching webpage text content,
    /// omitting HTML, scripts, styles, images, and respecting robots.txt rules.
    /// </summary>
    public class web_rhino_forum_search : IAIToolProvider
    {
        /// <summary>
        /// Returns the list of tools provided by this class.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: "web_rhino_forum_search",
                description: "Search Rhino Discourse forum posts by query and return up to 10 matching posts.",
                category: "Knowledge",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""query"": {
                            ""type"": ""string"",
                            ""description"": ""Search query for Rhino Discourse forum.""
                        }
                    },
                    ""required"": [""query""]
                }",
                execute: this.WebRhinoForumSearchAsync);
        }

        // TODO: take only 5 and return a summary of the posts
        private async Task<object> WebRhinoForumSearchAsync(JObject parameters)
        {
            string query = parameters.Value<string>("query") ?? throw new ArgumentException("Missing 'query' parameter.");
            var httpClient = new HttpClient();
            var searchUri = new Uri($"https://discourse.mcneel.com/search.json?q={Uri.EscapeDataString(query)}");
            var response = await httpClient.GetAsync(searchUri).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var json = JObject.Parse(content);
            var posts = json["posts"] as JArray ?? new JArray();
            posts = new JArray(posts.Take(10));
            var topics = json["topics"] as JArray ?? new JArray();

            // Build a map of topic ID to title
            var topicTitles = topics
                .Where(t => t["id"] != null)
                .ToDictionary(t => (int)t["id"], t => (string)(t["title"] ?? t["fancy_title"] ?? string.Empty));
            var result = new JArray(posts.Select(p =>
            {
                int postId = p.Value<int>("id");
                int topicId = p.Value<int>("topic_id");
                return new JObject
                {
                    ["id"] = postId,
                    ["username"] = p.Value<string>("username"),
                    ["topic_id"] = topicId,
                    ["title"] = topicTitles.GetValueOrDefault(topicId, string.Empty),
                    ["date"] = p.Value<string>("created_at"),
                    ["cooked"] = p.Value<string>("cooked"),
                };
            }));
            return result;
        }
    }
}
