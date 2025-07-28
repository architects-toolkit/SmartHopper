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
using System.Threading.Tasks;
using Newtonsoft.Json;
using SmartHopper.Infrastructure.Interfaces;
using SmartHopper.Infrastructure.Managers.AIProviders;
using SmartHopper.Infrastructure.Managers.ModelManager;
using SmartHopper.Infrastructure.Models;

namespace SmartHopper.Providers.MistralAI
{
    /// <summary>
    /// MistralAI provider-specific model management implementation.
    /// </summary>
    public class MistralAIProviderModels : AIProviderModels
    {
        private readonly MistralAIProvider _mistralProvider;

        public MistralAIProviderModels(MistralAIProvider provider) : base(provider)
        {
            _mistralProvider = provider;
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
                try
                {
                    var content = await CallApi("/models").ConfigureAwait(false);
                    var json = JObject.Parse(content);
                    var data = json["data"] as JArray;
                    var modelNames = new List<string>();
                    if (data != null)
                    {
                        foreach (var item in data.OfType<JObject>())
                        {
                            var name = item["id"]?.ToString();
                            if (!string.IsNullOrEmpty(name)) modelNames.Add(name);
                        }
                    }

                    return modelNames;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[MistralAI] Exception retrieving models: {ex.Message}");
                    throw new Exception($"Error retrieving models from MistralAI API: {ex.Message}", ex);
                }
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
        public override async Task<Dictionary<string, ModelCapabilities>> ModelsCapabilities()
        {
            var result = new Dictionary<string, ModelCapabilities>();
            
            try
            {
                Debug.WriteLine("[MistralAI] Fetching models and capabilities via API");
                
                // Get list of available models
                var models = await RetrieveAvailable();
                var processedCount = 0;

                foreach (var modelName in models)
                {
                    try
                    {
                        // Call the Mistral models/{model_id} endpoint
                        var response = await _mistralProvider.CallApi($"/v1/models/{modelName}", "GET", "", "application/json", "bearer");
                        var modelInfo = JsonConvert.DeserializeObject<dynamic>(response);

                        var capabilities = ModelCapability.None;
                        
                        // Map Mistral capabilities to our enum
                        if (modelInfo?.capabilities?.completion_chat == true)
                        {
                            capabilities |= ModelCapability.BasicChat;
                        }
                        if (modelInfo?.capabilities?.function_calling == true)
                        {
                            capabilities |= ModelCapability.FunctionCalling;
                        }
                        if (modelInfo?.capabilities?.vision == true)
                        {
                            capabilities |= ModelCapability.ImageInput;
                        }

                        // Create model capabilities object
                        var modelCapabilities = new ModelCapabilities
                        {
                            Provider = _provider.Name.ToLower(),
                            Model = modelName,
                            Capabilities = capabilities,
                            MaxContextLength = modelInfo?.max_context_length ?? 32768,
                            IsDeprecated = modelInfo?.deprecation != null,
                            ReplacementModel = modelInfo?.deprecation?.replacement_model
                        };

                        // Register capabilities and add to result
                        RegisterCapabilities(
                            modelName,
                            capabilities,
                            modelCapabilities.MaxContextLength,
                            modelCapabilities.IsDeprecated,
                            modelCapabilities.ReplacementModel);
                        
                        result[modelName] = modelCapabilities;
                        processedCount++;
                        Debug.WriteLine($"[MistralAI] Processed capabilities for {modelName}: {capabilities}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[MistralAI] Error processing capabilities for {modelName}: {ex.Message}");
                    }
                }

                Debug.WriteLine($"[MistralAI] Processed {processedCount} models with capabilities");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MistralAI] Error in ModelsCapabilities: {ex.Message}");
            }

            return result;
        }
    }
}
