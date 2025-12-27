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
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.JsonSchemas;
using SmartHopper.Infrastructure.AICall.Metrics;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Infrastructure.Streaming;

namespace SmartHopper.Providers.DeepSeek
{
    /// <summary>
    /// DeepSeek AI provider implementation.
    /// </summary>
    public sealed partial class DeepSeekProvider : AIProvider<DeepSeekProvider>
    {
        #region Compiled Regex Patterns

        /// <summary>
        /// Regex pattern for normalizing whitespace to single spaces.
        /// </summary>
        [GeneratedRegex(@"\s+")]
        private static partial Regex WhitespaceRegex();

        /// <summary>
        /// Regex pattern for extracting enum arrays from malformed JSON.
        /// </summary>
        [GeneratedRegex(@"enum[""']?:\s*\[([^\]]+)\]")]
        private static partial Regex EnumArrayRegex();

        #endregion

        /// <summary>
        /// The name of the provider. This will be displayed in the UI and used for provider selection.
        /// </summary>
        public static readonly string NameValue = "DeepSeek";

        /// <summary>
        /// Initializes a new instance of the <see cref="DeepSeekProvider"/> class.
        /// Private constructor to enforce singleton pattern.
        /// </summary>
        private DeepSeekProvider()
        {
            this.Models = new DeepSeekProviderModels(this);

            // Register provider-specific JSON schema adapter
            JsonSchemaAdapterRegistry.Register(new DeepSeekJsonSchemaAdapter());
        }

        /// <summary>
        /// Gets the name of the provider.
        /// </summary>
        public override string Name => NameValue;

        /// <summary>
        /// Gets the default server URL for the provider.
        /// </summary>
        public override Uri DefaultServerUrl => new Uri("https://api.deepseek.com");

        /// <summary>
        /// Gets a value indicating whether this provider is enabled and should be available for use.
        /// Set this to false for template or experimental providers that shouldn't be used in production.
        /// </summary>
        public override bool IsEnabled => true;

        /// <summary>
        /// Helper to retrieve the configured API key for this provider.
        /// Exposed to nested streaming adapter to avoid protected access issues.
        /// </summary>
        internal string GetApiKey()
        {
            return this.GetSetting<string>("ApiKey");
        }

        /// <summary>
        /// <inheritdoc/>
        protected override IStreamingAdapter CreateStreamingAdapter()
        {
            return new DeepSeekStreamingAdapter(this);
        }

        /// <summary>
        /// Gets the provider's icon.
        /// </summary>
        public override Image Icon
        {
            get
            {
                var iconBytes = Properties.Resources.deepseek_icon;
                using (var ms = new MemoryStream(iconBytes))
                {
                    return new Bitmap(ms);
                }
            }
        }

        /// <inheritdoc/>
        public override AIRequestCall PreCall(AIRequestCall request)
        {
            // First do the base PreCall
            request = base.PreCall(request);

            // Setup proper httpmethod, content type, and authentication
            request.HttpMethod = "POST";
            request.ContentType = "application/json";
            request.Authentication = "bearer";

            // Currently DeepSeek is only compatible with chat completions
            request.Endpoint = "/chat/completions";

            return request;
        }

