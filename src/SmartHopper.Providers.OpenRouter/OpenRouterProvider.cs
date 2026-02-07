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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.JsonSchemas;
using SmartHopper.Infrastructure.AICall.Metrics;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Infrastructure.Streaming;

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

                // For reasoning-enabled models (o-series via OpenRouter), include reasoning in content array
                if (!string.IsNullOrWhiteSpace(toolCallInteraction.Reasoning))
                {
                    var contentArray = new JArray
                    {
                        new JObject
                        {
                            ["type"] = "reasoning",
                            ["text"] = toolCallInteraction.Reasoning,
                        },
                    };
                    obj["content"] = contentArray;
                }
                else
                {
                    obj["content"] = string.Empty;
                }
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

                var result = new AIInteractionText();
                result.SetResult(
                    agent: AIAgent.Assistant,
                    content: content,
                    reasoning: string.IsNullOrWhiteSpace(reasoning) ? null : reasoning);

                // Extract metrics (tokens, model, finish reason) if present
                var metrics = new Infrastructure.AICall.Metrics.AIMetrics
                {
                    Provider = this.Name,
                    Model = response["model"]?.ToString(),
                };

                var usage = response["usage"] as JObject;
                if (usage != null)
                {
                    metrics.InputTokensPrompt = usage["prompt_tokens"]?.Value<int>() ?? 0;
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
            public OpenRouterStreamingAdapter(OpenRouterProvider provider) : base(provider)
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
                    var err = new AIReturn();
                    err.CreateProviderError($"HTTP {(int)response.StatusCode}: {content}", request);
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

                // Determine idle timeout from request (fallback to 60s if invalid)
                var idleTimeout = TimeSpan.FromSeconds(request.TimeoutSeconds > 0 ? request.TimeoutSeconds : 60);
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
                            foreach (var d in emitted) { yield return d; }
                        }
                        else
                        {
                            var emitted = await FlushAsync(force: false).ConfigureAwait(false);
                            foreach (var d in emitted) { yield return d; }
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
                        foreach (var d in emittedTc) { yield return d; }

                        // Emit current tool call snapshot with CallingTools status
                        var interactions = new List<IAIInteraction>();
                        foreach (var kv in toolCalls.OrderBy(k => k.Key))
                        {
                            var (id, name, argsSb) = kv.Value;
                            JObject argsObj = null;
                            var argsStr = argsSb.ToString();
                            try { if (!string.IsNullOrWhiteSpace(argsStr)) argsObj = JObject.Parse(argsStr); } catch { /* partial JSON, ignore */ }
                            interactions.Add(new AIInteractionToolCall { Id = id, Name = name, Arguments = argsObj });
                        }

                        var tcDelta = new AIReturn { Request = request, Status = AICallStatus.CallingTools };
                        tcDelta.SetBody(interactions);
                        yield return tcDelta;
                    }
                }

                // Final flush
                var finalEmitted = await FlushAsync(force: true).ConfigureAwait(false);
                foreach (var d in finalEmitted) { yield return d; }

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
                    try { if (!string.IsNullOrWhiteSpace(argsStr)) argsObj = JObject.Parse(argsStr); } catch { /* partial JSON */ }
                    finalBuilder.Add(new AIInteractionToolCall { Id = id, Name = name, Arguments = argsObj }, markAsNew: false);
                }

                final.SetBody(finalBuilder.Build());
                yield return final;
            }
        }
    }
}
