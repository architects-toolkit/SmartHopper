/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.Managers.AIContext;
using SmartHopper.Infrastructure.Managers.AIProviders;
using SmartHopper.Infrastructure.Models;

namespace SmartHopper.Core.Messaging
{
    public static class AIUtils
    {
        /// <summary>
        /// Tries to parse a JSON string into a JToken.
        /// </summary>
        /// <param name="strInput">The JSON string to parse.</param>
        /// <param name="jToken">The output JToken if successful.</param>
        /// <returns>True if parsing was successful, false otherwise.</returns>
        public static bool TryParseJson(string strInput, out JToken? jToken)
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

        public static async Task<AIResponse> GetResponse(
            string providerName,
            string model,
            List<KeyValuePair<string, string>> messages,
            string jsonSchema = "",
            string endpoint = "",
            string? toolFilter = null,
            string? contextProviderFilter = null,
            string? contextKeyFilter = null)
        {
            return await GetResponse(providerName, model, AIMessageBuilder.CreateMessage(messages), jsonSchema, endpoint, toolFilter, contextProviderFilter, contextKeyFilter).ConfigureAwait(false);
        }

        public static async Task<AIResponse> GetResponse(
            string providerName,
            string model,
            List<ChatMessageModel> messages,
            string jsonSchema = "",
            string endpoint = "",
            string? toolFilter = null,
            string? contextProviderFilter = null,
            string? contextKeyFilter = null)
        {
            return await GetResponse(providerName, model, AIMessageBuilder.CreateMessage(messages), jsonSchema, endpoint, toolFilter, contextProviderFilter, contextKeyFilter).ConfigureAwait(false);
        }

        private static async Task<AIResponse> GetResponse(
            string providerName,
            string model,
            JArray messages,
            string jsonSchema = "",
            string endpoint = "",
            string? toolFilter = null,
            string? contextProviderFilter = null,
            string? contextKeyFilter = null)
        {
            // Add message context
            try
            {
                // Add context from all registered context providers, applying filters if specified
                Debug.WriteLine($"[AIUtils] Adding context from providers: {contextProviderFilter ?? "null"}, keys: {contextKeyFilter ?? "null"}");
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
                            new ("system", contextMessage),
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

                var providers = ProviderManager.Instance.GetProviders().ToList();
                var selectedProvider = providers.FirstOrDefault(p => p.Name.Equals(providerName, StringComparison.OrdinalIgnoreCase));

                if (selectedProvider == null)
                {
                    stopwatch.Stop();
                    return new AIResponse
                    {
                        Response = $"Error: Unknown provider '{providerName}'. Available providers: {string.Join(", ", providers.Select(p => p.Name))}",
                        FinishReason = "error",
                        CompletionTime = stopwatch.Elapsed.TotalSeconds,
                    };
                }

                Debug.WriteLine($"[AIUtils] Loading getResponse from {selectedProvider.Name} with tools filtered by {toolFilter ?? "null"}");

                var response = await selectedProvider.GetResponse(messages, model, jsonSchema, endpoint, toolFilter).ConfigureAwait(false);
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
                    CompletionTime = 0,
                };
            }
            catch (Exception ex)
            {
                return new AIResponse
                {
                    Response = $"Error: {ex.Message}",
                    FinishReason = "error",
                    CompletionTime = 0,
                };
            }
        }
    }
}
