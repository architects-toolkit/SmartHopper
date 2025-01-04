/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SmartHopper.Config.Providers
{
    public sealed class OpenAI : IAIProvider
    {
        public const string _name = "OpenAI";
        private const string ApiURL = "https://api.openai.com/v1/chat/completions";
        private const string _defaultModel = "gpt-4o-mini";

        private static readonly Lazy<OpenAI> _instance = new Lazy<OpenAI>(() => new OpenAI());
        public static OpenAI Instance => _instance.Value;

        private OpenAI() { }

        public string Name => _name;
        public string DefaultModel => _defaultModel;

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
                    ["max_completion_tokens"] = maxTokens,
                    ["modalities"] = new JArray { "text" }
                };

                if (!string.IsNullOrEmpty(jsonSchema))
                {
                    var responseFormat = new JObject
                    {
                        ["type"] = "json_schema",
                        ["json_schema"] = JObject.Parse(jsonSchema)
                    };
                    requestBody["response_format"] = responseFormat;
                }

                var content = new StringContent(requestBody.ToString(), Encoding.UTF8, "application/json");

                Debug.WriteLine(requestBody.ToString());
                Debug.WriteLine(content.ToString());

                try
                {
                    var response = await client.PostAsync(ApiURL, content);
                    response.EnsureSuccessStatusCode();
                    var responseBody = await response.Content.ReadAsStringAsync();
                    var json = JObject.Parse(responseBody);

                    return new AIResponse
                    {
                        Response = json["choices"][0]["message"]["content"].ToString().Trim(),
                        Provider = _name,
                        Model = modelToUse,
                        InTokens = (int)json["usage"]["prompt_tokens"],
                        OutTokens = (int)json["usage"]["completion_tokens"],
                        FinishReason = json["choices"][0]["finish_reason"].ToString().Trim(),
                    };
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in OpenAI request: {ex.Message}");
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
    }
}
