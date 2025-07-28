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
using SmartHopper.Infrastructure.Interfaces;
using SmartHopper.Infrastructure.Managers.ModelManager;
using SmartHopper.Infrastructure.Models;

namespace SmartHopper.Infrastructure.Managers.AIProviders
{
    /// <summary>
    /// Base class for AI provider model management operations.
    /// </summary>
    public abstract class AIProviderModels : IAIProviderModels
    {
        protected readonly IAIProvider _provider;

        /// <summary>
        /// Initializes a new instance of the AIProviderModels.
        /// </summary>
        /// <param name="provider">The AI provider this model manager belongs to.</param>
        protected AIProviderModels(IAIProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        /// <summary>
        /// Retrieves the list of available model names for this provider.
        /// </summary>
        /// <returns>A list of available model names.</returns>
        public virtual async Task<List<string>> RetrieveAvailable()
        {
            // Default implementation returns empty list
            // Concrete providers should override this method
            Debug.WriteLine($"[AIProviderModels] No model retrieval implementation for {_provider.Name}");
            return await Task.FromResult(new List<string>());
        }



        /// <summary>
        /// Gets all models and their capabilities supported by this provider.
        /// Base implementation returns available models with their registered capabilities.
        /// Concrete providers should override this to provide provider-specific capability discovery.
        /// </summary>
        /// <returns>Dictionary of model names and their capabilities.</returns>
        public virtual async Task<Dictionary<string, ModelCapabilities>> ModelsCapabilities()
        {
            var result = new Dictionary<string, ModelCapabilities>();
            
            try
            {
                // Get available models
                var models = await RetrieveAvailable();
                
                // Get capabilities for each model
                foreach (var model in models)
                {
                    var capabilities = GetCapabilities(model);
                    if (capabilities != null)
                    {
                        result[model] = capabilities;
                    }
                }
                
                Debug.WriteLine($"[AIProviderModels] Retrieved {result.Count} models with capabilities for {_provider.Name}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AIProviderModels] Error retrieving models capabilities for {_provider.Name}: {ex.Message}");
            }
            
            return result;
        }

        /// <summary>
        /// Registers model capabilities for a specific model.
        /// </summary>
        /// <param name="modelName">The model name.</param>
        /// <param name="capabilities">The model capabilities.</param>
        /// <param name="maxContextLength">Maximum context length in tokens.</param>
        /// <param name="isDeprecated">Whether the model is deprecated.</param>
        /// <param name="replacementModel">Replacement model if deprecated.</param>
        protected virtual void RegisterCapabilities(string modelName, ModelCapability capabilities, 
            int maxContextLength = 4096, bool isDeprecated = false, string replacementModel = null)
        {
            var modelCapabilities = new ModelCapabilities
            {
                Provider = _provider.Name.ToLower(),
                Model = modelName,
                Capabilities = capabilities,
                MaxContextLength = maxContextLength,
                IsDeprecated = isDeprecated,
                ReplacementModel = replacementModel
            };

            // Use the in-memory ModelsManager to store capabilities
            ModelsManager.Instance.SetCapabilities(modelCapabilities);
        }

        /// <summary>
        /// Checks if a specific model supports the required capabilities.
        /// </summary>
        /// <param name="model">The model name to check.</param>
        /// <param name="requiredCapabilities">The required capabilities.</param>
        /// <returns>True if the model supports all required capabilities.</returns>
        public virtual bool SupportsCapabilities(string model, params ModelCapability[] requiredCapabilities)
        {
            return ModelsManager.Instance.SupportsCapabilities(_provider.Name, model, requiredCapabilities);
        }

        /// <summary>
        /// Gets the capability information for a specific model.
        /// </summary>
        /// <param name="model">The model name.</param>
        /// <returns>Model capabilities or null if not found.</returns>
        public virtual ModelCapabilities GetCapabilities(string model)
        {
            return ModelsManager.Instance.GetCapabilities(_provider.Name, model);
        }



        /// <summary>
        /// Gets all models from this provider that support the specified capabilities.
        /// </summary>
        /// <param name="requiredCapabilities">The required capabilities.</param>
        /// <returns>List of compatible models from this provider.</returns>
        public virtual List<ModelCapabilities> GetCompatible(params ModelCapability[] requiredCapabilities)
        {
            var allCompatible = ModelsManager.Instance.FindCompatibleModels(requiredCapabilities);
            return allCompatible.Where(m => m.Provider.Equals(_provider.Name, StringComparison.OrdinalIgnoreCase)).ToList();
        }
    }
}
