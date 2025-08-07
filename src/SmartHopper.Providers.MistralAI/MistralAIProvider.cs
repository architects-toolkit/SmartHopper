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
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.Infrastructure.Utils;

namespace SmartHopper.Providers.MistralAI
{
    public sealed class MistralAIProvider : AIProvider<MistralAIProvider>
    {
        private MistralAIProvider()
        {
            this.Models = new MistralAIProviderModels(this, this.CallApi);
        }

        /// <summary>
        /// Gets the name of the provider.
        /// </summary>
        public override string Name => "MistralAI";

        /// <summary>
        /// Gets the default server URL for the provider.
        /// </summary>
        public override string DefaultServerUrl => "https://api.mistral.ai/v1";

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

        public override async Task<AIReturn<string>> GetResponse(AIRequest request)
        {
            string providerName = request.Provider;
            string model = request.Model;
            List<IAIInteraction> messages = request.Body.Interactions;
            string jsonSchema = request.Body.JsonOutputSchema;
            string endpoint = request.Endpoint;
            string? toolFilter = request.Body.ToolFilter;
            string? contextFilter = request.Body.ContextFilter;
            
            // Get settings from the secure settings store
            int maxTokens = this.GetSetting<int>("MaxTokens");

            Debug.WriteLine($"[MistralAI] GetResponse - Model: {model}, MaxTokens: {maxTokens}");

            // Format messages for Mistral API
            var convertedMessages = new JArray();
            foreach (var msg in messages)
            {
                // Provider-specific: propagate assistant tool_call messages unmodified
                string role = msg["role"]?.ToString().ToLower(System.Globalization.CultureInfo.CurrentCulture) ?? "user";
                string msgContent = msg["content"]?.ToString() ?? string.Empty;
                msgContent = AI.StripThinkTags(msgContent);

                var messageObj = new JObject
                {
                    ["content"] = msgContent,
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
                ["model"] = model,
                ["messages"] = convertedMessages,
                ["max_tokens"] = maxTokens,
                ["temperature"] = this.GetSetting<double>("Temperature"),
            };

            // Add JSON response format if schema is provided
            if (!string.IsNullOrWhiteSpace(jsonSchema))
            {
                // Add response format for structured output
                requestBody["response_format"] = new JObject
                {
                    ["type"] = "json_object",
                };

                // Add schema as a system message to guide the model
                var systemMessage = new JObject
                {
                    ["role"] = "system",
                    ["content"] = "The response must be a valid JSON object that strictly follows this schema: " + jsonSchema,
                };
                convertedMessages.Insert(0, systemMessage);
            }

            // Add tools if requested
            if (!string.IsNullOrWhiteSpace(toolFilter))
            {
                var tools = this.GetFormattedTools(toolFilter);
                if (tools != null && tools.Count > 0)
                {
                    requestBody["tools"] = tools;
                    requestBody["tool_choice"] = "auto";
                }
            }

            Debug.WriteLine($"[MistralAI] Request: {requestBody}");

            try
            {
                // Use the new Call method for HTTP request
                var responseContent = await this.CallApi("/chat/completions", "POST", requestBody.ToString()).ConfigureAwait(false);
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

                var aiReturn = new AIReturn<string>
                {
                    Result = message["content"]?.ToString() ?? string.Empty,
                    Metrics = new AIMetrics
                    {
                        FinishReason = firstChoice?["finish_reason"]?.ToString() ?? string.Empty,
                        InputTokensPrompt = usage?["prompt_tokens"]?.Value<int>() ?? 0,
                        OutputTokensGeneration = usage?["completion_tokens"]?.Value<int>() ?? 0,
                        Provider = this.Name,
                        Model = model,
                    },
                    Status = AIStatus.Finished,
                };

                // Handle tool calls if any
                if (message["tool_calls"] is JArray toolCalls && toolCalls.Count > 0)
                {
                    aiReturn.ToolCalls = new List<AIToolCall>();
                    foreach (JObject toolCall in toolCalls)
                    {
                        var function = toolCall["function"] as JObject;
                        if (function != null)
                        {
                            aiReturn.ToolCalls.Add(new AIToolCall
                            {
                                Id = toolCall["id"]?.ToString(),
                                Name = function["name"]?.ToString(),
                                Arguments = function["arguments"]?.ToString(),
                            });
                        }
                    }
                    aiReturn.Status = AIStatus.CallingTools;
                }

                Debug.WriteLine($"[MistralAI] Response processed successfully: {aiReturn.Result.Substring(0, Math.Min(50, aiReturn.Result.Length))}...");
                return aiReturn;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MistralAI] Exception: {ex.Message}");
                throw new Exception($"Error communicating with MistralAI API: {ex.Message}", ex);
            }
        }
    }
}
