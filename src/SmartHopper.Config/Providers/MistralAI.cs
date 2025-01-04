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
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SmartHopper.Config.Providers
{
    public sealed class MistralAI : IAIProvider
    {
        public const string ProviderName = "MistralAI";
        private const string ApiURL = "https://api.mistral.ai/v1/chat/completions";
        public const string DefaultModelName = "mistral-small-latest";

        private static readonly Lazy<MistralAI> _instance = new Lazy<MistralAI>(() => new MistralAI());
        public static MistralAI Instance => _instance.Value;

        private MistralAI() { }

        public string Name => ProviderName;

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
                    DefaultValue = DefaultModelName,
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
            if (!settings.ProviderSettings.ContainsKey(ProviderName))
            {
                settings.ProviderSettings[ProviderName] = new Dictionary<string, object>();
            }
            var providerSettings = settings.ProviderSettings[ProviderName];

            string apiKey = providerSettings.ContainsKey("ApiKey") ? providerSettings["ApiKey"].ToString() : "";
            string modelToUse = !string.IsNullOrEmpty(model) ? model : 
                              (providerSettings.ContainsKey("Model") ? providerSettings["Model"].ToString() : DefaultModelName);
            int maxTokens = providerSettings.ContainsKey("MaxTokens") ? Convert.ToInt32(providerSettings["MaxTokens"]) : 150;

            if (modelToUse == "" || modelToUse == "mistralai")
            {
                modelToUse = DefaultModelName;
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
                        if (choice["message"] != null && choice["message"]["content"] != null)
                        {
                            return new AIResponse
                            {
                                Response = choice["message"]["content"].ToString().Trim(),
                                Provider = ProviderName,
                                Model = modelToUse,
                                InTokens = json["usage"]?["prompt_tokens"]?.Value<int>() ?? 0,
                                OutTokens = json["usage"]?["completion_tokens"]?.Value<int>() ?? 0,
                                FinishReason = choice["finish_reason"]?.ToString().Trim() ?? "unknown",
                            };
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
                    return new AIResponse
                    {
                        Response = $"Error: {ex.Message}",
                        FinishReason = "error"
                    };
                }
            }
        }
    }
}
