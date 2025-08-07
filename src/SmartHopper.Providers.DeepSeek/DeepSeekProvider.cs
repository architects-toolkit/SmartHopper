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
using System.Threading.Tasks;
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
            this.Models = new DeepSeekProviderModels(this, this.CallApi);
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

        /// <summary>
        /// Gets a response from the AI provider.
        /// </summary>
        /// <param name="messages">The messages to send to the AI provider.</param>
        /// <param name="model">The model to use, or empty for default.</param>
        /// <param name="jsonSchema">Optional JSON schema for response formatting.</param>
        /// <param name="endpoint">Optional custom endpoint URL.</param>
        /// <param name="toolFilter">Optional flag to include tool definitions in the response.</param>
        /// <returns>The AI response.</returns>
        public override async Task<AIReturn<string>> GetResponse(AIRequest request)
        {
            string providerName = request.Provider;
            string model = request.Model;
            List<IAIInteraction> messages = request.Body.Interactions;
            string jsonSchema = request.Body.JsonOutputSchema;
            string endpoint = request.Endpoint;
            string? toolFilter = request.Body.ToolFilter;
            string? contextFilter = request.Body.ContextFilter;
            
            try
            {
                int maxTokens = this.GetSetting<int>("MaxTokens");

                // Format messages for DeepSeek API
                var convertedMessages = new JArray();
                foreach (var msg in messages)
                {
                    string role = msg["role"]?.ToString().ToLower(System.Globalization.CultureInfo.CurrentCulture) ?? "user";
                    string msgContent = msg["content"]?.ToString() ?? string.Empty;
                    msgContent = AI.StripThinkTags(msgContent);

                    var messageObj = new JObject
                    {
                        ["content"] = msgContent,
                    };

                    // Map role names
                    if (role == "system")
                    {
                        // DeepSeek uses system role
                    }
                    else if (role == "assistant")
                    {
                        // DeepSeek uses assistant role

                        // DeepSeek doesn't support tool calls for assistant messages
                        if (msg["tool_calls"] != null)
                        {
                            var toolCalls = msg["tool_calls"] as JArray;
                            int i = 0;
                            foreach (JObject toolCall in toolCalls)
                            {
                                toolCalls[i]["function"]["arguments"] = JsonConvert.SerializeObject(toolCall["function"]["arguments"], Formatting.None);
                                i++;
                            }

                            messageObj["tool_calls"] = toolCalls;
                        }
                    }
                    else if (role == "tool")
                    {
                        // Ensure content is a string, not a json object
                        var jsonString = JsonConvert.SerializeObject(msg["content"], Formatting.None);
                        jsonString = jsonString.Replace("\"", string.Empty, StringComparison.OrdinalIgnoreCase);
                        jsonString = jsonString.Replace("\\r\\n", string.Empty, StringComparison.OrdinalIgnoreCase);
                        jsonString = jsonString.Replace("\\", string.Empty, StringComparison.OrdinalIgnoreCase);

                        // Remove two or more consecutive whitespace characters
                        jsonString = Regex.Replace(jsonString, @"\s+", " ");

                        // Replace content with the cleaned string
                        messageObj["content"] = jsonString;

                        // Propagate tool_call ID and name from incoming message
                        if (msg["name"] != null)
                        {
                            messageObj["name"] = msg["name"];
                        }

                        if (msg["tool_call_id"] != null)
                        {
                            messageObj["tool_call_id"] = msg["tool_call_id"];
                        }
                    }
                    else if (role == "tool_call")
                    {
                        // Omit it
                        continue;
                    }
                    else if (role == "user")
                    {
                        // DeepSeek uses user role
                    }
                    else
                    {
                        role = "system"; // Default to system
                    }

                    messageObj["role"] = role;
                    convertedMessages.Add(messageObj);
                }

                // Build request body
                var requestBody = new JObject
                {
                    ["model"] = model,
                    ["messages"] = convertedMessages,
                    ["max_tokens"] = maxTokens,
                    ["temperature"] = this.GetSetting<double>("Temperature"),
                };

                // Add JSON response format if schema is provided
                if (!string.IsNullOrWhiteSpace(jsonSchema))
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
                        ["content"] = "The response must be a valid JSON object that strictly follows this schema: " + jsonSchema,
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

                // Use the new Call method for HTTP request
                var responseString = await this.CallApi("/chat/completions", "POST", requestBody.ToString()).ConfigureAwait(false);
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
                        Provider = this.Name,
                        Model = model,
                    },
                    Status = AIStatus.Finished,
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
                    aiReturn.Status = AIStatus.CallingTools;
                }

                return aiReturn;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DeepSeek] Exception: {ex.Message}");
                throw new Exception($"Error communicating with DeepSeek API: {ex.Message}", ex);
            }
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
