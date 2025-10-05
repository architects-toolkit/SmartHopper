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
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.JsonSchemas;
using SmartHopper.Infrastructure.AICall.Metrics;
using SmartHopper.Infrastructure.AIProviders;

namespace SmartHopper.Providers.DeepSeek
{
    /// <summary>
    /// DeepSeek AI provider implementation.
    /// </summary>
    public sealed class DeepSeekProvider : AIProvider<DeepSeekProvider>
    {
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

            // Simple sequential encoding like OpenAI/MistralAI
            var convertedMessages = new JArray();
            foreach (var interaction in request.Body.Interactions)
            {
                try
                {
                    var token = this.EncodeToJToken(interaction);
                    if (token != null)
                    {
                        convertedMessages.Add(token);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[DeepSeek] Warning: Could not encode interaction: {ex.Message}");
                }
            }

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
            }
            else if (interaction is AIInteractionToolResult toolResultInteraction)
            {
                // DeepSeek requires cleaned/simplified tool result content
                var jsonString = JsonConvert.SerializeObject(toolResultInteraction.Result, Formatting.None);
                jsonString = jsonString.Replace("\"", string.Empty, StringComparison.OrdinalIgnoreCase);
                jsonString = jsonString.Replace("\\r\\n", string.Empty, StringComparison.OrdinalIgnoreCase);
                jsonString = jsonString.Replace("\\", string.Empty, StringComparison.OrdinalIgnoreCase);
                jsonString = Regex.Replace(jsonString, @"\s+", " ");

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
                messageObj["content"] = string.Empty; // assistant tool_calls messages should have empty content
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
                        var toolCall = new AIInteractionToolCall
                        {
                            Id = tc["id"]?.ToString(),
                            Name = tc["name"]?.ToString(),
                            Arguments = tc["arguments"] as JObject ?? new JObject(),
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

            // Create a new metrics instance
            var metrics = new AIMetrics();
            metrics.FinishReason = firstChoice?["finish_reason"]?.ToString() ?? metrics.FinishReason;
            metrics.InputTokensPrompt = usage?["prompt_tokens"]?.Value<int>() ?? metrics.InputTokensPrompt;
            metrics.OutputTokensGeneration = usage?["completion_tokens"]?.Value<int>() ?? metrics.OutputTokensGeneration;
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
                        // Look for pattern like: enum":["item1", "item2", ...]
                        var enumMatch = Regex.Match(content, @"enum[""']?:\s*\[([^\]]+)\]");
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
    }
}
