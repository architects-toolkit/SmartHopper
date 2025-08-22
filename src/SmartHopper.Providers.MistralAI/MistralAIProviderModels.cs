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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.AIProviders;

namespace SmartHopper.Providers.MistralAI
{
    /// <summary>
    /// MistralAI provider-specific model management implementation.
    /// </summary>
    public class MistralAIProviderModels : AIProviderModels
    {
        private readonly MistralAIProvider mistralProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="MistralAIProviderModels"/> class.
        /// </summary>
        /// <param name="provider">The MistralAI provider instance.</param>
        public MistralAIProviderModels(MistralAIProvider provider)
            : base(provider)
        {
            this.mistralProvider = provider;
        }

        /// <summary>
        /// Retrieves the list of available model names for MistralAI.
        /// </summary>
        /// <returns>A list of available model names.</returns>
        public override async Task<List<string>> RetrieveAvailable()
        {
            try
            {
                Debug.WriteLine("[MistralAI] Retrieving available models");

                // Use AIRequestCall to perform the request
                var request = new AIRequestCall();
                request.Initialize(this.mistralProvider.Name, string.Empty, string.Empty, "/models", AICapability.TextInput);
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
                var modelNames = new List<string>();
                if (data != null)
                {
                    foreach (var item in data.OfType<JObject>())
                    {
                        var name = item["id"]?.ToString();
                        if (!string.IsNullOrEmpty(name))
                        {
                            modelNames.Add(name);
                        }
                    }
                }

                // Fallback when API returns an empty list
                if (modelNames.Count == 0)
                {
                    Debug.WriteLine("[MistralAI] API returned 0 models, using fallback list");
                    return GetFallbackAvailableModels();
                }

                return modelNames;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MistralAI] Error retrieving available models: {ex.Message}");
                // Fallback to a sensible offline list when API is unavailable (e.g., missing API key)
                return GetFallbackAvailableModels();
            }
        }

        /// <summary>
        /// Gets all models and their capabilities supported by MistralAI, fetching fresh data from API.
        /// </summary>
        /// <returns>Dictionary of model names and their capabilities.</returns>
        public override async Task<Dictionary<string, AICapability>> RetrieveCapabilities()
        {
            var result = new Dictionary<string, AICapability>();

            try
            {
                Debug.WriteLine("[MistralAI] Fetching models and capabilities via API");

                // Get list of available models
                var models = await this.RetrieveAvailable().ConfigureAwait(false);

                // If no models were retrieved (likely due to API key issue), return default capabilities
                if (models == null || models.Count == 0)
                {
                    Debug.WriteLine("[MistralAI] No models retrieved, returning default capabilities");
                    return GetDefaultCapabilities();
                }

                var processedCount = 0;

                foreach (var modelName in models)
                {
                    // Use AIRequestCall to get model details
                    var request = new AIRequestCall();
                    request.Initialize(this.mistralProvider.Name, string.Empty, string.Empty, $"/models/{modelName}", AICapability.TextInput);
                    request.HttpMethod = "GET";
                    request.ContentType = "application/json";
                    request.Authentication = "bearer";

                    var aiReturn = await request.Exec().ConfigureAwait(false);
                    if (!aiReturn.Success)
                    {
                        Debug.WriteLine($"[MistralAI] Failed to get model details for {modelName}: {aiReturn.ErrorMessage}");
                        continue;
                    }

                    var response = aiReturn.Body.GetLastInteraction() as AIInteractionText;
                    var content = response?.Content ?? string.Empty;
                    var modelInfo = JsonConvert.DeserializeObject<dynamic>(content);

                    var capabilities = AICapability.None;

                    // Map Mistral capabilities to our enum
                    if (modelInfo?.capabilities?.completion_chat == true)
                    {
                        capabilities |= AICapability.Text2Text;
                    }

                    if (modelInfo?.capabilities?.function_calling == true)
                    {
                        capabilities |= AICapability.FunctionCalling;
                    }

                    if (modelInfo?.capabilities?.vision == true)
                    {
                        capabilities |= AICapability.ImageInput;
                    }

                    // Currently Mistral offers json_mode for all models
                    capabilities |= AICapability.JsonOutput;

                    result[modelName] = capabilities;
                    processedCount++;
                    Debug.WriteLine($"[MistralAI] Processed capabilities for {modelName}: {capabilities}");
                }

                Debug.WriteLine($"[MistralAI] Processed {processedCount} models with capabilities");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MistralAI] Error in RetrieveCapabilities: {ex.Message}");

                Debug.WriteLine("[MistralAI] Falling back to static/default model capabilities");

                // Fallback to static capabilities when API is unavailable (e.g., missing API key)
                result = GetFallbackCapabilities();
            }

