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
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartHopper.Config.Interfaces;
using SmartHopper.Config.Models;
using SmartHopper.Config.Utils;

namespace SmartHopper.Providers.DeepSeek
{
    /// <summary>
    /// DeepSeek AI provider implementation.
    /// </summary>
    public class DeepSeekProvider : AIProvider
    {
        // Static instance for singleton pattern
        private static readonly Lazy<DeepSeekProvider> _instance = new Lazy<DeepSeekProvider>(() => new DeepSeekProvider());

        /// <summary>
        /// Gets the singleton instance of the provider.
        /// </summary>
        public static DeepSeekProvider Instance => _instance.Value;

        /// <summary>
        /// The name of the provider. This will be displayed in the UI and used for provider selection.
        /// </summary>
        public static readonly string _name = "DeepSeek";

        /// <summary>
        /// The default model to use if none is specified.
        /// </summary>
        private const string _defaultModel = "deepseek-chat";
        private const string ApiURL = "https://api.deepseek.com/chat/completions";

        /// <summary>
        /// Private constructor to enforce singleton pattern.
        /// </summary>
        private DeepSeekProvider()
        {
            // Initialization code if needed
        }

        /// <summary>
        /// Gets the name of the provider.
        /// </summary>
        public override string Name => _name;

        /// <summary>
        /// Gets the default model for this provider.
        /// </summary>
        public override string DefaultModel => _defaultModel;

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
        /// <param name="includeToolDefinitions">Optional flag to include tool definitions in the response.</param>
        /// <returns>The AI response.</returns>
        public override async Task<AIResponse> GetResponse(JArray messages, string model, string jsonSchema = "", string endpoint = "", bool includeToolDefinitions = false)
        {
            try
            {
                string apiKey = GetSetting<string>("ApiKey");
                int maxTokens = GetSetting<int>("MaxTokens");
                string modelName = string.IsNullOrWhiteSpace(model) ? GetSetting<string>("Model") : model;
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    throw new Exception("DeepSeek API key is not configured or is invalid.");
                }
                if (string.IsNullOrWhiteSpace(modelName))
                {
                    modelName = this.DefaultModel;
                }

                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

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
                    ["model"] = modelName,
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
                        ["content"] = "You are a helpful assistant that returns responses in JSON format. " +
                                      "The response must be a valid JSON object that follows this schema exactly: " +
                                      jsonSchema
                    };
                    convertedMessages.Insert(0, systemMessage);
                }
                else
                {
                    requestBody["response_format"] = new JObject { ["type"] = "text" };
                }

                // Add tools if requested
                if (includeToolDefinitions)
                {
                    var tools = this.GetFormattedTools();
                    if (tools != null && tools.Count > 0)
                    {
                        requestBody["tools"] = tools;
                        requestBody["tool_choice"] = "auto";
                    }
                }

                var requestContent = new StringContent(requestBody.ToString(), Encoding.UTF8, "application/json");
                string url = ApiURL;
                Debug.WriteLine($"[DeepSeek] Request: {requestBody}");

                var response = await httpClient.PostAsync(url, requestContent).ConfigureAwait(false);
                var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                Debug.WriteLine($"[DeepSeek] Response status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[DeepSeek] Error response: {responseString}");
                    throw new Exception($"Error from DeepSeek API: {response.StatusCode} - {responseString}");
                }

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
                var combined = !string.IsNullOrWhiteSpace(reasoning)
                    ? $"<think>{reasoning}</think>{content}"
                    : content;

                var aiResponse = new AIResponse
                {
                    Response = combined,
                    Provider = this.Name,
                    Model = modelName,
                    FinishReason = firstChoice?["finish_reason"]?.ToString() ?? string.Empty,
                    InTokens = usage?["prompt_tokens"]?.Value<int>() ?? 0,
                    OutTokens = usage?["completion_tokens"]?.Value<int>() ?? 0,
                };

                if (message["tool_calls"] is JArray tcs && tcs.Count > 0)
                {
                    aiResponse.ToolCalls = new List<AIToolCall>();
                    foreach (JObject tc in tcs)
                    {
                        var fn = tc["function"] as JObject;
                        if (fn != null)
                        {
                            aiResponse.ToolCalls.Add(new AIToolCall
                            {
                                Id = tc["id"]?.ToString(),
                                Name = fn["name"]?.ToString(),
                                Arguments = fn["arguments"]?.ToString()
                            });
                        }
                    }
                }

                return aiResponse;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DeepSeek] Exception: {ex.Message}");
                throw new Exception($"Error communicating with DeepSeek API: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets the model to use for AI processing.
        /// </summary>
        /// <param name="settings">The provider settings.</param>
        /// <param name="requestedModel">The requested model, or empty for default.</param>
        /// <returns>The model to use.</returns>
        public override string GetModel(Dictionary<string, object> settings, string requestedModel = "")
        {
            // Use the requested model if provided
            if (!string.IsNullOrWhiteSpace(requestedModel))
            {
                return requestedModel;
            }

            // Use the model from settings if available
            string modelFromSettings = this.GetSetting<string>("Model");
            if (!string.IsNullOrWhiteSpace(modelFromSettings))
            {
                return modelFromSettings;
            }

            // Fall back to the default model
            return this.DefaultModel;
        }
    }
}
