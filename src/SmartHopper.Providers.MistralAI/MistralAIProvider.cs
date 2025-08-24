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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Metrics;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AIProviders;

namespace SmartHopper.Providers.MistralAI
{
    public sealed class MistralAIProvider : AIProvider<MistralAIProvider>
    {
        private MistralAIProvider()
        {
            this.Models = new MistralAIProviderModels(this);
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
        public override AIRequestCall PreCall(AIRequestCall request)
        {
            // First do the base PreCall
            request = base.PreCall(request);

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
        public override string Encode(IAIInteraction interaction)
        {
            // This method should encode a single interaction to string
            // For MistralAI, we'll serialize the interaction as JSON
            try
            {
                if (interaction is AIInteractionText textInteraction)
                {
                    return JsonConvert.SerializeObject(new
                    {
                        agent = textInteraction.Agent.ToString(),
                        content = textInteraction.Content,
                        reasoning = textInteraction.Reasoning
                    });
                }
                else if (interaction is AIInteractionToolCall toolCallInteraction)
                {
                    return JsonConvert.SerializeObject(new
                    {
                        agent = toolCallInteraction.Agent.ToString(),
                        id = toolCallInteraction.Id,
                        name = toolCallInteraction.Name,
                        arguments = toolCallInteraction.Arguments
                    });
                }
                else if (interaction is AIInteractionToolResult toolResultInteraction)
                {
                    return JsonConvert.SerializeObject(new
                    {
                        agent = toolResultInteraction.Agent.ToString(),
                        result = toolResultInteraction.Result,
                        id = toolResultInteraction.Id,
                        name = toolResultInteraction.Name
                    });
                }

                // Fallback
                return JsonConvert.SerializeObject(new
                {
                    agent = interaction.Agent.ToString(),
                    content = string.Empty,
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MistralAI] Encode error: {ex.Message}");
                return string.Empty;
            }
        }

        /// <inheritdoc/>
        public override string Encode(AIRequestCall request)
        {
            if (request.HttpMethod == "GET" || request.HttpMethod == "DELETE")
            {
                return "GET and DELETE requests do not use a request body";
            }
            // Encode request body for Mistral. Supports string and AIText content in interactions.

            int maxTokens = this.GetSetting<int>("MaxTokens");
            double temperature = this.GetSetting<double>("Temperature");

            string jsonSchema = request.Body.JsonOutputSchema;
            string? toolFilter = request.Body.ToolFilter;

            Debug.WriteLine($"[MistralAI] Encode - Model: {request.Model}, MaxTokens: {maxTokens}");

            // Format messages for Mistral API
            var convertedMessages = new JArray();
            foreach (var interaction in request.Body.Interactions)
            {
                AIAgent role = interaction.Agent;
                string roleName = string.Empty;
                string msgContent;
                // Handle different interaction types by casting to concrete types
                if (interaction is AIInteractionText textInteraction)
                {
                    msgContent = textInteraction.Content ?? string.Empty;
                }
                else if (interaction is AIInteractionToolResult toolResultInteraction)
                {
                    msgContent = toolResultInteraction.Result?.ToString() ?? string.Empty;
                }
                else if (interaction is AIInteractionToolCall toolCallInteraction)
                {
                    msgContent = string.Empty; // Tool calls don't have content
                }
                else if (interaction is AIInteractionImage imageInteraction)
                {
                    msgContent = imageInteraction.OriginalPrompt ?? string.Empty;
                }
                else
                {
                    // Fallback to empty string for unknown types
                    msgContent = string.Empty;
                }

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

                    // Tool calls are handled separately as AIInteractionToolCall objects
                    // Assistant messages don't directly contain tool calls in our architecture
                }
                else if (role == AIAgent.ToolResult)
                {
                    roleName = "tool";

                    // Propagate tool_call ID and name - cast to concrete type
                    if (interaction is AIInteractionToolResult toolResultInteraction)
                    {
                        messageObj["name"] = toolResultInteraction.Name;
                        messageObj["tool_call_id"] = toolResultInteraction.Id;
                    }
                }
                else if (role == AIAgent.ToolCall)
                {
                    roleName = "assistant";
                    
                    // Handle tool call as assistant message with tool_calls
                    if (interaction is AIInteractionToolCall toolCallInteraction)
                    {
                        var toolCallsArray = new JArray();
                        var toolCallObj = new JObject
                        {
                            ["id"] = toolCallInteraction.Id,
                            ["type"] = "function",
                            ["function"] = new JObject
                            {
                                ["name"] = toolCallInteraction.Name,
                                ["arguments"] = toolCallInteraction.Arguments is JToken jToken ? jToken.ToString() : (toolCallInteraction.Arguments?.ToString() ?? string.Empty),
                            },
                        };
                        toolCallsArray.Add(toolCallObj);
                        messageObj["tool_calls"] = toolCallsArray;
                        msgContent = string.Empty; // Tool calls don't have content
                    }
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
        public override List<IAIInteraction> Decode(string response)
        {
            var interactions = new List<IAIInteraction>();

            if (string.IsNullOrWhiteSpace(response))
            {
                return interactions;
            }

            try
            {
                var responseJson = JObject.Parse(response);
                var choices = responseJson["choices"] as JArray;
                var firstChoice = choices?.FirstOrDefault() as JObject;
                var message = firstChoice?["message"] as JObject;

                if (message == null)
                {
                    return interactions;
                }

                // Extract content and reasoning (thinking) parts from Mistral's structured content array
                string content = string.Empty;
                string reasoning = string.Empty;

                var contentToken = message["content"];
                if (contentToken is JArray contentArray)
                {
                    var contentParts = new List<string>();
                    var reasoningParts = new List<string>();

                    foreach (var part in contentArray.OfType<JObject>())
                    {
                        var type = part["type"]?.ToString();
                        if (string.Equals(type, "thinking", StringComparison.OrdinalIgnoreCase))
                        {
                            var thinkingToken = part["thinking"];
                            if (thinkingToken is JArray thinkingArray)
                            {
                                foreach (var t in thinkingArray)
                                {
                                    if (t is JObject to && string.Equals(to["type"]?.ToString(), "text", StringComparison.OrdinalIgnoreCase))
                                    {
                                        var textVal = to["text"]?.ToString();
                                        if (!string.IsNullOrEmpty(textVal)) reasoningParts.Add(textVal);
                                    }
                                    else
                                    {
                                        var textVal = t?.ToString();
                                        if (!string.IsNullOrEmpty(textVal)) reasoningParts.Add(textVal);
                                    }
                                }
                            }
                        }
                        else if (string.Equals(type, "text", StringComparison.OrdinalIgnoreCase))
                        {
                            var textVal = part["text"]?.ToString();
                            if (!string.IsNullOrEmpty(textVal)) contentParts.Add(textVal);
                        }
                    }

                    content = string.Join(string.Empty, contentParts).Trim();
                    reasoning = string.Join("\n\n", reasoningParts).Trim();
                }
                else if (contentToken != null)
                {
                    // Fallback: content as plain string
                    content = contentToken.ToString();
                }

                var interaction = new AIInteractionText();
                interaction.SetResult(
                    agent: AIAgent.Assistant,
                    content: content,
                    reasoning: string.IsNullOrWhiteSpace(reasoning) ? null : reasoning);

                var metrics = this.DecodeMetrics(response);

                interaction.Metrics = metrics;

                interactions.Add(interaction);

                // Add an AIInteractionToolCall for each tool call
                if (message["tool_calls"] is JArray tcs && tcs.Count > 0)
                {
                    foreach (JObject tc in tcs)
                    {
                        var toolCall = new AIInteractionToolCall
                        {
                            Id = tc["id"]?.ToString(),
                            Name = tc["function"]?["name"]?.ToString(),
                            Arguments = tc["function"]?["arguments"] as JObject,
                        };
                        interactions.Add(toolCall);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MistralAI] Decode error: {ex.Message}");
            }

            return interactions;
        }

        /// <inheritdoc/>
        private AIMetrics DecodeMetrics(string response)
        {
            var metrics = new AIMetrics();
            if (string.IsNullOrWhiteSpace(response))
            {
                return metrics;
            }

            try
            {
                var responseJson = JObject.Parse(response);
                var choices = responseJson["choices"] as JArray;
                var firstChoice = choices?.FirstOrDefault() as JObject;
                var usage = responseJson["usage"] as JObject;

                metrics.FinishReason = firstChoice?["finish_reason"]?.ToString() ?? metrics.FinishReason;
                metrics.InputTokensPrompt = usage?["prompt_tokens"]?.Value<int>() ?? metrics.InputTokensPrompt;
                metrics.OutputTokensGeneration = usage?["completion_tokens"]?.Value<int>() ?? metrics.OutputTokensGeneration;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MistralAI] DecodeMetrics error: {ex.Message}");
            }

            return metrics;
        }
    }
}

