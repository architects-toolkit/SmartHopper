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
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Config.Interfaces;
using SmartHopper.Config.Managers;
using SmartHopper.Config.Models;

namespace SmartHopper.Providers.OpenAI
{
    /// <summary>
    /// OpenAI provider implementation for SmartHopper.
    /// </summary>
    public sealed class OpenAI : AIProvider
    {
        private const string NameValue = "OpenAI";
        private const string ApiURL = "https://api.openai.com/v1/chat/completions";
        private const string DefaultModelValue = "gpt-4o-mini";

        private static readonly Lazy<OpenAI> InstanceValue = new (() => new OpenAI());

        public static OpenAI Instance => InstanceValue.Value;

        private OpenAI()
        {
        }

        public override string Name => NameValue;

        public override string DefaultModel => DefaultModelValue;

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

        public override IEnumerable<SettingDescriptor> GetSettingDescriptors()
        {
            return new[]
            {
                new SettingDescriptor
                {
                    Name = "ApiKey",
                    Type = typeof(string),
                    DefaultValue = string.Empty,
                    IsSecret = true,
                    DisplayName = "API Key",
                    Description = "Your OpenAI API key",
                },
                new SettingDescriptor
                {
                    Name = "Model",
                    Type = typeof(string),
                    DefaultValue = DefaultModelValue,
                    IsSecret = false,
                    DisplayName = "Model",
                    Description = "The model to use for completions",
                },
                new SettingDescriptor
                {
                    Name = "MaxTokens",
                    Type = typeof(int),
                    DefaultValue = 150,
                    IsSecret = false,
                    DisplayName = "Max Tokens",
                    Description = "Maximum number of tokens to generate",
                },
            };
        }

        public override bool ValidateSettings(Dictionary<string, object> settings)
        {
            Debug.WriteLine($"[OpenAI] ValidateSettings called. Settings null? {settings == null}");
            if (settings == null)
            {
                return false;
            }

            // Only validate settings that are actually provided
            // This allows partial setting updates rather than requiring all settings

            // Check API key format if present
            if (settings.TryGetValue("ApiKey", out var apiKeyObj) && apiKeyObj != null)
            {
                string apiKey = apiKeyObj.ToString();

                // Simple format validation - don't require presence, just valid format if provided
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    Debug.WriteLine("[OpenAI] API key format validation failed: empty key provided");
                    return false;
                }

                Debug.WriteLine($"[OpenAI] API key format validation passed (length: {apiKey.Length})");
            }

            // Check model format if present
            if (settings.TryGetValue("Model", out var modelObj) && modelObj != null)
            {
                string model = modelObj.ToString();
                if (string.IsNullOrWhiteSpace(model))
                {
                    Debug.WriteLine("[OpenAI] Model format validation failed: empty model name provided");
                    return false;
                }

                Debug.WriteLine($"[OpenAI] Model validation passed: {model}");
            }

            // Check max tokens if present - must be a positive number
            if (settings.TryGetValue("MaxTokens", out var maxTokensObj) && maxTokensObj != null)
            {
                // Try to parse as integer
                if (int.TryParse(maxTokensObj.ToString(), out int maxTokens))
                {
                    if (maxTokens <= 0)
                    {
                        Debug.WriteLine($"[OpenAI] MaxTokens validation failed: value must be positive, got {maxTokens}");
                        return false;
                    }

                    Debug.WriteLine($"[OpenAI] MaxTokens validation passed: {maxTokens}");
                }
                else
                {
                    Debug.WriteLine($"[OpenAI] MaxTokens validation failed: value must be an integer, got {maxTokensObj}");
                    return false;
                }
            }

            // All provided settings are valid
            return true;
        }

        public override async Task<AIResponse> GetResponse(JArray messages, string model, string jsonSchema = "", string endpoint = "", bool includeToolDefinitions = false)
        {
            // Get settings from the secure settings store
            string apiKey = this.GetSetting<string>("ApiKey");
            int maxTokens = this.GetSetting<int>("MaxTokens");
            string modelName = string.IsNullOrWhiteSpace(model) ? this.GetSetting<string>("Model") : model;

            // Validate API key
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new Exception("OpenAI API key is not configured or is invalid.");
            }

