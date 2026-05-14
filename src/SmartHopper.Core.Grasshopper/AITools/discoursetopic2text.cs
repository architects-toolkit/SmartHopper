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
using System.Text;
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
    /// Provides generic AI tools for fetching and summarizing Discourse forum topics from any Discourse instance.
    /// Requires the base URL to be specified as a parameter.
    /// </summary>
    public class discoursetopic2text : IAIToolProvider
    {
        private readonly string getTopicToolName = "discourse_topic_get";
        private readonly string summarizeTopicToolName = "discourse_topic_summarize";

        private readonly AICapability summarizeCapabilityRequirements = AICapability.TextInput | AICapability.TextOutput;

        private readonly string summarizeTopicSystemPromptTemplate =
            "You are a helpful assistant that summarizes entire forum topics for users. Provide a concise summary of the main question or issue, key ideas discussed, and any conclusions or solutions. Keep the summary focused and clear.";

        /// <inheritdoc/>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.getTopicToolName,
                description: "Retrieve all posts in a Discourse forum topic by ID from any Discourse instance (title, URL, posts array). Provide the base URL of the forum.",
                category: "Knowledge",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""base_url"": {
                            ""type"": ""string"",
                            ""description"": ""Base URL of the Discourse forum (e.g., https://discourse.example.com).""
                        },
                        ""topic_id"": {
                            ""type"": ""integer"",
                            ""description"": ""ID of the forum topic to fetch.""
                        },
                        ""max_posts"": {
                            ""type"": ""integer"",
                            ""description"": ""Optional maximum number of posts to return.""
                        }
                    },
                    ""required"": [""base_url"", ""topic_id""]
                }",
                execute: this.GetTopicAsync);

            yield return new AITool(
                name: this.summarizeTopicToolName,
                description: "Generate a concise summary of a Discourse forum topic by ID from any Discourse instance.",
                category: "Knowledge",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""base_url"": {
                            ""type"": ""string"",
                            ""description"": ""Base URL of the Discourse forum (e.g., https://discourse.example.com).""
                        },
                        ""topic_id"": {
                            ""type"": ""integer"",
                            ""description"": ""ID of the forum topic to summarize.""
                        },
                        ""max_posts"": {
                            ""type"": ""integer"",
                            ""description"": ""Optional maximum number of posts to include in the summary input (default: 50).""
                        },
                        ""instructions"": {
                            ""type"": ""string"",
                            ""description"": ""Optional targeted summary instructions.""
                        }
                    },
                    ""required"": [""base_url"", ""topic_id""]
                }",
                execute: this.SummarizeTopicAsync,
                requiredCapabilities: this.summarizeCapabilityRequirements);
        }

        private async Task<AIReturn> GetTopicAsync(AIToolCall toolCall)
        {
            var output = new AIReturn { Request = toolCall };

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

                int? topicIdNullable = args["topic_id"]?.Value<int>();
                if (!topicIdNullable.HasValue)
                {
                    output.CreateError("Missing 'topic_id' parameter.");
                    return output;
                }

                int? maxPostsNullable = args["max_posts"]?.Value<int?>();
                int maxPosts = maxPostsNullable.GetValueOrDefault(-1);

                int topicId = topicIdNullable.Value;
                var topicJson = await this.FetchTopicAsync(baseUrl, topicId).ConfigureAwait(false);

                var postStream = topicJson["post_stream"] as JObject;
                var posts = postStream?["posts"] as JArray ?? new JArray();

                if (maxPosts > 0 && posts.Count > maxPosts)
                {
                    posts = new JArray(posts.Take(maxPosts));
                }

                string title = topicJson.Value<string>("title") ?? string.Empty;
                string url = this.BuildTopicUrl(baseUrl, topicId, topicJson);

                var toolResult = new JObject
                {
                    ["topic_id"] = topicId,
                    ["title"] = title,
                    ["url"] = url,
                    ["post_count"] = posts.Count,
                    ["posts"] = posts,
                    ["topic"] = topicJson,
                };

                var builder = AIBodyBuilder.Create();
                builder.AddToolResult(toolResult, toolInfo.Id, toolInfo.Name);
                output.CreateSuccess(builder.Build(), toolCall);
                return output;
            }
            catch (HttpRequestException httpEx)
            {
                Debug.WriteLine($"[discoursetopic_get] HTTP error: {httpEx}.");
                output.CreateNetworkError(httpEx.Message, toolCall);
                return output;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[discoursetopic_get] Error: {ex}.");
                output.CreateError($"Error: {ex.Message}", toolCall);
                return output;
            }
        }

        private async Task<AIReturn> SummarizeTopicAsync(AIToolCall toolCall)
        {
            var output = new AIReturn { Request = toolCall };

            try
            {
                Debug.WriteLine("[DiscourseTools] Running SummarizeTopic tool");

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

                int? topicIdNullable = args["topic_id"]?.Value<int>();
                if (!topicIdNullable.HasValue)
                {
                    output.CreateToolError("Missing required parameter: topic_id", toolCall);
                    return output;
                }

                int topicId = topicIdNullable.Value;
                int maxPosts = args["max_posts"]?.Value<int>() ?? 50;
                if (maxPosts <= 0) maxPosts = 50;

                string instructions = args["instructions"]?.ToString();

                var topicJson = await this.FetchTopicAsync(baseUrl, topicId).ConfigureAwait(false);
                var postStream = topicJson["post_stream"] as JObject;
                var postsArray = postStream?["posts"] as JArray ?? new JArray();

                if (postsArray.Count > maxPosts)
                {
                    postsArray = new JArray(postsArray.Take(maxPosts));
                }

                string title = topicJson.Value<string>("title") ?? string.Empty;
                string url = this.BuildTopicUrl(baseUrl, topicId, topicJson);

                var contentBuilder = new StringBuilder();
                contentBuilder.AppendLine($"# Topic summary request");
                contentBuilder.AppendLine($"**Topic title:** {title}");
                contentBuilder.AppendLine($"**Topic URL:** {url}");
                contentBuilder.AppendLine($"**Number of posts included:** {postsArray.Count}");
                contentBuilder.AppendLine();
                contentBuilder.AppendLine("## Posts (markdown/raw content)");
                contentBuilder.AppendLine();

                int index = 1;
                foreach (var postToken in postsArray.OfType<JObject>())
                {
                    string username = postToken.Value<string>("username") ?? "Unknown";
                    string createdAt = postToken.Value<string>("created_at") ?? string.Empty;
                    string content = postToken.Value<string>("raw") ?? postToken.Value<string>("excerpt") ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(content))
                    {
                        content = postToken.Value<string>("cooked") ?? string.Empty;
                    }

                    contentBuilder.AppendLine($"### Post {index} by {username} ({createdAt})");
                    contentBuilder.AppendLine();
                    contentBuilder.AppendLine(content);
                    contentBuilder.AppendLine();
                    index++;
                }

                string userContent = contentBuilder.ToString();
                if (!string.IsNullOrWhiteSpace(instructions))
                {
                    userContent += "\n\nAdditional instructions:\n" + instructions;
                }

                var bodyBuilder = AIBodyBuilder.Create()
                    .AddText(AIAgent.Context, this.summarizeTopicSystemPromptTemplate)
                    .AddText(AIAgent.User, userContent)
                    .WithContextFilter("-*");

                var summaryRequest = new AIRequestCall();
                summaryRequest.Initialize(
                    provider: providerName,
                    model: modelName,
                    capability: this.summarizeCapabilityRequirements,
                    endpoint: this.summarizeTopicToolName,
                    body: bodyBuilder.Build());

                var summaryResult = await summaryRequest.Exec().ConfigureAwait(false);

                if (!summaryResult.Success)
                {
                    output.Messages = summaryResult.Messages;
                    return output;
                }

                var metrics = summaryResult.Metrics ?? new AIMetrics();
                string summaryText = summaryResult.Body?.GetLastText() ?? "Failed to generate summary.";

                var toolResult = new JObject
                {
                    ["topic_id"] = topicId,
                    ["title"] = title,
                    ["url"] = url,
                    ["post_count"] = postsArray.Count,
                    ["summary"] = summaryText,
                };

                toolResult.WithEnvelope(
                    ToolResultEnvelope.Create(
                        tool: this.summarizeTopicToolName,
                        type: ToolResultContentType.Text,
                        payloadPath: "summary",
                        provider: providerName,
                        model: modelName,
                        toolCallId: toolInfo?.Id));

                var toolBody = AIBodyBuilder.Create()
                    .AddToolResult(toolResult, id: toolInfo?.Id, name: this.summarizeTopicToolName, metrics: metrics, messages: summaryResult.Messages)
                    .Build();

                output.CreateSuccess(toolBody);
                return output;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DiscourseTools] Error in SummarizeTopic: {ex.Message}");
                output.CreateError($"Error: {ex.Message}");
                return output;
            }
        }

        private async Task<JObject> FetchTopicAsync(string baseUrl, int topicId)
        {
            return await this.FetchTopicWithQueryAsync(baseUrl, topicId, includeRaw: true, print: false).ConfigureAwait(false);
        }

        private async Task<JObject> FetchTopicWithQueryAsync(string baseUrl, int topicId, bool includeRaw, bool print)
        {
            using var httpClient = new HttpClient();

            var queryParts = new List<string>();
            if (includeRaw) queryParts.Add("include_raw=1");
            if (print) queryParts.Add("print=true");

            var builder = new StringBuilder($"{baseUrl}/t/{topicId}.json");
            if (queryParts.Count > 0)
            {
                builder.Append('?');
                builder.Append(string.Join("&", queryParts));
            }

            var topicUri = new Uri(builder.ToString());
            var response = await httpClient.GetAsync(topicUri).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                string serverMessage = DiscourseUtils.ExtractDiscourseErrorMessage(content);
                string errorMessage = string.IsNullOrWhiteSpace(serverMessage)
                    ? $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}"
                    : $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}: {serverMessage}";
                throw new HttpRequestException(errorMessage);
            }

            return JObject.Parse(content);
        }

        private string BuildTopicUrl(string baseUrl, int topicId, JObject topicJson)
        {
            string slug = topicJson.Value<string>("slug") ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(slug))
            {
                return $"{baseUrl}/t/{slug}/{topicId}";
            }
            return $"{baseUrl}/t/{topicId}";
        }
    }
}