        /// <inheritdoc/>
        public override string Encode(AIRequestCall request)
        {
            if (request.HttpMethod == "GET" || request.HttpMethod == "DELETE")
            {
                return "GET and DELETE requests do not use a request body";
            }

            int maxTokens = this.GetSetting<int>("MaxTokens");
            double temperature = this.GetSetting<double>("Temperature");
            string? toolFilter = request.Body.ToolFilter;

            Debug.WriteLine($"[DeepSeek] Encode - Model: {request.Model}, MaxTokens: {maxTokens}");

#if DEBUG
            // Log interaction sequence for debugging
            try
            {
                int cnt = request.Body.Interactions?.Count ?? 0;
                int tc = request.Body.Interactions?.Count(i => i is AIInteractionToolCall) ?? 0;
                int tr = request.Body.Interactions?.Count(i => i is AIInteractionToolResult) ?? 0;
                int tx = request.Body.Interactions?.Count(i => i is AIInteractionText) ?? 0;
                Debug.WriteLine($"[DeepSeek] BuildMessages: interactions={cnt} (toolCalls={tc}, toolResults={tr}, text={tx})");
            }
            catch { }
#endif

            // Group consecutive interactions by role to avoid multiple assistant messages
            // DeepSeek requires tool_calls and content to be in the SAME assistant message
            var convertedMessages = new JArray();
            string currentRole = null;
            JObject currentMessage = null;
            var currentToolCalls = new JArray();

            foreach (var interaction in request.Body.Interactions)
            {
                try
                {
                    var token = this.EncodeToJToken(interaction);
                    if (token == null)
                    {
                        continue;
                    }

                    var role = token["role"]?.ToString();
                    if (string.IsNullOrEmpty(role))
                    {
                        continue;
                    }

                    // Check if role changed
                    if (currentRole != role)
                    {
                        // Finalize previous message if exists
                        if (currentMessage != null)
                        {
                            // Add accumulated tool_calls to the message
                            if (currentToolCalls.Count > 0)
                            {
                                currentMessage["tool_calls"] = currentToolCalls;
                            }

                            // Remove reasoning_content from assistant messages without tool_calls
                            // per DeepSeek's recommendation to save bandwidth
                            if (string.Equals(currentMessage["role"]?.ToString(), "assistant", StringComparison.OrdinalIgnoreCase)
                                && (currentMessage["tool_calls"] == null || (currentMessage["tool_calls"] is JArray tcArray && tcArray.Count == 0)))
                            {
                                currentMessage.Remove("reasoning_content");
                            }

                            convertedMessages.Add(currentMessage);
                        }

                        // Start new message
                        currentRole = role;
                        currentMessage = new JObject
                        {
                            ["role"] = role,
                            ["content"] = token["content"]?.ToString() ?? string.Empty
                        };

                        if (!string.IsNullOrWhiteSpace(token["reasoning_content"]?.ToString()))
                        {
                            currentMessage["reasoning_content"] = token["reasoning_content"]?.ToString();
                        }

                        currentToolCalls = new JArray();

                        // Copy tool_calls if present in this token
                        if (token["tool_calls"] is JArray tc && tc.Count > 0)
                        {
                            foreach (var toolCall in tc)
                            {
                                currentToolCalls.Add(toolCall);
                            }
                        }

                        // Copy tool_call_id and name for tool results
                        if (token["tool_call_id"] != null)
                        {
                            currentMessage["tool_call_id"] = token["tool_call_id"];
                        }

                        if (token["name"] != null)
                        {
                            currentMessage["name"] = token["name"];
                        }
                    }
                    else
                    {
                        // Same role: merge content and tool_calls
                        var existingContent = currentMessage["content"]?.ToString() ?? string.Empty;
                        var newContent = token["content"]?.ToString() ?? string.Empty;
                        if (!string.IsNullOrEmpty(newContent))
                        {
                            currentMessage["content"] = string.IsNullOrEmpty(existingContent) ? newContent : existingContent + " " + newContent;
                        }

                        var existingReasoning = currentMessage["reasoning_content"]?.ToString() ?? string.Empty;
                        var newReasoning = token["reasoning_content"]?.ToString() ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(newReasoning))
                        {
                            currentMessage["reasoning_content"] = string.IsNullOrWhiteSpace(existingReasoning) ? newReasoning : existingReasoning + " " + newReasoning;
                        }

                        // Accumulate tool_calls
                        if (token["tool_calls"] is JArray tc && tc.Count > 0)
                        {
                            foreach (var toolCall in tc)
                            {
                                currentToolCalls.Add(toolCall);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[DeepSeek] Warning: Could not encode interaction: {ex.Message}");
                }
            }

            // Finalize last message
            if (currentMessage != null)
            {
                // Add accumulated tool_calls to the message
                if (currentToolCalls.Count > 0)
                {
                    currentMessage["tool_calls"] = currentToolCalls;
                }

                // Remove reasoning_content from assistant messages without tool_calls
                // per DeepSeek's recommendation to save bandwidth
                if (string.Equals(currentMessage["role"]?.ToString(), "assistant", StringComparison.OrdinalIgnoreCase)
                    && (currentMessage["tool_calls"] == null || (currentMessage["tool_calls"] is JArray tcArray && tcArray.Count == 0)))
                {
                    currentMessage.Remove("reasoning_content");
                }

                convertedMessages.Add(currentMessage);
            }

#if DEBUG
            // Log final messages array for debugging
            try
            {
                Debug.WriteLine($"[DeepSeek] Final encoded messages array ({convertedMessages.Count} messages):");
                for (int idx = 0; idx < convertedMessages.Count; idx++)
                {
                    var msg = convertedMessages[idx] as JObject;
                    var role = msg?["role"]?.ToString() ?? "?";
                    var hasToolCalls = msg?["tool_calls"] != null;
                    var toolCallId = msg?["tool_call_id"]?.ToString();
                    var content = msg?["content"]?.ToString();
                    var preview = content != null ? (content.Length > 50 ? content.Substring(0, 50) + "..." : content) : "";

                    if (hasToolCalls)
                    {
                        var tcArray = msg?["tool_calls"] as JArray;
                        var tcCount = tcArray?.Count ?? 0;
                        var tcIds = tcArray?.Select(tc => tc?["id"]?.ToString()).Where(id => !string.IsNullOrEmpty(id)).ToList();
                        Debug.WriteLine($"  [{idx}] role={role}, tool_calls={tcCount}, ids=[{string.Join(", ", tcIds ?? new List<string>())}], content='{preview}'");
                    }
                    else if (!string.IsNullOrEmpty(toolCallId))
                    {
                        Debug.WriteLine($"  [{idx}] role={role}, tool_call_id={toolCallId}, content={preview}");
                    }
                    else
                    {
                        Debug.WriteLine($"  [{idx}] role={role}, content={preview}");
                    }
                }
            }
            catch { }
#endif

            // Build request body
            var requestBody = new JObject
            {
                ["model"] = request.Model,
                ["messages"] = convertedMessages,
                ["max_tokens"] = maxTokens,
                ["temperature"] = temperature,
            };

            // Add JSON response format if schema is provided (centralized wrapping)
            if (!string.IsNullOrWhiteSpace(request.Body.JsonOutputSchema))
            {
                try
                {
                    var svc = JsonSchemaService.Instance;
                    var schemaObj = JObject.Parse(request.Body.JsonOutputSchema);
                    var (wrappedSchema, wrapperInfo) = svc.WrapForProvider(schemaObj, this.Name);
                    svc.SetCurrentWrapperInfo(wrapperInfo);
                    Debug.WriteLine($"[DeepSeek] Schema wrapper info stored (central): IsWrapped={wrapperInfo.IsWrapped}, Type={wrapperInfo.WrapperType}, Property={wrapperInfo.PropertyName}");

                    // Enforce structured output
                    requestBody["response_format"] = new JObject { ["type"] = "json_object" };

                    // Add system guidance including the wrapped schema
                    var systemMessage = new JObject
                    {
                        ["role"] = "system",
                        ["content"] = "The response must be a valid JSON object that strictly follows this schema: " + wrappedSchema.ToString(Newtonsoft.Json.Formatting.None),
                    };
                    convertedMessages.Insert(0, systemMessage);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[DeepSeek] Failed to parse/handle JSON schema: {ex.Message}");
                    JsonSchemaService.Instance.SetCurrentWrapperInfo(new SchemaWrapperInfo { IsWrapped = false });

                    // Fallback to text response
                    requestBody["response_format"] = new JObject { ["type"] = "text" };
                }
            }
            else
            {
                JsonSchemaService.Instance.SetCurrentWrapperInfo(new SchemaWrapperInfo { IsWrapped = false });
                requestBody["response_format"] = new JObject { ["type"] = "text" };
            }

            // Add tools if requested
            if (!string.IsNullOrWhiteSpace(toolFilter))
            {
                var tools = this.GetFormattedTools(toolFilter);
                if (tools != null && tools.Count > 0)
                {
                    requestBody["tools"] = tools;
                    requestBody["tool_choice"] = "auto";
                }
            }

#if DEBUG
            try
            {
                Debug.WriteLine($"[DeepSeek] Request body:");
                Debug.WriteLine(requestBody.ToString(Formatting.Indented));
            }
            catch { }
#else
            Debug.WriteLine($"[DeepSeek] Request: {requestBody}");
#endif

            return requestBody.ToString();
        }

        /// <inheritdoc/>
        public override string Encode(IAIInteraction interaction)
        {
            try
            {
                var token = this.EncodeToJToken(interaction);
                return token?.ToString() ?? string.Empty;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DeepSeek] Encode(IAIInteraction) error: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Converts a single interaction to a DeepSeek message object (JToken).
        /// Returns null for interactions that should not be sent (e.g., UI-only errors).
        /// </summary>
        private JToken? EncodeToJToken(IAIInteraction interaction)
        {
            if (interaction == null)
            {
                return null;
            }

            // UI-only diagnostics must not be sent to providers
            if (interaction is AIInteractionError)
            {
                return null;
            }

            // Map role
            var agent = interaction.Agent;
            string role;

            switch (agent)
            {
                case AIAgent.System:
                case AIAgent.Context:
                    role = "system";
                    break;
                case AIAgent.User:
                    role = "user";
                    break;
                case AIAgent.Assistant:
                case AIAgent.ToolCall:
                    role = "assistant";
                    break;
                case AIAgent.ToolResult:
                    role = "tool";
                    break;
                default:
                    return null;
            }

            var messageObj = new JObject { ["role"] = role };

            // Handle different interaction types
            if (interaction is AIInteractionText textInteraction)
            {
                messageObj["content"] = textInteraction.Content ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(textInteraction.Reasoning))
                {
                    messageObj["reasoning_content"] = textInteraction.Reasoning;
                }
            }
            else if (interaction is AIInteractionToolResult toolResultInteraction)
            {
                // DeepSeek requires cleaned/simplified tool result content
                var jsonString = JsonConvert.SerializeObject(toolResultInteraction.Result, Formatting.None);
                jsonString = jsonString.Replace("\"", string.Empty, StringComparison.OrdinalIgnoreCase);
                jsonString = jsonString.Replace("\\r\\n", string.Empty, StringComparison.OrdinalIgnoreCase);
                jsonString = jsonString.Replace("\\", string.Empty, StringComparison.OrdinalIgnoreCase);
                jsonString = WhitespaceRegex().Replace(jsonString, " ");

                messageObj["content"] = jsonString;

                if (!string.IsNullOrWhiteSpace(toolResultInteraction.Id))
                {
                    messageObj["tool_call_id"] = toolResultInteraction.Id;
                }

                if (!string.IsNullOrWhiteSpace(toolResultInteraction.Name))
                {
                    messageObj["name"] = toolResultInteraction.Name;
                }
            }
            else if (interaction is AIInteractionToolCall toolCallInteraction)
            {
                var toolCallObj = new JObject
                {
                    ["id"] = toolCallInteraction.Id ?? string.Empty,
                    ["type"] = "function",
                    ["function"] = new JObject
                    {
                        ["name"] = toolCallInteraction.Name ?? string.Empty,
                        ["arguments"] = toolCallInteraction.Arguments?.ToString() ?? "{}",
                    },
                };
                messageObj["tool_calls"] = new JArray { toolCallObj };
                messageObj["content"] = string.Empty;

                if (!string.IsNullOrWhiteSpace(toolCallInteraction.Reasoning))
                {
                    messageObj["reasoning_content"] = toolCallInteraction.Reasoning;
                }
            }
            else if (interaction is AIInteractionImage imageInteraction)
            {
                // DeepSeek does not support vision; fallback to prompt as text
                messageObj["content"] = imageInteraction.OriginalPrompt ?? string.Empty;
            }
            else
            {
                // Unknown interaction type
                messageObj["content"] = string.Empty;
            }

            return messageObj;
        }

        /// <inheritdoc/>
        public override List<IAIInteraction> Decode(JObject response)
        {
            // Decode DeepSeek chat completion response into a list of interactions (text only).
            // DeepSeek does not support image generation; we return a single AIInteractionText
            // representing the assistant message, and map any tool calls onto that interaction.
            var interactions = new List<IAIInteraction>();

            if (response == null)
            {
                return interactions;
            }

            try
            {
                var choices = response["choices"] as JArray;
                var firstChoice = choices?.FirstOrDefault() as JObject;
                var message = firstChoice?["message"] as JObject;
                if (message == null)
                {
                    Debug.WriteLine("[DeepSeek] Decode: No message found in response");
                    return interactions;
                }

                var reasoning = message["reasoning_content"]?.ToString() ?? string.Empty;

                // Robust content extraction: handle string, array of parts, or object content
                string content;
                var contentToken = message["content"];
                if (contentToken is JArray contentParts && contentParts.Count > 0)
                {
                    // Concatenate known text-bearing fields; fallback to ToString() for each part
                    var texts = new List<string>();
                    foreach (var part in contentParts)
                    {
                        var text = part?["text"]?.ToString()
                                   ?? part?["content"]?.ToString()
                                   ?? part?.ToString(Newtonsoft.Json.Formatting.None)
                                   ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            texts.Add(text);
                        }
                    }

                    content = string.Join("\n", texts);
                }
                else if (contentToken is JObject contentObj)
                {
                    // Try to unwrap arrays from common keys (items, list, enum)
                    var extractedArray = TryExtractArrayFromObject(contentObj);
                    content = extractedArray ?? contentObj.ToString(Newtonsoft.Json.Formatting.None);
                }
                else
                {
                    content = contentToken?.ToString() ?? string.Empty;
                }

                // Clean up DeepSeek's malformed JSON responses for array schemas
                content = CleanUpDeepSeekArrayResponse(content);
                try
                {
                    var trimmed = content?.TrimStart() ?? string.Empty;
                    if (trimmed.StartsWith("{"))
                    {
                        var obj = JObject.Parse(content);
                        var unwrapped = TryExtractArrayFromObject(obj);
                        if (!string.IsNullOrEmpty(unwrapped))
                        {
                            content = unwrapped;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[DeepSeek] Decode: unwrap attempt failed: {ex.Message}");
                }

                // Then apply centralized unwrapping using stored wrapper info
                var wrapperInfo = JsonSchemaService.Instance.GetCurrentWrapperInfo();
                if (wrapperInfo != null && wrapperInfo.IsWrapped)
                {
                    Debug.WriteLine($"[DeepSeek] Unwrapping response content using wrapper info (central): Type={wrapperInfo.WrapperType}, Property={wrapperInfo.PropertyName}");
                    content = JsonSchemaService.Instance.Unwrap(content, wrapperInfo);
                }

                var interaction = new AIInteractionText();
                interaction.SetResult(
                    agent: AIAgent.Assistant,
                    content: content,
                    reasoning: string.IsNullOrWhiteSpace(reasoning) ? null : reasoning);

                var metrics = this.DecodeMetrics(response);

                interaction.Metrics = metrics;

                interactions.Add(interaction);

                // Add an AIInteractionToolCall for each tool call
                if (message["tool_calls"] is JArray tcs && tcs.Count > 0)
                {
                    foreach (JObject tc in tcs)
                    {
                        var function = tc["function"] as JObject;
                        var argumentsStr = function?["arguments"]?.ToString() ?? "{}";

                        var toolCall = new AIInteractionToolCall
                        {
                            Id = tc["id"]?.ToString(),
                            Name = function?["name"]?.ToString(),
                            Arguments = JObject.Parse(argumentsStr),
                            Reasoning = string.IsNullOrWhiteSpace(reasoning) ? null : reasoning,
                        };
                        interactions.Add(toolCall);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DeepSeek] Decode error: {ex.Message}");
            }

            return interactions;
        }

        /// <summary>
        /// Decodes the metrics from the response.
        /// </summary>
        /// <param name="response">The response to decode.</param>
        /// <returns>The decoded metrics.</returns>
        private AIMetrics DecodeMetrics(JObject response)
        {
            Debug.WriteLine("[DeepSeek] PostCall: parsed response for metrics");

            var choices = response["choices"] as JArray;
            var firstChoice = choices?.FirstOrDefault() as JObject;
            var usage = response["usage"] as JObject;

            // Extract reasoning tokens from nested completion_tokens_details object
            var completionDetails = usage?["completion_tokens_details"] as JObject;
            var reasoningTokens = completionDetails?["reasoning_tokens"]?.Value<int>() ?? 0;

            // Create a new metrics instance
            var metrics = new AIMetrics();
            metrics.FinishReason = firstChoice?["finish_reason"]?.ToString() ?? metrics.FinishReason;
            metrics.InputTokensPrompt = usage?["prompt_tokens"]?.Value<int>() ?? metrics.InputTokensPrompt;
            metrics.OutputTokensGeneration = usage?["completion_tokens"]?.Value<int>() ?? metrics.OutputTokensGeneration;
            metrics.OutputTokensReasoning = reasoningTokens;
            return metrics;
        }

        /// <summary>
        /// Cleans up DeepSeek's malformed JSON responses where array data is incorrectly placed in an "enum" property.
        /// DeepSeek sometimes returns malformed JSON like: {"type":"array, items":{"type":"string"}, "enum":["item1", "item2", ...]}
        /// This method extracts the actual array from the "enum" property and returns it as a proper JSON array.
        /// </summary>
        /// <param name="content">The raw response content from DeepSeek.</param>
        /// <returns>Cleaned content with proper JSON array format.</returns>
        private static string CleanUpDeepSeekArrayResponse(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return content;
            }

            try
            {
                // Check if this looks like a malformed DeepSeek array response
                if (content.Contains("enum") && content.Contains('['))
                {
                    // Try to parse as JObject first
                    try
                    {
                        var responseObj = JObject.Parse(content);
                        var enumArray = responseObj["enum"] as JArray;

                        if (enumArray != null)
                        {
                            // Extract the array from the enum property and return as JSON array
                            var cleanedArray = enumArray.ToString(Newtonsoft.Json.Formatting.None);
                            Debug.WriteLine($"[DeepSeek] Cleaned enum array: {cleanedArray}");
                            return cleanedArray;
                        }
                    }
                    catch
                    {
                        // If JSON parsing fails, try regex extraction as fallback
                        // Look for pattern like: enum":"["item1", "item2", ...]
                        var enumMatch = EnumArrayRegex().Match(content);
                        if (enumMatch.Success)
                        {
                            var enumContent = enumMatch.Groups[1].Value;

                            // Construct a proper JSON array
                            var cleanedArray = $"[{enumContent}]";
                            Debug.WriteLine($"[DeepSeek] Regex extracted enum array: {cleanedArray}");
                            return cleanedArray;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DeepSeek] Error cleaning response: {ex.Message}");

                // Return original content if cleaning fails
            }

            return content;
        }

        /// <summary>
        /// Attempts to extract a JSON array from a wrapper object commonly returned by providers.
        /// Looks for keys like "items", "list", or DeepSeek's malformed "enum" arrays.
        /// Returns a compact JSON array string if found; otherwise null.
        /// </summary>
        /// <param name="obj">The object potentially wrapping an array.</param>
        /// <returns>JSON array string or null.</returns>
        private static string? TryExtractArrayFromObject(JObject obj)
        {
            if (obj == null)
            {
                return null;
            }

            // Preferred keys in order
            var keys = new[] { "items", "list", "enum" };
            foreach (var key in keys)
            {
                if (obj[key] is JArray arr)
                {
                    return arr.ToString(Newtonsoft.Json.Formatting.None);
                }
            }

            return null;
        }

        /// <summary>
        /// Provider-scoped streaming adapter for DeepSeek Chat Completions SSE.
        /// </summary>
        private sealed class DeepSeekStreamingAdapter : AIProviderStreamingAdapter, IStreamingAdapter
        {
            public DeepSeekStreamingAdapter(DeepSeekProvider provider) : base(provider)
            {
            }

            public async IAsyncEnumerable<AIReturn> StreamAsync(
                AIRequestCall request,
                StreamingOptions options,
                [EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                if (request == null)
                {
                    yield break;
                }

                // Prepare request via provider (sets endpoint, method, auth kind)
                request = this.Prepare(request);

                // Only chat completions are supported for streaming
                if (!string.Equals(request.Endpoint, "/chat/completions", StringComparison.Ordinal))
                {
                    var unsupported = new AIReturn();
                    unsupported.CreateProviderError("Streaming is only supported for /chat/completions in this adapter.", request);
                    yield return unsupported;
                    yield break;
                }

                // Build request body and enable streaming
                JObject body;
                try
                {
                    body = JObject.Parse(this.Provider.Encode(request));
                }
                catch
                {
                    body = new JObject();
                }

                body["stream"] = true;

                // Build URL with helper
                var fullUrl = this.BuildFullUrl(request.Endpoint);

                // Configure HTTP client and authentication via helpers
                using var httpClient = this.CreateHttpClient();
                AIReturn? authError = null;
                try
                {
                    var apiKey = ((DeepSeekProvider)this.Provider).GetApiKey();
                    this.ApplyAuthentication(httpClient, request.Authentication, apiKey);
                }
                catch (Exception ex)
                {
                    authError = new AIReturn();
                    authError.CreateProviderError(ex.Message, request);
                }

                if (authError != null)
                {
                    yield return authError;
                    yield break;
                }

                using var httpRequest = this.CreateSsePost(fullUrl, body.ToString(), "application/json");

                HttpResponseMessage response;
                AIReturn? sendError = null;
                try
                {
                    response = await this.SendForStreamAsync(httpClient, httpRequest, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    sendError = new AIReturn();
                    sendError.CreateNetworkError(ex.InnerException?.Message ?? ex.Message, request);
                    response = null!;
                }

                if (sendError != null)
                {
                    yield return sendError;
                    yield break;
                }

                if (!response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    var err = new AIReturn();
                    err.CreateProviderError($"HTTP {(int)response.StatusCode}: {content}", request);
                    yield return err;
                    yield break;
                }

                // Streaming state
                var buffer = new StringBuilder();
                var lastEmit = DateTime.UtcNow;
                var firstChunk = true;
                bool hadReasoningOnlySegment = false; // Track if we emitted reasoning-only
                string finalFinishReason = string.Empty;
                int promptTokens = 0;
                int completionTokens = 0;
                string reasoning = string.Empty;

                // Provider-local aggregate of assistant text
                var assistantAggregate = new AIInteractionText
                {
                    Agent = AIAgent.Assistant,
                    Content = string.Empty,
                    Reasoning = string.Empty,
                    Metrics = new AIMetrics { Provider = this.Provider.Name, Model = request.Model },
                };

                // Tool call accumulation (index -> partial)
                var toolCalls = new Dictionary<int, (string Id, string Name, StringBuilder Args)>();

                // Helper local function to emit text chunk
                async IAsyncEnumerable<AIReturn> EmitAsync(string text, bool streamingStatus)
                {
                    if (string.IsNullOrEmpty(text)) yield break;

                    // Append to provider-local aggregate
                    assistantAggregate.AppendDelta(contentDelta: text);

                    // Emit a snapshot copy to avoid aliasing across yields
                    var snapshot = new AIInteractionText
                    {
                        Agent = assistantAggregate.Agent,
                        Content = assistantAggregate.Content,
                        Reasoning = assistantAggregate.Reasoning,
                        Metrics = new AIMetrics
                        {
                            Provider = assistantAggregate.Metrics.Provider,
                            Model = assistantAggregate.Metrics.Model,
                            FinishReason = assistantAggregate.Metrics.FinishReason,
                            InputTokensCached = assistantAggregate.Metrics.InputTokensCached,
                            InputTokensPrompt = assistantAggregate.Metrics.InputTokensPrompt,
                            OutputTokensReasoning = assistantAggregate.Metrics.OutputTokensReasoning,
                            OutputTokensGeneration = assistantAggregate.Metrics.OutputTokensGeneration,
                            CompletionTime = assistantAggregate.Metrics.CompletionTime,
                        },
                    };

                    var delta = new AIReturn
                    {
                        Request = request,
                        Status = streamingStatus ? AICallStatus.Streaming : AICallStatus.Processing,
                    };
                    delta.SetBody(new List<IAIInteraction> { snapshot });
                    yield return delta;
                    await Task.Yield();
                }

                // Helper to maybe flush buffer per options and return deltas to emit
                async Task<List<AIReturn>> FlushAsync(bool force)
                {
                    var results = new List<AIReturn>();
                    if (buffer.Length == 0) return results;
                    var elapsed = (DateTime.UtcNow - lastEmit).TotalMilliseconds;
                    if (force || !options.CoalesceTokens || buffer.Length >= options.PreferredChunkSize || elapsed >= options.CoalesceDelayMs)
                    {
                        var text = buffer.ToString();
                        buffer.Clear();
                        lastEmit = DateTime.UtcNow;
                        await foreach (var d in EmitAsync(text, streamingStatus: true).WithCancellation(cancellationToken))
                        {
                            results.Add(d);
                        }
                    }

                    return results;
                }

                // Yield initial processing state
                {
                    var initial = new AIReturn { Request = request, Status = AICallStatus.Processing };
                    initial.SetBody(new List<IAIInteraction>());
                    yield return initial;
                }

                // Determine idle timeout from request (fallback to 60s if invalid)
                var idleTimeout = TimeSpan.FromSeconds(request.TimeoutSeconds > 0 ? request.TimeoutSeconds : 60);
                await foreach (var data in this.ReadSseDataAsync(
                    response,
                    idleTimeout,
                    null,
                    cancellationToken).WithCancellation(cancellationToken))
                {
                    JObject parsed;
                    try
                    {
                        parsed = JObject.Parse(data);
                    }
                    catch
                    {
                        // Skip malformed chunk
                        continue;
                    }

                    var choices = parsed["choices"] as JArray;
                    var choice = choices?.FirstOrDefault() as JObject;
                    var delta = choice?["delta"] as JObject;
                    var finishReason = choice?["finish_reason"]?.ToString();
                    bool hasFinish = !string.IsNullOrEmpty(finishReason);
                    if (hasFinish) finalFinishReason = finishReason;

                    // Usage metrics (may be present in final chunk)
                    var usage = parsed["usage"] as JObject;
                    if (usage != null)
                    {
                        var pt = usage["prompt_tokens"]?.Value<int?>();
                        var ct = usage["completion_tokens"]?.Value<int?>();
                        if (pt.HasValue) promptTokens = pt.Value;
                        if (ct.HasValue) completionTokens = ct.Value;

                        // Extract reasoning tokens from nested completion_tokens_details object
                        var completionDetails = usage["completion_tokens_details"] as JObject;
                        var rt = completionDetails?["reasoning_tokens"]?.Value<int?>() ?? 0;

                        // Update aggregate metrics
                        assistantAggregate.AppendDelta(metricsDelta: new AIMetrics
                        {
                            Provider = this.Provider.Name,
                            Model = request.Model,
                            InputTokensPrompt = pt ?? 0,
                            OutputTokensGeneration = ct ?? 0,
                            OutputTokensReasoning = rt,
                        });
                    }

                    // DeepSeek-specific: reasoning/content tracking
                    bool hasReasoningUpdate = false;
                    bool hasContentUpdate = false;
                    var reasoningDelta = delta?["reasoning_content"]?.ToString();
                    if (!string.IsNullOrEmpty(reasoningDelta))
                    {
                        reasoning += reasoningDelta;
                        assistantAggregate.AppendDelta(reasoningDelta: reasoningDelta);
                        hasReasoningUpdate = true;
                    }

                    // Content streaming
                    var contentDelta = delta?["content"]?.ToString();
                    if (!string.IsNullOrEmpty(contentDelta))
                    {
                        buffer.Append(contentDelta);
                        hasContentUpdate = true;
                    }

                    if (hasContentUpdate)
                    {
                        // If we had a reasoning-only segment, complete it first to trigger segmentation
                        if (hadReasoningOnlySegment)
                        {
                            // Emit completed reasoning-only interaction to set boundary flag
                            var reasoningComplete = new AIInteractionText
                            {
                                Agent = assistantAggregate.Agent,
                                Content = string.Empty,
                                Reasoning = assistantAggregate.Reasoning,
                                Time = DateTime.UtcNow,
                                Metrics = new AIMetrics
                                {
                                    Provider = assistantAggregate.Metrics.Provider,
                                    Model = assistantAggregate.Metrics.Model,
                                    OutputTokensReasoning = assistantAggregate.Metrics.OutputTokensReasoning,
                                },
                            };

                            var completeDelta = new AIReturn
                            {
                                Request = request,
                                Status = AICallStatus.Finished,
                            };

                            completeDelta.SetBody(new List<IAIInteraction> { reasoningComplete });
                            yield return completeDelta;
                            await Task.Yield();

                            hadReasoningOnlySegment = false;
                        }

                        if (firstChunk)
                        {
                            firstChunk = false;

                            // Force immediate first emit for snappy UX
                            var emitted = await FlushAsync(force: true).ConfigureAwait(false);
                            foreach (var d in emitted) { yield return d; }
                        }
                        else
                        {
                            var emitted = await FlushAsync(force: false).ConfigureAwait(false);
                            foreach (var d in emitted) { yield return d; }
                        }
                    }
                    else if (hasReasoningUpdate)
                    {
                        // Emit reasoning-only snapshot (no text content yet)
                        var snapshot = new AIInteractionText
                        {
                            Agent = assistantAggregate.Agent,
                            Content = assistantAggregate.Content,
                            Reasoning = assistantAggregate.Reasoning,
                            Metrics = new AIMetrics
                            {
                                Provider = assistantAggregate.Metrics.Provider,
                                Model = assistantAggregate.Metrics.Model,
                                FinishReason = assistantAggregate.Metrics.FinishReason,
                                InputTokensCached = assistantAggregate.Metrics.InputTokensCached,
                                InputTokensPrompt = assistantAggregate.Metrics.InputTokensPrompt,
                                OutputTokensReasoning = assistantAggregate.Metrics.OutputTokensReasoning,
                                OutputTokensGeneration = assistantAggregate.Metrics.OutputTokensGeneration,
                                CompletionTime = assistantAggregate.Metrics.CompletionTime,
                            },
                        };

                        var reasoningOnlyDelta = new AIReturn
                        {
                            Request = request,
                            Status = AICallStatus.Streaming,
                        };
                        reasoningOnlyDelta.SetBody(new List<IAIInteraction> { snapshot });
                        yield return reasoningOnlyDelta;
                        await Task.Yield();

                        hadReasoningOnlySegment = true; // Mark that we have a reasoning segment
                    }

                    // Tool calls streaming
                    var tcArray = delta?["tool_calls"] as JArray;
                    if (tcArray != null)
                    {
                        foreach (var t in tcArray.OfType<JObject>())
                        {
                            var idx = t["index"]?.Value<int?>() ?? 0;
                            if (!toolCalls.TryGetValue(idx, out var entry))
                            {
                                entry = (Id: string.Empty, Name: string.Empty, Args: new StringBuilder());
                                toolCalls[idx] = entry;
                            }

                            // id
                            var idVal = t["id"]?.ToString();
                            if (!string.IsNullOrEmpty(idVal)) entry.Id = idVal;

                            var func = t["function"] as JObject;
                            if (func != null)
                            {
                                var name = func["name"]?.ToString();
                                if (!string.IsNullOrEmpty(name)) entry.Name = name;
                                var args = func["arguments"]?.ToString();
                                if (!string.IsNullOrEmpty(args)) entry.Args.Append(args);
                            }

                            toolCalls[idx] = entry; // update
                        }

                        // If we know we're heading to tool calls, flush text first
                        var emittedTc = await FlushAsync(force: true).ConfigureAwait(false);
                        foreach (var d in emittedTc) { yield return d; }

                        // Emit current tool call snapshot with CallingTools status
                        var interactions = new List<IAIInteraction>();
                        foreach (var kv in toolCalls.OrderBy(k => k.Key))
                        {
                            var (id, name, argsSb) = kv.Value;
                            JObject argsObj = null;
                            var argsStr = argsSb.ToString();
                            try { if (!string.IsNullOrWhiteSpace(argsStr)) argsObj = JObject.Parse(argsStr); } catch { /* partial JSON, ignore */ }
                            interactions.Add(new AIInteractionToolCall { Id = id, Name = name, Arguments = argsObj, Reasoning = assistantAggregate.Reasoning });
                        }

                        var tcDelta = new AIReturn { Request = request, Status = AICallStatus.CallingTools };
                        tcDelta.SetBody(interactions);
                        yield return tcDelta;
                    }
                }

                // Final flush
                var finalEmitted = await FlushAsync(force: true).ConfigureAwait(false);
                foreach (var d in finalEmitted) { yield return d; }

                // Emit final Finished marker with the complete assistant interaction
                var final = new AIReturn
                {
                    Request = request,
                    Status = AICallStatus.Finished,
                };
                var finalMetrics = new AIMetrics
                {
                    Provider = this.Provider.Name,
                    Model = request.Model,
                    FinishReason = string.IsNullOrEmpty(finalFinishReason) ? "stop" : finalFinishReason,
                    InputTokensPrompt = promptTokens,
                    OutputTokensGeneration = completionTokens,
                };

                // Align aggregate metrics finish reason as well
                assistantAggregate.AppendDelta(metricsDelta: new AIMetrics { FinishReason = finalMetrics.FinishReason });

                // Build final body with text and tool calls
                var finalBuilder = AIBodyBuilder.Create();
                var hasToolCalls = toolCalls.Count > 0;

                // Add text interaction if there's actual content (not just reasoning when tool calls exist)
                // When tool calls are present, reasoning belongs to the tool calls for API purposes
                var shouldAddTextInteraction = !string.IsNullOrEmpty(assistantAggregate.Content) ||
                    (!hasToolCalls && !string.IsNullOrEmpty(assistantAggregate.Reasoning));

                if (shouldAddTextInteraction)
                {
                    var finalSnapshot = new AIInteractionText
                    {
                        Agent = assistantAggregate.Agent,
                        Content = assistantAggregate.Content,
                        Reasoning = hasToolCalls ? null : assistantAggregate.Reasoning,
                        Metrics = new AIMetrics
                        {
                            Provider = assistantAggregate.Metrics.Provider,
                            Model = assistantAggregate.Metrics.Model,
                            FinishReason = assistantAggregate.Metrics.FinishReason,
                            InputTokensCached = assistantAggregate.Metrics.InputTokensCached,
                            InputTokensPrompt = assistantAggregate.Metrics.InputTokensPrompt,
                            OutputTokensReasoning = assistantAggregate.Metrics.OutputTokensReasoning,
                            OutputTokensGeneration = assistantAggregate.Metrics.OutputTokensGeneration,
                            CompletionTime = assistantAggregate.Metrics.CompletionTime,
                        },
                    };
                    finalBuilder.Add(finalSnapshot, markAsNew: false);
                }

                // Add tool calls if present (already marked as NOT new since they were yielded)
                foreach (var kv in toolCalls.OrderBy(k => k.Key))
                {
                    var (id, name, argsSb) = kv.Value;
                    JObject argsObj = null;
                    var argsStr = argsSb.ToString();
                    try { if (!string.IsNullOrWhiteSpace(argsStr)) argsObj = JObject.Parse(argsStr); } catch { /* partial JSON */ }
                    finalBuilder.Add(new AIInteractionToolCall { Id = id, Name = name, Arguments = argsObj, Reasoning = assistantAggregate.Reasoning }, markAsNew: false);
                }

                final.SetBody(finalBuilder.Build());
                yield return final;
            }
        }
    }
}
