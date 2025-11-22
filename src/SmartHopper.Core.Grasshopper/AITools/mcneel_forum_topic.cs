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
using System.Text;
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
    /// Provides AI tools for fetching and summarizing McNeel Discourse forum topics.
    /// </summary>
    public class mcneel_forum_topic : IAIToolProvider
    {
        /// <summary>
        /// Name of the get topic tool.
        /// </summary>
        private readonly string getTopicToolName = "mcneel_forum_topic_get";

        /// <summary>
        /// Name of the summarize topic tool.
        /// </summary>
        private readonly string summarizeTopicToolName = "mcneel_forum_topic_summarize";

        /// <summary>
        /// Name of the related topics tool.
        /// </summary>
        private readonly string relatedTopicToolName = "mcneel_forum_topic_related";

        /// <summary>
        /// Capability requirements for topic summarization.
        /// </summary>
        private readonly AICapability summarizeCapabilityRequirements = AICapability.TextInput | AICapability.TextOutput;

        /// <summary>
        /// Returns the list of tools provided by this class.
        /// </summary>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.getTopicToolName,
                description: "Retrieve all posts in a McNeel Discourse forum topic by topic ID.",
                category: "Knowledge",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""topic_id"": {
                            ""type"": ""integer"",
                            ""description"": ""ID of the forum topic to fetch.""
                        },
                        ""max_posts"": {
                            ""type"": ""integer"",
                            ""description"": ""Optional maximum number of posts to return. If omitted, all available posts are returned up to the server limit.""
                        }
                    },
                    ""required"": [""topic_id""]
                }",
                execute: this.GetTopicAsync);

            yield return new AITool(
                name: this.summarizeTopicToolName,
                description: "Generate a concise summary of a McNeel Discourse forum topic by ID, based on its posts.",
                category: "Knowledge",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
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
                            ""description"": ""Optional targeted summary instructions to focus on a specific question, target, or concern.""
                        }
                    },
                    ""required"": [""topic_id""]
                }",
                execute: this.SummarizeTopicAsync,
                requiredCapabilities: this.summarizeCapabilityRequirements);

            yield return new AITool(
                name: this.relatedTopicToolName,
                description: "Retrieve suggested related topics for a McNeel Discourse forum topic by ID.",
                category: "Knowledge",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""topic_id"": {
                            ""type"": ""integer"",
                            ""description"": ""ID of the forum topic to get related topics for.""
                        }
                    },
                    ""required"": [""topic_id""]
                }",
                execute: this.GetRelatedTopicsAsync);
        }

        /// <summary>
        /// Retrieves all posts in a McNeel Discourse forum topic by ID.
        /// </summary>
        private async Task<AIReturn> GetTopicAsync(AIToolCall toolCall)
        {
            var output = new AIReturn
            {
                Request = toolCall,
            };

            try
            {
                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.Arguments ?? new JObject();

                int? topicIdNullable = args["topic_id"]?.Value<int>();
                if (!topicIdNullable.HasValue)
                {
                    output.CreateError("Missing 'topic_id' parameter.");
                    return output;
                }

                int? maxPostsNullable = args["max_posts"]?.Value<int?>();
                int maxPosts = maxPostsNullable.GetValueOrDefault(-1);

                int topicId = topicIdNullable.Value;
                var topicJson = await this.FetchTopicAsync(topicId).ConfigureAwait(false);

                var postStream = topicJson["post_stream"] as JObject;
                var posts = postStream?["posts"] as JArray ?? new JArray();

                if (maxPosts > 0 && posts.Count > maxPosts)
                {
                    posts = new JArray(posts.Take(maxPosts));
                }

                string title = topicJson.Value<string>("title") ?? string.Empty;
                string url = this.BuildTopicUrl(topicId, topicJson);

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
                var immutable = builder.Build();
                output.CreateSuccess(immutable, toolCall);
                return output;
            }
            catch (HttpRequestException httpEx)
            {
                Debug.WriteLine($"[mcneel_forum_topic_get] HTTP error while fetching topic: {httpEx}.");
                output.CreateNetworkError(httpEx.Message, toolCall);
                return output;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[mcneel_forum_topic_get] Error while fetching topic: {ex}.");
                output.CreateError($"Error: {ex.Message}", toolCall);
                return output;
            }
        }

        private async Task<AIReturn> GetRelatedTopicsAsync(AIToolCall toolCall)
        {
            var output = new AIReturn
            {
                Request = toolCall,
            };

            try
            {
                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.Arguments ?? new JObject();

                int? topicIdNullable = args["topic_id"]?.Value<int>();
                if (!topicIdNullable.HasValue)
                {
                    output.CreateError("Missing 'topic_id' parameter.");
                    return output;
                }

                int topicId = topicIdNullable.Value;

                Debug.WriteLine($"[mcneel_forum_topic_related] Fetching topic {topicId}.");

                var topicJson = await this.FetchTopicWithQueryAsync(topicId, includeRaw: true, print: true).ConfigureAwait(false);

                var suggestedTopics = topicJson["suggested_topics"] as JArray ?? new JArray();

                Debug.WriteLine($"[mcneel_forum_topic_related] suggested_topics count={suggestedTopics.Count}.");

                var filteredRelated = new JArray();
                foreach (var topicToken in suggestedTopics.OfType<JObject>())
                {
                    string filteredJson = McNeelForumUtils.FilterSuggestedTopicJson(topicToken.ToString(Newtonsoft.Json.Formatting.None));
                    try
                    {
                        var filteredObj = JObject.Parse(filteredJson);
                        filteredRelated.Add(filteredObj);
                    }
                    catch
                    {
                        filteredRelated.Add(topicToken);
                    }
                }

                Debug.WriteLine($"[mcneel_forum_topic_related] Filtered related topics count={filteredRelated.Count}.");

                string title = topicJson.Value<string>("title") ?? string.Empty;
                string url = this.BuildTopicUrl(topicId, topicJson);

                var toolResult = new JObject
                {
                    ["topic_id"] = topicId,
                    ["title"] = title,
                    ["url"] = url,
                    ["related_topics"] = filteredRelated,
                };

                var builder = AIBodyBuilder.Create();
                builder.AddToolResult(toolResult, toolInfo.Id, toolInfo.Name);
                var immutable = builder.Build();
                output.CreateSuccess(immutable, toolCall);
                return output;
            }
            catch (HttpRequestException httpEx)
            {
                Debug.WriteLine($"[mcneel_forum_topic_related] HTTP error while fetching related topics: {httpEx}.");
                output.CreateNetworkError(httpEx.Message, toolCall);
                return output;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[mcneel_forum_topic_related] Error while fetching related topics: {ex}.");
                output.CreateError($"Error: {ex.Message}", toolCall);
                return output;
            }
        }

        /// <summary>
        /// Generates a summary of a McNeel Discourse forum topic by ID.
        /// </summary>
        private async Task<AIReturn> SummarizeTopicAsync(AIToolCall toolCall)
        {
            var output = new AIReturn
            {
                Request = toolCall,
            };

            try
            {
                Debug.WriteLine("[McNeelForumTools] Running SummarizeTopic tool");

                string providerName = toolCall.Provider;
                string modelName = toolCall.Model;
                string endpoint = this.summarizeTopicToolName;

                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.Arguments ?? new JObject();

                int? topicIdNullable = args["topic_id"]?.Value<int>();
                if (!topicIdNullable.HasValue)
                {
                    output.CreateToolError("Missing required parameter: topic_id", toolCall);
                    return output;
                }

                int topicId = topicIdNullable.Value;

                int maxPosts = args["max_posts"]?.Value<int>() ?? 50;
                if (maxPosts <= 0)
                {
                    maxPosts = 50;
                }

                string instructions = args["instructions"]?.ToString();

                var topicJson = await this.FetchTopicAsync(topicId).ConfigureAwait(false);
                var postStream = topicJson["post_stream"] as JObject;
                var postsArray = postStream?["posts"] as JArray ?? new JArray();

                if (postsArray.Count > maxPosts)
                {
                    postsArray = new JArray(postsArray.Take(maxPosts));
                }

                string title = topicJson.Value<string>("title") ?? string.Empty;
                string url = this.BuildTopicUrl(topicId, topicJson);

                var contentBuilder = new StringBuilder();
                contentBuilder.AppendLine($"# Topic summary request");
                contentBuilder.AppendLine($"**Topic title:** {title}");
                contentBuilder.AppendLine($"**Topic URL:** {url}");
                contentBuilder.AppendLine($"**Number of posts included in this summary:** {postsArray.Count}");
                contentBuilder.AppendLine();
                contentBuilder.AppendLine("## Posts (markdown/raw content)");
                contentBuilder.AppendLine();

                int index = 1;
                foreach (var postToken in postsArray.OfType<JObject>())
                {
                    string username = postToken.Value<string>("username") ?? "Unknown";
                    string createdAt = postToken.Value<string>("created_at") ?? string.Empty;

                    string content = postToken.Value<string>("raw")
                        ?? postToken.Value<string>("excerpt")
                        ?? string.Empty;

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
                    userContent += "\n\nAdditional instructions for the summary:\n" + instructions;
                }

                var bodyBuilder = AIBodyBuilder.Create()
                    .AddText(
                        AIAgent.Context,
                        "You are a helpful assistant that summarizes entire forum topics for users. Provide a concise summary of the main question or issue, key ideas discussed, and any conclusions or solutions. Keep the summary focused and clear.")
                    .AddText(
                        AIAgent.User,
                        userContent)
                    .WithContextFilter("-*");

                var requestBody = bodyBuilder.Build();

                var summaryRequest = new AIRequestCall();
                summaryRequest.Initialize(
                    provider: providerName,
                    model: modelName,
                    capability: this.summarizeCapabilityRequirements,
                    endpoint: endpoint,
                    body: requestBody);

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
                Debug.WriteLine($"[McNeelForumTools] Error in SummarizeTopic: {ex.Message}");

                output.CreateError($"Error: {ex.Message}");
                return output;
            }
        }

        /// <summary>
        /// Helper method to fetch a topic from the McNeel Discourse forum.
        /// </summary>
        private async Task<JObject> FetchTopicAsync(int topicId)
        {
            return await this.FetchTopicWithQueryAsync(topicId, includeRaw: true, print: false).ConfigureAwait(false);
        }

        private async Task<JObject> FetchTopicWithQueryAsync(int topicId, bool includeRaw, bool print)
        {
            using var httpClient = new HttpClient();

            var queryParts = new List<string>();
            if (includeRaw)
            {
                queryParts.Add("include_raw=1");
            }

            if (print)
            {
                queryParts.Add("print=true");
            }

            var builder = new StringBuilder($"https://discourse.mcneel.com/t/{topicId}.json");
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
                string serverMessage = ExtractDiscourseErrorMessage(content);
                string errorMessage = string.IsNullOrWhiteSpace(serverMessage)
                    ? $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}"
                    : $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}: {serverMessage}";

                throw new HttpRequestException(errorMessage);
            }

            return JObject.Parse(content);
        }

        private static string ExtractDiscourseErrorMessage(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return string.Empty;
            }

            try
            {
                var json = JObject.Parse(content);
                var errors = json["errors"] as JArray;
                if (errors != null && errors.Count > 0)
                {
                    var messages = errors.Values<string>().Where(e => !string.IsNullOrWhiteSpace(e));
                    return string.Join("; ", messages);
                }
            }
            catch
            {
            }

            return content;
        }

        /// <summary>
        /// Helper method to build a human-readable topic URL.
        /// </summary>
        private string BuildTopicUrl(int topicId, JObject topicJson)
        {
            string slug = topicJson.Value<string>("slug") ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(slug))
            {
                return $"https://discourse.mcneel.com/t/{slug}/{topicId}";
            }

            return $"https://discourse.mcneel.com/t/{topicId}";
        }
    }
}
