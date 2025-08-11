/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall;
using SmartHopper.Infrastructure.AIContext;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.AIProviders;

namespace SmartHopper.Core.Messaging
{
    public static class AIUtils
    {
        /// <summary>
        /// Tries to parse a JSON string into a JToken.
        /// </summary>
        /// <param name="strInput">The JSON string to parse.</param>
        /// <param name="jToken">The output JToken if successful.</param>
        /// <returns>True if parsing was successful, false otherwise.</returns>
        public static bool TryParseJson(string strInput, out JToken? jToken)
        {
            try
            {
                jToken = JToken.Parse(strInput);
                return true;
            }
            catch (Exception)
            {
                jToken = null;
                return false;
            }
        }

        /// <summary>
        /// Gets an AI response using the specified provider and parameters.
        /// </summary>
        /// <param name="providerName">The name of the AI provider to use.</param>
        /// <param name="model">The model to use for the request. If empty, uses provider's default model.</param>
        /// <param name="messages">The conversation messages as key-value pairs (role, content).</param>
        /// <param name="jsonSchema">Optional JSON schema for structured output.</param>
        /// <param name="endpoint">Optional custom endpoint to use.</param>
        /// <param name="toolFilter">Optional filter for available AI tools.</param>
        /// <param name="contextFilter">Optional filter for context providers.</param>
        /// <returns>An AIReturn containing the generated response and metadata.</returns>
        public static async Task<AIReturn<string>> GetResponse(
            string providerName,
            string model,
            List<KeyValuePair<string, string>> messages,
            string jsonSchema = "",
            string endpoint = "",
            string? toolFilter = null,
            string? contextFilter = null)
        {
            return await GetResponse(providerName, model, AIMessageBuilder.CreateMessage(messages), jsonSchema, endpoint, toolFilter, contextFilter).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets an AI response using the specified provider and parameters.
        /// </summary>
        /// <param name="providerName">The name of the AI provider to use.</param>
        /// <param name="model">The model to use for the request. If empty, uses provider's default model.</param>
        /// <param name="messages">The conversation messages as AIInteraction objects.</param>
        /// <param name="jsonSchema">Optional JSON schema for structured output.</param>
        /// <param name="endpoint">Optional custom endpoint to use.</param>
        /// <param name="toolFilter">Optional filter for available AI tools.</param>
        /// <param name="contextFilter">Optional filter for context providers.</param>
        /// <returns>An AIReturn containing the generated response and metadata.</returns>
        public static async Task<AIReturn<string>> GetResponse(
            string providerName,
            string model,
            List<AIInteraction<string>> messages,
            string jsonSchema = "",
            string endpoint = "",
            string? toolFilter = null,
            string? contextFilter = null)
        {
            return await GetResponse(providerName, model, AIMessageBuilder.CreateMessage(messages), jsonSchema, endpoint, toolFilter, contextFilter).ConfigureAwait(false);
        }

        /// <summary>
        /// Internal implementation for getting AI responses with full context management.
        /// </summary>
        /// <param name="providerName">The name of the AI provider to use.</param>
        /// <param name="model">The model to use for the request. If empty, uses provider's default model.</param>
        /// <param name="messages">The conversation messages as a JArray.</param>
        /// <param name="jsonSchema">Optional JSON schema for structured output.</param>
        /// <param name="endpoint">Optional custom endpoint to use.</param>
        /// <param name="toolFilter">Optional filter for available AI tools.</param>
        /// <param name="contextFilter">Optional filter for context providers.</param>
        /// <returns>An AIReturn containing the generated response and metadata.</returns>
        private static async Task<AIReturn<string>> GetResponse(
            string providerName,
            string model,
            JArray messages,
            string jsonSchema = "",
            string endpoint = "",
            string? toolFilter = null,
            string? contextFilter = null)
        {
            var request = new AIRequest
            {
                Provider = providerName,
                Model = model,
                Body = new AIRequestBody
                {
                    Interactions = messages,
                    JsonOutputSchema = jsonSchema,
                    ToolFilter = toolFilter,
                    ContextFilter = contextFilter,
                },
                Endpoint = endpoint,
            };
            return await GetResponse(request).ConfigureAwait(false);
        }

        /// <summary>
        /// Internal implementation for getting AI responses with full context management.
        /// </summary>
        /// <param name="request">The AIRequest containing the request parameters.</param>
        /// <returns>An AIReturn containing the generated response and metadata.</returns>
        private static async Task<AIReturn<string>> GetResponse(AIRequest request)
        {
            return await request.Do().ConfigureAwait(false);
        }

        /// <summary>
        /// Generates an image using the specified provider and parameters.
        /// </summary>
        /// <param name="providerName">The name of the AI provider to use.</param>
        /// <param name="prompt">The text prompt describing the desired image.</param>
        /// <param name="model">The model to use for image generation.</param>
        /// <param name="size">The size of the generated image.</param>
        /// <param name="quality">The quality of the generated image.</param>
        /// <param name="style">The style of the generated image.</param>
        /// <returns>An AIImageResponse containing the generated image data.</returns>
        public static async Task<AIReturn<string>> GenerateImage(
            string providerName,
            string prompt,
            string model = "",
            string size = "1024x1024",
            string quality = "standard",
            string style = "vivid")
        {
            try
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();

                var providers = ProviderManager.Instance.GetProviders().ToList();
                var selectedProvider = providers.FirstOrDefault(p => p.Name.Equals(providerName, StringComparison.OrdinalIgnoreCase));

                if (selectedProvider == null)
                {
                    stopwatch.Stop();

                    var error = $"Error: Provider '{providerName}' not found. Available providers: {string.Join(", ", providers.Select(p => p.Name))}";

                    return new AIReturn<string>
                    {
                        FinishReason = "error",
                        Result = error,
                        Metrics = new AIMetrics()
                        {
                            FinishReason = "error",
                            CompletionTime = stopwatch.Elapsed.TotalSeconds,
                        },
                        Status = AICallStatus.Finished,
                        ErrorMessage = error,
                        OriginalPrompt = prompt,
                        ImageSize = size,
                        ImageQuality = quality,
                        ImageStyle = style,
                    };
                }

                // If no model is specified or the specified model is not compatible with image generation, use the provider's default image model
                if (string.IsNullOrWhiteSpace(model) || !ModelManager.Instance.ValidateCapabilities(selectedProvider.Name, model, AICapability.ImageGenerator))
                {
                    model = selectedProvider.GetDefaultModel(AICapability.ImageGenerator);

                    // If no image model is found, early exit
                    if (string.IsNullOrWhiteSpace(model))
                    {
                        stopwatch.Stop();

                        var error = $"Error: The {selectedProvider.Name} provider does not support image generation. Please select a provider that supports image generation (e.g., OpenAI).";

                        return new AIReturn<string>
                        {
                            FinishReason = "error",
                            Result = error,
                            Metrics = new AIMetrics()
                            {
                                FinishReason = "error",
                                CompletionTime = stopwatch.Elapsed.TotalSeconds,
                            },
                            Status = AICallStatus.Finished,
                            ErrorMessage = error,
                            OriginalPrompt = prompt,
                            ImageSize = size,
                            ImageQuality = quality,
                            ImageStyle = style,
                        };
                    }

                    Debug.WriteLine($"[AIUtils] No model specified for image generation, using: {model}");
                }

                Debug.WriteLine($"[AIUtils] Generating image with {selectedProvider.Name} using model '{model}'");
                Debug.WriteLine($"[AIUtils] Image parameters - Size: {size}, Quality: {quality}, Style: {style}");

                var response = await selectedProvider.GenerateImage(prompt, model, size, quality, style).ConfigureAwait(false);
                stopwatch.Stop();
                response.CompletionTime = stopwatch.Elapsed.TotalSeconds;
                return response;
            }
            catch (HttpRequestException ex)
            {
                var error = $"Error: API request failed - {ex.Message}";
                
                return new AIReturn<string>
                {
                    FinishReason = "error",
                    Result = error,
                    Metrics = new AIMetrics()
                    {
                        FinishReason = "error",
                        CompletionTime = stopwatch.Elapsed.TotalSeconds,
                    },
                    Status = AICallStatus.Finished,
                    ErrorMessage = error,
                    OriginalPrompt = prompt,
                    ImageSize = size,
                    ImageQuality = quality,
                    ImageStyle = style
                };
            }
            catch (Exception ex)
            {
                var error = $"Error: {ex.Message}";
                
                return new AIReturn<string>
                {
                    FinishReason = "error",
                    Result = error,
                    Metrics = new AIMetrics()
                    {
                        FinishReason = "error",
                        CompletionTime = stopwatch.Elapsed.TotalSeconds,
                    },
                    Status = AICallStatus.Finished,
                    ErrorMessage = error,
                    OriginalPrompt = prompt,
                    ImageSize = size,
                    ImageQuality = quality,
                    ImageStyle = style
                };
            }
        }
    }
}
