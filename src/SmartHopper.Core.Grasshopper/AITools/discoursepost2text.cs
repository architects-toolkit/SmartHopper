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
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
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
    /// Provides generic AI tools for fetching and summarizing Discourse forum posts from any Discourse instance.
    /// Requires the base URL to be specified as a parameter.
    /// </summary>
    public class discoursepost2text : IAIToolProvider
    {
        private readonly string getPostToolName = "discourse_post_get";
        private readonly string summarizeToolName = "discourse_post_summarize";

        private readonly string summarizeSystemPromptTemplate =
            "You are a helpful assistant that summarizes forum posts concisely. Provide a brief 1-2 sentence summary of the main point or question.";

        private readonly AICapability summarizeCapabilityRequirements = AICapability.TextInput | AICapability.TextOutput;

        /// <inheritdoc/>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.getPostToolName,
                description: "Retrieve a filtered Discourse forum post by ID from any Discourse instance (username, date, title, raw markdown). Provide the base URL of the forum.",
                category: "Knowledge",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""base_url"": {
                            ""type"": ""string"",
                            ""description"": ""Base URL of the Discourse forum (e.g., https://discourse.example.com).""
                        },
                        ""id"": {
                            ""type"": ""integer"",
                            ""description"": ""ID of the forum post to fetch.""
                        }
                    },
                    ""required"": [""base_url"", ""id""]
                }",
                execute: this.GetPostAsync);

            yield return new AITool(
                name: this.summarizeToolName,
                description: "Generate a concise summary of one or more Discourse forum posts by ID from any Discourse instance.",
                category: "Knowledge",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""base_url"": {
                            ""type"": ""string"",
                            ""description"": ""Base URL of the Discourse forum (e.g., https://discourse.example.com).""
                        },
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
                    ""required"": [""base_url"", ""ids""]
                }",
                execute: this.SummarizePostAsync,
                requiredCapabilities: this.summarizeCapabilityRequirements);
        }

        private async Task<AIReturn> GetPostAsync(AIToolCall toolCall)
        {
            var output = new AIReturn() { Request = toolCall };

            try
            {
                toolCall.SkipMetricsValidation = true;

                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.Arguments ?? new JObject();

                string baseUrl = args["base_url"]?.ToString();
                if (string.IsNullOrWhiteSpace(baseUrl))
                {
                    output.CreateError("Missing 'base_url' parameter.");
                    return output;
                }

                int? idNullable = args["id"]?.Value<int>();
                if (!idNullable.HasValue)
                {
                    output.CreateError("Missing 'id' parameter.");
                    return output;
                }

                int id = idNullable.Value;
                var filteredPost = await this.FetchFilteredPostAsync(baseUrl, id).ConfigureAwait(false);

                var toolResult = new JObject
                {
                    ["id"] = id,
                    ["post"] = filteredPost,
                };

                var builder = AIBodyBuilder.Create();
                builder.AddToolResult(toolResult, toolInfo.Id, toolInfo.Name);
                output.CreateSuccess(builder.Build(), toolCall);
                return output;
            }
            catch (Exception ex)
            {
                output.CreateError($"Error: {ex.Message}");
                return output;
            }
        }

        private async Task<AIReturn> SummarizePostAsync(AIToolCall toolCall)
        {
            var output = new AIReturn() { Request = toolCall };

            try
            {
                Debug.WriteLine("[DiscourseTools] Running SummarizePost tool");

                string providerName = toolCall.Provider;
                string modelName = toolCall.Model;

                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.Arguments ?? new JObject();

                string baseUrl = args["base_url"]?.ToString();
                if (string.IsNullOrWhiteSpace(baseUrl))
                {
                    output.CreateError("Missing 'base_url' parameter.");
                    return output;
                }

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
                else if (idsToken != null && idsToken.Type == JTokenType.Integer)
                {
                    ids.Add(idsToken.Value<int>());
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
                    var filteredPost = await this.FetchFilteredPostAsync(baseUrl, id).ConfigureAwait(false);

                    string username = filteredPost.Value<string>("username") ?? "Unknown";
                    string date = filteredPost.Value<string>("date") ?? string.Empty;
                    string raw = filteredPost.Value<string>("raw") ?? string.Empty;

                    string userContent = $"Summarize this forum post:\n\nAuthor: {username}\nDate: {date}\nContent:\n{raw}";

                    if (!string.IsNullOrWhiteSpace(instructions))
                    {
                        userContent += $"\n\nFocus the summary on the following question/target/concern:\n{instructions}";
                    }

                    var bodyBuilder = AIBodyBuilder.Create()
                        .AddText(AIAgent.Context, this.summarizeSystemPromptTemplate)
                        .AddText(AIAgent.User, userContent)
                        .WithContextFilter("-*");

                    var summaryRequest = new AIRequestCall();
                    summaryRequest.Initialize(
                        provider: providerName,
                        model: modelName,
                        capability: AICapability.TextInput | AICapability.TextOutput,
                        endpoint: this.summarizeToolName,
                        body: bodyBuilder.Build());

                    var summaryResult = await summaryRequest.Exec().ConfigureAwait(false);

                    if (!summaryResult.Success)
                    {
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

                    summariesArray.Add(new JObject
                    {
                        ["id"] = id,
                        ["summary"] = summary,
                        ["username"] = username,
                        ["date"] = date,
                    });
                }

                var toolResult = new JObject { ["summaries"] = summariesArray };

                if (summariesArray.Count == 1 && summariesArray[0] is JObject firstSummary)
                {
                    toolResult["id"] = firstSummary["id"];
                    toolResult["summary"] = firstSummary["summary"];
                    toolResult["username"] = firstSummary["username"];
                    toolResult["date"] = firstSummary["date"];
                }

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
                Debug.WriteLine($"[DiscourseTools] Error in SummarizePost: {ex.Message}");
                output.CreateError($"Error: {ex.Message}");
                return output;
            }
        }

        private async Task<JObject> FetchPostAsync(string baseUrl, int id)
        {
            using var httpClient = new HttpClient();
            var postUri = new Uri($"{baseUrl}/posts/{id}.json");
            var response = await httpClient.GetAsync(postUri).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JObject.Parse(content);
        }

        private async Task<JObject> FetchFilteredPostAsync(string baseUrl, int id)
        {
            var postJson = await this.FetchPostAsync(baseUrl, id).ConfigureAwait(false);
            string filteredJson = DiscourseUtils.FilterPostJson(postJson.ToString(Newtonsoft.Json.Formatting.None));
            return JObject.Parse(filteredJson);
        }
    }
}
