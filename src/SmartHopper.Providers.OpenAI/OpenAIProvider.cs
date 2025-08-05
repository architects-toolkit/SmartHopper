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
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.Managers.AIProviders;
using SmartHopper.Infrastructure.Managers.ModelManager;
using SmartHopper.Infrastructure.Models;
using SmartHopper.Infrastructure.Utils;

namespace SmartHopper.Providers.OpenAI
{
    /// <summary>
    /// OpenAI provider implementation for SmartHopper.
    /// </summary>
    public sealed class OpenAIProvider : AIProvider
    {
        private const string NameValue = "OpenAI";
        private const string DefaultServerUrlValue = "https://api.openai.com/v1";

        private static readonly Lazy<OpenAIProvider> InstanceValue = new (() => new OpenAIProvider());

        /// <summary>
        /// Gets the singleton instance of the OpenAI provider.
        /// </summary>
        public static OpenAIProvider Instance => InstanceValue.Value;

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenAIProvider"/> class.
        /// </summary>
        private OpenAIProvider()
        {
            this.Models = new OpenAIProviderModels(this, this.CallApi);
        }

        /// <summary>
        /// Gets the name of the provider.
        /// </summary>
        public override string Name => NameValue;

        /// <summary>
        /// Gets the default server URL for the provider.
        /// </summary>
        public override string DefaultServerUrl => DefaultServerUrlValue;

        /// <summary>
        /// Gets a value indicating whether gets whether this provider is enabled and should be available for use.
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

