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
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.Managers.AIProviders;
using SmartHopper.Infrastructure.Models;
using SmartHopper.Infrastructure.Utils;

namespace SmartHopper.Providers.OpenAI
{
    /// <summary>
    /// OpenAI provider implementation for SmartHopper.
    /// </summary>
    public sealed class OpenAIProvider : AIProvider
    {
        private const string NameValue = "OpenAI";
        private const string DefaultModelValue = "gpt-4.1-mini";
        private const string DefaultServerUrlValue = "https://api.openai.com/v1";

        private static readonly Lazy<OpenAIProvider> InstanceValue = new(() => new OpenAIProvider());

        public static OpenAIProvider Instance => InstanceValue.Value;

        private OpenAIProvider()
        {
        }

        public override string Name => NameValue;

        public override string DefaultModel => DefaultModelValue;

        /// <summary>
        /// Gets the default server URL for the provider.
        /// </summary>
        public override string DefaultServerUrl => DefaultServerUrlValue;

        /// <summary>
        /// Gets a value indicating whether gets whether this provider is enabled and should be available for use.
        /// </summary>
        public override bool IsEnabled => true;

        /// <summary>
        /// Gets the provider's icon.
        /// </summary>
        public override Image Icon
        {
            get
            {
                var iconBytes = Properties.Resources.openai_icon;
                using (var ms = new System.IO.MemoryStream(iconBytes))
                {
                    return new Bitmap(ms);
                }
            }
        }

        /// <summary>
        /// Sends messages to the OpenAI Chat Completions endpoint, injecting a reasoning summary parameter when supported
        /// and wrapping any returned reasoning_summary in <think> tags before the actual content.
        /// </summary>
        /// <remarks>
        /// We pass reasoning_effort (configurable as "low", "medium", or "high") in the request; if the API returns a
        /// reasoning_summary field, we embed it as <think>…</think> immediately preceding the assistant's response.
        /// </remarks>
        public override async Task<AIResponse> GetResponse(JArray messages, string model, string jsonSchema = "", string endpoint = "", string? toolFilter = null)
        {
            // Get settings from the secure settings store
            int maxTokens = this.GetSetting<int>("MaxTokens");
            string modelName = string.IsNullOrWhiteSpace(model) ? this.GetSetting<string>("Model") : model;
            string reasoningEffort = this.GetSetting<string>("ReasoningEffort") ?? "medium";

            // Use default model if none specified
            if (string.IsNullOrWhiteSpace(modelName))
            {
                modelName = DefaultModelValue;
            }

            Debug.WriteLine($"[OpenAI] GetResponse - Model: {modelName}, MaxTokens: {maxTokens}");

                // Format messages for OpenAI API
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

                    if (role == "assistant")
                    {
                        // Pass tool_calls if available
                        if (msg["tool_calls"] is JArray toolCalls && toolCalls.Count > 0)
                        {
                            foreach (JObject toolCall in toolCalls)
                            {
                                var function = toolCall["function"] as JObject;
                                if (function != null)
                                {
                                    // Ensure 'arguments' is serialized as a JSON string
                                    if (function["arguments"] is JObject argumentsObject)
                                    {
                                        function["arguments"] = argumentsObject.ToString(Newtonsoft.Json.Formatting.None);
                                    }
                                }
                            }

                            messageObj["tool_calls"] = toolCalls;
                        }
                    }
                    else if (role == "tool")
                    {
                        var toolCallId = msg["tool_call_id"]?.ToString();
                        var toolName = msg["name"]?.ToString();
                        if (!string.IsNullOrEmpty(toolCallId))
                        {
                            messageObj["tool_call_id"] = toolCallId;
                        }

                        if (!string.IsNullOrEmpty(toolName))
                        {
                            messageObj["name"] = toolName;
                        }
                    }

                    messageObj["role"] = role;
                    convertedMessages.Add(messageObj);
                }

                // Build request body for the new Responses API
                var requestBody = new JObject
                {
                    ["model"] = modelName,
                    ["messages"] = convertedMessages,
                    ["max_completion_tokens"] = maxTokens,
                    ["temperature"] = this.GetSetting<double>("Temperature"),
                };

