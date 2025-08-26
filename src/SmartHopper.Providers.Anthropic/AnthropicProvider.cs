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
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.JsonSchemas;
using SmartHopper.Infrastructure.AICall.Metrics;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Infrastructure.Streaming;
using SmartHopper.Infrastructure.Utils;

namespace SmartHopper.Providers.Anthropic
{
    public sealed class AnthropicProvider : AIProvider<AnthropicProvider>
    {
        private AnthropicProvider()
        {
            this.Models = new AnthropicProviderModels(this);
            // No provider-specific schema adapter required at the moment.
        }

        /// <summary>
        /// Gets the name of the provider.
        /// </summary>
        public override string Name => "Anthropic";

        /// <summary>
        /// Gets the default server URL for the provider.
        /// </summary>
        public override string DefaultServerUrl => "https://api.anthropic.com/v1";

        /// <summary>
        /// Gets a value indicating whether this provider is enabled.
        /// </summary>
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
        /// Gets the provider's icon.
        /// </summary>
        public override Image Icon
        {
            get
            {
                try
                {
                    var bytes = Properties.Resources.anthropic_icon;
                    if (bytes != null && bytes.Length > 0)
                    {
                        using (var ms = new MemoryStream(bytes))
                        using (var img = Image.FromStream(ms))
                        {
                            // Create a decoupled Bitmap so the MemoryStream can be disposed safely
                            return new Bitmap(img);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Anthropic] Icon load error: {ex.Message}");
                }
                return new Bitmap(1, 1);
            }
        }

        /// <summary>
        /// Returns a streaming adapter for Anthropic that yields incremental AIReturn deltas.
        /// </summary>
        public IStreamingAdapter GetStreamingAdapter()
        {
            return new AnthropicStreamingAdapter(this);
        }

        /// <inheritdoc/>
        public override AIRequestCall PreCall(AIRequestCall request)
        {
            request = base.PreCall(request);

            switch (request.Endpoint)
            {
                case "/models":
                    request.HttpMethod = "GET";
                    request.RequestKind = AIRequestKind.Backoffice;
                    break;
                default:
                    request.HttpMethod = "POST";
                    request.Endpoint = "/messages"; // Anthropic Messages API
                    break;
            }

            request.ContentType = "application/json";
            request.Authentication = "x-api-key";

            if (!request.Headers.ContainsKey("anthropic-version"))
            {
                // Default version if not provided by caller
                request.Headers["anthropic-version"] = "2023-06-01";
            }

            return request;
        }

        // No CustomizeHttpClientHeaders override; headers are provided in request.Headers.

        /// <inheritdoc/>
        public override string Encode(IAIInteraction interaction)
        {
            try
            {
                if (interaction is AIInteractionText text)
                {
                    var message = new JObject
                    {
                        ["role"] = MapRole(interaction.Agent),
                        ["content"] = new JArray(new JObject
                        {
                            ["type"] = "text",
                            ["text"] = text.Content ?? string.Empty,
                        }),
                    };
                    return message.ToString();
                }
                else if (interaction is AIInteractionToolResult toolResult)
                {
                    // Represent tool results as user content block for simplicity
                    var message = new JObject
                    {
                        ["role"] = "user",
                        ["content"] = new JArray(new JObject
                        {
                            ["type"] = "text",
                            ["text"] = toolResult.Result?.ToString() ?? string.Empty,
                        }),
                    };
                    return message.ToString();
                }

                // Fallback empty text message
                var fallback = new JObject
                {
                    ["role"] = MapRole(interaction.Agent),
                    ["content"] = new JArray(new JObject { ["type"] = "text", ["text"] = string.Empty }),
                };
                return fallback.ToString();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Anthropic] Encode(IAIInteraction) error: {ex.Message}");
                return string.Empty;
            }
        }

        private static string MapRole(AIAgent agent)
        {
            if (agent == AIAgent.System || agent == AIAgent.Context) return "system";
            if (agent == AIAgent.Assistant || agent == AIAgent.ToolCall) return "assistant";
            return "user";
        }

