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
using SmartHopper.Infrastructure.AICall;
using SmartHopper.Infrastructure.AICall.SpecialTypes;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.Infrastructure.Utils;

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
            this.Models = new OpenAIProviderModels(this, request => this.CallApi<string>(request));
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

            // Determine endpoint based on return type
            if (typeof(T) == typeof(AIImage))
            {
                // Image generation endpoint
                request.HttpMethod = "POST";
                request.Endpoint = "/images/generations";
            }
            else
            {
                // Default to chat completions
                request.HttpMethod = "POST";
                request.Endpoint = "/chat/completions";
            }

            // Handle specific endpoints if already set
            if (!string.IsNullOrEmpty(request.Endpoint))
            {
                switch (request.Endpoint)
                {
                    case "/models":
                        request.HttpMethod = "GET";
                        break;
                    case "/images/generations":
                        request.HttpMethod = "POST";
                        break;
                    default:
                        request.HttpMethod = "POST";
                        break;
                }
            }

            request.ContentType = "application/json";
            request.Authentication = "bearer";

            return request;
        }

        /// <inheritdoc/>
        public override string FormatRequestBody(AIRequestCall request)
        {
            if (request.HttpMethod == "GET" || request.HttpMethod == "DELETE")
            {
                return "GET and DELETE requests do not use a request body";
            }

            // Handle different endpoints
            if (request.Endpoint == "/images/generations")
            {
                return FormatImageGenerationRequestBody(request);
            }
            else
            {
                return FormatChatCompletionsRequestBody(request);
            }
        }

        /// <summary>
        /// Formats request body for chat completions endpoint.
        /// </summary>
        private string FormatChatCompletionsRequestBody(AIRequestCall request)
        {
            int maxTokens = this.GetSetting<int>("MaxTokens");
            string reasoningEffort = this.GetSetting<string>("ReasoningEffort") ?? "medium";
            string jsonSchema = request.Body.JsonOutputSchema;
            string? toolFilter = request.Body.ToolFilter;

            Debug.WriteLine($"[OpenAI] FormatRequestBody - Model: {request.Model}, MaxTokens: {maxTokens}");

            // Format messages for OpenAI API
            var convertedMessages = new JArray();

            foreach (var interaction in request.Body.Interactions)
            {
                AIAgent role = interaction.Agent;
                string roleName = string.Empty;

                // Extract message content based on interaction body type
                string msgContent;
                var body = interaction.Body;
                if (body is string s)
                {
                    msgContent = s;
                }
                else if (body is SmartHopper.Infrastructure.AICall.SpecialTypes.AIText text)
                {
                    // For AIText, only send the actual content
                    msgContent = text.Content ?? string.Empty;
                }
                else
                {
                    // Fallback to string representation
                    msgContent = body?.ToString() ?? string.Empty;
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

                    // Pass tool_calls if available from ToolCalls property
                    if (interaction.ToolCalls != null && interaction.ToolCalls.Count > 0)
                    {
                        var toolCallsArray = new JArray();
                        foreach (var toolCall in interaction.ToolCalls)
                        {
                            var toolCallObj = new JObject
                            {
                                ["id"] = toolCall.Id,
                                ["type"] = "function",
                                ["function"] = new JObject
                                {
                                    ["name"] = toolCall.Name,
                                    ["arguments"] = toolCall.Arguments,
                                }
                            };
                            toolCallsArray.Add(toolCallObj);
                        }
                        messageObj["tool_calls"] = toolCallsArray;
                    }
                }
                else if (role == AIAgent.ToolResult)
                {
                    roleName = "tool";

                    // Propagate tool_call ID and name from ToolCalls property
                    if (interaction.ToolCalls != null && interaction.ToolCalls.Count > 0)
                    {
                        var toolCall = interaction.ToolCalls.First();
                        messageObj["name"] = toolCall.Name;
                        messageObj["tool_call_id"] = toolCall.Id;
                    }
                }
                else if (role == AIAgent.ToolCall)
                {
                    // Omit tool call messages
                    continue;
                }
                else
                {
                    roleName = "user";
                }

                messageObj["role"] = roleName;
                convertedMessages.Add(messageObj);
            }

            // Build request body for chat completions
            var requestBody = new JObject
            {
                ["model"] = request.Model,
                ["messages"] = convertedMessages,
            };

            // Configure tokens and parameters based on model family
            // - o-series (o1/o3/o4...): use max_completion_tokens and reasoning_effort; omit temperature
            // - others: use max_tokens and temperature
            if (Regex.IsMatch(request.Model, @"^o[0-9]", RegexOptions.IgnoreCase))
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
        private string FormatImageGenerationRequestBody(IAIRequest request)
        {
            // Define default values
            string prompt = string.Empty;
            string size = "1024x1024";
            string quality = "standard";
            string style = "vivid";

            // Get parameters from AIImage object in first interaction body
            if (request.Body.Interactions.Count > 0)
            {
                var interaction = request.Body.Interactions.First();
                if (interaction.Body is AIImage imageRequest)
                {
                    prompt = imageRequest.OriginalPrompt ?? string.Empty;
                    size = imageRequest.ImageSize ?? "1024x1024";
                    quality = imageRequest.ImageQuality ?? "standard";
                    style = imageRequest.ImageStyle ?? "vivid";
                    Debug.WriteLine($"[OpenAI] Image parameters extracted: prompt='{prompt.Substring(0, Math.Min(50, prompt.Length))}...', size={size}, quality={quality}, style={style}");
                }
                else
                {
                    // Fallback: treat body as string prompt and create minimal AIImage
                    prompt = interaction.Body.ToString() ?? string.Empty;
                    Debug.WriteLine($"[OpenAI] Fallback: using body as prompt string: '{prompt.Substring(0, Math.Min(50, prompt.Length))}...'");
                }
            }
            else
            {
                Debug.WriteLine($"[OpenAI] No interactions found ?�");
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
            // First do the base PostCall
            response = base.PostCall(response);

            // Handle different endpoints
            if (response.Request.Endpoint == "/images/generations")
            {
                return ProcessImageGenerationResponse(response);
            }
            else
            {
                return ProcessChatCompletionsResponse(response);
            }
        }

        /// <summary>
        /// Processes chat completions response.
        /// </summary>
        private IAIReturn ProcessChatCompletionsResponse(IAIReturn response)
        {
            try
            {
                if (response.Result is string s)
                {
                    // Strings are valid responses for this tool
                }
                else if (response.Result is AIText text)
                {
                    // AIText is valid response for this tool
                }
                else
                {
                    throw new Exception($"Error: Type of response {typeof(T).Name} is not supported for {this.Name} provider");
                }

                var responseJson = JObject.Parse(response.RawResult);
                Debug.WriteLine($"[OpenAI] PostCall - ChatCompletions response parsed successfully");

                // Extract response content
                var choices = responseJson["choices"] as JArray;
                var firstChoice = choices?.FirstOrDefault() as JObject;
                var message = firstChoice?["message"] as JObject;
                var usage = responseJson["usage"] as JObject;

                if (message == null)
                {
                    Debug.WriteLine($"[OpenAI] No message in response: {responseJson}");
                    throw new Exception("Invalid response from OpenAI API: No message found");
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

                var result = new AIText
                {
                    Content = content,
                    Reasoning = string.IsNullOrWhiteSpace(reasoning) ? null : reasoning,
                };

                // Implement schema unwrapping if needed
                var wrapperInfo = CurrentWrapperInfo.Value;
                if (wrapperInfo != null && wrapperInfo.IsWrapped)
                {
                    Debug.WriteLine($"[OpenAI] Unwrapping response content using wrapper info: Type={wrapperInfo.WrapperType}, Property={wrapperInfo.PropertyName}");
                    result.Content = UnwrapResponseContent(result.Content, wrapperInfo);
                    Debug.WriteLine($"[OpenAI] Content after unwrapping: {result.Content.Substring(0, Math.Min(100, result.Content.Length))}...");
                }

                var aiReturn = new AIReturn<AIText>
                {
                    Result = result,
                    Metrics = new AIMetrics
                    {
                        FinishReason = firstChoice?["finish_reason"]?.ToString() ?? string.Empty,
                        InputTokensPrompt = usage?["prompt_tokens"]?.Value<int>() ?? 0,
                        OutputTokensGeneration = usage?["completion_tokens"]?.Value<int>() ?? 0,
                    },
                    Status = AICallStatus.Finished,
                };

                // Handle tool calls if any
                if (message["tool_calls"] is JArray toolCalls && toolCalls.Count > 0)
                {
                    aiReturn.ToolCalls = new List<AIToolCall>();
                    foreach (JObject toolCall in toolCalls)
                    {
                        var function = toolCall["function"] as JObject;
                        if (function != null)
                        {
                            aiReturn.ToolCalls.Add(new AIToolCall
                            {
                                Id = toolCall["id"]?.ToString(),
                                Name = function["name"]?.ToString(),
                                Arguments = function["arguments"]?.ToString(),
                            });
                        }
                    }
                    aiReturn.Status = AICallStatus.CallingTools;
                }

                Debug.WriteLine($"[OpenAI] PostCall - Response processed successfully: {aiReturn.Result.Content.Substring(0, Math.Min(50, aiReturn.Result.Content.Length))}...");
                return aiReturn;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OpenAI] PostCall - Exception: {ex.Message}");
                throw new Exception($"Error processing OpenAI chat response: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Processes image generation response.
        /// </summary>
        private IAIReturn<AIImage> ProcessImageGenerationResponse(IAIReturn<AIImage> response)
        {
            try
            {
                var responseJson = JObject.Parse(response.RawResult);
                Debug.WriteLine($"[OpenAI] PostCall - ImageGeneration response parsed successfully");

                var dataArray = responseJson["data"] as JArray;
                if (dataArray == null || dataArray.Count == 0)
                {
                    throw new Exception("No image data returned from OpenAI");
                }

                var imageData = dataArray[0];
                var imageUrl = imageData["url"]?.ToString() ?? string.Empty;
                var revisedPrompt = imageData["revised_prompt"]?.ToString() ?? string.Empty;

                // Create the result AIImage object using the original request parameters
                var imageItem = (AIImage)response.Request.Body.Interactions.First().Body;

                // Set the result data from the API response
                imageItem.SetResult(
                    imageUrl: imageUrl,
                    revisedPrompt: revisedPrompt);

                Debug.WriteLine($"[OpenAI] Final AIImage result: URL={imageUrl}, revisedPrompt='{revisedPrompt?.Substring(0, Math.Min(50, revisedPrompt?.Length ?? 0))}...'");

                var aiReturn = new AIReturn<AIImage>
                {
                    Result = imageItem,
                    Metrics = new AIMetrics
                    {
                        FinishReason = "success",
                    },
                    Status = AICallStatus.Finished,
                };

                Debug.WriteLine($"[OpenAI] PostCall - Image response processed successfully: {aiReturn.Result.ImageUrl}");
                return aiReturn;
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
