/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using Newtonsoft.Json.Linq;
using SmartHopper.Config.Configuration;
using SmartHopper.Config.Interfaces;
using SmartHopper.Config.Models;
using SmartHopper.Config.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SmartHopper.Providers.MistralAI
{
    public sealed class MistralAI : IAIProvider
    {
        public const string _name = "MistralAI";
        private const string ApiURL = "https://api.mistral.ai/v1/chat/completions";
        private const string _defaultModel = "mistral-small-latest";

        private static readonly Lazy<MistralAI> _instance = new Lazy<MistralAI>(() => new MistralAI());
        public static MistralAI Instance => _instance.Value;

        private MistralAI() { }

        public string Name => _name;
        public string DefaultModel => _defaultModel;

        /// <summary>
        /// Gets whether this provider is enabled and should be available for use.
        /// </summary>
        public bool IsEnabled => true;

        /// <summary>
        /// Gets the provider's icon
        /// </summary>
        public Image Icon
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

        public IEnumerable<SettingDescriptor> GetSettingDescriptors()
        {
            return new[]
            {
                new SettingDescriptor
                {
                    Name = "ApiKey",
                    Type = typeof(string),
                    DefaultValue = "",
                    IsSecret = true,
                    DisplayName = "API Key",
                    Description = "Your MistralAI API key"
                },
                new SettingDescriptor
                {
                    Name = "Model",
                    Type = typeof(string),
                    DefaultValue = _defaultModel,
                    IsSecret = false,
                    DisplayName = "Model",
                    Description = "The model to use for completions"
                },
                new SettingDescriptor
                {
                    Name = "MaxTokens",
                    Type = typeof(int),
                    DefaultValue = 150,
                    IsSecret = false,
                    DisplayName = "Max Tokens",
                    Description = "Maximum number of tokens to generate"
                }
            };
        }

        public bool ValidateSettings(Dictionary<string, object> settings)
        {
            return settings.ContainsKey("ApiKey") &&
                   !string.IsNullOrEmpty(settings["ApiKey"].ToString()) &&
                   settings.ContainsKey("Model") &&
                   !string.IsNullOrEmpty(settings["Model"].ToString());
        }

        public async Task<AIResponse> GetResponse(JArray messages, string model, string jsonSchema = "", string endpoint = "")
        {
            var settings = SmartHopperSettings.Load();
            if (!settings.ProviderSettings.ContainsKey(_name))
            {
                settings.ProviderSettings[_name] = new Dictionary<string, object>();
            }
            var providerSettings = settings.ProviderSettings[_name];
            if (!ValidateSettings(providerSettings))
                throw new InvalidOperationException("Invalid provider settings");

            var modelToUse = GetModel(providerSettings, model);

            string apiKey = providerSettings.ContainsKey("ApiKey") ? providerSettings["ApiKey"].ToString() : "";
            int maxTokens = providerSettings.ContainsKey("MaxTokens") ? Convert.ToInt32(providerSettings["MaxTokens"]) : 150;

            if (modelToUse == "" || modelToUse == "mistralai")
            {
                modelToUse = _defaultModel;
                Debug.WriteLine($"Using default model: {modelToUse}");
            }

            if (string.IsNullOrEmpty(apiKey))
            {
                Debug.WriteLine("API Key is null or empty");
                return new AIResponse
                {
                    Response = "Error: API Key is missing",
                    FinishReason = "error"
                };
            }

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                string responseType = "text";

                if (!string.IsNullOrEmpty(jsonSchema))
                {
                    string jsonPrompt = $"STRICTLY RETURN THE ANSWER IN THE FOLLOWING JSON SCHEMA:\n{jsonSchema}";
                    messages.Add(new JObject { ["role"] = "system", ["content"] = jsonSchema });
                    responseType = "json_object";
                };

                var requestBody = new JObject
                {
                    ["model"] = modelToUse,
                    ["messages"] = messages,
                    ["temperature"] = 0.3,
                    ["max_tokens"] = maxTokens,
                    ["response_format"] = new JObject
                    {
                        ["type"] = responseType
                    }
                };

                // Add tools to request if available
                var toolsArray = GetFormattedTools();
                if (toolsArray != null && toolsArray.Count > 0)
                {
                    requestBody["tools"] = toolsArray;
                    requestBody["tool_choice"] = "auto";
                    Debug.WriteLine($"Added {toolsArray.Count} tools to the request");
                }

                Debug.WriteLine(requestBody.ToString());

                var content = new StringContent(requestBody.ToString(), Encoding.UTF8, "application/json");

                try
                {
                    var response = await client.PostAsync(ApiURL, content);
                    response.EnsureSuccessStatusCode();
                    var responseBody = await response.Content.ReadAsStringAsync();

                    Debug.WriteLine($"Raw API Response: {responseBody}");

                    if (!response.IsSuccessStatusCode)
                    {
                        Debug.WriteLine($"API returned non-success status code: {response.StatusCode}");
                        return new AIResponse
                        {
                            Response = $"API Error: {responseBody}",
                            FinishReason = "error"
                        };
                    }

                    var json = JObject.Parse(responseBody);

                    Debug.WriteLine(json.ToString());

                    if (json["choices"] != null && json["choices"].Type == JTokenType.Array && json["choices"].Any())
                    {
                        var choice = json["choices"][0];
                        if (choice["message"] != null)
                        {
                            var message = choice["message"];

                            // Check for tool calls
                            if (message["tool_calls"] != null && message["tool_calls"].Type != JTokenType.Null)
                            {
                                var toolCalls = message["tool_calls"];
                                // Format the tool call response for our system
                                var toolCall = toolCalls[0];
                                var toolName = toolCall["function"]["name"].ToString();
                                var toolArgs = toolCall["function"]["arguments"].ToString();

                                // Create a formatted function call message
                                string functionCallResponse = $"function_call: {{ \"name\": \"{toolName}\", \"arguments\": {toolArgs} }}";

                                return new AIResponse
                                {
                                    Response = functionCallResponse,
                                    Provider = _name,
                                    Model = json["model"]?.Value<string>() ?? "Unknown",
                                    InTokens = json["usage"]?["prompt_tokens"]?.Value<int>() ?? 0,
                                    OutTokens = json["usage"]?["completion_tokens"]?.Value<int>() ?? 0,
                                    FinishReason = "tool_call"
                                };
                            }
                            else if (message["content"] != null)
                            {
                                return new AIResponse
                                {
                                    Response = message["content"].ToString().Trim(),
                                    Provider = _name,
                                    Model = json["model"]?.Value<string>() ?? "Unknown",
                                    InTokens = json["usage"]?["prompt_tokens"]?.Value<int>() ?? 0,
                                    OutTokens = json["usage"]?["completion_tokens"]?.Value<int>() ?? 0,
                                    FinishReason = choice["finish_reason"]?.ToString().Trim() ?? "Unknown",
                                };
                            }
                        }
                    }

                    // If we get here, the response wasn't in the expected format
                    return new AIResponse
                    {
                        Response = "Error: Unexpected API response format",
                        FinishReason = "error"
                    };
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in MistralAI request: {ex.Message}");
                    return new AIResponse
                    {
                        Response = $"Error: {ex.Message}",
                        FinishReason = "error"
                    };
                }
            }
        }

        public string GetModel(Dictionary<string, object> providerSettings, string model)
        {
            return !string.IsNullOrEmpty(model) ? model :
                   (providerSettings.ContainsKey("Model") ? providerSettings["Model"].ToString() : _defaultModel);
        }

        /// <summary>
        /// Gets the tools formatted for the MistralAI API
        /// </summary>
        /// <returns>JArray of formatted tools</returns>
        private JArray GetFormattedTools()
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
                            ["parameters"] = JObject.Parse(tool.Value.ParametersSchema)
                        }
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