            // If we still have no results, use fallback
            if (result.Count == 0)
            {
                Debug.WriteLine("[MistralAI] No models found, using fallback capabilities");
                result = GetFallbackCapabilities();
            }

            return result;
        }

        /// <summary>
        /// Fallback list of available model IDs when API is unavailable.
        /// </summary>
        private List<string> GetFallbackAvailableModels()
        {
            return new List<string>
            {
                // Concrete, commonly used names
                "mistral-small-latest",
                "mistral-medium-latest",
                "mistral-large-latest",
                "magistral-small-latest",
                "magistral-medium-latest",
            };
        }

        /// <summary>
        /// Gets fallback model capabilities when API is unavailable.
        /// </summary>
        /// <returns>Dictionary of model names and their capabilities.</returns>
        private Dictionary<string, AICapability> GetFallbackCapabilities()
        {
            var result = new Dictionary<string, AICapability>();

            // Mistral Small models - text input/output, structured output, function calling
            result["mistral-small*"] = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling;

            // Ensure concrete default ID exists alongside wildcard to allow default registration
            result["mistral-small-latest"] = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling;

            // Mistral Medium models - text input/output, structured output, function calling
            result["mistral-medium*"] = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling;

            // Mistral Large models - text input/output, structured output, function calling
            result["mistral-large*"] = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling;

            // Magistral Small models - text input/output, structured output, function calling
            result["magistral-small*"] = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning;

            // Ensure concrete default ID exists alongside wildcard to allow default registration
            result["magistral-small-latest"] = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning;

            // Magistral Medium models - text input/output, structured output, function calling
            result["magistral-medium*"] = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput | AICapability.FunctionCalling | AICapability.Reasoning;

            Debug.WriteLine($"[MistralAI] Registered {result.Count} fallback model patterns");
            return result;
        }

        /// <summary>
        /// Gets all default models supported by MistralAI.
        /// </summary>
        /// <returns>Dictionary of model names and their capabilities.</returns>
        public override Dictionary<string, AICapability> RetrieveDefault()
        {
            var result = new Dictionary<string, AICapability>();

            result["mistral-small-latest"] = AICapability.ToolChat | AICapability.Text2Json;
            result["magistral-small-latest"] = AICapability.ToolReasoningChat;

            return result;
        }

        /// <summary>
        /// Gets default capabilities for common MistralAI models.
        /// Used when API is not available (e.g., during initialization without API key).
        /// </summary>
        /// <returns>Dictionary of default model capabilities.</returns>
        private static Dictionary<string, AICapability> GetDefaultCapabilities()
        {
            var result = new Dictionary<string, AICapability>();

            // Ministral models
            result["ministral-8b*"] = AICapability.ToolChat | AICapability.Text2Json;
            result["ministral-3b*"] = AICapability.ToolChat | AICapability.Text2Json;

            // Add wildcard patterns for future versions
            result["mistral-small*"] = AICapability.ToolChat | AICapability.Text2Json | AICapability.ImageInput;
            result["mistral-medium*"] = AICapability.ToolChat | AICapability.Text2Json | AICapability.ImageInput;
            result["mistral-large*"] = AICapability.ToolChat | AICapability.Text2Json;
            result["pixtral*"] = AICapability.ToolChat | AICapability.ImageInput | AICapability.Text2Json;
            result["codestral*"] = AICapability.ToolChat | AICapability.Text2Json;
            result["magistral*"] = AICapability.ToolReasoningChat | AICapability.Text2Json;

            return result;
        }
    }
}
