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
using SmartHopper.Infrastructure.Interfaces;
using SmartHopper.Infrastructure.Managers.ModelManager;

namespace SmartHopper.Infrastructure.Managers.AIProviders
{
    /// <summary>
    /// Base class for AI provider model management operations.
    /// </summary>
    public abstract class AIProviderModels : IAIProviderModels
    {
        protected readonly IAIProvider _provider;
        protected readonly Func<string, string, string, string, string, Task<string>> _apiCaller;

        /// <summary>
        /// Initializes a new instance of the AIProviderModels.
        /// </summary>
        /// <param name="provider">The AI provider this model manager belongs to.</param>
        protected AIProviderModels(IAIProvider provider, Func<string, string, string, string, string, Task<string>> apiCaller)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _apiCaller = apiCaller ?? throw new ArgumentNullException(nameof(apiCaller));
        }

        /// <summary>
        /// Gets the model to use for AI processing.
        /// </summary>
        /// <param name="requestedModel">The requested model, or empty for default.</param>
        /// <returns>The model to use.</returns>
        public virtual string GetModel(string requestedModel = "")
        {
            // Use the requested model if provided
            if (!string.IsNullOrWhiteSpace(requestedModel))
            {
                return requestedModel;
            }

            // Use the model from settings if available
            return this._provider.GetDefaultModel();
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
        public virtual async Task<Dictionary<string, AIModelCapabilities>> RetrieveCapabilities()
        {
            var result = new Dictionary<string, AIModelCapabilities>();

            try
            {
                // Get available models
                var models = await this.RetrieveAvailable();

                // Get capabilities for each model
                foreach (var model in models)
                {
                    var capabilities = this.RetrieveCapabilities(model);
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
        /// Gets the capability information for a specific model.
        /// </summary>
        /// <param name="model">The model name.</param>
        /// <returns>Model capabilities or null if not found.</returns>
        public virtual AIModelCapabilities RetrieveCapabilities(string model)
        {
            return new AIModelCapabilities()
            {
                Provider = _provider.Name,
                Model = model,
                Capabilities = AIModelCapability.None,
                MaxContextLength = 4096,
                IsDeprecated = false,
                ReplacementModel = "",
            };
        }
    }
}
