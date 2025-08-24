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
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Metrics;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.AIProviders;

namespace SmartHopper.Providers.OpenAI
{
    /// <summary>
    /// OpenAI provider implementation for SmartHopper.
    /// </summary>
    public sealed class OpenAIProvider : AIProvider<OpenAIProvider>
    {
        /// <summary>
        /// Thread-local storage for schema wrapper information during request/response cycle.
        /// </summary>
        private static readonly ThreadLocal<SchemaWrapperInfo> CurrentWrapperInfo = new ThreadLocal<SchemaWrapperInfo>();

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenAIProvider"/> class.
        /// </summary>
        private OpenAIProvider()
        {
            this.Models = new OpenAIProviderModels(this);
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
                    dummyRequest.Body = new AIBody();
                    dummyRequest.Body.Interactions = new List<IAIInteraction> { new AIInteractionImage { Agent = AIAgent.User } };
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
                    var (wrappedSchema, wrapperInfo) = WrapSchemaForOpenAI(schemaObj);

                    // Store wrapper info for response unwrapping
                    CurrentWrapperInfo.Value = wrapperInfo;
                    Debug.WriteLine($"[OpenAI] Schema wrapper info stored: IsWrapped={wrapperInfo.IsWrapped}, Type={wrapperInfo.WrapperType}, Property={wrapperInfo.PropertyName}");

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
                    CurrentWrapperInfo.Value = new SchemaWrapperInfo { IsWrapped = false };
                }
            }
            else
            {
                // No schema, so no wrapping needed
                CurrentWrapperInfo.Value = new SchemaWrapperInfo { IsWrapped = false };
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
                var wrapperInfo = CurrentWrapperInfo.Value;
                if (wrapperInfo != null && wrapperInfo.IsWrapped)
                {
                    Debug.WriteLine($"[OpenAI] Unwrapping response content using wrapper info: Type={wrapperInfo.WrapperType}, Property={wrapperInfo.PropertyName}");
                    content = UnwrapResponseContent(content, wrapperInfo);
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
        /// Wraps non-object root schemas to meet OpenAI Structured Outputs requirements.
        /// OpenAI requires root schemas to be objects, so we wrap arrays and other types.
        /// </summary>
        /// <param name="originalSchema">The original JSON schema.</param>
        /// <returns>Tuple with wrapped schema and wrapper info for response unwrapping.</returns>
        private static (JObject schema, SchemaWrapperInfo wrapperInfo) WrapSchemaForOpenAI(JObject originalSchema)
        {
            var schemaType = originalSchema["type"]?.ToString();

            // If it's already an object, return as-is
            if ("object".Equals(schemaType, StringComparison.OrdinalIgnoreCase))
            {
                return (originalSchema, new SchemaWrapperInfo { IsWrapped = false });
            }

            // For arrays, wrap in an object with "items" property
            if ("array".Equals(schemaType, StringComparison.OrdinalIgnoreCase))
            {
                var wrappedSchema = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["items"] = originalSchema,
                    },
                    ["required"] = new JArray { "items" },
                    ["additionalProperties"] = false,
                };

                return (wrappedSchema, new SchemaWrapperInfo { IsWrapped = true, WrapperType = "array", PropertyName = "items" });
            }

            // For other primitive types (string, number, integer, boolean), wrap them
            if (new[] { "string", "number", "integer", "boolean" }.Contains(schemaType))
            {
                var wrappedSchema = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["value"] = originalSchema,
                    },
                    ["required"] = new JArray { "value" },
                    ["additionalProperties"] = false,
                };

                return (wrappedSchema, new SchemaWrapperInfo { IsWrapped = true, WrapperType = schemaType, PropertyName = "value" });
            }

            // For unknown types, wrap generically
            var genericWrappedSchema = new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["data"] = originalSchema,
                },
                ["required"] = new JArray { "data" },
                ["additionalProperties"] = false,
            };

            return (genericWrappedSchema, new SchemaWrapperInfo { IsWrapped = true, WrapperType = "unknown", PropertyName = "data" });
        }

        /// <summary>
        /// Unwraps OpenAI responses that were wrapped due to schema transformation.
        /// </summary>
        /// <param name="content">The response content from OpenAI.</param>
        /// <param name="wrapperInfo">Information about how the schema was wrapped.</param>
        /// <returns>The unwrapped content in original format.</returns>
        private static string UnwrapResponseContent(string content, SchemaWrapperInfo wrapperInfo)
        {
            if (!wrapperInfo.IsWrapped || string.IsNullOrWhiteSpace(content))
            {
                return content;
            }

            try
            {
                var responseObj = JObject.Parse(content);
                var unwrappedValue = responseObj[wrapperInfo.PropertyName];

                if (unwrappedValue != null)
                {
                    // For arrays and objects, return as JSON string
                    if (unwrappedValue.Type == JTokenType.Array || unwrappedValue.Type == JTokenType.Object)
                    {
                        return unwrappedValue.ToString(Newtonsoft.Json.Formatting.None);
                    }

                    // For primitive values, return the value directly
                    return unwrappedValue.ToString();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OpenAI] Failed to unwrap response: {ex.Message}");

                // Return original content if unwrapping fails
            }

            return content;
        }

        /// <summary>
        /// Information about schema wrapping for response unwrapping.
        /// </summary>
        private class SchemaWrapperInfo
        {
            /// <summary>
            /// Gets or sets a value indicating whether the response content is wrapped.
            /// </summary>
            public bool IsWrapped { get; set; }

            /// <summary>
            /// Gets or sets the type of wrapper applied to the response content.
            /// Expected values could include "array", "object", or other schema-related types.
            /// </summary>
            public string WrapperType { get; set; } = string.Empty;

            /// <summary>
            /// Gets or sets the name of the property in the wrapped response that contains the actual data.
            /// </summary>
            public string PropertyName { get; set; } = string.Empty;
        }
    }
}

