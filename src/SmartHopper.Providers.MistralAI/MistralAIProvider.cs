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
            this.Models = new MistralAIProviderModels(this, request => this.CallApi<string>(request));
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

        /// <inheritdoc/>
        public override IAIRequest PreCall<T>(IAIRequest request)
        {
            // First do the base PreCall
            request = base.PreCall<T>(request);

            switch (request.Endpoint)
            {
                case "/models":
                    request.HttpMethod = "GET";
                    break;
                default:
                    // Setup proper httpmethod, content type, and authentication
                    request.HttpMethod = "POST";
                    request.Endpoint = "/chat/completions";
                    break;
            }
            request.ContentType = "application/json";
            request.Authentication = "bearer";

            return request;
        }

        /// <inheritdoc/>
        public override string FormatRequestBody(IAIRequest request)
        {
            // If Body type is not string return error
            if (request.Body.Interactions.OfType<AIInteraction<string>>() == null)
            {
                throw new Exception("Error: Body type " + request.Body.GetType().Name + " is not supported for " + this.Name + " provider");
            }

            int maxTokens = this.GetSetting<int>("MaxTokens");
            double temperature = this.GetSetting<double>("Temperature");

            string jsonSchema = request.Body.JsonOutputSchema;
            string? toolFilter = request.Body.ToolFilter;

            Debug.WriteLine($"[MistralAI] FormatRequestBody - Model: {model}, MaxTokens: {maxTokens}");

            // Format messages for Mistral API
            var convertedMessages = new JArray();
            foreach (var interaction in request.Body.Interactions)
            {
                AIAgent role = interaction.Agent;
                string roleName = string.Empty;
                string msgContent = interaction.Body.ToString() ?? string.Empty;
                msgContent = AI.StripThinkTags(msgContent);

                var messageObj = new JObject
                {
                    ["content"] = msgContent,
                };

                // Map role names
                if (role == AIAgent.System || role == AIAgent.Context)
                {
                    roleName = "system";
                }
                else if (role == AIAgent.Assistant)
                {
                    roleName = "assistant";

                    // Pass tool_calls if available from ToolCalls property
                    if (interaction.ToolCalls != null && interaction.ToolCalls.Count > 0)
                    {
                        var toolCallsArray = new JArray();
                        foreach (var toolCall in interaction.ToolCalls)
                        {
                            var toolCallObj = new JObject
                            {
                                ["id"] = toolCall.Id,
                                ["type"] = "function",
                                ["function"] = new JObject
                                {
                                    ["name"] = toolCall.Name,
                                    ["arguments"] = toolCall.Arguments,
                                }
                            };
                            toolCallsArray.Add(toolCallObj);
                        }
                        messageObj["tool_calls"] = toolCallsArray;
                    }
                }
                else if (role == AIAgent.ToolResult)
                {
                    roleName = "tool";

                    // Propagate tool_call ID and name from ToolCalls property
                    if (interaction.ToolCalls != null && interaction.ToolCalls.Count > 0)
                    {
                        var toolCall = interaction.ToolCalls.First();
                        messageObj["name"] = toolCall.Name;
                        messageObj["tool_call_id"] = toolCall.Id;
                    }
                }
                else if (role == AIAgent.ToolCall)
                {
                    // Omit it
                    continue;
                }
                else
                {
                    roleName = "user";
                }

                if(!string.IsNullOrEmpty(roleName))
                {
                    messageObj["role"] = roleName;
                    convertedMessages.Add(messageObj);
                }

                
            }

            // Create request body for Mistral API
            var requestBody = new JObject
            {
                ["model"] = request.Model,
                ["messages"] = convertedMessages,
                ["max_tokens"] = maxTokens,
                ["temperature"] = temperature,
            };

            // Add JSON schema if provided
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
            return requestBody.ToString();
        }

        /// <inheritdoc/>
        public override IAIReturn<T> PostCall<T>(IAIReturn<T> response)
        {
            // First do the base PostCall
            response = base.PostCall<T>(response);

            // If type is not string return error
            if (!(typeof(T) == typeof(string) && response is IAIReturn<string> stringResponse))
            {
                throw new Exception("Error: Type " + typeof(T).Name + " is not supported for " + this.Name + " provider");
            }

            try
            {
                var responseJson = JObject.Parse(stringResponse.RawResult);
                Debug.WriteLine($"[MistralAI] PostCall - Response parsed successfully");

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

                var content = message["content"]?.ToString() ?? string.Empty;

                var aiReturn = new AIReturn<string>
                {
                    Result = content,
                    Metrics = new AIMetrics
                    {
                        FinishReason = firstChoice?["finish_reason"]?.ToString() ?? string.Empty,
                        InputTokensPrompt = usage?["prompt_tokens"]?.Value<int>() ?? 0,
                        OutputTokensGeneration = usage?["completion_tokens"]?.Value<int>() ?? 0,
                    },
                    Status = AICallStatus.Finished,
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
                    aiReturn.Status = AICallStatus.CallingTools;
                }

                Debug.WriteLine($"[MistralAI] PostCall - Response processed successfully: {aiReturn.Result.Substring(0, Math.Min(50, aiReturn.Result.Length))}...");
                return (IAIReturn<T>)aiReturn;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MistralAI] PostCall - Exception: {ex.Message}");
                throw new Exception($"Error processing MistralAI response: {ex.Message}", ex);
            }

            return response;
        }
    }
}
