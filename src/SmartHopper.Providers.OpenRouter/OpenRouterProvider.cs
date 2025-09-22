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
    /// OpenRouter provider implementation using the Responses API.
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
        public override string DefaultServerUrl => "https://openrouter.ai/api/v1";

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
                // Default to Responses endpoint for text
                request.HttpMethod = "POST";
                request.Endpoint = "/responses";
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

            // Build messages from interactions. OpenRouter Responses accepts an `input` array of role/content pairs.
            var messages = new JArray();
            foreach (var interaction in request.Body.Interactions)
            {
                try
                {
                    var msg = this.EncodeToJToken(interaction);
                    if (msg != null)
                    {
                        messages.Add(msg);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[{this.Name}] Warning: Could not encode interaction: {ex.Message}");
                }
            }

            // Parameters
            int maxTokens = this.GetSetting<int>("MaxTokens");
            double temperature;
            // Temperature stored as string in settings to align with other providers
            if (!double.TryParse(this.GetSetting<string>("Temperature"), out temperature))
            {
                temperature = 0.5;
            }

            // Provider selection settings
            bool allowFallbacks = this.GetSetting<bool>("AllowFallbacks"); // default true
            string sort = this.GetSetting<string>("Sort") ?? "price";      // default price
            string dataCollection = this.GetSetting<string>("DataCollection") ?? "deny"; // default deny

            var body = new JObject
            {
                ["model"] = request.Model,
                ["input"] = messages,
                ["max_tokens"] = maxTokens,
                ["temperature"] = temperature,
                ["provider"] = new JObject
                {
                    ["allow_fallbacks"] = allowFallbacks,
                    ["sort"] = sort,
                    ["data_collection"] = dataCollection,
                },
            };

            // Note: schema/response_format and tools can be added later once mapped for OpenRouter Responses API

            Debug.WriteLine($"[OpenRouter] Responses Request: {body}");
            return body.ToString();
        }

        /// <inheritdoc/>
        public override string Encode(IAIInteraction interaction)
        {
            var j = this.EncodeToJToken(interaction);
            return j?.ToString();
        }

        private JToken? EncodeToJToken(IAIInteraction interaction)
        {
            // OpenRouter Responses accepts role/content similar to chat style.
            var obj = new JObject();
            switch (interaction.Agent)
            {
                case AIAgent.System:
                case AIAgent.Context:
                    obj["role"] = "system";
                    break;
                case AIAgent.User:
                    obj["role"] = "user";
                    break;
                case AIAgent.Assistant:
                case AIAgent.ToolCall:
                    obj["role"] = "assistant";
                    break;
                case AIAgent.ToolResult:
                    // Map tool results as tool role content appended as text
                    obj["role"] = "tool";
                    break;
                default:
                    throw new ArgumentException($"Agent {interaction.Agent} not supported by {this.Name}");
            }

            if (interaction is AIInteractionText t)
            {
                obj["content"] = t.Content ?? string.Empty;
            }
            else if (interaction is AIInteractionImage)
            {
                // OpenRouter text-only: ignore images but keep placeholder note
                obj["content"] = "[image content omitted: provider supports text only]";
            }
            else if (interaction is AIInteractionToolResult tr)
            {
                obj["content"] = tr.Result?.ToString() ?? string.Empty;
            }
            else if (interaction is AIInteractionToolCall tc)
            {
                // Represent tool call as assistant text for now (tool calling APIs to be added later)
                obj["content"] = $"<tool_call name=\"{tc.Name}\">{tc.Arguments}</tool_call>";
            }
            else
            {
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
                // Try OpenRouter Responses-style outputs first
                string content = string.Empty;

                // Some Responses implementations return an "output" array with text items
                if (response["output"] is JArray outputArr && outputArr.Count > 0)
                {
                    var parts = new List<string>();
                    foreach (var item in outputArr.OfType<JObject>())
                    {
                        // Try common fields
                        var text = item["text"]?.ToString() ?? item["content"]?.ToString();
                        if (!string.IsNullOrEmpty(text)) parts.Add(text);
                    }
                    content = string.Join("", parts);
                }
                else if (response["response"] != null)
                {
                    content = response["response"]?.ToString() ?? string.Empty;
                }
                else if (response["choices"] is JArray choices && choices.Count > 0)
                {
                    // Fallback to OpenAI-like decoding if proxy returns that shape
                    var first = choices[0] as JObject;
                    content = first?["message"]?["content"]?.ToString() ?? string.Empty;
                }
                else if (response["message"]? ["content"] != null)
                {
                    content = response["message"]?["content"]?.ToString() ?? string.Empty;
                }
                else
                {
                    content = response.ToString();
                }

                var result = new AIInteractionText();
                result.SetResult(agent: AIAgent.Assistant, content: content);

                // Attempt to read usage metrics if present (alignment with common shapes)
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
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{this.Name}] Decode error: {ex.Message}");
            }

            return interactions;
        }
    }
}
