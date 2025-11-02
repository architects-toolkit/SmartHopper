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
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
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
                description: "Search McNeel Discourse forum posts by query and optionally get summaries. Returns matching posts with optional AI-generated summaries.",
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
                        },
                        ""summarize"": {
                            ""type"": ""boolean"",
                            ""description"": ""Whether to generate AI summaries for the posts. Limited to first 5 posts when enabled (default: false)."",
                            ""default"": false
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
                Debug.WriteLine("[McNeelForumTools] Running Search tool");

                // Extract provider and model from toolCall
                string providerName = toolCall.Provider;
                string modelName = toolCall.Model;

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

                bool summarize = args["summarize"]?.Value<bool>() ?? false;

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

                // Build a map of topic ID to title
                var topicTitles = topics
                    .Where(t => t["id"] != null)
                    .ToDictionary(t => (int)t["id"], t => (string)(t["title"] ?? t["fancy_title"] ?? string.Empty));

                var result = new JArray();

                // Process posts
                for (int i = 0; i < posts.Count; i++)
                {
                    var p = posts[i];
                    int postId = p.Value<int>("id");
                    int topicId = p.Value<int>("topic_id");

                    var postObj = new JObject
                    {
                        ["id"] = postId,
                        ["username"] = p.Value<string>("username"),
                        ["topic_id"] = topicId,
                        ["title"] = topicTitles.GetValueOrDefault(topicId, string.Empty),
                        ["date"] = p.Value<string>("created_at"),
                        ["cooked"] = p.Value<string>("cooked"),
                    };

                    // Generate summary if requested and within limit
                    if (summarize && i < 5)
                    {
                        try
                        {
                            string summary = await this.GenerateSummaryAsync(postId, providerName, modelName).ConfigureAwait(false);
                            postObj["summary"] = summary;
                        }
                        catch (Exception ex)
                        {
                            postObj["summary"] = $"Summary generation failed: {ex.Message}";
                        }
                    }

                    result.Add(postObj);
                }

                var toolResult = new JObject
                {
                    ["query"] = query,
                    ["results"] = result,
                    ["count"] = result.Count,
                    ["summarized"] = summarize ? Math.Min(result.Count, 5) : 0,
                };

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

        /// <summary>
        /// Generates a summary for a post using the mcneel_forum_post_summarize subtool.
        /// </summary>
        private async Task<string> GenerateSummaryAsync(int postId, string providerName, string modelName)
        {
            // Create a tool call for the summarize subtool
            var summarizeArgs = new JObject
            {
                ["id"] = postId,
            };

            var toolCallInteraction = new AIInteractionToolCall(
                AIAgent.Assistant,
                id: Guid.NewGuid().ToString(),
                name: "mcneel_forum_post_summarize",
                arguments: summarizeArgs);

            var toolCallRequest = new AIToolCall(toolCallInteraction, providerName, modelName);
            var result = await toolCallRequest.Exec().ConfigureAwait(false);

            if (result.Status == AICallStatus.Success)
            {
                var lastInteraction = result.Body?.Interactions?.LastOrDefault();
                if (lastInteraction is AIInteractionToolResult toolResult)
                {
                    var resultJson = toolResult.Result as JObject;
                    return resultJson?["summary"]?.ToString() ?? "No summary available.";
                }
            }

            return "Summary generation failed.";
        }
    }
}