        /// <inheritdoc/>
        public override string Encode(AIRequestCall request)
        {
            if (request.HttpMethod == "GET" || request.HttpMethod == "DELETE")
            {
                return "GET and DELETE requests do not use a request body";
            }

            int maxTokens = this.GetSetting<int>("MaxTokens");
            double temperature = this.GetSetting<double>("Temperature");
            if (double.IsNaN(temperature) || temperature <= 0) temperature = 0.5;

            string jsonSchema = request.Body.JsonOutputSchema;
            string? toolFilter = request.Body.ToolFilter;

            var messages = new JArray();
            // Anthropic requires system instructions at top-level "system", not as a message role.
            // We collect all system/context texts and combine them into a single string separated by "\n---\n".
            var systemTexts = new List<string>();
            foreach (var interaction in request.Body.Interactions)
            {
                try
                {
                    if (interaction is AIInteractionText text)
                    {
                        var role = MapRole(interaction.Agent);
                        if (string.Equals(role, "system", StringComparison.Ordinal))
                        {
                            // Collect system/context content for top-level system field (as plain text)
                            systemTexts.Add(text.Content ?? string.Empty);
                        }
                        else
                        {
                            var textBlock = new JObject
                            {
                                ["type"] = "text",
                                ["text"] = text.Content ?? string.Empty,
                            };
                            messages.Add(new JObject
                            {
                                ["role"] = role,
                                ["content"] = new JArray(textBlock),
                            });
                        }
                    }
                    else if (interaction is AIInteractionImage img)
                    {
                        // Encode original prompt as text; image content ignored for now
                        messages.Add(new JObject
                        {
                            ["role"] = "user",
                            ["content"] = new JArray(new JObject
                            {
                                ["type"] = "text",
                                ["text"] = img.OriginalPrompt ?? string.Empty,
                            })
                        });
                    }
                    else if (interaction is AIInteractionToolResult toolResult)
                    {
                        // Simplify: include tool result as user text
                        messages.Add(new JObject
                        {
                            ["role"] = "user",
                            ["content"] = new JArray(new JObject
                            {
                                ["type"] = "text",
                                ["text"] = toolResult.Result?.ToString() ?? string.Empty,
                            })
                        });
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Anthropic] Warning: Could not encode interaction: {ex.Message}");
                }
            }

            var requestBody = new JObject
            {
                ["model"] = request.Model,
                ["messages"] = messages,
                ["max_tokens"] = maxTokens,
                ["temperature"] = temperature,
            };

            // Add JSON schema if provided (centralized wrapping)
            if (!string.IsNullOrWhiteSpace(jsonSchema))
            {
                try
                {
                    var schemaObj = JObject.Parse(jsonSchema);
                    var svc = JsonSchemaService.Instance;
                    var (wrappedSchema, wrapperInfo) = svc.WrapForProvider(schemaObj, this.Name);
                    svc.SetCurrentWrapperInfo(wrapperInfo);

                    // Anthropic supports json_object response format; we also add a system instruction for strictness
                    requestBody["response_format"] = new JObject { ["type"] = "json_object" };

                    // Add schema constraint as a top-level system instruction (merged with other system texts)
                    var schemaInstructionText = "The response must be a valid JSON object that strictly follows this schema: " + wrappedSchema.ToString(Formatting.None);
                    systemTexts.Add(schemaInstructionText);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Anthropic] Failed to parse JSON schema: {ex.Message}");
                    JsonSchemaService.Instance.SetCurrentWrapperInfo(new SchemaWrapperInfo { IsWrapped = false });
                }
            }
            else
            {
                JsonSchemaService.Instance.SetCurrentWrapperInfo(new SchemaWrapperInfo { IsWrapped = false });
            }

            // After collecting both conversation system texts and optional schema instruction,
            // set the top-level system string, joining entries with "\n---\n".
            if (systemTexts.Count > 0)
            {
                var combinedSystem = string.Join("\n---\n", systemTexts.Where(s => !string.IsNullOrWhiteSpace(s)));
                if (!string.IsNullOrWhiteSpace(combinedSystem))
                {
                    requestBody["system"] = combinedSystem;
                }
            }

