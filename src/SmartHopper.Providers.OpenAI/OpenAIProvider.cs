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
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.JsonSchemas;
using SmartHopper.Infrastructure.AICall.Metrics;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Infrastructure.Streaming;

namespace SmartHopper.Providers.OpenAI
{
    /// <summary>
    /// OpenAI provider implementation for SmartHopper.
    /// </summary>
    public sealed class OpenAIProvider : AIProvider<OpenAIProvider>
    {
        // Schema wrapper information is centralized in JsonSchemaService via AsyncLocal.

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenAIProvider"/> class.
        /// </summary>
        private OpenAIProvider()
        {
            this.Models = new OpenAIProviderModels(this);
            // Register provider-specific JSON schema adapter
            JsonSchemaAdapterRegistry.Register(new OpenAIJsonSchemaAdapter());
        }

        /// <summary>
        /// Gets the name of the provider.
        /// </summary>
        public override string Name => "OpenAI";

        /// <summary>
        /// Gets the default server URL for the provider.
        /// </summary>
        public override string DefaultServerUrl => "https://api.openai.com/v1";

        /// <summary>
        /// Gets a value indicating whether this provider is enabled and should be available for use.
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
                var iconBytes = Properties.Resources.openai_icon;
                using (var ms = new System.IO.MemoryStream(iconBytes))
                {
                    return new Bitmap(ms);
                }
            }
        }

        /// <summary>
        /// Returns a streaming adapter for OpenAI that yields incremental AIReturn deltas.
        /// </summary>
        public IStreamingAdapter GetStreamingAdapter()
        {
            return new OpenAIStreamingAdapter(this);
        }

        /// <inheritdoc/>
        public override AIRequestCall PreCall(AIRequestCall request)
        {
            // First do the base PreCall
            request = base.PreCall(request);

            // Setup HTTP method, content type, and authentication
            request.HttpMethod = "POST";
            request.ContentType = "application/json";
            request.Authentication = "bearer";

            // Determine endpoint based on request capability
            if (request.Capability.HasFlag(AICapability.ImageOutput))
            {
                request.Endpoint = "/images/generations";
            }
            else if (request.Endpoint == "/models")
            {
                request.HttpMethod = "GET";
                request.RequestKind = AIRequestKind.Backoffice;
            }
            else
            {
                // Default to chat completions
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

            // Handle different endpoints
            if (request.Endpoint == "/images/generations")
            {
                return this.FormatImageGenerationRequestBody(request);
            }
            else
            {
                // Convert interactions to OpenAI format using the JToken-based encoder
                var messages = new JArray();
                foreach (var interaction in request.Body.Interactions)
                {
                    try
                    {
                        var messageToken = this.EncodeToJToken(interaction);
                        if (messageToken != null) messages.Add(messageToken);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[{this.Name}] Warning: Could not encode interaction: {ex.Message}");
                    }
                }

                return this.FormatChatCompletionsRequestBody(request, messages);
            }
        }

        /// <inheritdoc/>
        public override string Encode(IAIInteraction interaction)
        {
            var messageObj = this.EncodeToJToken(interaction);
            return messageObj?.ToString();
        }

        /// <inheritdoc/>
        private JToken? EncodeToJToken(IAIInteraction interaction)
        {
            var messageObj = new JObject();

            switch (interaction.Agent)
            {
                case AIAgent.System:
                    messageObj["role"] = "system";
                    break;
                case AIAgent.Context:
                    messageObj["role"] = "system";
                    break;
                case AIAgent.User:
                    messageObj["role"] = "user";
                    break;
                case AIAgent.Assistant:
                    messageObj["role"] = "assistant";
                    break;
                case AIAgent.ToolCall:
                    messageObj["role"] = "assistant";
                    break;
                case AIAgent.ToolResult:
                    messageObj["role"] = "tool";
                    break;
                default:
                    throw new ArgumentException($"Agent {interaction.Agent} not supported by OpenAI");
            }

            // Handle different interaction types
            string msgContent = string.Empty;
            bool contentSetExplicitly = false;

            if (interaction is AIInteractionText textInteraction)
            {
                msgContent = textInteraction.Content ?? string.Empty;
            }
            else if (interaction is AIInteractionToolResult toolResultInteraction)
            {
                messageObj["tool_call_id"] = toolResultInteraction.Id;
                if (!string.IsNullOrWhiteSpace(toolResultInteraction.Name))
                {
                    messageObj["name"] = toolResultInteraction.Name;
                }
                msgContent = toolResultInteraction.Result?.ToString() ?? string.Empty;
            }
            else if (interaction is AIInteractionToolCall toolCallInteraction)
            {
                var toolCallObj = new JObject
                {
                    ["id"] = toolCallInteraction.Id,
                    ["type"] = "function",
                    ["function"] = new JObject
                    {
                        ["name"] = toolCallInteraction.Name,
                        ["arguments"] = toolCallInteraction.Arguments?.ToString(),
                    },
                };
                messageObj["tool_calls"] = new JArray { toolCallObj };
                msgContent = string.Empty; // assistant tool_calls messages should have empty content
            }
            else if (interaction is AIInteractionImage imageInteraction)
            {
                // Handle image interactions (for vision models)
                var contentArray = new JArray
                {
                    new JObject
                    {
                        ["type"] = "image_url",
                        ["image_url"] = new JObject
                        {
                            ["url"] = imageInteraction.ImageUrl ?? imageInteraction.ImageData,
                        },
                    },
                };
                messageObj["content"] = contentArray;
                contentSetExplicitly = true;
            }
            else
            {
                // Fallback to empty string for unknown types
                msgContent = string.Empty;
            }

            if (!contentSetExplicitly)
            {
                messageObj["content"] = msgContent;
            }

            return messageObj;
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
                
                // Handle different response types based on the response structure
                if (responseJson["data"] != null)
                {
                    // Image generation response - create a dummy request for processing
                    var dummyRequest = new AIRequestCall();
                    var body = AIBodyBuilder.Create()
                        .Add(new AIInteractionImage { Agent = AIAgent.Assistant })
                        .Build();
                    dummyRequest.Body = body;
                    return this.ProcessImageGenerationResponseData(responseJson, dummyRequest);
                }
                else if (responseJson["choices"] != null)
                {
                    // Chat completion response
                    return this.ProcessChatCompletionsResponseData(responseJson);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OpenAI] Decode error: {ex.Message}");
            }

            return interactions;
        }

        /// <summary>
        /// Formats request body for chat completions endpoint.
        /// </summary>
        private string FormatChatCompletionsRequestBody(AIRequestCall request, JArray messages)
        {
            int maxTokens = this.GetSetting<int>("MaxTokens");
            string reasoningEffort = this.GetSetting<string>("ReasoningEffort") ?? "medium";
            string jsonSchema = request.Body.JsonOutputSchema;
            string? toolFilter = request.Body.ToolFilter;

            Debug.WriteLine($"[OpenAI] FormatRequestBody - Model: {request.Model}, MaxTokens: {maxTokens}");

            // Build request body for chat completions
            var requestBody = new JObject
            {
                ["model"] = request.Model,
                ["messages"] = messages,
            };

            // Configure tokens and parameters based on model family
            // - o-series (o1/o3/o4...): use max_completion_tokens and reasoning_effort; omit temperature
            // - others: use max_tokens and temperature
            if (Regex.IsMatch(request.Model, @"^o[0-9]", RegexOptions.IgnoreCase) || Regex.IsMatch(request.Model, @"^gpt-5", RegexOptions.IgnoreCase))
            {
                requestBody["reasoning_effort"] = reasoningEffort;
                requestBody["max_completion_tokens"] = maxTokens;
            }
            else
            {
                requestBody["max_tokens"] = maxTokens;
                requestBody["temperature"] = this.GetSetting<double>("Temperature");
            }

            // Add response format if JSON schema is provided
            if (!string.IsNullOrEmpty(jsonSchema))
            {
                try
                {
                    var schemaObj = JObject.Parse(jsonSchema);
                    var svc = JsonSchemaService.Instance;
                    var (wrappedSchema, wrapperInfo) = svc.WrapForProvider(schemaObj, this.Name);
                    // Store wrapper info for response unwrapping centrally
                    svc.SetCurrentWrapperInfo(wrapperInfo);
                    Debug.WriteLine($"[OpenAI] Schema wrapper info stored (central): IsWrapped={wrapperInfo.IsWrapped}, Type={wrapperInfo.WrapperType}, Property={wrapperInfo.PropertyName}");

                    requestBody["response_format"] = new JObject
                    {
                        ["type"] = "json_schema",
                        ["json_schema"] = new JObject
                        {
                            ["name"] = "response_schema",
                            ["schema"] = wrappedSchema,
                            ["strict"] = true,
                        },
                    };
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[OpenAI] Failed to parse JSON schema: {ex.Message}");
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

            Debug.WriteLine($"[OpenAI] ChatCompletions Request: {requestBody}");
            return requestBody.ToString();
        }

        /// <summary>
        /// Formats request body for image generation endpoint.
        /// </summary>
        private string FormatImageGenerationRequestBody(AIRequestCall request)
        {
            // Define default values
            string prompt = string.Empty;
            string size = "1024x1024";
            string quality = "standard";
            string style = "vivid";

            // Get parameters from AIInteractionImage in interactions
            if (request.Body.Interactions.Count > 0)
            {
                var interaction = request.Body.Interactions.FirstOrDefault(i => i is AIInteractionImage);
                if (interaction is AIInteractionImage imageRequest)
                {
                    prompt = imageRequest.OriginalPrompt ?? string.Empty;
                    size = imageRequest.ImageSize ?? "1024x1024";
                    quality = imageRequest.ImageQuality ?? "standard";
                    style = imageRequest.ImageStyle ?? "vivid";
                    Debug.WriteLine($"[OpenAI] Image parameters extracted: prompt='{prompt.Substring(0, Math.Min(50, prompt.Length))}...', size={size}, quality={quality}, style={style}");
                }
                else
                {
                    // Fallback: treat first interaction content as prompt
                    var firstInteraction = request.Body.Interactions.First();
                    if (firstInteraction is AIInteractionText textInteraction)
                    {
                        prompt = textInteraction.Content ?? string.Empty;
                        Debug.WriteLine($"[OpenAI] Fallback: using text interaction as prompt: '{prompt.Substring(0, Math.Min(50, prompt.Length))}...'");
                    }
                }
            }
            else
            {
                Debug.WriteLine($"[OpenAI] No interactions found");
            }

            // Build request payload for image generation
            var requestPayload = new JObject
            {
                ["model"] = request.Model,
                ["prompt"] = prompt,
                ["n"] = 1,
                ["size"] = size,
            };

            // Add quality and style for DALL-E 3 models
            if (request.Model.Contains("dall-e-3", StringComparison.OrdinalIgnoreCase))
            {
                requestPayload["quality"] = quality;
                requestPayload["style"] = style;
            }

            Debug.WriteLine($"[OpenAI] ImageGeneration Request: {requestPayload}");
            return requestPayload.ToString();
        }

        /// <inheritdoc/>
        public override IAIReturn PostCall(IAIReturn response)
        {
            // The base implementation already handles the processing
            // Just return the response as-is since processing is done in Call method
            return response;
        }

        /// <summary>
        /// Decodes metrics from OpenAI response.
        /// </summary>
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
                var usage = responseJson["usage"] as JObject;

                if (usage != null)
                {
                    metrics.InputTokensPrompt = usage["prompt_tokens"]?.Value<int>() ?? metrics.InputTokensPrompt;
                    metrics.OutputTokensGeneration = usage["completion_tokens"]?.Value<int>() ?? metrics.OutputTokensGeneration;
                    // Note: TotalTokens is calculated automatically from InputTokensPrompt + OutputTokensGeneration
                }

                // Handle finish reason for chat completions
                var choices = responseJson["choices"] as JArray;
                var firstChoice = choices?.FirstOrDefault() as JObject;
                if (firstChoice != null)
                {
                    metrics.FinishReason = firstChoice["finish_reason"]?.ToString() ?? metrics.FinishReason;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OpenAI] DecodeMetrics error: {ex.Message}");
            }

            return metrics;
        }

        /// <summary>
        /// Processes chat completions response data and converts to interactions.
        /// </summary>
        private List<IAIInteraction> ProcessChatCompletionsResponseData(JObject responseJson)
        {
            var interactions = new List<IAIInteraction>();

            try
            {
                Debug.WriteLine($"[OpenAI] ProcessChatCompletionsResponseData - Processing response");

                // Extract response content
                var choices = responseJson["choices"] as JArray;
                var firstChoice = choices?.FirstOrDefault() as JObject;
                var message = firstChoice?["message"] as JObject;

                if (message == null)
                {
                    Debug.WriteLine($"[OpenAI] No message in response: {responseJson}");
                    return interactions;
                }

                // Extract content and reasoning if OpenAI returns structured content parts
                // Reasoning parts may appear as items with type "reasoning" or "thinking"; text in type "text"
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
                        else if (string.Equals(type, "output_text", StringComparison.OrdinalIgnoreCase))
                        {
                            // OpenAI Responses-style content item; treat as normal assistant text
                            var textVal = part["text"]?.ToString();
                            if (!string.IsNullOrEmpty(textVal)) contentParts.Add(textVal);
                        }
                    }

                    content = string.Join("", contentParts).Trim();
                    reasoning = string.Join("\n\n", reasoningParts).Trim();
                }
                else if (contentToken != null)
                {
                    content = contentToken.ToString();
                }

                // Implement schema unwrapping if needed
                var wrapperInfo = JsonSchemaService.Instance.GetCurrentWrapperInfo();
                if (wrapperInfo != null && wrapperInfo.IsWrapped)
                {
                    Debug.WriteLine($"[OpenAI] Unwrapping response content using wrapper info (central): Type={wrapperInfo.WrapperType}, Property={wrapperInfo.PropertyName}");
                    content = JsonSchemaService.Instance.Unwrap(content, wrapperInfo);
                    Debug.WriteLine($"[OpenAI] Content after unwrapping: {content.Substring(0, Math.Min(100, content.Length))}...");
                }

                var interaction = new AIInteractionText();
                interaction.SetResult(
                    agent: AIAgent.Assistant,
                    content: content,
                    reasoning: string.IsNullOrWhiteSpace(reasoning) ? null : reasoning);

                var metrics = this.DecodeMetrics(responseJson.ToString());

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
                Debug.WriteLine($"[OpenAI] ProcessChatCompletionsResponseData error: {ex.Message}");
            }

            return interactions;
        }

        /// <summary>
        /// Processes image generation response data and converts to interactions.
        /// </summary>
        private List<IAIInteraction> ProcessImageGenerationResponseData(JObject responseJson, AIRequestCall request)
        {
            try
            {
                Debug.WriteLine($"[OpenAI] PostCall - ImageGeneration response parsed successfully");

                var dataArray = responseJson["data"] as JArray;
                if (dataArray == null || dataArray.Count == 0)
                {
                    throw new Exception("No image data returned from OpenAI");
                }

                var imageData = dataArray[0];
                var imageUrl = imageData["url"]?.ToString() ?? string.Empty;
                var revisedPrompt = imageData["revised_prompt"]?.ToString() ?? string.Empty;

                // Get original image request parameters
                var originalImageInteraction = request.Body.Interactions.FirstOrDefault(i => i is AIInteractionImage) as AIInteractionImage;
                
                // Create result interaction
                var resultInteraction = new AIInteractionImage
                {
                    Agent = AIAgent.Assistant,
                    OriginalPrompt = originalImageInteraction?.OriginalPrompt ?? string.Empty,
                    ImageSize = originalImageInteraction?.ImageSize ?? "1024x1024",
                    ImageQuality = originalImageInteraction?.ImageQuality ?? "standard",
                    ImageStyle = originalImageInteraction?.ImageStyle ?? "vivid",
                };

                // Set the result data from the API response
                resultInteraction.SetResult(
                    imageUrl: imageUrl,
                    revisedPrompt: revisedPrompt);

                Debug.WriteLine($"[OpenAI] Final AIInteractionImage result: URL={imageUrl}, revisedPrompt='{revisedPrompt?.Substring(0, Math.Min(50, revisedPrompt?.Length ?? 0))}...'");

                return new List<IAIInteraction> { resultInteraction };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OpenAI] PostCall - Exception: {ex.Message}");
                throw new Exception($"Error processing OpenAI image response: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Provider-scoped streaming adapter for OpenAI Chat Completions SSE.
        /// </summary>
        private sealed class OpenAIStreamingAdapter : AIProviderStreamingAdapter, IStreamingAdapter
        {
            public OpenAIStreamingAdapter(OpenAIProvider provider) : base(provider)
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

                // Only chat completions are supported for streaming for now
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
                // Ask OpenAI to include token usage in the final streaming chunk
                // See: stream_options.include_usage = true
                body["stream_options"] = new JObject
                {
                    ["include_usage"] = true,
                };

                // Build URL with helper
                var fullUrl = this.BuildFullUrl(request.Endpoint);

                // Configure HTTP client and authentication via helpers
                using var httpClient = this.CreateHttpClient();
                AIReturn? authError = null;
                try
                {
                    var apiKey = ((OpenAIProvider)this.Provider).GetApiKey();
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
                    Reasoning = string.Empty,
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

                // Yield initial processing state (optional)
                {
                    var initial = new AIReturn { Request = request, Status = AICallStatus.Processing };
                    initial.SetBody(new List<IAIInteraction>());
                    yield return initial;
                }

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

                    var choices = parsed["choices"] as JArray;
                    var choice = choices?.FirstOrDefault() as JObject;
                    var delta = choice?["delta"] as JObject;
                    var finishReason = choice?["finish_reason"]?.ToString();
                    if (!string.IsNullOrEmpty(finishReason)) finalFinishReason = finishReason;

                    // Usage metrics (only present in final chunk when include_usage=true)
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
                final.Metrics = finalMetrics;

                // Align aggregate metrics finish reason as well
                assistantAggregate.AppendDelta(metricsDelta: new AIMetrics { FinishReason = finalMetrics.FinishReason });

                // Snapshot the final assistant interaction
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

