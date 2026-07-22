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
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.ProviderSdk.AICall.Core;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Requests;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.AICall.JsonSchemas;
using SmartHopper.ProviderSdk.AICall.Metrics;
using SmartHopper.ProviderSdk.AIProviders;
using SmartHopper.ProviderSdk.Diagnostics;
using SmartHopper.ProviderSdk.Streaming;

namespace SmartHopper.Providers.OpenRouter
{
    /// <summary>
    /// OpenRouter provider implementation using the Chat Completions API (OpenAI-compatible).
    /// Provides access to multiple AI models through a unified interface.
    /// Supports text and vision input; image generation is handled via the img_generate tool.
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
        /// Exposed to nested streaming adapter to avoid protected access issues.
        /// </summary>
        internal string GetApiKey()
        {
            return this.GetSetting<string>("ApiKey");
        }

        /// <summary>
        /// <inheritdoc/>
        protected override IStreamingAdapter CreateStreamingAdapter()
        {
            return new OpenRouterStreamingAdapter(this);
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
            var p = request.Parameters;

            int maxTokens = p?.MaxTokens ?? this.GetSetting<int>("MaxTokens");
            double temperature;
            if (p?.Temperature.HasValue == true)
            {
                temperature = p.Temperature.Value;
            }
            else if (!double.TryParse(this.GetSetting<string>("Temperature"), out temperature))
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
            catch
            {
                // Intentionally empty
            }
#endif

            // Build messages from interactions
            var messages = new JArray();

            // Merge System and Summary interactions before encoding
            var mergedInteractions = this.MergeSystemAndSummary(request.Body.Interactions);

            foreach (var interaction in mergedInteractions)
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

            // Provider selection settings: extras take precedence over global settings
            bool allowFallbacks = (p?.Extras != null && p.Extras.TryGetValue("allow_fallback", out var afToken))
                ? afToken?.Value<bool>() ?? this.GetSetting<bool>("AllowFallbacks")
                : this.GetSetting<bool>("AllowFallbacks");
            string sort = (p?.Extras != null && p.Extras.TryGetValue("sort", out var sortToken))
                ? sortToken?.ToString() ?? this.GetSetting<string>("Sort") ?? "price"
                : this.GetSetting<string>("Sort") ?? "price";
            string dataCollection = (p?.Extras != null && p.Extras.TryGetValue("data_collection", out var dcToken))
                ? dcToken?.ToString() ?? this.GetSetting<string>("DataCollection") ?? "deny"
                : this.GetSetting<string>("DataCollection") ?? "deny";

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

            // Apply seed, top_p, and other optional parameters from extras only
            if (p?.Extras != null)
            {
                if (p.Extras.TryGetValue("seed", out var seedToken) && seedToken != null)
                    body["seed"] = seedToken.Value<int?>();
                if (p.Extras.TryGetValue("top_p", out var topPToken) && topPToken != null)
                    body["top_p"] = topPToken.Value<double?>();
                if (p.Extras.TryGetValue("top_k", out var topKToken) && topKToken != null)
                    body["top_k"] = topKToken.Value<int?>();
                if (p.Extras.TryGetValue("frequency_penalty", out var freqPenaltyToken) && freqPenaltyToken != null)
                    body["frequency_penalty"] = freqPenaltyToken.Value<double?>();
                if (p.Extras.TryGetValue("presence_penalty", out var presPenaltyToken) && presPenaltyToken != null)
                    body["presence_penalty"] = presPenaltyToken.Value<double?>();
                if (p.Extras.TryGetValue("repetition_penalty", out var repPenaltyToken) && repPenaltyToken != null)
                    body["repetition_penalty"] = repPenaltyToken.Value<double?>();
                if (p.Extras.TryGetValue("min_p", out var minPToken) && minPToken != null)
                    body["min_p"] = minPToken.Value<double?>();
                if (p.Extras.TryGetValue("top_a", out var topAToken) && topAToken != null)
                    body["top_a"] = topAToken.Value<double?>();
                if (p.Extras.TryGetValue("logprobs", out var logprobsToken) && logprobsToken != null)
                    body["logprobs"] = logprobsToken.Value<bool?>();
                if (p.Extras.TryGetValue("top_logprobs", out var topLogprobsToken) && topLogprobsToken != null)
                    body["top_logprobs"] = topLogprobsToken.Value<int?>();
                if (p.Extras.TryGetValue("enable_caching", out var enableCachingToken) && enableCachingToken?.Value<bool>() == true)
                {
                    // Top-level cache_control is Anthropic-specific (automatic caching).
                    // Sending it for other models is undocumented and restricts OpenRouter routing,
                    // so only emit it for Anthropic models. Other providers (OpenAI, DeepSeek,
                    // Grok, Groq, Gemini 2.5) cache automatically without any request changes.
                    if (request.Model?.StartsWith("anthropic/", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        body["cache_control"] = new JObject { ["type"] = "ephemeral" };
                    }

                    // Stable session_id enables OpenRouter provider sticky routing from the first
                    // request, keeping subsequent requests on the same provider endpoint so prompt
                    // caches stay warm. Derived from model + first system text so all requests
                    // sharing the same stable prefix land on the same endpoint.
                    var sessionId = ComputeSessionId(request.Model, mergedInteractions);
                    if (!string.IsNullOrEmpty(sessionId))
                    {
                        body["session_id"] = sessionId;
                    }
                }
            }

            // Add tools if requested
            if (!string.IsNullOrWhiteSpace(request.Body.ToolFilter))
            {
                var tools = this.GetFormattedTools(request.Body.ToolFilter);
                if (tools != null && tools.Count > 0)
                {
                    body["tools"] = tools;

                    // Handle forced tool call: OpenRouter uses tool_choice with type and function name (OpenAI-compatible)
                    if (request.ForceToolCall && !string.IsNullOrWhiteSpace(request.ForceToolName))
                    {
                        body["tool_choice"] = new JObject
                        {
                            ["type"] = "function",
                            ["function"] = new JObject { ["name"] = request.ForceToolName, },
                        };
                        Debug.WriteLine($"[OpenRouter] Forcing tool call: {request.ForceToolName}");
                    }
                    else
                    {
                        body["tool_choice"] = "auto";
                    }
                }
            }

            // Attach structured output schema when JSON output is requested
            var jsonSchema = request.Body?.JsonOutputSchema;
            if (!string.IsNullOrWhiteSpace(jsonSchema))
            {
                try
                {
                    var schemaObj = JObject.Parse(jsonSchema);
                    var svc = JsonSchemaService.Instance;
                    var (wrappedSchema, wrapperInfo) = svc.WrapForProvider(schemaObj, this.Name);

                    // Store wrapper info so response validators can unwrap consistently
                    svc.SetCurrentWrapperInfo(wrapperInfo);

                    body["response_format"] = new JObject
                    {
                        ["type"] = "json_schema",
                        ["json_schema"] = new JObject
                        {
                            ["name"] = "response_schema",
                            ["strict"] = true,
                            ["schema"] = wrappedSchema,
                        },
                    };

                    // Hint to OpenRouter that structured outputs are required
                    body["structured_outputs"] = true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[OpenRouter] Failed to attach JSON schema: {ex.Message}");

                    // Fall back to unstructured output; clear wrapper info to avoid inconsistent unwrapping
                    JsonSchemaService.Instance.SetCurrentWrapperInfo(new SchemaWrapperInfo
                    {
                        IsWrapped = false,
                        ProviderName = this.Name,
                    });
                }
            }
            else
            {
                // Ensure wrapper info is reset for non-JSON-output requests
                JsonSchemaService.Instance.SetCurrentWrapperInfo(new SchemaWrapperInfo
                {
                    IsWrapped = false,
                    ProviderName = this.Name,
                });
            }

#if DEBUG
            try
            {
                Debug.WriteLine($"[OpenRouter] Request body:");
                Debug.WriteLine(body.ToString(Newtonsoft.Json.Formatting.Indented));
            }
            catch
            {
                // Intentionally empty
            }
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
        /// Computes a stable session identifier for OpenRouter provider sticky routing.
        /// Derived from the model name and the first system/context text so that all
        /// requests sharing the same stable prompt prefix are routed to the same
        /// provider endpoint, keeping prompt caches warm across requests.
        /// </summary>
        /// <param name="model">The requested model name.</param>
        /// <param name="interactions">The merged interactions of the request body.</param>
        /// <returns>A stable hash-based session id, or an empty string when no seed content is available.</returns>
        private static string ComputeSessionId(string? model, IEnumerable<IAIInteraction> interactions)
        {
            try
            {
                var firstSystem = interactions?
                    .OfType<AIInteractionText>()
                    .FirstOrDefault(i => i.Agent == AIAgent.System || i.Agent == AIAgent.Context);
                var seed = (model ?? string.Empty) + "\n" + (firstSystem?.Content ?? string.Empty);
                if (string.IsNullOrWhiteSpace(seed))
                {
                    return string.Empty;
                }

                using var sha = SHA256.Create();
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(seed));
                return "sh-" + Convert.ToHexString(hash).ToLowerInvariant();
            }
            catch
            {
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
            if (interaction is AIInteractionRuntimeMessage)
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
                // Format for tool results (OpenRouter docs only document role, tool_call_id and content).
                obj["tool_call_id"] = toolResultInteraction.Id;
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

                // For reasoning-enabled models, emit reasoning in the official OpenRouter field
                if (!string.IsNullOrWhiteSpace(toolCallInteraction.Reasoning))
                {
                    obj["reasoning"] = toolCallInteraction.Reasoning;
                }

                obj["content"] = string.Empty;
            }
            else if (interaction is AIInteractionImage imageInteraction)
            {
                // OpenRouter supports vision via OpenAI-compatible image_url format
                string imageUrlValue = null;

                if (imageInteraction.ImageUrl != null)
                {
                    imageUrlValue = imageInteraction.ImageUrl.ToString();
                }
                else if (!string.IsNullOrWhiteSpace(imageInteraction.ImageData))
                {
                    var mimeType = imageInteraction.MimeType ?? "image/png";
                    imageUrlValue = $"data:{mimeType};base64,{imageInteraction.ImageData}";
                }

                if (imageUrlValue != null)
                {
                    var contentArray = new JArray
                    {
                        new JObject
                        {
                            ["type"] = "image_url",
                            ["image_url"] = new JObject
                            {
                                ["url"] = imageUrlValue,
                            },
                        },
                    };
                    obj["content"] = contentArray;
                }
                else
                {
                    obj["content"] = imageInteraction.OriginalPrompt ?? string.Empty;
                }
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
                // Handle provider error responses (e.g. batch items with status_code 4xx/5xx).
                // OpenRouter is OpenAI-compatible: {"error": {"message": "...", "code": "..."}}
                if (response["error"] is JObject errorObj)
                {
                    var errMsg = errorObj["message"]?.ToString()
                              ?? errorObj["code"]?.ToString()
                              ?? "Provider returned an error";
                    Debug.WriteLine($"[OpenRouter] Decode: provider error in response body: {errMsg}");
                    interactions.Add(new AIInteractionRuntimeMessage { Severity = SHRuntimeMessageSeverity.Error, Content = errMsg });
                    return interactions;
                }

                // OpenRouter response format
                var choices = response["choices"] as JArray;
                var firstChoice = choices?.FirstOrDefault() as JObject;
                var message = firstChoice?["message"] as JObject;

                if (message == null)
                {
                    return interactions;
                }

                // Extract text content
                // Extract content and reasoning (for o-series models via OpenRouter)
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
                        if (string.Equals(type, "reasoning", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(type, "thinking", StringComparison.OrdinalIgnoreCase))
                        {
                            var textVal = part["text"]?.ToString() ?? part["content"]?.ToString();
                            if (!string.IsNullOrEmpty(textVal)) reasoningParts.Add(textVal);
                        }
                        else if (string.Equals(type, "text", StringComparison.OrdinalIgnoreCase))
                        {
                            var textVal = part["text"]?.ToString() ?? part["content"]?.ToString();
                            if (!string.IsNullOrEmpty(textVal)) contentParts.Add(textVal);
                        }
                    }

                    content = string.Join(string.Empty, contentParts).Trim();
                    reasoning = string.Join("\n\n", reasoningParts).Trim();
                }
                else if (contentToken != null)
                {
                    content = contentToken.ToString() ?? string.Empty;
                }

                // Extract reasoning from official OpenRouter fields (preferred over legacy content-array)
                var reasoningToken = message["reasoning"];
                if (reasoningToken != null && !string.IsNullOrEmpty(reasoningToken.ToString()))
                {
                    reasoning = reasoningToken.ToString();
                }

                var reasoningDetails = message["reasoning_details"] as JArray;
                if (reasoningDetails != null && reasoningDetails.Count > 0)
                {
                    var reasoningParts = new List<string>();
                    foreach (var detail in reasoningDetails.OfType<JObject>())
                    {
                        var detailType = detail["type"]?.ToString();
                        if (string.Equals(detailType, "reasoning.text", StringComparison.OrdinalIgnoreCase))
                        {
                            var text = detail["text"]?.ToString();
                            if (!string.IsNullOrEmpty(text)) reasoningParts.Add(text);
                        }
                        else if (string.Equals(detailType, "reasoning.summary", StringComparison.OrdinalIgnoreCase))
                        {
                            var summary = detail["summary"]?.ToString();
                            if (!string.IsNullOrEmpty(summary)) reasoningParts.Add(summary);
                        }
                    }

                    if (reasoningParts.Count > 0)
                    {
                        reasoning = string.Join("\n\n", reasoningParts);
                    }
                }

                var result = new AIInteractionText();
                result.SetResult(
                    agent: AIAgent.Assistant,
                    content: content,
                    reasoning: string.IsNullOrWhiteSpace(reasoning) ? null : reasoning);

                // Extract metrics (tokens, model, finish reason) if present
                var metrics = new ProviderSdk.AICall.Metrics.AIMetrics
                {
                    Provider = this.Name,
                    Model = response["model"]?.ToString(),
                };

                var usage = response["usage"] as JObject;
                if (usage != null)
                {
                    var totalPromptTokens = usage["prompt_tokens"]?.Value<int>() ?? 0;

                    // Extract cached tokens from nested prompt_tokens_details object
                    var promptDetails = usage["prompt_tokens_details"] as JObject;
                    metrics.InputTokensCached = promptDetails?["cached_tokens"]?.Value<int>() ?? 0;
                    metrics.InputTokensPrompt = totalPromptTokens - metrics.InputTokensCached;

                    metrics.OutputTokensGeneration = usage["completion_tokens"]?.Value<int>() ?? 0;
                }

                var finishReason = firstChoice?["finish_reason"]?.ToString();
                if (!string.IsNullOrWhiteSpace(finishReason))
                {
                    metrics.FinishReason = finishReason;
                }

                result.Metrics = metrics;

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
                            Reasoning = string.IsNullOrWhiteSpace(reasoning) ? null : reasoning,
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

        /// <summary>
        /// Provider-scoped streaming adapter for OpenRouter Chat Completions SSE.
        /// </summary>
        private sealed class OpenRouterStreamingAdapter : AIProviderStreamingAdapter, IStreamingAdapter
        {
            public OpenRouterStreamingAdapter(OpenRouterProvider provider)
                : base(provider)
            {
            }

            public async IAsyncEnumerable<AIReturn> StreamAsync(
                AIRequestCall request,
                StreamingOptions options,
                [EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                if (request == null)
                {
                    yield break;
                }

                // Prepare request via provider (sets endpoint, method, auth kind)
                request = this.Prepare(request);

                // Only chat completions are supported for streaming
                if (!string.Equals(request.Endpoint, "/chat/completions", StringComparison.Ordinal))
                {
                    var unsupported = new AIReturn();
                    unsupported.CreateProviderError("Streaming is only supported for /chat/completions in this adapter.", request);
                    yield return unsupported;
                    yield break;
                }

                // Build request body and enable streaming
                JObject body;
                try
                {
                    body = JObject.Parse(this.Provider.Encode(request));
                }
                catch
                {
                    body = new JObject();
                }

                body["stream"] = true;

                // Build URL with helper
                var fullUrl = this.BuildFullUrl(request.Endpoint);

                // Configure HTTP client and authentication via helpers
                using var httpClient = this.CreateHttpClient();
                AIReturn? authError = null;
                try
                {
                    var apiKey = ((OpenRouterProvider)this.Provider).GetApiKey();
                    this.ApplyAuthentication(httpClient, request.Authentication, apiKey);
                }
                catch (Exception ex)
                {
                    authError = new AIReturn();
                    authError.CreateProviderError(ex.Message, request);
                }

                if (authError != null)
                {
                    yield return authError;
                    yield break;
                }

                using var httpRequest = this.CreateSsePost(fullUrl, body.ToString(), "application/json");

                // Add OpenRouter-specific headers
                httpRequest.Headers.Add("X-Title", "SmartHopper");
                httpRequest.Headers.Add("Referer", "https://smarthopper.xyz");

                HttpResponseMessage response;
                AIReturn? sendError = null;
                try
                {
                    response = await this.SendForStreamAsync(httpClient, httpRequest, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    sendError = new AIReturn();
                    sendError.CreateNetworkError(ex.InnerException?.Message ?? ex.Message, request);
                    response = null!;
                }

                if (sendError != null)
                {
                    yield return sendError;
                    yield break;
                }

                if (!response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    var (message, isNetworkLike) = AIProvider.ClassifyHttpError((int)response.StatusCode, response.ReasonPhrase, content, this.Provider.Name);
                    var err = new AIReturn();
                    if (isNetworkLike)
                    {
                        err.CreateNetworkError(message, request);
                    }
                    else
                    {
                        err.CreateProviderError(message, request);
                    }

                    yield return err;
                    yield break;
                }

                // Streaming state
                var buffer = new StringBuilder();
                var lastEmit = DateTime.UtcNow;
                var firstChunk = true;
                string finalFinishReason = string.Empty;
                int promptTokens = 0;
                int completionTokens = 0;

                // Provider-local aggregate of assistant text
                var assistantAggregate = new AIInteractionText
                {
                    Agent = AIAgent.Assistant,
                    Content = string.Empty,
                    Metrics = new AIMetrics { Provider = this.Provider.Name, Model = request.Model },
                };

                // Tool call accumulation (index -> partial)
                var toolCalls = new Dictionary<int, (string Id, string Name, StringBuilder Args)>();

                // Helper local function to emit text chunk
                async IAsyncEnumerable<AIReturn> EmitAsync(string text, bool streamingStatus)
                {
                    if (string.IsNullOrEmpty(text)) yield break;

                    // Append to provider-local aggregate
                    assistantAggregate.AppendDelta(contentDelta: text);

                    // Emit a snapshot copy to avoid aliasing across yields
                    var snapshot = new AIInteractionText
                    {
                        Agent = assistantAggregate.Agent,
                        Content = assistantAggregate.Content,
                        Metrics = new AIMetrics
                        {
                            Provider = assistantAggregate.Metrics.Provider,
                            Model = assistantAggregate.Metrics.Model,
                            FinishReason = assistantAggregate.Metrics.FinishReason,
                            InputTokensCached = assistantAggregate.Metrics.InputTokensCached,
                            InputTokensPrompt = assistantAggregate.Metrics.InputTokensPrompt,
                            OutputTokensReasoning = assistantAggregate.Metrics.OutputTokensReasoning,
                            OutputTokensGeneration = assistantAggregate.Metrics.OutputTokensGeneration,
                            CompletionTime = assistantAggregate.Metrics.CompletionTime,
                        },
                    };

                    var delta = new AIReturn
                    {
                        Request = request,
                        Status = streamingStatus ? AICallStatus.Streaming : AICallStatus.Processing,
                    };
                    delta.SetBody(new List<IAIInteraction> { snapshot });
                    yield return delta;
                    await Task.Yield();
                }

                // Helper to maybe flush buffer per options and return deltas to emit
                async Task<List<AIReturn>> FlushAsync(bool force)
                {
                    var results = new List<AIReturn>();
                    if (buffer.Length == 0) return results;
                    var elapsed = (DateTime.UtcNow - lastEmit).TotalMilliseconds;
                    if (force || !options.CoalesceTokens || buffer.Length >= options.PreferredChunkSize || elapsed >= options.CoalesceDelayMs)
                    {
                        var text = buffer.ToString();
                        buffer.Clear();
                        lastEmit = DateTime.UtcNow;
                        await foreach (var d in EmitAsync(text, streamingStatus: true).WithCancellation(cancellationToken))
                        {
                            results.Add(d);
                        }
                    }

                    return results;
                }

                // Yield initial processing state
                {
                    var initial = new AIReturn { Request = request, Status = AICallStatus.Processing };
                    initial.SetBody(new List<IAIInteraction>());
                    yield return initial;
                }

                // Determine idle timeout from request; fall back to shared default if invalid.
                var idleTimeout = TimeSpan.FromSeconds((double)(request.TimeoutSeconds > 0 ? request.TimeoutSeconds : TimeoutDefaults.DefaultTimeoutSeconds));
                await foreach (var data in this.ReadSseDataAsync(
                    response,
                    idleTimeout,
                    null,
                    cancellationToken).WithCancellation(cancellationToken))
                {
                    JObject parsed;
                    try
                    {
                        parsed = JObject.Parse(data);
                    }
                    catch
                    {
                        // Skip malformed chunk
                        continue;
                    }

                    var choices = parsed["choices"] as JArray;
                    var choice = choices?.FirstOrDefault() as JObject;
                    var delta = choice?["delta"] as JObject;
                    var finishReason = choice?["finish_reason"]?.ToString();
                    bool hasFinish = !string.IsNullOrEmpty(finishReason);
                    if (hasFinish) finalFinishReason = finishReason;

                    // Usage metrics (may be present in final chunk)
                    var usage = parsed["usage"] as JObject;
                    if (usage != null)
                    {
                        var pt = usage["prompt_tokens"]?.Value<int?>();
                        var ct = usage["completion_tokens"]?.Value<int?>();
                        if (pt.HasValue) promptTokens = pt.Value;
                        if (ct.HasValue) completionTokens = ct.Value;

                        // Update aggregate metrics
                        assistantAggregate.AppendDelta(metricsDelta: new AIMetrics
                        {
                            Provider = this.Provider.Name,
                            Model = request.Model,
                            InputTokensPrompt = pt ?? 0,
                            OutputTokensGeneration = ct ?? 0,
                        });
                    }

                    // Content streaming
                    var contentDelta = delta?["content"]?.ToString();
                    if (!string.IsNullOrEmpty(contentDelta))
                    {
                        buffer.Append(contentDelta);
                        if (firstChunk)
                        {
                            firstChunk = false;

                            // Force immediate first emit for snappy UX
                            var emitted = await FlushAsync(force: true).ConfigureAwait(false);
                            foreach (var d in emitted)
                            {
                                yield return d;
                            }
                        }
                        else
                        {
                            var emitted = await FlushAsync(force: false).ConfigureAwait(false);
                            foreach (var d in emitted)
                            {
                                yield return d;
                            }
                        }
                    }

                    // Tool calls streaming
                    var tcArray = delta?["tool_calls"] as JArray;
                    if (tcArray != null)
                    {
                        foreach (var t in tcArray.OfType<JObject>())
                        {
                            var idx = t["index"]?.Value<int?>() ?? 0;
                            if (!toolCalls.TryGetValue(idx, out var entry))
                            {
                                entry = (Id: string.Empty, Name: string.Empty, Args: new StringBuilder());
                                toolCalls[idx] = entry;
                            }

                            // id
                            var idVal = t["id"]?.ToString();
                            if (!string.IsNullOrEmpty(idVal)) entry.Id = idVal;

                            var func = t["function"] as JObject;
                            if (func != null)
                            {
                                var name = func["name"]?.ToString();
                                if (!string.IsNullOrEmpty(name)) entry.Name = name;
                                var args = func["arguments"]?.ToString();
                                if (!string.IsNullOrEmpty(args)) entry.Args.Append(args);
                            }

                            toolCalls[idx] = entry; // update
                        }

                        // If we know we're heading to tool calls, flush text first
                        var emittedTc = await FlushAsync(force: true).ConfigureAwait(false);
                        foreach (var d in emittedTc)
                        {
                            yield return d;
                        }

                        // Emit current tool call snapshot with CallingTools status
                        var interactions = new List<IAIInteraction>();
                        foreach (var kv in toolCalls.OrderBy(k => k.Key))
                        {
                            var (id, name, argsSb) = kv.Value;
                            JObject argsObj = null;
                            var argsStr = argsSb.ToString();
                            try
                            {
                                if (!string.IsNullOrWhiteSpace(argsStr))
                                {
                                    argsObj = JObject.Parse(argsStr);
                                }
                            }
                            catch
                            {
                                // Partial JSON, ignore
                            }

                            interactions.Add(new AIInteractionToolCall { Id = id, Name = name, Arguments = argsObj });
                        }

                        var tcDelta = new AIReturn { Request = request, Status = AICallStatus.CallingTools };
                        tcDelta.SetBody(interactions);
                        yield return tcDelta;
                    }
                }

                // Final flush
                var finalEmitted = await FlushAsync(force: true).ConfigureAwait(false);
                foreach (var d in finalEmitted)
                {
                    yield return d;
                }

                // Emit final Finished marker with the complete assistant interaction
                var final = new AIReturn
                {
                    Request = request,
                    Status = AICallStatus.Finished,
                };
                var finalMetrics = new AIMetrics
                {
                    Provider = this.Provider.Name,
                    Model = request.Model,
                    FinishReason = string.IsNullOrEmpty(finalFinishReason) ? "stop" : finalFinishReason,
                    InputTokensPrompt = promptTokens,
                    OutputTokensGeneration = completionTokens,
                };

                // Align aggregate metrics finish reason as well
                assistantAggregate.AppendDelta(metricsDelta: new AIMetrics { FinishReason = finalMetrics.FinishReason });

                // Build final body with text and tool calls
                var finalBuilder = AIBodyBuilder.Create();

                // Add text interaction if present
                if (!string.IsNullOrEmpty(assistantAggregate.Content))
                {
                    var finalSnapshot = new AIInteractionText
                    {
                        Agent = assistantAggregate.Agent,
                        Content = assistantAggregate.Content,
                        Metrics = new AIMetrics
                        {
                            Provider = assistantAggregate.Metrics.Provider,
                            Model = assistantAggregate.Metrics.Model,
                            FinishReason = assistantAggregate.Metrics.FinishReason,
                            InputTokensCached = assistantAggregate.Metrics.InputTokensCached,
                            InputTokensPrompt = assistantAggregate.Metrics.InputTokensPrompt,
                            OutputTokensReasoning = assistantAggregate.Metrics.OutputTokensReasoning,
                            OutputTokensGeneration = assistantAggregate.Metrics.OutputTokensGeneration,
                            CompletionTime = assistantAggregate.Metrics.CompletionTime,
                        },
                    };
                    finalBuilder.Add(finalSnapshot, markAsNew: false);
                }

                // Add tool calls if present (already marked as NOT new since they were yielded)
                foreach (var kv in toolCalls.OrderBy(k => k.Key))
                {
                    var (id, name, argsSb) = kv.Value;
                    JObject argsObj = null;
                    var argsStr = argsSb.ToString();
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(argsStr))
                        {
                            argsObj = JObject.Parse(argsStr);
                        }
                    }
                    catch
                    {
                        // Partial JSON
                    }

                    finalBuilder.Add(new AIInteractionToolCall { Id = id, Name = name, Arguments = argsObj }, markAsNew: false);
                }

                final.SetBody(finalBuilder.Build());
                yield return final;
            }
        }

        /// <inheritdoc/>
        public override IEnumerable<AIExtraDescriptor> GetExtraDescriptors()
        {
            return new[]
            {
                // General parameters (shared across providers)
                new AIExtraDescriptor(
                    "seed",
                    "Seed",
                    "Reproducibility seed for deterministic sampling. Use the same seed to get similar outputs. Leave empty for random.",
                    typeof(int),
                    null),
                new AIExtraDescriptor(
                    "top_p",
                    "Top P",
                    "Nucleus sampling parameter (0.0–1.0). Lower values make output more focused; higher values more diverse. Leave empty to use default.",
                    typeof(double),
                    null),
                new AIExtraDescriptor(
                    "top_k",
                    "Top K",
                    "Only sample from the top K options for each token. Lower values make output more focused.",
                    typeof(int),
                    null),
                new AIExtraDescriptor(
                    "frequency_penalty",
                    "Frequency Penalty",
                    "Penalizes frequent tokens (-2.0 to 2.0). Positive values reduce repetition.",
                    typeof(double),
                    null),
                new AIExtraDescriptor(
                    "presence_penalty",
                    "Presence Penalty",
                    "Penalizes tokens already in the text (-2.0 to 2.0). Positive values encourage new topics.",
                    typeof(double),
                    null),

                // OpenRouter-specific parameters
                new AIExtraDescriptor(
                    "repetition_penalty",
                    "Repetition Penalty",
                    "Alternative penalty for repeated tokens (0.0–2.0). Higher values reduce repetition more strongly.",
                    typeof(double),
                    null),
                new AIExtraDescriptor(
                    "min_p",
                    "Min P",
                    "Minimum probability for token sampling (0.0–1.0). Tokens below this threshold are ignored.",
                    typeof(double),
                    null),
                new AIExtraDescriptor(
                    "top_a",
                    "Top A",
                    "Alternative nucleus sampling method. Threshold based on probability of most likely token.",
                    typeof(double),
                    null),
                new AIExtraDescriptor(
                    "logprobs",
                    "Log Probabilities",
                    "Return log probabilities of output tokens. Useful for analyzing model confidence.",
                    typeof(bool),
                    null),
                new AIExtraDescriptor(
                    "top_logprobs",
                    "Top Logprobs",
                    "Number of most likely tokens to return log probabilities for (0–20). Requires logprobs=true.",
                    typeof(int),
                    null),

                // Provider selection settings
                new AIExtraDescriptor(
                    "allow_fallback",
                    "Allow Fallback",
                    "Whether to allow OpenRouter to fall back to other providers if the primary is unavailable.",
                    typeof(bool),
                    null),
                new AIExtraDescriptor(
                    "sort",
                    "Sort",
                    "Provider routing sort order: 'price' (cheapest), 'throughput' (fastest), or 'latency' (lowest latency).",
                    typeof(string),
                    "price",
                    new[] { "price", "throughput", "latency" }),
                new AIExtraDescriptor(
                    "data_collection",
                    "Data Collection",
                    "Whether to allow provider to collect data from requests: 'allow' or 'deny'.",
                    typeof(string),
                    "deny",
                    new[] { "allow", "deny" }),
                new AIExtraDescriptor(
                    "allow_fallback",
                    "Allow Fallback",
                    "Whether to allow OpenRouter to fall back to other providers if the primary is unavailable.",
                    typeof(bool),
                    null),

                // OpenRouter prompt caching parameters
                new AIExtraDescriptor(
                    "enable_caching",
                    "Enable Prompt Caching",
                    "Adds cache_control to the request body, enabling prompt caching for supported providers routed through OpenRouter.",
                    typeof(bool),
                    null),
            };
        }
    }
}