            // Add tools if requested: map OpenAI-style tools to Anthropic 'tools'
            if (!string.IsNullOrWhiteSpace(toolFilter))
            {
                try
                {
                    var toolsOpenAI = this.GetFormattedTools(toolFilter);
                    if (toolsOpenAI != null && toolsOpenAI.Count > 0)
                    {
                        var toolsAnthropic = new JArray();
                        foreach (var t in toolsOpenAI.OfType<JObject>())
                        {
                            if (!string.Equals(t["type"]?.ToString(), "function", StringComparison.OrdinalIgnoreCase)) continue;
                            var fn = t["function"] as JObject;
                            if (fn == null) continue;
                            var toolObj = new JObject
                            {
                                ["name"] = fn["name"],
                                ["description"] = fn["description"],
                                ["input_schema"] = fn["parameters"],
                            };
                            toolsAnthropic.Add(toolObj);
                        }
                        if (toolsAnthropic.Count > 0)
                        {
                            requestBody["tools"] = toolsAnthropic;
                            requestBody["tool_choice"] = new JObject { ["type"] = "auto" };
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Anthropic] Tools mapping error: {ex.Message}");
                }
            }

            Debug.WriteLine($"[Anthropic] Request: {requestBody}");
            return requestBody.ToString();
        }

        /// <inheritdoc/>
        public override List<IAIInteraction> Decode(JObject response)
        {
            var interactions = new List<IAIInteraction>();
            if (response == null) return interactions;

            try
            {
                // Anthropic message response has top-level 'content' array and 'role': 'assistant'
                var content = response["content"] as JArray;
                string contentText = string.Empty;
                var toolCalls = new List<IAIInteraction>();

                if (content != null)
                {
                    var textParts = new List<string>();
                    foreach (var block in content.OfType<JObject>())
                    {
                        var type = block["type"]?.ToString();
                        if (string.Equals(type, "text", StringComparison.OrdinalIgnoreCase))
                        {
                            var t = block["text"]?.ToString();
                            if (!string.IsNullOrEmpty(t)) textParts.Add(t);
                        }
                        else if (string.Equals(type, "tool_use", StringComparison.OrdinalIgnoreCase))
                        {
                            var toolCall = new AIInteractionToolCall
                            {
                                Id = block["id"]?.ToString(),
                                Name = block["name"]?.ToString(),
                                Arguments = block["input"] as JObject,
                                Agent = AIAgent.ToolCall,
                            };
                            toolCalls.Add(toolCall);
                        }
                    }
                    contentText = string.Join(string.Empty, textParts);
                }

                // Unwrap schema if wrapped centrally
                var wrapperInfo = JsonSchemaService.Instance.GetCurrentWrapperInfo();
                if (wrapperInfo != null && wrapperInfo.IsWrapped)
                {
                    contentText = JsonSchemaService.Instance.Unwrap(contentText, wrapperInfo);
                }

                var interaction = new AIInteractionText();
                interaction.SetResult(agent: AIAgent.Assistant, content: contentText, reasoning: null);
                interaction.Metrics = this.DecodeMetrics(response);
                interactions.Add(interaction);

                if (toolCalls.Count > 0)
                {
                    interactions.AddRange(toolCalls);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Anthropic] Decode error: {ex.Message}");
            }

            return interactions;
        }

        private AIMetrics DecodeMetrics(JObject response)
        {
            var m = new AIMetrics();
            if (response == null) return m;
            try
            {
                if (response["usage"] is JObject usage)
                {
                    m.InputTokensPrompt = usage["input_tokens"]?.Value<int>() ?? m.InputTokensPrompt;
                    m.OutputTokensGeneration = usage["output_tokens"]?.Value<int>() ?? m.OutputTokensGeneration;
                }
                m.FinishReason = response["stop_reason"]?.ToString() ?? m.FinishReason;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Anthropic] DecodeMetrics error: {ex.Message}");
            }
            return m;
        }

        /// <summary>
        /// Streaming adapter for Anthropic messages using SSE.
        /// </summary>
        private sealed class AnthropicStreamingAdapter : AIProviderStreamingAdapter, IStreamingAdapter
        {
            private readonly AnthropicProvider provider;

