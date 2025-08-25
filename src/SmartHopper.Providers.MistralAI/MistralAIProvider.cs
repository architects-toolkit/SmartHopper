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
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Infrastructure.Streaming;

namespace SmartHopper.Providers.MistralAI
{
    public sealed class MistralAIProvider : AIProvider<MistralAIProvider>
    {
        private MistralAIProvider()
        {
            this.Models = new MistralAIProviderModels(this);
            // Register provider-specific JSON schema adapter
            JsonSchemaAdapterRegistry.Register(new MistralAIJsonSchemaAdapter());
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
                var iconBytes = Properties.Resources.mistralai_icon;
                using (var ms = new System.IO.MemoryStream(iconBytes))
                {
                    return new Bitmap(ms);
                }
            }
        }

        /// <summary>
        /// Returns a streaming adapter for MistralAI that yields incremental AIReturn deltas.
        /// </summary>
        public IStreamingAdapter GetStreamingAdapter()
        {
            return new MistralAIStreamingAdapter(this);
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
                    request.RequestKind = AIRequestKind.Backoffice;
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

            // Add JSON schema if provided (centralized wrapping)
            if (!string.IsNullOrWhiteSpace(jsonSchema))
            {
                try
                {
                    var schemaObj = JObject.Parse(jsonSchema);
                    var svc = JsonSchemaService.Instance;
                    var (wrappedSchema, wrapperInfo) = svc.WrapForProvider(schemaObj, this.Name);
                    // Store wrapper info for response unwrapping centrally
                    svc.SetCurrentWrapperInfo(wrapperInfo);
                    Debug.WriteLine($"[MistralAI] Schema wrapper info stored (central): IsWrapped={wrapperInfo.IsWrapped}, Type={wrapperInfo.WrapperType}, Property={wrapperInfo.PropertyName}");

                    // Mistral supports json_object response_format; we guide with a system message including wrapped schema
                    requestBody["response_format"] = new JObject { ["type"] = "json_object" };

                    var systemMessage = new JObject
                    {
                        ["role"] = "system",
                        ["content"] = "The response must be a valid JSON object that strictly follows this schema: " + wrappedSchema.ToString(Newtonsoft.Json.Formatting.None),
                    };
                    convertedMessages.Insert(0, systemMessage);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[MistralAI] Failed to parse JSON schema: {ex.Message}");
                    // Continue without schema if parsing fails
                    JsonSchemaService.Instance.SetCurrentWrapperInfo(new SchemaWrapperInfo { IsWrapped = false });
                }
            }
            else
            {
                // No schema, so no wrapping needed
                JsonSchemaService.Instance.SetCurrentWrapperInfo(new SchemaWrapperInfo { IsWrapped = false });
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

                // Unwrap response content if schema was wrapped centrally
                var wrapperInfo = JsonSchemaService.Instance.GetCurrentWrapperInfo();
                if (wrapperInfo != null && wrapperInfo.IsWrapped)
                {
                    Debug.WriteLine($"[MistralAI] Unwrapping response content using wrapper info (central): Type={wrapperInfo.WrapperType}, Property={wrapperInfo.PropertyName}");
                    content = JsonSchemaService.Instance.Unwrap(content, wrapperInfo);
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

        /// <summary>
        /// Streaming adapter for MistralAI chat completions using SSE.
        /// </summary>
        private sealed class MistralAIStreamingAdapter : AIProviderStreamingAdapter, IStreamingAdapter
        {
            private readonly MistralAIProvider provider;

            public MistralAIStreamingAdapter(MistralAIProvider provider) : base(provider)
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

                // Prepare through provider pipeline (sets endpoint/method/auth)
                request = this.Prepare(request);

                // Support chat completions endpoint only
                if (!string.Equals(request.Endpoint, "/chat/completions", StringComparison.Ordinal))
                {
                    var unsupported = new AIReturn();
                    unsupported.CreateError($"Streaming not supported for endpoint '{request.Endpoint}'. Use /chat/completions.", request);
                    yield return unsupported;
                    yield break;
                }

                // Build request body and enable streaming
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
                HttpResponseMessage response;
                AIReturn? sendError = null;
                try
                {
                    response = await this.SendForStreamAsync(client, httpReq, cancellationToken).ConfigureAwait(false);
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

                // Emit initial processing state (helps UI prepare)
                var initial = new AIReturn { Request = request, Status = AICallStatus.Processing };
                initial.SetBody(new List<IAIInteraction>());
                yield return initial;

                var textBuffer = new StringBuilder();
                var haveStreamedAny = false;
                // Collect metrics from streaming (Mistral includes usage in the last chunk per docs)
                var streamMetrics = new AIMetrics
                {
                    Provider = this.provider.Name,
                    Model = request.Model,
                };
                string? lastFinishReason = null;

                // Provider-local aggregate of assistant text
                var assistantAggregate = new AIInteractionText
                {
                    Agent = AIAgent.Assistant,
                    Content = string.Empty,
                    Reasoning = string.Empty,
                    Metrics = new AIMetrics { Provider = this.provider.Name, Model = request.Model },
                };

                await foreach (var data in this.ReadSseDataAsync(response, cancellationToken).WithCancellation(cancellationToken))
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

                    // Mistral chat stream is expected to contain choices[].delta or choices[].message fragments
                    var choices = parsed["choices"] as JArray;
                    var choice = choices?.FirstOrDefault() as JObject;
                    if (choice == null)
                    {
                        continue;
                    }

                    // Capture model if present on any chunk
                    var modelVal = parsed["model"]?.ToString();
                    if (!string.IsNullOrEmpty(modelVal))
                    {
                        streamMetrics.Model = modelVal;
                    }

                    // Capture usage metrics if present (Mistral returns usage in the last chunk)
                    if (parsed["usage"] is JObject usageObj)
                    {
                        streamMetrics.InputTokensPrompt = usageObj["prompt_tokens"]?.Value<int>() ?? streamMetrics.InputTokensPrompt;
                        streamMetrics.OutputTokensGeneration = usageObj["completion_tokens"]?.Value<int>() ?? streamMetrics.OutputTokensGeneration;

                        // Update aggregate metrics as they become available
                        assistantAggregate.AppendDelta(metricsDelta: new AIMetrics
                        {
                            Provider = this.provider.Name,
                            Model = streamMetrics.Model,
                            InputTokensPrompt = usageObj["prompt_tokens"]?.Value<int>() ?? 0,
                            OutputTokensGeneration = usageObj["completion_tokens"]?.Value<int>() ?? 0,
                        });
                    }

                    // Try delta.content (string or array); fallback to message.content
                    string newText = string.Empty;
                    var delta = choice["delta"] as JObject;
                    if (delta != null)
                    {
                        var contentToken = delta["content"];
                        if (contentToken is JArray contentArray)
                        {
                            foreach (var part in contentArray.OfType<JObject>())
                            {
                                if (string.Equals(part["type"]?.ToString(), "text", StringComparison.OrdinalIgnoreCase))
                                {
                                    var t = part["text"]?.ToString();
                                    if (!string.IsNullOrEmpty(t)) newText += t;
                                }
                            }
                        }
                        else if (contentToken != null)
                        {
                            newText = contentToken.ToString();
                        }

                        // Minimal tool call streaming support
                        if (delta["tool_calls"] is JArray tcs && tcs.Count > 0)
                        {
                            var toolInteractions = new List<IAIInteraction>();
                            foreach (var tcTok in tcs.OfType<JObject>())
                            {
                                var toolCall = new AIInteractionToolCall
                                {
                                    Id = tcTok["id"]?.ToString(),
                                    Name = tcTok["function"]?[(object)"name"]?.ToString(),
                                    Arguments = tcTok["function"]?[(object)"arguments"] as JObject,
                                    Agent = AIAgent.ToolCall,
                                };
                                toolInteractions.Add(toolCall);
                            }

                            var tcDelta = new AIReturn { Request = request, Status = AICallStatus.CallingTools };
                            tcDelta.SetBody(toolInteractions);
                            yield return tcDelta;
                            haveStreamedAny = true;
                        }
                    }
                    else
                    {
                        var message = choice["message"] as JObject;
                        var content = message?["content"];
                        if (content is JArray contentArray)
                        {
                            foreach (var part in contentArray.OfType<JObject>())
                            {
                                if (string.Equals(part["type"]?.ToString(), "text", StringComparison.OrdinalIgnoreCase))
                                {
                                    var t = part["text"]?.ToString();
                                    if (!string.IsNullOrEmpty(t)) newText += t;
                                }
                            }
                        }
                        else if (content != null)
                        {
                            newText = content.ToString();
                        }
                    }

                    if (!string.IsNullOrEmpty(newText))
                    {
                        textBuffer.Append(newText);
                        // Append to provider-local aggregate and emit a snapshot
                        assistantAggregate.AppendDelta(contentDelta: newText);

                        var snapshot = new AIInteractionText
                        {
                            Agent = assistantAggregate.Agent,
                            Content = assistantAggregate.Content,
                            Reasoning = assistantAggregate.Reasoning,
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

                        var deltaRet = new AIReturn
                        {
                            Request = request,
                            Status = AICallStatus.Streaming,
                        };
                        deltaRet.SetBody(new List<IAIInteraction> { snapshot });
                        yield return deltaRet;
                        haveStreamedAny = true;
                    }

                    // Handle finish reason if present to emit final status later (record before potential break)
                    var finishReason = choice["finish_reason"]?.ToString();
                    if (!string.IsNullOrEmpty(finishReason))
                    {
                        lastFinishReason = finishReason;
                    }
                    if (!string.IsNullOrEmpty(finishReason) && string.Equals(finishReason, "stop", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }
                }

                // Emit final finished state with complete assistant interaction
                var final = new AIReturn
                {
                    Request = request,
                    Status = AICallStatus.Finished,
                };

                // Attach metrics so UI can display usage after streaming completes
                streamMetrics.FinishReason = lastFinishReason ?? (haveStreamedAny ? "stop" : streamMetrics.FinishReason);
                final.Metrics = streamMetrics;

                // Align aggregate finish reason
                assistantAggregate.AppendDelta(metricsDelta: new AIMetrics { FinishReason = streamMetrics.FinishReason });

                var finalSnapshot = new AIInteractionText
                {
                    Agent = assistantAggregate.Agent,
                    Content = assistantAggregate.Content,
                    Reasoning = assistantAggregate.Reasoning,
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
                final.SetBody(new List<IAIInteraction> { finalSnapshot });
                yield return final;
            }
        }
    }
}
