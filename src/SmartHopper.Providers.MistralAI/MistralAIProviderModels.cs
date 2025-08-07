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
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Infrastructure.AIModels;

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
        /// <param name="apiCaller">The API caller function for making HTTP requests.</param>
        public MistralAIProviderModels(MistralAIProvider provider, Func<string, string, string, string, string, Task<string>> apiCaller)
            : base(provider, apiCaller)
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

                var content = await this._apiCaller("/models", "GET", string.Empty, "application/json", "bearer").ConfigureAwait(false);
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

                return modelNames;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MistralAI] Error retrieving available models: {ex.Message}");
                return new List<string>();
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
                    // Call the Mistral models/{model_id} endpoint
                    var response = await this._apiCaller($"/models/{modelName}", "GET", string.Empty, "application/json", "bearer").ConfigureAwait(false);
                    var modelInfo = JsonConvert.DeserializeObject<dynamic>(response);

                    var capabilities = AICapability.None;

                    // Map Mistral capabilities to our enum
                    if (modelInfo?.capabilities?.completion_chat == true)
                    {
                        capabilities |= AICapability.BasicChat;
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

                // Return default capabilities on error
                return GetDefaultCapabilities();
            }

            return result;
        }

        /// <summary>
        /// Gets all default models supported by MistralAI.
        /// </summary>
        /// <returns>Dictionary of model names and their capabilities.</returns>
        public override Dictionary<string, AICapability> RetrieveDefault()
        {
            var result = new Dictionary<string, AICapability>();

            result["mistral-small-latest"] = AICapability.AdvancedChat | AICapability.JsonGenerator;
            result["magistral-small-latest"] = AICapability.ReasoningChat;

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
            result["ministral-8b*"] = AICapability.AdvancedChat | AICapability.JsonGenerator;
            result["ministral-3b*"] = AICapability.AdvancedChat | AICapability.JsonGenerator;

            // Add wildcard patterns for future versions
            result["mistral-small*"] = AICapability.AdvancedChat | AICapability.JsonGenerator | AICapability.ImageInput;
            result["mistral-medium*"] = AICapability.AdvancedChat | AICapability.JsonGenerator | AICapability.ImageInput;
            result["mistral-large*"] = AICapability.AdvancedChat | AICapability.JsonGenerator;
            result["pixtral*"] = AICapability.AdvancedChat | AICapability.ImageInput | AICapability.JsonGenerator;
            result["codestral*"] = AICapability.AdvancedChat | AICapability.JsonGenerator;
            result["magistral*"] = AICapability.ReasoningChat | AICapability.JsonGenerator;

            return result;
        }
    }
}