            public AnthropicStreamingAdapter(AnthropicProvider provider) : base(provider)
            {
                this.provider = provider;
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

                request = this.Prepare(request);

                if (!string.Equals(request.Endpoint, "/messages", StringComparison.Ordinal))
                {
                    var unsupported = new AIReturn();
                    unsupported.CreateError($"Streaming not supported for endpoint '{request.Endpoint}'. Use /messages.", request);
                    yield return unsupported;
                    yield break;
                }

                JObject bodyObj;
                AIReturn? bodyError = null;
                try
                {
                    var body = this.provider.Encode(request);
                    bodyObj = string.IsNullOrWhiteSpace(body) ? new JObject() : JObject.Parse(body);
                }
                catch (Exception ex)
                {
                    bodyObj = new JObject();
                    bodyError = new AIReturn();
                    bodyError.CreateProviderError($"Failed to prepare streaming body: {ex.Message}", request);
                }
                if (bodyError != null)
                {
                    yield return bodyError;
                    yield break;
                }

                bodyObj["stream"] = true;

                var url = this.BuildFullUrl(request.Endpoint);
                using var client = this.CreateHttpClient();
                AIReturn? authError = null;
                try
                {
                    this.ApplyAuthentication(client, request.Authentication, this.provider.GetApiKey());
                    HttpHeadersHelper.ApplyExtraHeaders(client, request.Headers);
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

                using var httpReq = this.CreateSsePost(url, bodyObj.ToString());
                HttpResponseMessage responseMsg;
                AIReturn? sendError = null;
                try
                {
                    responseMsg = await this.SendForStreamAsync(client, httpReq, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    sendError = new AIReturn();
                    sendError.CreateNetworkError(ex.InnerException?.Message ?? ex.Message, request);
                    responseMsg = null!;
                }
                if (sendError != null)
                {
                    yield return sendError;
                    yield break;
                }

                if (!responseMsg.IsSuccessStatusCode)
                {
                    var content = await responseMsg.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    var err = new AIReturn();
                    err.CreateProviderError($"HTTP {(int)responseMsg.StatusCode}: {content}", request);
                    yield return err;
                    yield break;
                }

                var initial = new AIReturn { Request = request, Status = AICallStatus.Processing };
                initial.SetBody(new List<IAIInteraction>());
                yield return initial;

                var textBuffer = new StringBuilder();
                var streamMetrics = new AIMetrics { Provider = this.provider.Name, Model = request.Model };
                string? lastFinishReason = null;

                await foreach (var data in this.ReadSseDataAsync(responseMsg, cancellationToken).WithCancellation(cancellationToken))
                {
                    JObject parsed;
                    try
                    {
                        parsed = JObject.Parse(data);
                    }
                    catch
                    {
                        continue;
                    }

                    var type = parsed["type"]?.ToString();
                    if (string.Equals(type, "content_block_delta", StringComparison.OrdinalIgnoreCase))
                    {
                        var delta = parsed["delta"] as JObject;
                        var t = delta?["text"]?.ToString();
                        if (!string.IsNullOrEmpty(t))
                        {
                            textBuffer.Append(t);
                            var snapshot = new AIInteractionText
                            {
                                Agent = AIAgent.Assistant,
                                Content = textBuffer.ToString(),
                                Reasoning = string.Empty,
                                Metrics = new AIMetrics { Provider = this.provider.Name, Model = request.Model },
                            };

                            var deltaRet = new AIReturn { Request = request, Status = AICallStatus.Streaming };
                            deltaRet.SetBody(new List<IAIInteraction> { snapshot });
                            yield return deltaRet;
                        }
                    }
                    else if (string.Equals(type, "message_delta", StringComparison.OrdinalIgnoreCase))
                    {
                        if (parsed["usage"] is JObject usage)
                        {
                            streamMetrics.InputTokensPrompt = usage["input_tokens"]?.Value<int>() ?? streamMetrics.InputTokensPrompt;
                            streamMetrics.OutputTokensGeneration = usage["output_tokens"]?.Value<int>() ?? streamMetrics.OutputTokensGeneration;
                        }
                        lastFinishReason = parsed["stop_reason"]?.ToString() ?? lastFinishReason;
                    }
                    else if (string.Equals(type, "message_stop", StringComparison.OrdinalIgnoreCase))
                    {
                        lastFinishReason = lastFinishReason ?? "stop";
                        break;
                    }
                }

                var final = new AIReturn { Request = request, Status = AICallStatus.Finished };
                streamMetrics.FinishReason = lastFinishReason ?? streamMetrics.FinishReason;

                var finalInteraction = new AIInteractionText
                {
                    Agent = AIAgent.Assistant,
                    Content = textBuffer.ToString(),
                    Reasoning = string.Empty,
                    Metrics = streamMetrics,
                };
                final.SetBody(new List<IAIInteraction> { finalInteraction });
                yield return final;
            }
        }
    }
}
