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
using SmartHopper.Config.Interfaces;
using SmartHopper.Config.Models;
using SmartHopper.Config.Managers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SmartHopper.Providers.OpenAI
{
    /// <summary>
    /// OpenAI provider implementation for SmartHopper.
    /// </summary>
    public sealed class OpenAI : AIProvider
    {
        private const string _name = "OpenAI";
        private const string ApiURL = "https://api.openai.com/v1/chat/completions";
        private const string _defaultModel = "gpt-4o-mini";

        private static readonly Lazy<OpenAI> _instance = new Lazy<OpenAI>(() => new OpenAI());

        public static OpenAI Instance => _instance.Value;

        private OpenAI() { }

        public override string Name => _name;

        public override string DefaultModel => _defaultModel;

        /// <summary>
        /// Gets whether this provider is enabled and should be available for use.
        /// </summary>
        public override bool IsEnabled => true;

        /// <summary>
        /// Gets the provider's icon
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

        /// <inheritdoc />
        public void InitializeSettings(Dictionary<string, object> settings)
        {
            _injectedSettings = settings ?? new Dictionary<string, object>();
        }

        public override IEnumerable<SettingDescriptor> GetSettingDescriptors()
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
                    Description = "Your OpenAI API key"
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

        public override bool ValidateSettings(Dictionary<string, object> settings)
        {
            // The API key might be stored as a boolean flag indicating it exists
            bool hasApiKey = settings.ContainsKey("ApiKey") && 
                             (settings["ApiKey"] is bool apiKeyDefined ? apiKeyDefined : 
                              !string.IsNullOrEmpty(settings["ApiKey"]?.ToString()));
                              
            bool hasModel = settings.ContainsKey("Model") && !string.IsNullOrEmpty(settings["Model"]?.ToString());
            
            return hasApiKey && hasModel;
        }

        public override async Task<AIResponse> GetResponse(JArray messages, string model, string jsonSchema = "", string endpoint = "", bool includeToolDefinitions = false)
        {
            try
            {
                var providerSettings = _injectedSettings;

                if (!ValidateSettings(providerSettings))
                    throw new InvalidOperationException("Invalid provider settings");

                var modelToUse = GetModel(providerSettings, model);

                string apiKey = providerSettings.ContainsKey("ApiKey") ? providerSettings["ApiKey"].ToString() : "";
                int maxTokens = providerSettings.ContainsKey("MaxTokens") ? Convert.ToInt32(providerSettings["MaxTokens"]) : 150;

                if (modelToUse == "" || modelToUse == "openai")
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

                    var requestBody = new JObject
                    {
                        ["model"] = modelToUse,
                        ["messages"] = messages,
                        ["temperature"] = 0.7,
                        ["max_tokens"] = maxTokens
                    };

                    // Add functions to request if available
                    var toolsArray = includeToolDefinitions ? GetFormattedTools() : null;
                    if (toolsArray != null && toolsArray.Count > 0)
                    {
                        requestBody["tools"] = toolsArray;
                        requestBody["tool_choice"] = "auto";
                        Debug.WriteLine($"Added {toolsArray.Count} tools to the request");
                    }

                    if (!string.IsNullOrEmpty(jsonSchema))
                    {
                        var responseFormat = new JObject
                        {
                            ["type"] = "json_object"
                        };
                        requestBody["response_format"] = responseFormat;
                    }

                    var content = new StringContent(requestBody.ToString(), Encoding.UTF8, "application/json");

                    Debug.WriteLine($"Sending request to OpenAI with model: {modelToUse}");

                    var response = await client.PostAsync(ApiURL, content);
                    response.EnsureSuccessStatusCode();
                    var responseBody = await response.Content.ReadAsStringAsync();
                    var json = JObject.Parse(responseBody);

                    return new AIResponse
                    {
                        Response = json["choices"][0]["message"]["content"].ToString().Trim(),
                        Provider = _name,
                        Model = json["model"]?.Value<string>() ?? "Unknown",
                        InTokens = (int)json["usage"]["prompt_tokens"],
                        OutTokens = (int)json["usage"]["completion_tokens"],
                        FinishReason = json["choices"][0]["finish_reason"].ToString().Trim(),
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OpenAI request: {ex.Message}");
                throw new Exception($"Error getting response from OpenAI: {ex.Message}", ex);
            }
        }

        public string GetModel(Dictionary<string, object> settings, string requestedModel = "")
        {
            // Use the requested model if provided
            if (!string.IsNullOrWhiteSpace(requestedModel))
                return requestedModel;

            // Use the model from settings if available
            if (settings != null && settings.ContainsKey("Model") && !string.IsNullOrWhiteSpace(settings["Model"]?.ToString()))
                return settings["Model"].ToString();

            // Fall back to the default model
            return _defaultModel;
        }

        /// <summary>
        /// Gets the tools formatted for the OpenAI API
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
                    // Format each tool according to OpenAI's requirements
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