            // Use default model if none specified
            if (string.IsNullOrWhiteSpace(modelName))
            {
                modelName = DefaultModelValue;
            }

            Debug.WriteLine($"[OpenAI] GetResponse - Model: {modelName}, MaxTokens: {maxTokens}");

            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                // Format messages for OpenAI API
                var convertedMessages = new JArray();
                foreach (var msg in messages)
                {
                    string role = msg["role"]?.ToString().ToLower(System.Globalization.CultureInfo.CurrentCulture) ?? "user";
                    string content = msg["content"]?.ToString() ?? string.Empty;

                    var messageObj = new JObject
                    {
                        ["content"] = content,
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
                    ["max_tokens"] = maxTokens,
                };

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
                if (includeToolDefinitions)
                {
                    var tools = GetFormattedTools();
                    if (tools != null && tools.Count > 0)
                    {
                        requestBody["tools"] = tools;
                        requestBody["tool_choice"] = "auto";
                    }
                }

                var requestContent = new StringContent(requestBody.ToString(), Encoding.UTF8, "application/json");
                Debug.WriteLine($"[OpenAI] Request: {requestBody}");

                try
                {
                    var response = await httpClient.PostAsync(ApiURL, requestContent).ConfigureAwait(false);
                    var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    Debug.WriteLine($"[OpenAI] Response status: {response.StatusCode}");

                    if (!response.IsSuccessStatusCode)
                    {
                        Debug.WriteLine($"[OpenAI] Error response: {responseContent}");
                        var errorObj = JObject.Parse(responseContent);
                        var errorMessage = errorObj["error"]?["message"]?.ToString() ?? responseContent;
                        throw new Exception($"Error from OpenAI API: {response.StatusCode} - {errorMessage}");
                    }

                    var responseJson = JObject.Parse(responseContent);
                    Debug.WriteLine($"[OpenAI] Response parsed successfully");

                    // Extract response content from the Chat Completions API format
                    var message = responseJson["choices"]?[0]?["message"] as JObject;
                    var usage = responseJson["usage"] as JObject;

                    if (message == null)
                    {
                        Debug.WriteLine($"[OpenAI] No message in response: {responseJson}");
                        throw new Exception("Invalid response from OpenAI API: No message found");
                    }


                    var aiResponse = new AIResponse
                    {
                        Response = message?["content"]?.ToString() ?? string.Empty,
                        Provider = "OpenAI",
                        Model = modelName,
                        FinishReason = responseJson["choices"]?[0]?["finish_reason"]?.ToString() ?? "unknown",
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
        }

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

        /// <summary>
        /// Gets the tools formatted for the OpenAI API.
        /// </summary>
        /// <returns>JArray of formatted tools.</returns>
        private static new JArray? GetFormattedTools()
        {
            try
            {
                // Ensure tools are discovered
                AIToolManager.DiscoverTools();

                // Get all available tools
                var tools = AIToolManager.GetTools();
                if (tools.Count == 0)
                {
                    Debug.WriteLine("No tools available.");
                    return null;
                }

                var toolsArray = new JArray();

                foreach (var tool in tools)
                {
                    // Format each tool according to OpenAI's requirements
                    var toolObject = new JObject
                    {
                        ["type"] = "function",
                        ["function"] = new JObject
                        {
                            ["name"] = tool.Value.Name,
                            ["description"] = tool.Value.Description,
                            ["parameters"] = JObject.Parse(tool.Value.ParametersSchema),
                        },
                    };

                    toolsArray.Add(toolObject);
                }

                Debug.WriteLine($"Formatted {toolsArray.Count} tools for OpenAI");
                return toolsArray;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error formatting tools: {ex.Message}");
                return null;
            }
        }
    }
}
