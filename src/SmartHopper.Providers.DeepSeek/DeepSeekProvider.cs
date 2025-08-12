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
using SmartHopper.Infrastructure.AICall;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.Infrastructure.Utils;

namespace SmartHopper.Providers.DeepSeek
{
    /// <summary>
    /// DeepSeek AI provider implementation.
    /// </summary>
    public class DeepSeekProvider : AIProvider<DeepSeekProvider>
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
            this.Models = new DeepSeekProviderModels(this, request => this.CallApi<string>(request));
        }

        /// <summary>
        /// Gets the name of the provider.
        /// </summary>
        public override string Name => NameValue;

        /// <summary>
        /// Gets the default server URL for the provider.
        /// </summary>
        public override string DefaultServerUrl => "https://api.deepseek.com";

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
            int maxTokens = this.GetSetting<int>("MaxTokens");
            double temperature = this.GetSetting<double>("Temperature");
            string? toolFilter = request.Body.ToolFilter;

            // Format messages for DeepSeek API
            var convertedMessages = new JArray();
            foreach (var interaction in request.Body.Interactions)
            {
                try
                {
                    var messageObj = this.Encode(interaction);
                    convertedMessages.Add(messageObj);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[{this.Name}] Warning: Could not encode interaction: {ex.Message}");
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

            // Add JSON response format if schema is provided
            if (!string.IsNullOrWhiteSpace(request.Body.JsonOutputSchema))
            {
                // Add response format for structured output
                requestBody["response_format"] = new JObject
                {
                    ["type"] = "json_object",
                };

                // Add schema as a system message to guide the model
                var systemMessage = new JObject
                {
                    ["role"] = "system",
                    ["content"] = "The response must be a valid JSON object that strictly follows this schema: " + request.Body.JsonOutputSchema,
                };
                convertedMessages.Insert(0, systemMessage);
            }
            else
            {
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

            Debug.WriteLine($"[DeepSeek] Request: {requestBody}");

            return requestBody.ToString();
        }

        /// <inheritdoc/>
        public override string Encode(IAIInteraction interaction)
        {
            AIAgent role = interaction.Agent;
            string roleName = string.Empty;
            string msgContent;

            // Handle interactions based on type
            if (interaction as AIInteractionText != null)
            {
                // For AIInteractionText, only send the actual content
                msgContent = interaction.Body.Content ?? string.Empty; 
            }
            else
            {
                throw new Exception("Type of interaction not supported by DeepSeek");
            }

            var messageObj = new JObject
            {
                ["content"] = msgContent,
            };

            // Map role names
            if (role == AIAgent.System)
            {
                // DeepSeek uses system role
                roleName = "system";
            }
            else if (role == AIAgent.Context)
            {
                // Rename context to system
                roleName = "system";
            }
            else if (role == AIAgent.Assistant)
            {
                // DeepSeek uses assistant role
                roleName = "assistant";
            }
            else if (role == AIAgent.ToolResult)
            {
                roleName = "tool";

                // Ensure content is a string, not a json object
                var jsonString = JsonConvert.SerializeObject(interaction.Body, Formatting.None);
                jsonString = jsonString.Replace("\"", string.Empty, StringComparison.OrdinalIgnoreCase);
                jsonString = jsonString.Replace("\\r\\n", string.Empty, StringComparison.OrdinalIgnoreCase);
                jsonString = jsonString.Replace("\\", string.Empty, StringComparison.OrdinalIgnoreCase);

                // Remove two or more consecutive whitespace characters
                jsonString = Regex.Replace(jsonString, @"\s+", " ");

                // Replace content with the cleaned string
                messageObj["content"] = jsonString;

                // Propagate tool_call ID and name from incoming message
                if (interaction.Body is JObject bodyObj)
                {
                    if (bodyObj["name"] != null)
                    {
                        messageObj["name"] = bodyObj["name"];
                    }

                    if (bodyObj["tool_call_id"] != null)
                    {
                        messageObj["tool_call_id"] = bodyObj["tool_call_id"];
                    }
                }
            }
            else if (role == AIAgent.ToolCall)
            {
                // Omit it
                return string.Empty;
            }
            else
            {
                // DeepSeek uses user role
                roleName = "user";
            }

            // Add tool calls if present
            if (interaction.ToolCalls != null && interaction.ToolCalls.Count > 0)
            {
                var toolCallsArray = new JArray();

                foreach (var toolCall in interaction.ToolCalls)
                {
                    var toolCallObj = new JObject
                    {
                        ["id"] = toolCall.Id,
                        ["type"] = "function",
                        ["function"] = new JObject
                        {
                            ["name"] = toolCall.Name,
                            ["arguments"] = toolCall.Arguments,
                        },
                    };

                    toolCallsArray.Add(toolCallObj);
                }

                messageObj["tool_calls"] = toolCallsArray;
            }

            // Add message to converted messages
            if (!string.IsNullOrEmpty(roleName))
            {
                messageObj["role"] = roleName;
            }

            return messageObj;
        }

        /// <inheritdoc/>
        public override List<IAIInteraction> Decode(string response)
        {
            // Decode DeepSeek chat completion response into a list of interactions (text only).
            // DeepSeek does not support image generation; we return a single AIInteractionText
            // representing the assistant message, and map any tool calls onto that interaction.
            var interactions = new List<IAIInteraction>();

            if (string.IsNullOrWhiteSpace(response))
            {
                return interactions;
            }

            try
            {
                var responseJson = JObject.Parse(response);

                var choices = responseJson["choices"] as JArray;
                var firstChoice = choices?.FirstOrDefault() as JObject;
                var message = firstChoice?["message"] as JObject;
                if (message == null)
                {
                    Debug.WriteLine("[DeepSeek] Decode: No message found in response");
                    return interactions;
                }

                var reasoning = message["reasoning_content"]?.ToString() ?? string.Empty;
                var content = message["content"]?.ToString() ?? string.Empty;

                // Clean up DeepSeek's malformed JSON responses for array schemas
                content = CleanUpDeepSeekArrayResponse(content);

                var interaction = new AIInteractionText
                {
                    Agent = AIAgent.Assistant,
                    Content = content,
                    Reasoning = string.IsNullOrWhiteSpace(reasoning) ? null : reasoning,
                    Time = DateTime.UtcNow,
                };

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
                            Arguments = tc["arguments"]?.ToString(),
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
        private AIMetrics DecodeMetrics(string response)
        {
            var responseString = response.EncodedResult;
            var responseJson = JObject.Parse(responseString);
            Debug.WriteLine("[DeepSeek] PostCall: parsed response for metrics");

            var choices = responseJson["choices"] as JArray;
            var firstChoice = choices?.FirstOrDefault() as JObject;
            var usage = responseJson["usage"] as JObject;

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
    }
}