                // Add reasoning effort if model starts with "(0-9)o"
                if (Regex.IsMatch(modelName, @"^[0-9]o", RegexOptions.IgnoreCase))
                {
                    requestBody["reasoning_effort"] = reasoningEffort;
                }

                // Add response format if JSON schema is provided
                if (!string.IsNullOrEmpty(jsonSchema))
                {
                    try
                    {
                        var schemaObj = JObject.Parse(jsonSchema);
                        requestBody["response_format"] = new JObject
                        {
                            ["type"] = "json_schema",
                            ["schema"] = schemaObj,
                            ["strict"] = true
                        };
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[OpenAI] Failed to parse JSON schema: {ex.Message}");
                        // Continue without schema if parsing fails
                    }
                }


                // Add tools if requested
                if (!string.IsNullOrWhiteSpace(toolFilter))
                {
                    var tools = this.GetFormattedTools();
                    if (tools != null && tools.Count > 0)
                    {
                        requestBody["tools"] = tools;
                        requestBody["tool_choice"] = "auto";
                    }
                }

            Debug.WriteLine($"[OpenAI] Request: {requestBody}");

            try
            {
                // Use the new Call method for HTTP request
                var responseContent = await CallApi("/chat/completions", "POST", requestBody.ToString()).ConfigureAwait(false);

                var responseJson = JObject.Parse(responseContent);
                Debug.WriteLine($"[OpenAI] Response parsed successfully");

                // Extract response content
                var choices = responseJson["choices"] as JArray;
                var firstChoice = choices?.FirstOrDefault() as JObject;
                var message = firstChoice?["message"] as JObject;
                var usage = responseJson["usage"] as JObject;

                if (message == null)
                {
                    Debug.WriteLine($"[OpenAI] No message in response: {responseJson}");
                    throw new Exception("Invalid response from OpenAI API: No message found");
                }

                var content = message["content"]?.ToString() ?? string.Empty;
                var reasoningSummary = message["reasoning_summary"]?.ToString();

                // If we have a reasoning summary, wrap it in <think> tags before the actual content
                if (!string.IsNullOrWhiteSpace(reasoningSummary))
                {
                    content = $"<think>{reasoningSummary}</think>\n\n{content}";
                }

                var aiResponse = new AIResponse
                {
                    Response = content,
                    Provider = "OpenAI",
                    Model = modelName,
                    FinishReason = firstChoice?["finish_reason"]?.ToString() ?? "unknown",
                    InTokens = usage?["prompt_tokens"]?.Value<int>() ?? 0,
                    OutTokens = usage?["completion_tokens"]?.Value<int>() ?? 0,
                };

                // Handle tool calls if any
                if (message["tool_calls"] is JArray toolCalls && toolCalls.Count > 0)
                {
                    aiResponse.ToolCalls = new List<AIToolCall>();
                    foreach (JObject toolCall in toolCalls)
                    {
                        var function = toolCall["function"] as JObject;
                        if (function != null)
                        {
                            aiResponse.ToolCalls.Add(new AIToolCall
                            {
                                Id = toolCall["id"]?.ToString(),
                                Name = function["name"]?.ToString(),
                                Arguments = function["arguments"]?.ToString(),
                            });
                        }
                    }
                }

                Debug.WriteLine($"[OpenAI] Response processed successfully: {aiResponse.Response.Substring(0, Math.Min(50, aiResponse.Response.Length))}...");
                return aiResponse;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OpenAI] Exception: {ex.Message}");
                throw new Exception($"Error communicating with OpenAI API: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Retrieves the list of available model IDs from OpenAI.
        /// </summary>
        public override async Task<List<string>> RetrieveAvailableModels()
        {
            Debug.WriteLine("[OpenAI] Retrieving available models");
            try
            {
                var content = await CallApi("/models").ConfigureAwait(false);
                var json = JObject.Parse(content);
                var data = json["data"] as JArray;
                var modelIds = new List<string>();
                if (data != null)
                {
                    foreach (var item in data.OfType<JObject>())
                    {
                        var id = item["id"]?.ToString();
                        if (!string.IsNullOrEmpty(id)) modelIds.Add(id);
                    }
                }

                return modelIds;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OpenAI] Exception retrieving models: {ex.Message}");
                throw new Exception($"Error retrieving models from OpenAI API: {ex.Message}", ex);
            }
        }
    }
}
