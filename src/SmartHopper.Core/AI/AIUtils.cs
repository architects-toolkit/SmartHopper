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
using SmartHopper.Config.Models;
using SmartHopper.Config.Providers;
using SmartHopper.Core.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SmartHopper.Core.Utils
{
    public static class AIUtils
    {
        public static string ExtractToolName(string assistantMessage)
        {
            if (string.IsNullOrEmpty(assistantMessage))
                return null;

            var match = Regex.Match(assistantMessage, @"function_call.*?name.*?['""](.+?)['""]", RegexOptions.Singleline);
            return match.Success ? match.Groups[1].Value : null;
        }

        public static string ExtractToolArgs(string assistantMessage)
        {
            if (string.IsNullOrEmpty(assistantMessage))
                return null;

            var match = Regex.Match(assistantMessage, @"arguments.*?({.+?})", RegexOptions.Singleline);
            return match.Success ? match.Groups[1].Value : null;
        }

        public static bool TryParseJson(string strInput, out JToken jToken)
        {
            try
            {
                jToken = JToken.Parse(strInput);
                return true;
            }
            catch (Exception)
            {
                jToken = null;
                return false;
            }
        }

        public static Dictionary<string, string> GetRoleReplacement()
        {
            return new Dictionary<string, string>()
            {
                { OpenAI.ProviderName, "assistant" },
                { MistralAI.ProviderName, "assistant" },
                { "User", "user" }
            };
        }

        public static async Task<AIResponse> GetResponse(string providerName, string model, List<KeyValuePair<string, string>> messages, string jsonSchema = "", string endpoint = "")
        {
            return await GetResponse(providerName, model, AIMessageBuilder.CreateMessage(messages), jsonSchema, endpoint);
        }

        public static async Task<AIResponse> GetResponse(string providerName, string model, List<TextChatModel> messages, string jsonSchema = "", string endpoint = "")
        {
            return await GetResponse(providerName, model, AIMessageBuilder.CreateMessage(messages), jsonSchema, endpoint);
        }

        private static async Task<AIResponse> GetResponse(string providerName, string model, JArray messages, string jsonSchema = "", string endpoint = "")
        {
            try
            {
                var stopwatch = new System.Diagnostics.Stopwatch();
                stopwatch.Start();

                var settings = SmartHopperSettings.Load();
                if (!settings.ProviderSettings.ContainsKey(providerName))
                {
                    stopwatch.Stop();
                    return new AIResponse
                    {
                        Response = $"Error: Provider '{providerName}' not found in settings",
                        FinishReason = "error",
                        CompletionTime = stopwatch.Elapsed.TotalSeconds
                    };
                }

                var provider = settings.ProviderSettings[providerName];
                if (!provider.ContainsKey("MaxTokens"))
                {
                    provider["MaxTokens"] = 150; // Default value if not set
                }

                var providers = SmartHopperSettings.DiscoverProviders().ToList();
                var selectedProvider = providers.FirstOrDefault(p => p.Name.Equals(providerName, StringComparison.OrdinalIgnoreCase));

                if (selectedProvider == null)
                {
                    stopwatch.Stop();
                    return new AIResponse
                    {
                        Response = $"Error: Unknown provider '{providerName}'. Available providers: {string.Join(", ", providers.Select(p => p.Name))}",
                        FinishReason = "error",
                        CompletionTime = stopwatch.Elapsed.TotalSeconds
                    };
                }

                var response = await selectedProvider.GetResponse(messages, model, jsonSchema, endpoint);
                stopwatch.Stop();
                response.CompletionTime = stopwatch.Elapsed.TotalSeconds;
                response.Provider = providerName;
                response.Model = model;
                return response;
            }
            catch (HttpRequestException ex)
            {
                return new AIResponse
                {
                    Response = $"Error: API request failed - {ex.Message}",
                    FinishReason = "error",
                    CompletionTime = 0
                };
            }
            catch (Exception ex)
            {
                return new AIResponse
                {
                    Response = $"Error: {ex.Message}",
                    FinishReason = "error",
                    CompletionTime = 0
                };
            }
        }
    }
}