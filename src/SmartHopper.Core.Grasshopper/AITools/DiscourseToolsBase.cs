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
using SmartHopper.Infrastructure.Diagnostics;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Base class providing core Discourse forum functionality.
    /// Can be configured with any Discourse instance base URL.
    /// </summary>
    public abstract class DiscourseToolsBase : IAIToolProvider
    {
        /// <summary>
        /// Gets the preset base URL for the Discourse forum (e.g., "https://discourse.mcneel.com").
        /// If null, the tools will require base_url as a parameter.
        /// </summary>
        protected virtual string? PresetBaseUrl => null;

        /// <summary>
        /// Gets the display name of the forum for tool descriptions.
        /// </summary>
        protected abstract string ForumName { get; }

        /// <summary>
        /// Gets the name prefix for tools (e.g., "mcneel", "ladybug", "discourse").
        /// </summary>
        protected abstract string ToolPrefix { get; }

        /// <summary>
        /// Gets a value indicating whether base_url is required as a parameter.
        /// </summary>
        protected bool RequiresBaseUrlParameter => this.PresetBaseUrl == null;

        /// <summary>
        /// Capability requirements for summarization tools.
        /// </summary>
        protected readonly AICapability SummarizeCapabilityRequirements = AICapability.TextInput | AICapability.TextOutput;

        /// <summary>
        /// System prompt template for summarizing forum posts.
        /// </summary>
        protected readonly string SummarizePostSystemPromptTemplate =
            "You are a helpful assistant that summarizes forum posts concisely. Provide a brief 1-2 sentence summary of the main point or question.";

        /// <summary>
        /// System prompt template for summarizing forum topics.
        /// </summary>
        protected readonly string SummarizeTopicSystemPromptTemplate =
            "You are a helpful assistant that summarizes entire forum topics for users. Provide a concise summary of the main question or issue, key ideas discussed, and any conclusions or solutions. Keep the summary focused and clear.";

        /// <summary>
        /// Returns the list of tools provided by this class.
        /// </summary>
        public virtual IEnumerable<AITool> GetTools()
        {
            string getPostToolName = $"{this.ToolPrefix}_forum_post_get";
            string summarizePostToolName = $"{this.ToolPrefix}_forum_post_summarize";
            string getTopicToolName = $"{this.ToolPrefix}_forum_topic_get";
            string summarizeTopicToolName = $"{this.ToolPrefix}_forum_topic_summarize";
            string searchToolName = $"{this.ToolPrefix}_forum_search";

            string baseUrlProperty = this.RequiresBaseUrlParameter
                ? "\"base_url\": { \"type\": \"string\", \"description\": \"Base URL of the Discourse forum (e.g., https://discourse.example.com).\" },"
                : string.Empty;

            string baseUrlRequired = this.RequiresBaseUrlParameter ? "\"base_url\", " : string.Empty;

            yield return new AITool(
                name: getPostToolName,
                description: $"Retrieve a filtered {this.ForumName} forum post by ID (username, date, title, raw markdown).",
                category: "Knowledge",
                parametersSchema: $"{{ \"type\": \"object\", \"properties\": {{ {baseUrlProperty} \"id\": {{ \"type\": \"integer\", \"description\": \"ID of the forum post to fetch.\" }} }}, \"required\": [{baseUrlRequired}\"id\"] }}",
                execute: this.GetPostAsync);

            yield return new AITool(
                name: summarizePostToolName,
                description: $"Generate a concise summary of one or more {this.ForumName} forum posts by ID.",
                category: "Knowledge",
                parametersSchema: $"{{ \"type\": \"object\", \"properties\": {{ {baseUrlProperty} \"ids\": {{ \"type\": \"array\", \"items\": {{ \"type\": \"integer\" }}, \"description\": \"ID or list of forum post IDs to summarize.\" }}, \"instructions\": {{ \"type\": \"string\", \"description\": \"Optional targeted summary instructions to focus on a specific question, target, or concern.\" }} }}, \"required\": [{baseUrlRequired}\"ids\"] }}",
                execute: this.SummarizePostAsync,
                requiredCapabilities: this.SummarizeCapabilityRequirements,
                mutatesCanvas: false,
                tags: new[] { "knowledge", "forum", "web", "read-only", "external", "ai-generation" },
                outputSchema: @"{ ""type"": ""object"", ""properties"": { ""summary"": { ""type"": ""string"" } } }",
                annotations: new AIToolAnnotations(readOnlyHint: true, openWorldHint: true));

            yield return new AITool(
                name: getTopicToolName,
                description: $"Retrieve all posts in a {this.ForumName} forum topic by topic ID (title, URL, posts array).",
                category: "Knowledge",
                parametersSchema: $"{{ \"type\": \"object\", \"properties\": {{ {baseUrlProperty} \"topic_id\": {{ \"type\": \"integer\", \"description\": \"ID of the forum topic to fetch.\" }}, \"max_posts\": {{ \"type\": \"integer\", \"description\": \"Optional maximum number of posts to return. If omitted, all available posts are returned up to the server limit.\" }} }}, \"required\": [{baseUrlRequired}\"topic_id\"] }}",
                execute: this.GetTopicAsync);

            yield return new AITool(
                name: summarizeTopicToolName,
                description: $"Generate a concise summary of a {this.ForumName} forum topic by ID, based on its posts.",
                category: "Knowledge",
                parametersSchema: $"{{ \"type\": \"object\", \"properties\": {{ {baseUrlProperty} \"topic_id\": {{ \"type\": \"integer\", \"description\": \"ID of the forum topic to summarize.\" }}, \"max_posts\": {{ \"type\": \"integer\", \"description\": \"Optional maximum number of posts to include in the summary input (default: 50).\" }}, \"instructions\": {{ \"type\": \"string\", \"description\": \"Optional targeted summary instructions to focus on a specific question, target, or concern.\" }} }}, \"required\": [{baseUrlRequired}\"topic_id\"] }}",
                execute: this.SummarizeTopicAsync,
                requiredCapabilities: this.SummarizeCapabilityRequirements,
                mutatesCanvas: false,
                tags: new[] { "knowledge", "forum", "web", "read-only", "external", "ai-generation" },
                outputSchema: @"{ ""type"": ""object"", ""properties"": { ""summary"": { ""type"": ""string"" } } }",
                annotations: new AIToolAnnotations(readOnlyHint: true, openWorldHint: true));

            yield return new AITool(
                name: searchToolName,
                description: $"Search {this.ForumName} forum posts by query and return matching results.",
                category: "Knowledge",
                parametersSchema: $"{{ \"type\": \"object\", \"properties\": {{ {baseUrlProperty} \"query\": {{ \"type\": \"string\", \"description\": \"Search query for the forum.\" }} }}, \"required\": [{baseUrlRequired}\"query\"] }}",
                execute: this.SearchAsync);
        }

        /// <summary>
        /// Gets the base URL from arguments or falls back to preset.
        /// </summary>
        protected string? GetBaseUrl(JObject args)
        {
            // When PresetBaseUrl is set, always use it and ignore any base_url in args
            if (!this.RequiresBaseUrlParameter)
            {
                return this.PresetBaseUrl;
            }

            // For generic mode, get URL from args
            string? url = args["base_url"]?.ToString();
            return string.IsNullOrWhiteSpace(url) ? null : url;
        }

        /// <summary>
        /// Validates that base URL is provided.
        /// </summary>
        protected bool ValidateBaseUrl(string? baseUrl, AIReturn output)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                output.CreateError("Missing 'base_url' parameter.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Retrieves a full forum post by ID.
        /// </summary>
        private async Task<AIReturn> GetPostAsync(AIToolCall toolCall)
        {
            var output = new AIReturn()
            {
                Request = toolCall,
            };

            try
            {
                toolCall.SkipMetricsValidation = true;

                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.Arguments ?? new JObject();

                string? baseUrl = this.GetBaseUrl(args);
                if (!this.ValidateBaseUrl(baseUrl, output))
                {
                    return output;
                }

                int? idNullable = args["id"]?.Value<int>();
                if (!idNullable.HasValue)
                {
                    output.CreateError("Missing 'id' parameter.");
                    return output;
                }

                int id = idNullable.Value;
                var filteredPost = await this.FetchFilteredPostAsync(baseUrl!, id).ConfigureAwait(false);

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
        /// Generates a summary of one or more forum posts by ID.
        /// </summary>
        private async Task<AIReturn> SummarizePostAsync(AIToolCall toolCall)
        {
            var output = new AIReturn()
            {
                Request = toolCall,
            };

            try
            {
                Debug.WriteLine($"[{this.ForumName}ForumTools] Running SummarizePost tool");

                string providerName = toolCall.Provider;
                string modelName = toolCall.Model;
                string endpoint = $"{this.ToolPrefix}_forum_post_summarize";

                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.GetArgumentsOrEmpty();

                string? baseUrl = this.GetBaseUrl(args);
                if (!this.ValidateBaseUrl(baseUrl, output))
                {
                    return output;
                }

                string? baseUrl = this.GetBaseUrl(args);
                if (!this.ValidateBaseUrl(baseUrl, output))
                {
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
                var allMessages = new List<SHRuntimeMessage>();

                foreach (int id in ids)
                {
                    var filteredPost = await this.FetchFilteredPostAsync(baseUrl!, id).ConfigureAwait(false);

                    string username = filteredPost.Value<string>("username") ?? "Unknown";
                    string date = filteredPost.Value<string>("date") ?? string.Empty;
                    string raw = filteredPost.Value<string>("raw") ?? string.Empty;
                    string topicId = filteredPost.Value<string>("topic_id") ?? string.Empty;
                    string postNumber = filteredPost.Value<string>("post_number") ?? string.Empty;
                    string postUrl = $"{baseUrl}/t/{topicId}/{postNumber}";

                    string userContent =
                        $"Summarize this forum post:\n\nAuthor: {username}\nDate: {date}\nContent:\n{raw}";

                    if (!string.IsNullOrWhiteSpace(instructions))
                    {
                        userContent += $"\n\nFocus the summary on the following question/target/concern:\n{instructions}";
                    }

                    var bodyBuilder = AIBodyBuilder.Create()
                        .AddText(AIAgent.Context, this.SummarizePostSystemPromptTemplate)
                        .AddText(AIAgent.User, userContent)
                        .WithContextFilter("-*");

                    var requestBody = bodyBuilder.Build();

                    var summaryRequest = new AIRequestCall();
                    summaryRequest.Initialize(
                        provider: providerName,
                        model: modelName,
                        capability: AICapability.TextInput | AICapability.TextOutput,
                        endpoint: endpoint,
                        body: requestBody);

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

                    var summaryObject = new JObject
                    {
                        ["id"] = id,
                        ["summary"] = summary,
                        ["username"] = username,
                        ["date"] = date,
                        ["url"] = postUrl,
                    };

                    summariesArray.Add(summaryObject);
                }

                var toolResult = new JObject
                {
                    ["summaries"] = summariesArray,
                };

                if (summariesArray.Count == 1 && summariesArray[0] is JObject firstSummary)
                {
                    toolResult["id"] = firstSummary["id"];
                    toolResult["summary"] = firstSummary["summary"];
                    toolResult["username"] = firstSummary["username"];
                    toolResult["date"] = firstSummary["date"];
                    toolResult["url"] = firstSummary["url"];
                }

                toolResult.WithEnvelope(
                    ToolResultEnvelope.Create(
                        tool: $"{this.ToolPrefix}_forum_post_summarize",
                        type: ToolResultContentType.List,
                        payloadPath: "summaries",
                        provider: providerName,
                        model: modelName,
                        toolCallId: toolInfo?.Id));

                var toolBody = AIBodyBuilder.Create()
                    .AddToolResult(toolResult, id: toolInfo?.Id, name: $"{this.ToolPrefix}_forum_post_summarize", metrics: accumulatedMetrics, messages: allMessages)
                    .Build();

                output.CreateSuccess(toolBody);
                return output;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{this.ForumName}ForumTools] Error in SummarizePost: {ex.Message}");
                output.CreateError($"Error: {ex.Message}");
                return output;
            }
        }

        /// <summary>
        /// Retrieves all posts in a forum topic by ID.
        /// </summary>
        private async Task<AIReturn> GetTopicAsync(AIToolCall toolCall)
        {
            var output = new AIReturn
            {
                Request = toolCall,
            };

            try
            {
                toolCall.SkipMetricsValidation = true;

                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.GetArgumentsOrEmpty();

                string? baseUrl = this.GetBaseUrl(args);
                if (!this.ValidateBaseUrl(baseUrl, output))
                {
                    return output;
                }

                string? baseUrl = this.GetBaseUrl(args);
                if (!this.ValidateBaseUrl(baseUrl, output))
                {
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
                var topicJson = await this.FetchTopicAsync(baseUrl!, topicId).ConfigureAwait(false);

                var postStream = topicJson["post_stream"] as JObject;
                var posts = postStream?["posts"] as JArray ?? new JArray();

                if (maxPosts > 0 && posts.Count > maxPosts)
                {
                    posts = new JArray(posts.Take(maxPosts));
                }

                string title = topicJson.Value<string>("title") ?? string.Empty;
                string url = this.BuildTopicUrl(baseUrl!, topicId, topicJson);

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
                Debug.WriteLine($"[{this.ToolPrefix}_forum_topic_get] HTTP error while fetching topic: {httpEx}.");
                output.CreateNetworkError(httpEx.Message, toolCall);
                return output;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{this.ToolPrefix}_forum_topic_get] Error while fetching topic: {ex}.");
                output.CreateError($"Error: {ex.Message}", toolCall);
                return output;
            }
        }

        /// <summary>
        /// Generates a summary of a forum topic by ID.
        /// </summary>
        private async Task<AIReturn> SummarizeTopicAsync(AIToolCall toolCall)
        {
            var output = new AIReturn
            {
                Request = toolCall,
            };

            try
            {
                Debug.WriteLine($"[{this.ForumName}ForumTools] Running SummarizeTopic tool");

                string providerName = toolCall.Provider;
                string modelName = toolCall.Model;
                string endpoint = $"{this.ToolPrefix}_forum_topic_summarize";

                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.GetArgumentsOrEmpty();

                string? baseUrl = this.GetBaseUrl(args);
                if (!this.ValidateBaseUrl(baseUrl, output))
                {
                    return output;
                }

                string? baseUrl = this.GetBaseUrl(args);
                if (!this.ValidateBaseUrl(baseUrl, output))
                {
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
                if (maxPosts <= 0)
                {
                    maxPosts = 50;
                }

                string instructions = args["instructions"]?.ToString() ?? string.Empty;

                var topicJson = await this.FetchTopicAsync(baseUrl!, topicId).ConfigureAwait(false);
                var postStream = topicJson["post_stream"] as JObject;
                var postsArray = postStream?["posts"] as JArray ?? new JArray();

                if (postsArray.Count > maxPosts)
                {
                    postsArray = new JArray(postsArray.Take(maxPosts));
                }

                string title = topicJson.Value<string>("title") ?? string.Empty;
                string url = this.BuildTopicUrl(baseUrl!, topicId, topicJson);

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
                    .AddText(AIAgent.Context, this.SummarizeTopicSystemPromptTemplate)
                    .AddText(AIAgent.User, userContent)
                    .WithContextFilter("-*");

                var requestBody = bodyBuilder.Build();

                var summaryRequest = new AIRequestCall();
                summaryRequest.Initialize(
                    provider: providerName,
                    model: modelName,
                    capability: this.SummarizeCapabilityRequirements,
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
                        tool: $"{this.ToolPrefix}_forum_topic_summarize",
                        type: ToolResultContentType.Text,
                        payloadPath: "summary",
                        provider: providerName,
                        model: modelName,
                        toolCallId: toolInfo?.Id));

                var toolBody = AIBodyBuilder.Create()
                    .AddToolResult(toolResult, id: toolInfo?.Id, name: $"{this.ToolPrefix}_forum_topic_summarize", metrics: metrics, messages: summaryResult.Messages)
                    .Build();

                output.CreateSuccess(toolBody);
                return output;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{this.ForumName}ForumTools] Error in SummarizeTopic: {ex.Message}");
                output.CreateError($"Error: {ex.Message}");
                return output;
            }
        }

        /// <summary>
        /// Searches the forum for posts matching the query.
        /// </summary>
        private async Task<AIReturn> SearchAsync(AIToolCall toolCall)
        {
            var output = new AIReturn
            {
                Request = toolCall,
            };

            try
            {
                toolCall.SkipMetricsValidation = true;

                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.GetArgumentsOrEmpty();

                string? baseUrl = this.GetBaseUrl(args);
                if (!this.ValidateBaseUrl(baseUrl, output))
                {
                    return output;
                }

                string query = args["query"]?.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(query))
                {
                    output.CreateError("Missing 'query' parameter.");
                    return output;
                }

                var results = await this.SearchForumAsync(baseUrl!, query).ConfigureAwait(false);

                var toolResult = new JObject
                {
                    ["query"] = query,
                    ["results"] = results,
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
        /// Helper method to fetch a post from the forum.
        /// </summary>
        private async Task<JObject> FetchPostAsync(string baseUrl, int id)
        {
            using var httpClient = new HttpClient();
            var postUri = new Uri($"{baseUrl}/posts/{id}.json");
            var response = await httpClient.GetAsync(postUri).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JObject.Parse(content);
        }

        /// <summary>
        /// Helper method to fetch a filtered post.
        /// </summary>
        private async Task<JObject> FetchFilteredPostAsync(string baseUrl, int id)
        {
            var postJson = await this.FetchPostAsync(baseUrl, id).ConfigureAwait(false);
            string filteredJson = DiscourseUtils.FilterPostJson(postJson.ToString(Newtonsoft.Json.Formatting.None), baseUrl);
            return JObject.Parse(filteredJson);
        }

        /// <summary>
        /// Helper method to fetch a topic from the forum.
        /// </summary>
        private async Task<JObject> FetchTopicAsync(string baseUrl, int topicId)
        {
            return await this.FetchTopicWithQueryAsync(baseUrl, topicId, includeRaw: true, print: false).ConfigureAwait(false);
        }

        /// <summary>
        /// Helper method to fetch a topic with query parameters.
        /// </summary>
        private async Task<JObject> FetchTopicWithQueryAsync(string baseUrl, int topicId, bool includeRaw, bool print)
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

        /// <summary>
        /// Helper method to search the forum.
        /// Enriches posts with topic titles and generates excerpts from available content.
        /// </summary>
        private async Task<JArray> SearchForumAsync(string baseUrl, string query)
        {
            using var httpClient = new HttpClient();

            var searchUri = new Uri($"{baseUrl}/search.json?q={Uri.EscapeDataString(query)}");
            Debug.WriteLine($"[{this.ForumName}ForumTools] Search API call: {searchUri}");
            var response = await httpClient.GetAsync(searchUri).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            var searchResults = JObject.Parse(content);
            var posts = searchResults["posts"] as JArray ?? new JArray();
            var topics = searchResults["topics"] as JArray ?? new JArray();

            // Build topic lookup by topic_id for title enrichment
            var topicLookup = new Dictionary<int, string>();
            foreach (var topic in topics)
            {
                if (topic is JObject topicObj)
                {
                    int topicId = topicObj.Value<int>("id");
                    string title = topicObj.Value<string>("title") ?? string.Empty;
                    if (topicId > 0 && !string.IsNullOrWhiteSpace(title))
                    {
                        topicLookup[topicId] = title;
                    }
                }
            }

            Debug.WriteLine($"[{this.ForumName}ForumTools] Search returned {posts.Count} posts, {topics.Count} topics, {topicLookup.Count} topic titles mapped");

            var filteredResults = new JArray();
            foreach (var post in posts)
            {
                var filteredPost = this.EnrichSearchResultPost(post as JObject, topicLookup, baseUrl);
                if (filteredPost != null)
                {
                    filteredResults.Add(filteredPost);
                }
            }

            return filteredResults;
        }

        /// <summary>
        /// Enriches a search result post with topic title and generated excerpt.
        /// </summary>
        private JObject EnrichSearchResultPost(JObject post, Dictionary<int, string> topicLookup, string baseUrl)
        {
            if (post == null)
            {
                return null;
            }

            try
            {
                int postId = post.Value<int>("id");
                int topicId = post.Value<int>("topic_id");

                // Get title from topic lookup
                string title = topicLookup.TryGetValue(topicId, out var topicTitle) ? topicTitle : null;

                // Generate excerpt from available content (in priority order)
                string excerpt = DiscourseUtils.GenerateExcerpt(post);

                int postNumber = post.Value<int>("post_number");
                string normalizedBaseUrl = baseUrl.TrimEnd('/');
                string fullUrl = $"{normalizedBaseUrl}/t/{topicId}/{postNumber}";

                // Extract reads and likes from search result
                int reads = post.Value<int>("reads");
                int likeCount = post.Value<int>("like_count");

                // Get raw/cooked content if available
                string rawContent = post.Value<string>("raw") ?? string.Empty;
                string cookedContent = post.Value<string>("cooked") ?? string.Empty;
                string blurb = post.Value<string>("blurb") ?? string.Empty;

                return new JObject
                {
                    ["id"] = postId,
                    ["username"] = post["username"],
                    ["name"] = post["name"],
                    ["created_at"] = post["created_at"],
                    ["excerpt"] = excerpt,
                    ["topic_title"] = title,
                    ["topic_id"] = topicId,
                    ["post_number"] = postNumber,
                    ["url"] = fullUrl,
                    ["reads"] = reads,
                    ["likes"] = likeCount,
                    ["raw"] = rawContent,
                    ["cooked"] = cookedContent,
                    ["blurb"] = blurb,
                };
            }
            catch
            {
                return post;
            }
        }

        /// <summary>
        /// Helper method to build a human-readable topic URL.
        /// </summary>
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
