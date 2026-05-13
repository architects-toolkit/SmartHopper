/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024-2026 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this library; if not, see <https://www.gnu.org/licenses/lgpl-3.0.html>.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Core;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.JsonSchemas;
using SmartHopper.Infrastructure.AICall.Metrics;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Infrastructure.Diagnostics;
using SmartHopper.Infrastructure.Streaming;

namespace SmartHopper.Providers.Ollama
{
    /// <summary>
    /// Ollama provider implementation.
    /// Targets local Ollama instances (https://ollama.com/) through the
    /// OpenAI-compatible <c>/v1/chat/completions</c> endpoint.
    /// </summary>
    public sealed partial class OllamaProvider : AIProvider<OllamaProvider>
    {
        /// <summary>
        /// The name of the provider. Used by the UI for provider selection.
        /// </summary>
        public static readonly string NameValue = "Ollama";

        /// <summary>
        /// Fallback default server URL when no Base URL has been configured.
        /// Ollama defaults to port 11434 on localhost.
        /// </summary>
        public static readonly Uri FallbackServerUrl = new ("http://localhost:11434/v1");

        /// <summary>
        /// Initializes a new instance of the <see cref="OllamaProvider"/> class.
        /// Private constructor to enforce singleton pattern.
        /// </summary>
        private OllamaProvider()
        {
            this.Models = new OllamaProviderModels(this);

            // Register provider-specific JSON schema adapter (object-root wrapper, OpenAI-style)
            JsonSchemaAdapterRegistry.Register(new OllamaJsonSchemaAdapter());
        }

        /// <inheritdoc/>
        public override string Name => NameValue;

