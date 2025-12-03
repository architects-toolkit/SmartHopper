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
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Utils;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Metrics;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.AITools;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Provides AI tools for fetching and summarizing McNeel Discourse forum posts.
    /// </summary>
    public class mcneel_forum_post : IAIToolProvider
    {
        /// <summary>
        /// Name of the get post tool.
        /// </summary>
        private readonly string getPostToolName = "mcneel_forum_post_get";

        /// <summary>
        /// Name of the summarize post tool.
        /// </summary>
        private readonly string summarizeToolName = "mcneel_forum_post_summarize";

        /// <summary>
        /// System prompt template for summarizing forum posts.
        /// </summary>
        private readonly string summarizeSystemPromptTemplate =
            "You are a helpful assistant that summarizes forum posts concisely. Provide a brief 1-2 sentence summary of the main point or question.";

        /// <summary>
        /// Returns the list of tools provided by this class.
        /// </summary>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.getPostToolName,
                description: "Retrieve a filtered McNeel Discourse forum post by ID (username, date, title, raw markdown). Typically use after mcneel_forum_search or when the user provides a specific post URL/ID.",
                category: "Knowledge",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""id"": {
                            ""type"": ""integer"",
                            ""description"": ""ID of the forum post to fetch.""
                        }
                    },
                    ""required"": [""id""]
                }",
                execute: this.GetPostAsync);

            yield return new AITool(
                name: this.summarizeToolName,
                description: "Generate a concise summary of one or more McNeel Discourse forum posts by ID. Usually use on post IDs from mcneel_forum_search. Prefer mcneel_forum_topic_summarize to better understand the topic context.",
                category: "Knowledge",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""ids"": {
                            ""type"": ""array"",
                            ""items"": {
                                ""type"": ""integer""
                            },
                            ""description"": ""ID or list of forum post IDs to summarize.""
                        },
                        ""instructions"": {
                            ""type"": ""string"",
                            ""description"": ""Optional targeted summary instructions to focus on a specific question, target, or concern.""
                        }
                    },
                    ""required"": [""ids""]
                }",
                execute: this.SummarizePostAsync);
        }

        /// <summary>
        /// Retrieves a full McNeel Discourse forum post by ID.
        /// </summary>
        private async Task<AIReturn> GetPostAsync(AIToolCall toolCall)
        {
            var output = new AIReturn()
            {
                Request = toolCall,
            };

            try
            {
                // Local tool: skip metrics validation (provider/model/finish_reason not required)
                toolCall.SkipMetricsValidation = true;

                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.Arguments ?? new JObject();
                int? idNullable = args["id"]?.Value<int>();
                if (!idNullable.HasValue)
                {
                    output.CreateError("Missing 'id' parameter.");
                    return output;
                }

                int id = idNullable.Value;
                var filteredPost = await this.FetchFilteredPostAsync(id).ConfigureAwait(false);

                var toolResult = new JObject
                {
                    ["id"] = id,
                    ["post"] = filteredPost,
                };

                var builder = AIBodyBuilder.Create();
                builder.AddToolResult(toolResult, toolInfo.Id, toolInfo.Name);
                var immutable = builder.Build();
                output.CreateSuccess(immutable, toolCall);
                return output;
            }
            catch (Exception ex)
            {
                output.CreateError($"Error: {ex.Message}");
                return output;
            }
        }

        /// <summary>
        /// Generates a summary of one or more McNeel Discourse forum posts by ID.
        /// </summary>
        private async Task<AIReturn> SummarizePostAsync(AIToolCall toolCall)
        {
            var output = new AIReturn()
            {
                Request = toolCall,
            };

            try
            {
                Debug.WriteLine("[McNeelForumTools] Running SummarizePost tool");

                // Extract provider and model from toolCall
                string providerName = toolCall.Provider;
                string modelName = toolCall.Model;
                string endpoint = this.summarizeToolName;

                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.Arguments ?? new JObject();

                // Collect IDs from optional "ids" array and legacy "id" field
                var ids = new List<int>();

                var idsToken = args["ids"];
                if (idsToken is JArray idsArray)
                {
                    foreach (var token in idsArray)
                    {
                        if (token != null && token.Type == JTokenType.Integer)
                        {
                            ids.Add(token.Value<int>());
                        }
                    }
                }

                // When only one id is provided, add it to the list
                else if (idsToken != null && idsToken.Type == JTokenType.Integer)
                {
                    ids.Add(idsToken.Value<int>());
                }

                int? idNullable = args["id"]?.Value<int>();
                if (idNullable.HasValue && !ids.Contains(idNullable.Value))
                {
                    ids.Add(idNullable.Value);
                }

                if (ids.Count == 0)
                {
                    output.CreateToolError("Missing required parameter: ids", toolCall);
                    return output;
                }

                string instructions = args["instructions"]?.ToString();

                var summariesArray = new JArray();
                var accumulatedMetrics = new AIMetrics();
                var allMessages = new List<AIRuntimeMessage>();

                foreach (int id in ids)
                {
                    var filteredPost = await this.FetchFilteredPostAsync(id).ConfigureAwait(false);

                    // Extract key information for summarization
                    string username = filteredPost.Value<string>("username") ?? "Unknown";
                    string date = filteredPost.Value<string>("date") ?? string.Empty;
                    string raw = filteredPost.Value<string>("raw") ?? string.Empty;

                    // Build user content with optional targeted instructions
                    string userContent =
                        $"Summarize this forum post:\n\nAuthor: {username}\nDate: {date}\nContent:\n{raw}";

                    if (!string.IsNullOrWhiteSpace(instructions))
                    {
                        userContent += $"\n\nFocus the summary on the following question/target/concern:\n{instructions}";
                    }

                    // Create the summary request body
                    var bodyBuilder = AIBodyBuilder.Create()
                        .AddText(
                            AIAgent.Context,
                            this.summarizeSystemPromptTemplate)
                        .AddText(
                            AIAgent.User,
                            userContent)
                        .WithContextFilter("-*");

                    var requestBody = bodyBuilder.Build();

                    // Initiate AIRequestCall with explicit provider and model
                    var summaryRequest = new AIRequestCall();
                    summaryRequest.Initialize(
                        provider: providerName,
                        model: modelName,
                        capability: AICapability.TextInput | AICapability.TextOutput,
                        endpoint: endpoint,
                        body: requestBody);

                    // Execute the AIRequestCall
                    var summaryResult = await summaryRequest.Exec().ConfigureAwait(false);

                    if (!summaryResult.Success)
                    {
                        // Propagate structured messages from AI call
                        output.Messages = summaryResult.Messages;
                        return output;
                    }

                    if (summaryResult.Metrics != null)
                    {
                        accumulatedMetrics.Combine(summaryResult.Metrics);
                    }

                    if (summaryResult.Messages != null && summaryResult.Messages.Count > 0)
                    {
                        allMessages.AddRange(summaryResult.Messages);
                    }

                    string summary = summaryResult.Body?.GetLastText() ?? "Failed to generate summary.";

                    var summaryObject = new JObject
                    {
                        ["id"] = id,
                        ["summary"] = summary,
                        ["username"] = username,
                        ["date"] = date,
                    };

                    summariesArray.Add(summaryObject);
                }

                var toolResult = new JObject
                {
                    ["summaries"] = summariesArray,
                };

                // Backward compatibility: expose first summary at root level when only one ID is provided
                if (summariesArray.Count == 1 && summariesArray[0] is JObject firstSummary)
                {
                    toolResult["id"] = firstSummary["id"];
                    toolResult["summary"] = firstSummary["summary"];
                    toolResult["username"] = firstSummary["username"];
                    toolResult["date"] = firstSummary["date"];
                }

                // Attach non-breaking result envelope
                toolResult.WithEnvelope(
                    ToolResultEnvelope.Create(
                        tool: this.summarizeToolName,
                        type: ToolResultContentType.List,
                        payloadPath: "summaries",
                        provider: providerName,
                        model: modelName,
                        toolCallId: toolInfo?.Id));

                var toolBody = AIBodyBuilder.Create()
                    .AddToolResult(toolResult, id: toolInfo?.Id, name: this.summarizeToolName, metrics: accumulatedMetrics, messages: allMessages)
                    .Build();

                output.CreateSuccess(toolBody);
                return output;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[McNeelForumTools] Error in SummarizePost: {ex.Message}");

                output.CreateError($"Error: {ex.Message}");
                return output;
            }
        }

        /// <summary>
        /// Helper method to fetch a post from the McNeel Discourse forum.
        /// </summary>
        private async Task<JObject> FetchPostAsync(int id)
        {
            using var httpClient = new HttpClient();
            var postUri = new Uri($"https://discourse.mcneel.com/posts/{id}.json");
            var response = await httpClient.GetAsync(postUri).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JObject.Parse(content);
        }

        private async Task<JObject> FetchFilteredPostAsync(int id)
        {
            var postJson = await this.FetchPostAsync(id).ConfigureAwait(false);
            string filteredJson = McNeelForumUtils.FilterPostJson(postJson.ToString(Newtonsoft.Json.Formatting.None));
            return JObject.Parse(filteredJson);
        }
    }
}
