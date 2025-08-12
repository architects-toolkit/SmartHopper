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
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.AICall;

namespace SmartHopper.Providers.OpenAI
{
    /// <summary>
    /// OpenAI provider-specific model management implementation.
    /// </summary>
    public class OpenAIProviderModels : AIProviderModels
    {
        private readonly OpenAIProvider openAIProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenAIProviderModels"/> class.
        /// </summary>
        /// <param name="provider">The OpenAI provider instance.</param>
        public OpenAIProviderModels(OpenAIProvider provider)
            : base(provider)
        {
            this.openAIProvider = provider;
        }

        /// <summary>
        /// Retrieves the list of available model names for OpenAI.
        /// </summary>
        /// <returns>A list of available model names.</returns>
        public override async Task<List<string>> RetrieveAvailable()
        {
            Debug.WriteLine("[OpenAI] Retrieving available models");
            try
            {
                // Use AIRequestCall to perform the request
                var request = new AIRequestCall();
                request.Initialize(this.openAIProvider.Name, string.Empty, string.Empty, "/models", AICapability.TextInput);
                request.HttpMethod = "GET";
                request.ContentType = "application/json";
                request.Authentication = "bearer";

                var aiReturn = await request.Exec().ConfigureAwait(false);
                if (!aiReturn.Success)
                {
                    throw new Exception($"API request failed: {aiReturn.ErrorMessage}");
                }

                var response = aiReturn.Body.GetLastInteraction() as AIInteractionText;
                var content = response?.Content ?? string.Empty;
                var json = JObject.Parse(content);
                var data = json["data"] as JArray;
                var modelIds = new List<string>();
                if (data != null)
                {
                    foreach (var item in data.OfType<JObject>())
                    {
                        var id = item["id"]?.ToString();
                        if (!string.IsNullOrEmpty(id))
                        {
                            modelIds.Add(id);
                        }
                    }
                }

                // Fallback when API returns an empty list
                if (modelIds.Count == 0)
                {
                    Debug.WriteLine("[OpenAI] API returned 0 models, using fallback list");
                    return GetFallbackAvailableModels();
                }

                return modelIds;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OpenAI] Exception retrieving models: {ex.Message}");
                // Fallback to a sensible offline list when API is unavailable (e.g., missing API key)
                return GetFallbackAvailableModels();
            }
        }

        /// <summary>
        /// Gets all models and their capabilities supported by OpenAI, fetching fresh data from API.
        /// </summary>
        /// <returns>Dictionary of model names and their capabilities.</returns>
        public override async Task<Dictionary<string, AICapability>> RetrieveCapabilities()
        {
            var result = new Dictionary<string, AICapability>();

            // GPT-5 models - text input/output, image input, structured output, function calling, reasoning
            result["gpt-5*"] = AICapability.TextInput | AICapability.TextOutput | AICapability.ImageInput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning;
            result["gpt-5-mini"] = AICapability.TextInput | AICapability.TextOutput | AICapability.ImageInput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning;

            // GPT-4.1 models - text input/output, image input, structured output, function calling
            result["gpt-4.1*"] = AICapability.TextInput | AICapability.TextOutput | AICapability.ImageInput | AICapability.JsonOutput | AICapability.FunctionCalling;

            // O4-mini models - text input/output, image input, structured output, function calling
            result["o4-mini*"] = AICapability.TextInput | AICapability.TextOutput | AICapability.ImageInput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning;

            // O3 models - text input/output, structured output, function calling
            result["o3*"] = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning;

            // GPT-4o models - text input/output, image input, structured output, function calling
            result["gpt-4o*"] = AICapability.TextInput | AICapability.TextOutput | AICapability.ImageInput | AICapability.JsonOutput | AICapability.FunctionCalling;

            // GPT-image-1 - text input, image input and output
            result["gpt-image-1"] = AICapability.TextInput | AICapability.ImageInput | AICapability.ImageOutput;

            // DALL-E 3 - text input, image output
            result["dall-e-3"] = AICapability.TextInput | AICapability.ImageOutput;

            // DALL-E 2 - text input, image output
            result["dall-e-2"] = AICapability.TextInput | AICapability.ImageOutput;

            // GPT-3.5 models - text input and output
            result["gpt-3.5*"] = AICapability.TextInput | AICapability.TextOutput;

            // GPT-4 models - text input and output
            result["gpt-4*"] = AICapability.TextInput | AICapability.TextOutput;

            return result;
        }

        /// <summary>
        /// Fallback list of available model IDs when API is unavailable.
        /// </summary>
        private List<string> GetFallbackAvailableModels()
        {
            return new List<string>
            {
                // Concrete, commonly used names aligned with our capability map
                "gpt-5",
                "gpt-5-mini",
                "gpt-5-nano",
                "gpt-4.1-mini",
                "o4-mini",
                "gpt-4o-mini",
                "dall-e-3",
            };
        }

        /// <summary>
        /// Gets all default models supported by OpenAI
        /// </summary>
        /// <returns>Dictionary of model names and their capabilities.</returns>
        public override Dictionary<string, AICapability> RetrieveDefault()
        {
            var result = new Dictionary<string, AICapability>();

            result["gpt-5-mini"] = AICapability.BasicChat | AICapability.AdvancedChat | AICapability.JsonGenerator | AICapability.ReasoningChat;
            result["dall-e-3"] = AICapability.ImageGenerator;

            return result;
        }
    }
}
