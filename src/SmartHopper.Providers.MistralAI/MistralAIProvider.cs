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
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Config.Interfaces;
using SmartHopper.Config.Managers;
using SmartHopper.Config.Models;

namespace SmartHopper.Providers.MistralAI
{
    public sealed class MistralAIProvider : AIProvider
    {
        private const string NameValue = "MistralAI";
        private const string ApiURL = "https://api.mistral.ai/v1/chat/completions";
        private const string DefaultModelValue = "mistral-small-latest";

        private static readonly Lazy<MistralAIProvider> InstanceValue = new (() => new MistralAIProvider());

        public static MistralAIProvider Instance => InstanceValue.Value;

        private MistralAIProvider()
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
                var iconBytes = Properties.Resources.mistralai_icon;
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
                    Description = "Your MistralAI API key",
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
            Debug.WriteLine($"[MistralAI] ValidateSettings called. Settings null? {settings == null}");
            if (settings == null)
            {
                return false;
            }

            // Extract values from settings dictionary
            string apiKey = null;
            string model = null;
            int? maxTokens = null;

            // Get API key if present
            if (settings.TryGetValue("ApiKey", out var apiKeyObj) && apiKeyObj != null)
            {
                apiKey = apiKeyObj.ToString();
                Debug.WriteLine($"[MistralAI] API key extracted (length: {apiKey.Length})");
            }

            // Get model if present
            if (settings.TryGetValue("Model", out var modelObj) && modelObj != null)
            {
                model = modelObj.ToString();
                Debug.WriteLine($"[MistralAI] Model extracted: {model}");
            }

            // Check max tokens if present - must be a positive number
            if (settings.TryGetValue("MaxTokens", out var maxTokensObj) && maxTokensObj != null)
            {
                // Try to parse as integer
                if (int.TryParse(maxTokensObj.ToString(), out int parsedMaxTokens))
                {
                    maxTokens = parsedMaxTokens;
                }
            }
            
            // Use the centralized validation method for the common settings
            bool isValid = true;
            
            // Only validate settings that are actually provided (partial updates allowed)
            if (apiKey != null || model != null || maxTokens.HasValue)
            {
                isValid = MistralAIProviderSettings.ValidateSettingsLogic(apiKey, model, maxTokens);
            }

            Debug.WriteLine($"[MistralAI] Settings validation result: {isValid}");
            return isValid;
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
                throw new Exception("MistralAI API key is not configured or is invalid.");
            }

            // Use default model if none specified
            if (string.IsNullOrWhiteSpace(modelName))
            {
                modelName = DefaultModelValue;
            }

            Debug.WriteLine($"[MistralAI] GetResponse - Model: {modelName}, MaxTokens: {maxTokens}");

            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                // Format messages for Mistral API
                var convertedMessages = new JArray();
                foreach (var msg in messages)
                {
                    // Provider-specific: propagate assistant tool_call messages unmodified
                    string role = msg["role"]?.ToString().ToLower(System.Globalization.CultureInfo.CurrentCulture) ?? "user";
                    string content = msg["content"]?.ToString() ?? string.Empty;

                    var messageObj = new JObject
                    {
                        ["content"] = content,
                    };

                    // Map role names
                    if (role == "system")
                    {
                        // Mistral uses system role
                    }
                    else if (role == "assistant")
                    {
                        // Mistral uses assistant role

                        // Pass tool_calls if available
                        if (msg["tool_calls"] != null)
                        {
                            messageObj["tool_calls"] = msg["tool_calls"];
                        }
                    }
                    else if (role == "tool")
                    {
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
                        // Mistral uses user role
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
                };

                // Add JSON response format if schema is provided
                if (!string.IsNullOrWhiteSpace(jsonSchema))
                {
                    // Add response format for structured output
                    requestBody["response_format"] = new JObject
                    {
                        ["type"] = "json_object"
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
                Debug.WriteLine($"[MistralAI] Request: {requestBody}");

                try
                {
                    var response = await httpClient.PostAsync(ApiURL, requestContent).ConfigureAwait(false);
                    var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    Debug.WriteLine($"[MistralAI] Response status: {response.StatusCode}");

                    if (!response.IsSuccessStatusCode)
                    {
                        Debug.WriteLine($"[MistralAI] Error response: {responseContent}");
                        throw new Exception($"Error from MistralAI API: {response.StatusCode} - {responseContent}");
                    }

                    var responseJson = JObject.Parse(responseContent);
                    Debug.WriteLine($"[MistralAI] Response parsed successfully");

                    // Extract response content
                    var choices = responseJson["choices"] as JArray;
                    var firstChoice = choices?.FirstOrDefault() as JObject;
                    var message = firstChoice?["message"] as JObject;
                    var usage = responseJson["usage"] as JObject;

                    if (message == null)
                    {
                        Debug.WriteLine($"[MistralAI] No message in response: {responseJson}");
                        throw new Exception("Invalid response from MistralAI API: No message found");
                    }

                    var aiResponse = new AIResponse
                    {
                        Response = message["content"]?.ToString() ?? string.Empty,
                        Provider = "MistralAI",
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

                    Debug.WriteLine($"[MistralAI] Response processed successfully: {aiResponse.Response.Substring(0, Math.Min(50, aiResponse.Response.Length))}...");
                    return aiResponse;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[MistralAI] Exception: {ex.Message}");
                    throw new Exception($"Error communicating with MistralAI API: {ex.Message}", ex);
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
        /// Gets the tools formatted for the MistralAI API.
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
                    // Format each tool according to MistralAI's requirements
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

                Debug.WriteLine($"Formatted {toolsArray.Count} tools for MistralAI");
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
