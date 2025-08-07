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
        public override IAIRequest PreCall<T>(IAIRequest request)
        {
            // First do the base PreCall
            request = base.PreCall<T>(request);

            // Setup proper httpmethod, content type, and authentication
            request.HttpMethod = "POST";
            request.ContentType = "application/json";
            request.Authentication = "bearer";

            // Currently DeepSeek is only compatible with chat completions
            request.Endpoint = "/chat/completions";

            return request;
        }

        /// <inheritdoc/>
        public override string FormatRequestBody(IAIRequest request)
        {
            // If Body type is not string return error
            if (request.Body.Interactions.OfType<AIInteraction<string>>() == null)
            {
                throw new Exception("Error: Body type " + request.Body.GetType().Name + " is not supported for " + this.Name + " provider");
            }

            int maxTokens = this.GetSetting<int>("MaxTokens");
            double temperature = this.GetSetting<double>("Temperature");
            string? toolFilter = request.Body.ToolFilter;

            // Format messages for DeepSeek API
            var convertedMessages = new JArray();
            foreach (var interaction in request.Body.Interactions)
            {
                AIAgent role = interaction.Agent;
                string roleName = string.Empty;
                string msgContent = interaction.Body.ToString() ?? string.Empty;
                msgContent = AI.StripThinkTags(msgContent);

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
                    continue;
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
                    convertedMessages.Add(messageObj);
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
        public override IAIReturn<T> PostCall<T>(IAIReturn<T> response)
        {
            // First do the base PostCall
            response = base.PostCall<T>(response);

            // If type is not string return error
            if (!(typeof(T) == typeof(string) && response is IAIReturn<string> stringResponse))
            {
                throw new Exception("Error: Type " + typeof(T).Name + " is not supported for " + this.Name + " provider");
            }

            var responseString = response.RawResult;
            var responseJson = JObject.Parse(responseString);
            Debug.WriteLine($"[DeepSeek] Response parsed successfully");

            var choices = responseJson["choices"] as JArray;
            var firstChoice = choices?.FirstOrDefault() as JObject;
            var message = firstChoice?["message"] as JObject;
            if (message == null)
            {
                Debug.WriteLine($"[DeepSeek] No message in response: {responseString}");
                throw new Exception("Invalid response from DeepSeek API: No message found");
            }

            var usage = responseJson["usage"] as JObject;
            var reasoning = message["reasoning_content"]?.ToString();
            var content = message["content"]?.ToString() ?? string.Empty;

            // Clean up DeepSeek's malformed JSON responses for array schemas
            content = CleanUpDeepSeekArrayResponse(content);

            var combined = !string.IsNullOrWhiteSpace(reasoning)
                ? $"<think>{reasoning}</think>{content}"
                : content;

            var aiReturn = new AIReturn<string>
            {
                Result = combined,
                Metrics = new AIMetrics
                {
                    FinishReason = firstChoice?["finish_reason"]?.ToString() ?? string.Empty,
                    InputTokensPrompt = usage?["prompt_tokens"]?.Value<int>() ?? 0,
                    OutputTokensGeneration = usage?["completion_tokens"]?.Value<int>() ?? 0,
                },
                Status = AICallStatus.Finished,
            };

            if (message["tool_calls"] is JArray tcs && tcs.Count > 0)
            {
                aiReturn.ToolCalls = new List<AIToolCall>();
                foreach (JObject tc in tcs)
                {
                    var fn = tc["function"] as JObject;
                    if (fn != null)
                    {
                        aiReturn.ToolCalls.Add(new AIToolCall
                        {
                            Id = tc["id"]?.ToString(),
                            Name = fn["name"]?.ToString(),
                            Arguments = fn["arguments"]?.ToString(),
                        });
                    }
                }
                aiReturn.Status = AICallStatus.CallingTools;
            }

            return (IAIReturn<T>)aiReturn;
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