        /// <summary>
        /// Sends messages to the OpenAI Chat Completions endpoint, injecting a reasoning summary parameter when supported
        /// and wrapping any returned reasoning_summary in &lt;think&gt; tags before the actual content.
        /// </summary>
        /// <param name="messages">The conversation messages to send.</param>
        /// <param name="model">The model to use for the request.</param>
        /// <param name="jsonSchema">Optional JSON schema for structured output.</param>
        /// <param name="endpoint">Optional endpoint override.</param>
        /// <param name="toolFilter">Optional tool filter.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <remarks>
        /// We pass reasoning_effort (configurable as "low", "medium", or "high") in the request; if the API returns a
        /// reasoning_summary field, we embed it as &lt;think&gt;…&lt;/think&gt; immediately preceding the assistant's response.
        /// </remarks>
        public override async Task<AIResponse> GetResponse(JArray messages, string model, string jsonSchema = "", string endpoint = "", string? toolFilter = null)
        {
            // Get settings from the secure settings store
            int maxTokens = this.GetSetting<int>("MaxTokens");
            string reasoningEffort = this.GetSetting<string>("ReasoningEffort") ?? "medium";

            Debug.WriteLine($"[OpenAI] GetResponse - Model: {model}, MaxTokens: {maxTokens}");

            // Format messages for OpenAI API
            var convertedMessages = new JArray();

            foreach (var msg in messages)
            {
                string role = msg["role"]?.ToString().ToLower(System.Globalization.CultureInfo.CurrentCulture) ?? "user";
                string msgContent = msg["content"]?.ToString() ?? string.Empty;
                msgContent = AI.StripThinkTags(msgContent);

                var messageObj = new JObject
                {
                    ["content"] = msgContent,
                };

                if (role == "assistant")
                {
                    // Pass tool_calls if available
                    if (msg["tool_calls"] is JArray toolCalls && toolCalls.Count > 0)
                    {
                        foreach (JObject toolCall in toolCalls)
                        {
                            var function = toolCall["function"] as JObject;
                            if (function != null)
                            {
                                // Ensure 'arguments' is serialized as a JSON string
                                if (function["arguments"] is JObject argumentsObject)
                                {
                                    function["arguments"] = argumentsObject.ToString(Newtonsoft.Json.Formatting.None);
                                }
                            }
                        }

                        messageObj["tool_calls"] = toolCalls;
                    }
                }
                else if (role == "tool")
                {
                    var toolCallId = msg["tool_call_id"]?.ToString();
                    var toolName = msg["name"]?.ToString();
                    if (!string.IsNullOrEmpty(toolCallId))
                    {
                        messageObj["tool_call_id"] = toolCallId;
                    }

                    if (!string.IsNullOrEmpty(toolName))
                    {
                        messageObj["name"] = toolName;
                    }
                }

                messageObj["role"] = role;
                convertedMessages.Add(messageObj);
            }

            // Build request body for the new Responses API
            var requestBody = new JObject
            {
                ["model"] = model,
                ["messages"] = convertedMessages,
                ["max_completion_tokens"] = maxTokens,
                ["temperature"] = this.GetSetting<double>("Temperature"),
            };

            // Add reasoning effort if model starts with "o-series"
            if (Regex.IsMatch(model, @"^o[0-9]", RegexOptions.IgnoreCase))
            {
                requestBody["reasoning_effort"] = reasoningEffort;
                requestBody["temperature"] = 1; // Only 1 is accepted for o-series models
            }

            // Store wrapper info for response unwrapping
            SchemaWrapperInfo wrapperInfo = new () { IsWrapped = false };

            // Add response format if JSON schema is provided
            if (!string.IsNullOrEmpty(jsonSchema))
            {
                try
                {
                    var schemaObj = JObject.Parse(jsonSchema);
                    var wrappedSchema = WrapSchemaForOpenAI(schemaObj);
                    wrapperInfo = wrappedSchema.wrapperInfo;

                    requestBody["response_format"] = new JObject
                    {
                        ["type"] = "json_schema",
                        ["json_schema"] = new JObject
                        {
                            ["name"] = "response_schema",
                            ["schema"] = wrappedSchema.schema,
                            ["strict"] = true,
                        },
                    };
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[OpenAI] Failed to parse JSON schema: {ex.Message}");

                    // Continue without schema if parsing fails
                }
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

            Debug.WriteLine($"[OpenAI] Request: {requestBody}");

            try
            {
                // Use the new Call method for HTTP request
                var responseContent = await this.CallApi("/chat/completions", "POST", requestBody.ToString()).ConfigureAwait(false);

                var responseJson = JObject.Parse(responseContent);
                Debug.WriteLine($"[OpenAI] Response parsed successfully");

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

                var content = message["content"]?.ToString() ?? string.Empty;
                var reasoningSummary = message["reasoning_summary"]?.ToString();

                // Unwrap response content if it was wrapped for schema compatibility
                content = UnwrapResponseContent(content, wrapperInfo);

                // If we have a reasoning summary, wrap it in <think> tags before the actual content
                if (!string.IsNullOrWhiteSpace(reasoningSummary))
                {
                    content = $"<think>{reasoningSummary}</think>\n\n{content}";
                }

                var aiResponse = new AIResponse
                {
                    Response = content,
                    Provider = "OpenAI",
                    Model = model,
                    FinishReason = firstChoice?["finish_reason"]?.ToString() ?? "unknown",
                    InTokens = usage?["prompt_tokens"]?.Value<int>() ?? 0,
                    OutTokens = usage?["completion_tokens"]?.Value<int>() ?? 0,
                };

                // Handle tool calls if any
                if (message["tool_calls"] is JArray toolCalls && toolCalls.Count > 0)
                {
                    aiResponse.ToolCalls = new List<AIToolCall>();
                    foreach (JObject toolCall in toolCalls)
                    {
                        var function = toolCall["function"] as JObject;
                        if (function != null)
                        {
                            aiResponse.ToolCalls.Add(new AIToolCall
                            {
                                Id = toolCall["id"]?.ToString(),
                                Name = function["name"]?.ToString(),
                                Arguments = function["arguments"]?.ToString(),
                            });
                        }
                    }
                }

                Debug.WriteLine($"[OpenAI] Response processed successfully: {aiResponse.Response.Substring(0, Math.Min(50, aiResponse.Response.Length))}...");
                return aiResponse;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OpenAI] Exception: {ex.Message}");
                throw new Exception($"Error communicating with OpenAI API: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Generates an image based on a text prompt using OpenAI's DALL-E models.
        /// </summary>
        /// <param name="prompt">The text prompt describing the desired image.</param>
        /// <param name="model">The model to use for image generation (e.g., "dall-e-3", "dall-e-2").</param>
        /// <param name="size">The size of the generated image (e.g., "1024x1024", "1792x1024", "1024x1792").</param>
        /// <param name="quality">The quality of the generated image ("standard" or "hd").</param>
        /// <param name="style">The style of the generated image ("vivid" or "natural").</param>
        /// <returns>An AIResponse containing the generated image data in image-specific fields.</returns>
        public override async Task<AIResponse> GenerateImage(string prompt, string model = "", string size = "1024x1024", string quality = "standard", string style = "vivid")
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // Use default model if none specified
                string modelName = string.IsNullOrWhiteSpace(model) ? this.GetSetting<string>("ImageModel") : model;
                if (string.IsNullOrWhiteSpace(modelName))
                {
                    modelName = this.GetDefaultModel(AIModelCapability.ImageGenerator);
                }

                Debug.WriteLine($"[OpenAI] GenerateImage - Model: {modelName}, Size: {size}, Quality: {quality}, Style: {style}");
                Debug.WriteLine($"[OpenAI] GenerateImage - Prompt: {prompt}");

                // Build request payload
                var requestPayload = new JObject
                {
                    ["model"] = modelName,
                    ["prompt"] = prompt,
                    ["n"] = 1, // Number of images to generate
                    ["size"] = size,

                    // Note: The OpenAI Images API supports the response_format parameter with values 'url' or 'b64_json'.
                    // This implementation does not use the parameter, and images are returned as URLs by default.
                };

                // Add quality and style for DALL-E 3 models
                if (modelName.Contains("dall-e-3", StringComparison.OrdinalIgnoreCase))
                {
                    requestPayload["quality"] = quality;
                    requestPayload["style"] = style;
                }

                var jsonRequest = requestPayload.ToString(Newtonsoft.Json.Formatting.None);
                Debug.WriteLine($"[OpenAI] GenerateImage - Request: {jsonRequest}");

                // Make API call to image generation endpoint
                var content = await this.CallApi("/images/generations", "POST", jsonRequest).ConfigureAwait(false);
                Debug.WriteLine($"[OpenAI] GenerateImage - Response: {content}");

                stopwatch.Stop();

                // Parse response
                var responseObj = JObject.Parse(content);
                var dataArray = responseObj["data"] as JArray;

                if (dataArray == null || dataArray.Count == 0)
                {
                    return new AIResponse
                    {
                        FinishReason = "error",
                        ErrorMessage = "No image data returned from OpenAI",
                        OriginalPrompt = prompt,
                        ImageSize = size,
                        ImageQuality = quality,
                        ImageStyle = style,
                        Provider = this.Name,
                        Model = modelName,
                        CompletionTime = stopwatch.Elapsed.TotalSeconds,
                    };
                }

                var imageData = dataArray[0];
                var imageUrl = imageData["url"]?.ToString() ?? string.Empty;
                var revisedPrompt = imageData["revised_prompt"]?.ToString() ?? prompt;

                return new AIResponse
                {
                    ImageUrl = imageUrl,
                    RevisedPrompt = revisedPrompt,
                    OriginalPrompt = prompt,
                    ImageSize = size,
                    ImageQuality = quality,
                    ImageStyle = style,
                    FinishReason = "success",
                    Provider = this.Name,
                    Model = modelName,
                    CompletionTime = stopwatch.Elapsed.TotalSeconds,
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Debug.WriteLine($"[OpenAI] GenerateImage - Exception: {ex.Message}");

                return new AIResponse
                {
                    FinishReason = "error",
                    ErrorMessage = $"Error generating image: {ex.Message}",
                    OriginalPrompt = prompt,
                    ImageSize = size,
                    ImageQuality = quality,
                    ImageStyle = style,
                    Provider = this.Name,
                    Model = model,
                    CompletionTime = stopwatch.Elapsed.TotalSeconds,
                };
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
