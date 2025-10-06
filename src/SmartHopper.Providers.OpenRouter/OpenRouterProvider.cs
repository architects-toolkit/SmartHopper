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
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AIProviders;

namespace SmartHopper.Providers.OpenRouter
{
    /// <summary>
    /// OpenRouter provider implementation using the Chat Completions API (OpenAI-compatible).
    /// Provides access to multiple AI models through a unified interface.
    /// Text-only; image generation is not supported.
    /// </summary>
    public sealed class OpenRouterProvider : AIProvider<OpenRouterProvider>
    {
        private OpenRouterProvider()
        {
            // Initialize provider-specific models registry
            this.Models = new OpenRouterProviderModels(this);
        }

        /// <inheritdoc/>
        public override string Name => "OpenRouter";

        /// <inheritdoc/>
        public override Uri DefaultServerUrl => new Uri("https://openrouter.ai/api/v1");

        /// <inheritdoc/>
        public override bool IsEnabled => true;

        /// <summary>
        /// Helper to retrieve the configured API key for this provider.
        /// </summary>
        internal string GetApiKey()
        {
            return this.GetSetting<string>("ApiKey");
        }

        /// <inheritdoc/>
        public override Image Icon
        {
            get
            {
                try
                {
                    var bytes = Properties.Resources.openrouter_icon;
                    if (bytes != null && bytes.Length > 0)
                    {
                        using (var ms = new MemoryStream(bytes))
                        using (var img = Image.FromStream(ms))
                        {
                            return new Bitmap(img);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[OpenRouter] Icon load error: {ex.Message}");
                }

                return new Bitmap(1, 1);
            }
        }

        /// <inheritdoc/>
        public override AIRequestCall PreCall(AIRequestCall request)
        {
            // Base pipeline first
            request = base.PreCall(request);

            // Common settings
            request.ContentType = "application/json";
            request.Authentication = "bearer";

            // Required headers for OpenRouter usage attribution
            request.Headers["X-Title"] = "SmartHopper";
            request.Headers["Referer"] = "https://smarthopper.xyz";

            // Support models listing via GET
            if (request.Endpoint == "/models")
            {
                request.HttpMethod = "GET";
                request.RequestKind = AIRequestKind.Backoffice;
            }
            else
            {
                // Default to Chat Completions endpoint
                request.HttpMethod = "POST";
                request.Endpoint = "/chat/completions";
            }

            return request;
        }

        /// <inheritdoc/>
        public override string Encode(AIRequestCall request)
        {
            if (request.HttpMethod == "GET" || request.HttpMethod == "DELETE")
            {
                return "GET and DELETE requests do not use a request body";
            }

            // Parameters
            int maxTokens = this.GetSetting<int>("MaxTokens");
            double temperature;

            // Temperature stored as string in settings to align with other providers
            if (!double.TryParse(this.GetSetting<string>("Temperature"), out temperature))
            {
                temperature = 0.5;
            }

            Debug.WriteLine($"[OpenRouter] Encode - Model: {request.Model}, MaxTokens: {maxTokens}");

#if DEBUG
            // Log interaction sequence for debugging
            try
            {
                int cnt = request.Body.Interactions?.Count ?? 0;
                int tc = request.Body.Interactions?.Count(i => i is AIInteractionToolCall) ?? 0;
                int tr = request.Body.Interactions?.Count(i => i is AIInteractionToolResult) ?? 0;
                int tx = request.Body.Interactions?.Count(i => i is AIInteractionText) ?? 0;
                Debug.WriteLine($"[OpenRouter] BuildMessages: interactions={cnt} (toolCalls={tc}, toolResults={tr}, text={tx})");
            }
            catch { }
#endif

            // Build messages from interactions
            var messages = new JArray();
            foreach (var interaction in request.Body.Interactions)
            {
                try
                {
                    var token = this.EncodeToJToken(interaction);
                    if (token != null)
                    {
                        messages.Add(token);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[OpenRouter] Warning: Could not encode interaction: {ex.Message}");
                }
            }

            // Provider selection settings
            bool allowFallbacks = this.GetSetting<bool>("AllowFallbacks"); // default true
            string sort = this.GetSetting<string>("Sort") ?? "price";      // default price
            string dataCollection = this.GetSetting<string>("DataCollection") ?? "deny"; // default deny

            var body = new JObject
            {
                ["model"] = request.Model,
                ["messages"] = messages,
                ["max_tokens"] = maxTokens,
                ["temperature"] = temperature,
                ["provider"] = new JObject
                {
                    ["allow_fallbacks"] = allowFallbacks,
                    ["sort"] = sort,
                    ["data_collection"] = dataCollection,
                },
            };

            // Add tools if requested
            if (!string.IsNullOrWhiteSpace(request.Body.ToolFilter))
            {
                var tools = this.GetFormattedTools(request.Body.ToolFilter);
                if (tools != null && tools.Count > 0)
                {
                    body["tools"] = tools;
                    body["tool_choice"] = "auto";
                }
            }

            // Note: schema/response_format can be added later once mapped for OpenRouter

#if DEBUG
            try
            {
                Debug.WriteLine($"[OpenRouter] Request body:");
                Debug.WriteLine(body.ToString(Newtonsoft.Json.Formatting.Indented));
            }
            catch { }
#else
            Debug.WriteLine($"[OpenRouter] Request: {body}");
#endif

            return body.ToString();
        }

        /// <inheritdoc/>
        public override string Encode(IAIInteraction interaction)
        {
            try
            {
                var token = this.EncodeToJToken(interaction);
                return token?.ToString() ?? string.Empty;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OpenRouter] Encode(IAIInteraction) error: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Converts a single interaction to an OpenRouter message object (JToken).
        /// Returns null for interactions that should not be sent (e.g., UI-only errors).
        /// </summary>
        private JToken? EncodeToJToken(IAIInteraction interaction)
        {
            if (interaction == null)
            {
                return null;
            }

            // UI-only diagnostics must not be sent to providers
            if (interaction is AIInteractionError)
            {
                return null;
            }

            // Map role
            var agent = interaction.Agent;
            string role;

            switch (agent)
            {
                case AIAgent.System:
                case AIAgent.Context:
                    role = "system";
                    break;
                case AIAgent.User:
                    role = "user";
                    break;
                case AIAgent.Assistant:
                case AIAgent.ToolCall:
                    role = "assistant";
                    break;
                case AIAgent.ToolResult:
                    role = "tool";
                    break;
                default:
                    return null;
            }

            var obj = new JObject { ["role"] = role };

            // Handle different interaction types
            if (interaction is AIInteractionText textInteraction)
            {
                obj["content"] = textInteraction.Content ?? string.Empty;
            }
            else if (interaction is AIInteractionToolResult toolResultInteraction)
            {
                // Format for tool results
                obj["tool_call_id"] = toolResultInteraction.Id;
                if (!string.IsNullOrWhiteSpace(toolResultInteraction.Name))
                {
                    obj["name"] = toolResultInteraction.Name;
                }

                obj["content"] = toolResultInteraction.Result?.ToString() ?? string.Empty;
            }
            else if (interaction is AIInteractionToolCall toolCallInteraction)
            {
                // Format for tool calls
                var toolCallObj = new JObject
                {
                    ["id"] = toolCallInteraction.Id,
                    ["type"] = "function",
                    ["function"] = new JObject
                    {
                        ["name"] = toolCallInteraction.Name,
                        ["arguments"] = toolCallInteraction.Arguments is JToken jt
                            ? jt.ToString()
                            : (toolCallInteraction.Arguments?.ToString() ?? string.Empty),
                    },
                };
                obj["tool_calls"] = new JArray { toolCallObj };
                obj["content"] = string.Empty; // assistant tool_calls messages should have empty content
            }
            else if (interaction is AIInteractionImage)
            {
                // OpenRouter text-only: ignore images but keep placeholder note
                obj["content"] = "[image content omitted: provider supports text only]";
            }
            else
            {
                // Unknown interaction type
                obj["content"] = string.Empty;
            }

            return obj;
        }

        /// <inheritdoc/>
        public override List<IAIInteraction> Decode(JObject response)
        {
            var interactions = new List<IAIInteraction>();
            if (response == null)
            {
                return interactions;
            }

            try
            {
                // OpenRouter response format
                var choices = response["choices"] as JArray;
                var firstChoice = choices?.FirstOrDefault() as JObject;
                var message = firstChoice?["message"] as JObject;

                if (message == null)
                {
                    return interactions;
                }

                // Extract text content
                string content = message["content"]?.ToString() ?? string.Empty;

                var result = new AIInteractionText();
                result.SetResult(agent: AIAgent.Assistant, content: content);

                // Extract usage metrics if present
                var usage = response["usage"] as JObject;
                if (usage != null)
                {
                    result.Metrics = new Infrastructure.AICall.Metrics.AIMetrics
                    {
                        Provider = this.Name,
                        InputTokensPrompt = usage["prompt_tokens"]?.Value<int>() ?? 0,
                        OutputTokensGeneration = usage["completion_tokens"]?.Value<int>() ?? 0,
                    };
                }
                else
                {
                    result.Metrics = new Infrastructure.AICall.Metrics.AIMetrics { Provider = this.Name };
                }

                interactions.Add(result);

                // Extract tool calls if present
                if (message["tool_calls"] is JArray toolCalls && toolCalls.Count > 0)
                {
                    foreach (JObject tc in toolCalls)
                    {
                        var func = tc["function"] as JObject;
                        var argsToken = func?["arguments"];
                        JObject? argsObj = null;

                        if (argsToken is JObject ao)
                        {
                            argsObj = ao;
                        }
                        else if (argsToken != null)
                        {
                            var argsString = argsToken.Type == JTokenType.String
                                ? argsToken.ToString()
                                : argsToken.ToString(Newtonsoft.Json.Formatting.None);

                            if (string.IsNullOrWhiteSpace(argsString))
                            {
                                argsObj = new JObject();
                            }
                            else
                            {
                                try
                                {
                                    argsObj = JObject.Parse(argsString);
                                }
                                catch
                                {
                                    Debug.WriteLine($"[OpenRouter] Failed to parse tool call arguments: {argsString}");
                                }
                            }
                        }

                        var toolCall = new AIInteractionToolCall
                        {
                            Id = tc["id"]?.ToString(),
                            Name = func?["name"]?.ToString(),
                            Arguments = argsObj,
                        };
                        interactions.Add(toolCall);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{this.Name}] Decode error: {ex.Message}");
            }

            return interactions;
        }
    }
}
