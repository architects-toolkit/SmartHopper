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
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Utils;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Provides AI tools for searching McNeel Discourse forum posts.
    /// </summary>
    public class mcneel_forum_search : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "mcneel_forum_search";

        /// <summary>
        /// Returns the list of tools provided by this class.
        /// </summary>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Search McNeel Discourse forum posts by query and return filtered post JSON objects. Typically call this first, then use mcneel_forum_topic_get or mcneel_forum_post_get / _summarize on interesting results.",
                category: "Knowledge",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""query"": {
                            ""type"": ""string"",
                            ""description"": ""Search query for McNeel Discourse forum.""
                        },
                        ""limit"": {
                            ""type"": ""integer"",
                            ""description"": ""Maximum number of posts to return (default: 10, max: 50)."",
                            ""default"": 10
                        }
                    },
                    ""required"": [""query""]
                }",
                execute: this.SearchAsync);
        }

        private async Task<AIReturn> SearchAsync(AIToolCall toolCall)
        {
            var output = new AIReturn()
            {
                Request = toolCall,
            };

            try
            {
                // Local tool: skip metrics validation (provider/model/finish_reason not required)
                toolCall.SkipMetricsValidation = true;

                Debug.WriteLine("[McNeelForumTools] Running Search tool");

                // Extract parameters
                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.Arguments ?? new JObject();
                string query = args["query"]?.ToString();
                if (string.IsNullOrEmpty(query))
                {
                    output.CreateError("Missing 'query' parameter.");
                    return output;
                }

                int limit = args["limit"]?.Value<int>() ?? 10;
                limit = Math.Max(1, Math.Min(limit, 50)); // Clamp between 1 and 50

                // Fetch search results
                using var httpClient = new HttpClient();
                var searchUri = new Uri($"https://discourse.mcneel.com/search.json?q={Uri.EscapeDataString(query)}");
                var response = await httpClient.GetAsync(searchUri).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var json = JObject.Parse(content);
                var posts = json["posts"] as JArray ?? new JArray();
                posts = new JArray(posts.Take(limit));
                var topics = json["topics"] as JArray ?? new JArray();

                int rawPostsCount = (json["posts"] as JArray)?.Count ?? 0;
                Debug.WriteLine($"[McNeelForumTools] Search response parsed. RawPosts={rawPostsCount}, ClampedPosts={posts.Count}, Topics={topics.Count}");

                // Build a map of topic ID to title
                var topicTitles = topics
                    .Where(t => t["id"] != null)
                    .ToDictionary(t => (int)t["id"], t => (string)(t["title"] ?? t["fancy_title"] ?? string.Empty));

                var result = new JArray();

                // Process posts
                for (int i = 0; i < posts.Count; i++)
                {
                    var p = posts[i];
                    int topicId = p.Value<int>("topic_id");

                    var baseObject = (JObject)p.DeepClone();
                    baseObject["title"] = topicTitles.GetValueOrDefault(topicId, string.Empty);

                    string filteredJson = McNeelForumUtils.FilterPostJson(baseObject.ToString(Newtonsoft.Json.Formatting.None));
                    var filteredObject = JObject.Parse(filteredJson);

                    result.Add(filteredObject);
                }

                var toolResult = new JObject
                {
                    ["query"] = query,
                    ["results"] = result,
                    ["count"] = result.Count,
                };

                Debug.WriteLine($"[McNeelForumTools] Returning {result.Count} posts for query='{query}'");

                var builder = AIBodyBuilder.Create();
                builder.AddToolResult(toolResult, toolInfo.Id, toolInfo.Name);
                var immutable = builder.Build();
                output.CreateSuccess(immutable, toolCall);
                return output;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[McNeelForumTools] Error in Search: {ex.Message}");

                output.CreateError($"Error: {ex.Message}");
                return output;
            }
        }
    }
}
