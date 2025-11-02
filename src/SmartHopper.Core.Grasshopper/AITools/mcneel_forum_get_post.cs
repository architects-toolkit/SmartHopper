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
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.Infrastructure.Utils;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Provides AI tools for fetching and summarizing McNeel Discourse forum posts.
    /// </summary>
    public class mcneel_forum_get_post : IAIToolProvider
    {
        /// <summary>
        /// Name of the get post tool.
        /// </summary>
        private readonly string getPostToolName = "mcneel_forum_get_post";

        /// <summary>
        /// Name of the summarize post tool.
        /// </summary>
        private readonly string summarizeToolName = "mcneel_forum_post_summarize";

        /// <summary>
        /// Returns the list of tools provided by this class.
        /// </summary>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.getPostToolName,
                description: "Retrieve a full McNeel Discourse forum post by ID.",
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
                description: "Generate a concise summary of a McNeel Discourse forum post by ID. Returns a brief summary of the post content.",
                category: "Knowledge",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""id"": {
                            ""type"": ""integer"",
                            ""description"": ""ID of the forum post to summarize.""
                        }
                    },
                    ""required"": [""id""]
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
                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.Arguments ?? new JObject();
                int? idNullable = args["id"]?.Value<int>();
                if (!idNullable.HasValue)
                {
                    output.CreateError("Missing 'id' parameter.");
                    return output;
                }

                int id = idNullable.Value;
                var postJson = await this.FetchPostAsync(id).ConfigureAwait(false);

                var toolResult = new JObject
                {
                    ["id"] = id,
                    ["post"] = postJson,
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
        /// Generates a summary of a McNeel Discourse forum post by ID.
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
                int? idNullable = args["id"]?.Value<int>();
                if (!idNullable.HasValue)
                {
                    output.CreateToolError("Missing required parameter: id", toolCall);
                    return output;
                }

                int id = idNullable.Value;
                var postJson = await this.FetchPostAsync(id).ConfigureAwait(false);

                // Extract key information for summarization
                string username = postJson.Value<string>("username") ?? "Unknown";
                string cooked = postJson.Value<string>("cooked") ?? string.Empty;
                string createdAt = postJson.Value<string>("created_at") ?? string.Empty;

                // Create the summary request body
                var requestBody = AIBodyBuilder.Create()
                    .AddInteractionText(
                        AIAgent.Context,
                        "You are a helpful assistant that summarizes forum posts concisely. Provide a brief 1-2 sentence summary of the main point or question.")
                    .AddInteractionText(
                        AIAgent.User,
                        $"Summarize this forum post:\n\nAuthor: {username}\nDate: {createdAt}\nContent:\n{cooked}")
                    .Build();

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

                string summary = summaryResult.GetLastText() ?? "Failed to generate summary.";

                var toolResult = new JObject
                {
                    ["id"] = id,
                    ["summary"] = summary,
                    ["username"] = username,
                    ["date"] = createdAt,
                };

                // Attach non-breaking result envelope
                toolResult.WithEnvelope(
                    ToolResultEnvelope.Create(
                        tool: this.summarizeToolName,
                        type: ToolResultContentType.Text,
                        payloadPath: "summary",
                        provider: providerName,
                        model: modelName,
                        toolCallId: toolInfo?.Id));

                var toolBody = AIBodyBuilder.Create()
                    .AddToolResult(toolResult, id: toolInfo?.Id, name: this.summarizeToolName, metrics: summaryResult.Metrics, messages: summaryResult.Messages)
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
    }
}