        /// <summary>
        /// Gets the server URL configured for this provider.
        /// Reads the user-provided <c>ServerUrl</c> setting and falls back to <see cref="FallbackServerUrl"/>
        /// when the setting is empty or not a valid absolute HTTP(S) URL.
        /// </summary>
        public override Uri DefaultServerUrl
        {
            get
            {
                var configured = this.GetServerUrlSetting();
                if (!string.IsNullOrWhiteSpace(configured)
                    && Uri.TryCreate(configured, UriKind.Absolute, out var uri)
                    && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                {
                    return uri;
                }

                return FallbackServerUrl;
            }
        }

        /// <inheritdoc/>
        public override bool IsEnabled => true;

        /// <summary>
        /// Helper to retrieve the configured API key for this provider.
        /// Exposed to the nested streaming adapter to avoid protected access issues.
        /// </summary>
        internal string GetApiKey()
        {
            return this.GetSetting<string>("ApiKey");
        }

        /// <summary>
        /// Helper to retrieve the configured Base URL setting (raw string).
        /// </summary>
        internal string GetServerUrlSetting()
        {
            return this.GetSetting<string>("ServerUrl");
        }

        /// <inheritdoc/>
        public override Image Icon
        {
            get
            {
                var iconBytes = Properties.Resources.ollama_icon;
                using (var ms = new MemoryStream(iconBytes))
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

            // Setup HTTP method, content type, and authentication.
            request.HttpMethod = "POST";
            request.ContentType = "application/json";

            // Ollama does not require an API key by default; the OpenAI-compatible
            // endpoint accepts an arbitrary placeholder. Use bearer auth only when
            // the user has explicitly configured one (e.g., a reverse-proxy in front).
            var apiKey = this.GetApiKey();
            request.Authentication = string.IsNullOrWhiteSpace(apiKey) ? "none" : "bearer";

            // Ollama exposes the OpenAI-compatible chat completions endpoint
            request.Endpoint = "/chat/completions";

            return request;
        }

        /// <inheritdoc/>
        public override string Encode(AIRequestCall request)
        {
            if (request.HttpMethod == "GET" || request.HttpMethod == "DELETE")
            {
                return "GET and DELETE requests do not use a request body";
            }

            var p = request.Parameters;

            int maxTokens = p?.MaxTokens ?? this.GetSetting<int>("MaxTokens");
            double temperature = p?.Temperature ?? this.GetSetting<double>("Temperature");
            string? toolFilter = request.Body.ToolFilter;

            Debug.WriteLine($"[Ollama] Encode - Model: {request.Model}, MaxTokens: {maxTokens}");

            // Simple sequential encoding (same approach as OpenAI/MistralAI/DeepSeek providers).
            var convertedMessages = new JArray();

            // Merge System and Summary interactions before encoding
            var mergedInteractions = this.MergeSystemAndSummary(request.Body.Interactions);

            foreach (var interaction in mergedInteractions)
            {
                try
                {
                    var token = this.EncodeToJToken(interaction);
                    if (token == null)
                    {
                        continue;
                    }

                    var role = token["role"]?.ToString();
                    if (string.IsNullOrEmpty(role))
                    {
                        continue;
                    }

                    convertedMessages.Add(token);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Ollama] Warning: Could not encode interaction: {ex.Message}");
                }
            }

            // Build request body
            var requestBody = new JObject
            {
                ["model"] = request.Model,
                ["messages"] = convertedMessages,
                ["max_tokens"] = maxTokens,
                ["temperature"] = temperature,
            };

            // Apply optional parameters from extras only
            if (p?.Extras != null)
            {
                if (p.Extras.TryGetValue("top_p", out var topPToken) && topPToken != null)
                {
                    requestBody["top_p"] = topPToken.Value<double?>();
                }

                if (p.Extras.TryGetValue("top_k", out var topKToken) && topKToken != null)
                {
                    requestBody["top_k"] = topKToken.Value<int?>();
                }

                if (p.Extras.TryGetValue("seed", out var seedToken) && seedToken != null)
                {
                    requestBody["seed"] = seedToken.Value<int?>();
                }

                if (p.Extras.TryGetValue("presence_penalty", out var ppToken) && ppToken != null)
                {
                    requestBody["presence_penalty"] = ppToken;
                }

                if (p.Extras.TryGetValue("frequency_penalty", out var fpToken) && fpToken != null)
                {
                    requestBody["frequency_penalty"] = fpToken;
                }
            }

            // Add JSON response format if schema is provided (centralized wrapping)
            if (!string.IsNullOrWhiteSpace(request.Body.JsonOutputSchema))
            {
                try
                {
                    var svc = JsonSchemaService.Instance;
                    var schemaObj = JObject.Parse(request.Body.JsonOutputSchema);
                    var (wrappedSchema, wrapperInfo) = svc.WrapForProvider(schemaObj, this.Name);
                    svc.SetCurrentWrapperInfo(wrapperInfo);
                    Debug.WriteLine($"[Ollama] Schema wrapper info stored (central): IsWrapped={wrapperInfo.IsWrapped}, Type={wrapperInfo.WrapperType}, Property={wrapperInfo.PropertyName}");

                    // Ollama supports json_object response_format on its OpenAI-compatible endpoint
                    requestBody["response_format"] = new JObject { ["type"] = "json_object" };

                    // Add a system guidance message including the wrapped schema
                    var systemMessage = new JObject
                    {
                        ["role"] = "system",
                        ["content"] = "The response must be a valid JSON object that strictly follows this schema: " + wrappedSchema.ToString(Newtonsoft.Json.Formatting.None),
                    };
                    convertedMessages.Insert(0, systemMessage);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Ollama] Failed to parse/handle JSON schema: {ex.Message}");
                    JsonSchemaService.Instance.SetCurrentWrapperInfo(new SchemaWrapperInfo { IsWrapped = false });
                    requestBody["response_format"] = new JObject { ["type"] = "text" };
                }
            }
            else
            {
                JsonSchemaService.Instance.SetCurrentWrapperInfo(new SchemaWrapperInfo { IsWrapped = false });
                requestBody["response_format"] = new JObject { ["type"] = "text" };
            }

            // Add tools if requested
            if (!string.IsNullOrWhiteSpace(toolFilter))
            {
                var tools = this.GetFormattedTools(toolFilter);
                if (tools != null && tools.Count > 0)
                {
                    requestBody["tools"] = tools;

                    // Forced tool call uses OpenAI-compatible tool_choice with function name
                    if (request.ForceToolCall && !string.IsNullOrWhiteSpace(request.ForceToolName))
                    {
                        requestBody["tool_choice"] = new JObject
                        {
                            ["type"] = "function",
                            ["function"] = new JObject { ["name"] = request.ForceToolName, },
                        };
                        Debug.WriteLine($"[Ollama] Forcing tool call: {request.ForceToolName}");
                    }
                    else
                    {
                        requestBody["tool_choice"] = "auto";
                    }
                }
            }

            return requestBody.ToString();
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
                Debug.WriteLine($"[Ollama] Encode(IAIInteraction) error: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Converts a single interaction into a Ollama/OpenAI-compatible message object.
        /// Returns null for interactions that should not be sent (e.g., UI-only diagnostics).
        /// </summary>
        private JToken? EncodeToJToken(IAIInteraction interaction)
        {
            if (interaction == null)
            {
                return null;
            }

            // UI-only diagnostics must not be sent to providers
            if (interaction is AIInteractionRuntimeMessage)
            {
                return null;
            }

            // Map role
            string role;
            switch (interaction.Agent)
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

            var messageObj = new JObject { ["role"] = role };

            if (interaction is AIInteractionText textInteraction)
            {
                messageObj["content"] = textInteraction.Content ?? string.Empty;
            }
            else if (interaction is AIInteractionToolResult toolResultInteraction)
            {
                messageObj["content"] = toolResultInteraction.Result != null
                    ? JsonConvert.SerializeObject(toolResultInteraction.Result, Formatting.None)
                    : string.Empty;

                if (!string.IsNullOrWhiteSpace(toolResultInteraction.Id))
                {
                    messageObj["tool_call_id"] = toolResultInteraction.Id;
                }

                if (!string.IsNullOrWhiteSpace(toolResultInteraction.Name))
                {
                    messageObj["name"] = toolResultInteraction.Name;
                }
            }
            else if (interaction is AIInteractionToolCall toolCallInteraction)
            {
                var toolCallObj = new JObject
                {
                    ["id"] = toolCallInteraction.Id ?? string.Empty,
                    ["type"] = "function",
                    ["function"] = new JObject
                    {
                        ["name"] = toolCallInteraction.Name ?? string.Empty,
                        ["arguments"] = toolCallInteraction.Arguments?.ToString() ?? "{}",
                    },
                };
                messageObj["tool_calls"] = new JArray { toolCallObj };
                messageObj["content"] = string.Empty;
            }
            else if (interaction is AIInteractionImage imageInteraction)
            {
                // Ollama vision support depends on the loaded model; fall back to the original prompt
                messageObj["content"] = imageInteraction.OriginalPrompt ?? string.Empty;
            }
            else
            {
                messageObj["content"] = string.Empty;
            }

            return messageObj;
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
                // OpenAI-compatible error envelope: {"error": {"message": "...", "type": "..."}}
                if (response["error"] is JObject errorObj)
                {
                    var errMsg = errorObj["message"]?.ToString()
                              ?? errorObj["type"]?.ToString()
                              ?? "Provider returned an error";
                    Debug.WriteLine($"[Ollama] Decode: provider error in response body: {errMsg}");
                    interactions.Add(new AIInteractionRuntimeMessage { Severity = SHRuntimeMessageSeverity.Error, Content = errMsg });
                    return interactions;
                }

                var choices = response["choices"] as JArray;
                var firstChoice = choices?.FirstOrDefault() as JObject;
                var message = firstChoice?["message"] as JObject;
                if (message == null)
                {
                    Debug.WriteLine("[Ollama] Decode: No message found in response");
                    return interactions;
                }

                // Robust content extraction: handle string, array of parts, or object content
                string content;
                var contentToken = message["content"];
                if (contentToken is JArray contentParts && contentParts.Count > 0)
                {
                    var texts = new List<string>();
                    foreach (var part in contentParts)
                    {
                        var text = part?["text"]?.ToString()
                                   ?? part?["content"]?.ToString()
                                   ?? part?.ToString(Newtonsoft.Json.Formatting.None)
                                   ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            texts.Add(text);
                        }
                    }

                    content = string.Join("\n", texts);
                }
                else
                {
                    content = contentToken?.ToString() ?? string.Empty;
                }

                // Apply centralized unwrapping using stored wrapper info
                var wrapperInfo = JsonSchemaService.Instance.GetCurrentWrapperInfo();
                if (wrapperInfo != null && wrapperInfo.IsWrapped)
                {
                    Debug.WriteLine($"[Ollama] Unwrapping response content using wrapper info (central): Type={wrapperInfo.WrapperType}, Property={wrapperInfo.PropertyName}");
                    content = JsonSchemaService.Instance.Unwrap(content, wrapperInfo);
                }

                // Some Ollama builds surface reasoning text via reasoning_content (e.g. when running thinking models)
                var reasoning = message["reasoning_content"]?.ToString();

                var interaction = new AIInteractionText();
                interaction.SetResult(
                    agent: AIAgent.Assistant,
                    content: content,
                    reasoning: string.IsNullOrWhiteSpace(reasoning) ? null : reasoning);
                interaction.Metrics = this.DecodeMetrics(response);
                interactions.Add(interaction);

                // Add an AIInteractionToolCall for each tool call
                if (message["tool_calls"] is JArray tcs && tcs.Count > 0)
                {
                    foreach (JObject tc in tcs.OfType<JObject>())
                    {
                        var function = tc["function"] as JObject;
                        var argumentsStr = function?["arguments"]?.ToString() ?? "{}";
                        JObject? argumentsObj = null;
                        try
                        {
                            if (!string.IsNullOrWhiteSpace(argumentsStr))
                            {
                                argumentsObj = JObject.Parse(argumentsStr);
                            }
                        }
                        catch
                        {
                            // Some local backends emit non-JSON arguments; pass them through as-is
                        }

                        var toolCall = new AIInteractionToolCall
                        {
                            Id = tc["id"]?.ToString(),
                            Name = function?["name"]?.ToString(),
                            Arguments = argumentsObj,
                            Reasoning = string.IsNullOrWhiteSpace(reasoning) ? null : reasoning,
                        };
                        interactions.Add(toolCall);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Ollama] Decode error: {ex.Message}");
            }

            return interactions;
        }

        /// <summary>
        /// Decodes the metrics from the response (token usage + finish reason).
        /// </summary>
        private AIMetrics DecodeMetrics(JObject response)
        {
            var choices = response["choices"] as JArray;
            var firstChoice = choices?.FirstOrDefault() as JObject;
            var usage = response["usage"] as JObject;

            var metrics = new AIMetrics
            {
                Provider = this.Name,
                FinishReason = firstChoice?["finish_reason"]?.ToString() ?? string.Empty,
                InputTokensPrompt = usage?["prompt_tokens"]?.Value<int>() ?? 0,
                OutputTokensGeneration = usage?["completion_tokens"]?.Value<int>() ?? 0,
            };

            return metrics;
        }

        /// <inheritdoc/>
        public override IEnumerable<AIExtraDescriptor> GetExtraDescriptors()
        {
            return new[]
            {
                new AIExtraDescriptor(
                    "top_p",
                    "Top P",
                    "Nucleus sampling parameter (0.0–1.0). Lower values make output more focused; higher values more diverse. Leave empty to use the model default.",
                    typeof(double),
                    null),
                new AIExtraDescriptor(
                    "top_k",
                    "Top K",
                    "Limits the next-token candidate pool to the K most likely tokens. Leave empty to use the model default.",
                    typeof(int),
                    null),
                new AIExtraDescriptor(
                    "seed",
                    "Seed",
                    "Reproducibility seed for deterministic sampling. Use the same seed to get similar outputs. Leave empty for random.",
                    typeof(int),
                    null),
                new AIExtraDescriptor(
                    "presence_penalty",
                    "Presence Penalty",
                    "Penalizes tokens already present in the text (-2.0 to 2.0). Positive values encourage new topics.",
                    typeof(double),
                    null),
                new AIExtraDescriptor(
                    "frequency_penalty",
                    "Frequency Penalty",
                    "Penalizes frequent tokens (-2.0 to 2.0). Positive values reduce repetition.",
                    typeof(double),
                    null),
            };
        }
    }
}
