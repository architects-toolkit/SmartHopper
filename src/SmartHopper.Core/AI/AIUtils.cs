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
using SmartHopper.Core.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SmartHopper.Core.AI
{
    public static class AIUtils
    {
        /// <summary>
        /// Extracts tool name from an assistant message containing a function call
        /// </summary>
        /// <param name="assistantMessage">The full assistant message</param>
        /// <returns>The extracted tool name or null if none found</returns>
        public static string ExtractToolName(string assistantMessage)
        {
            if (string.IsNullOrEmpty(assistantMessage))
                return null;

            var match = Regex.Match(assistantMessage, @"function_call.*?name.*?['""](.+?)['""]", RegexOptions.Singleline);
            return match.Success ? match.Groups[1].Value : null;
        }

        /// <summary>
        /// Extracts tool arguments from an assistant message containing a function call
        /// </summary>
        /// <param name="assistantMessage">The full assistant message</param>
        /// <returns>The extracted arguments as a JSON string or null if none found</returns>
        public static string ExtractToolArgs(string assistantMessage)
        {
            if (string.IsNullOrEmpty(assistantMessage))
                return null;

            var match = Regex.Match(assistantMessage, @"arguments.*?({.+?})", RegexOptions.Singleline);
            return match.Success ? match.Groups[1].Value : null;
        }

        /// <summary>
        /// Tries to parse a JSON string into a JToken
        /// </summary>
        /// <param name="strInput">The JSON string to parse</param>
        /// <param name="jToken">The output JToken if successful</param>
        /// <returns>True if parsing was successful, false otherwise</returns>
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

        //public static Dictionary<string, string> GetRoleReplacement()
        //{
        //    return new Dictionary<string, string>()
        //    {
        //        { OpenAI._name, "assistant" },
        //        { MistralAI._name, "assistant" },
        //        { "User", "user" }
        //    };
        //}

        public static async Task<AIResponse> GetResponse(string providerName, string model, List<KeyValuePair<string, string>> messages, string jsonSchema = "", string endpoint = "", string contextProviderFilter = null, string contextKeyFilter = null)
        {
            return await GetResponse(providerName, model, AIMessageBuilder.CreateMessage(messages), jsonSchema, endpoint, contextProviderFilter, contextKeyFilter);
        }

        public static async Task<AIResponse> GetResponse(string providerName, string model, List<TextChatModel> messages, string jsonSchema = "", string endpoint = "", string contextProviderFilter = null, string contextKeyFilter = null)
        {
            return await GetResponse(providerName, model, AIMessageBuilder.CreateMessage(messages), jsonSchema, endpoint, contextProviderFilter, contextKeyFilter);
        }

        private static async Task<AIResponse> GetResponse(string providerName, string model, JArray messages, string jsonSchema = "", string endpoint = "", string contextProviderFilter = null, string contextKeyFilter = null)
        {
            // Add message context
            try
            {
                // Add context from all registered context providers, applying filters if specified
                var contextData = AIContextManager.GetCurrentContext(contextProviderFilter, contextKeyFilter);
                if (contextData.Count > 0)
                {
                    var contextMessages = contextData
                        .Where(kv => !string.IsNullOrEmpty(kv.Value))
                        .Select(kv => $"- {kv.Key}: {kv.Value}");

                    if (contextMessages.Any())
                    {
                        var contextMessage = "Conversation context:\n\n" + 
                                             string.Join("\n", contextMessages);
                        var contextArray = AIMessageBuilder.CreateMessage(new List<KeyValuePair<string, string>> 
                        { 
                            new KeyValuePair<string, string>("system", contextMessage)
                        });
                        
                        // Insert context at the beginning of messages
                        var newMessages = new JArray();
                        newMessages.Merge(contextArray);
                        newMessages.Merge(messages);
                        messages = newMessages;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding context: {ex.Message}");
            }

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