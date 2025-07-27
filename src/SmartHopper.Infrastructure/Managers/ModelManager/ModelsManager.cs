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
using SmartHopper.Infrastructure.Models;

namespace SmartHopper.Infrastructure.Managers.ModelManager
{
    /// <summary>
    /// Manager class for model-related operations including capability management.
    /// </summary>
    public class ModelsManager
    {
        private readonly IAIProvider _provider;

        /// <summary>
        /// Initializes a new instance of the ModelsManager.
        /// </summary>
        /// <param name="provider">The AI provider this manager belongs to.</param>
        public ModelsManager(IAIProvider provider)
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
            Debug.WriteLine($"[ModelsManager] No model retrieval implementation for {_provider.Name}");
            return await Task.FromResult(new List<string>());
        }

        /// <summary>
        /// Updates the capability information for this provider's models.
        /// Base implementation does nothing - models remain unregistered (unknown capabilities).
        /// Concrete providers should override this to register their model capabilities.
        /// </summary>
        /// <returns>True if capabilities were successfully updated.</returns>
        public virtual async Task<bool> UpdateCapabilities()
        {
            Debug.WriteLine($"[ModelsManager] No capability registration for {_provider.Name} - models remain unregistered");
            return await Task.FromResult(true);
        }

        /// <summary>
        /// Registers model capabilities for a specific model.
        /// </summary>
        /// <param name="modelName">The model name.</param>
        /// <param name="capabilities">The model capabilities.</param>
        /// <param name="maxContextLength">Maximum context length in tokens.</param>
        /// <param name="isDeprecated">Whether the model is deprecated.</param>
        /// <param name="replacementModel">Replacement model if deprecated.</param>
        public virtual void RegisterCapabilities(string modelName, ModelCapability capabilities, 
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

            ModelCapabilityManager.Instance.Registry.SetCapabilities(modelCapabilities);
            ModelCapabilityManager.Instance.SaveCapabilities();
        }

        /// <summary>
        /// Checks if a specific model supports the required capabilities.
        /// </summary>
        /// <param name="model">The model name to check.</param>
        /// <param name="requiredCapabilities">The required capabilities.</param>
        /// <returns>True if the model supports all required capabilities.</returns>
        public virtual bool SupportsCapabilities(string model, params ModelCapability[] requiredCapabilities)
        {
            return ModelCapabilityManager.Instance.SupportsCapabilities(_provider.Name, model, requiredCapabilities);
        }

        /// <summary>
        /// Gets the capability information for a specific model.
        /// </summary>
        /// <param name="model">The model name.</param>
        /// <returns>Model capabilities or null if not found.</returns>
        public virtual ModelCapabilities GetCapabilities(string model)
        {
            return ModelCapabilityManager.Instance.GetCapabilities(_provider.Name, model);
        }

        /// <summary>
        /// Validates if a tool can be executed with the given model.
        /// </summary>
        /// <param name="toolName">The name of the tool to validate.</param>
        /// <param name="model">The model to use for execution.</param>
        /// <returns>Validation result with error message if invalid.</returns>
        public virtual ToolCapabilityValidationResult ValidateToolExecution(string toolName, string model)
        {
            return ModelCapabilityManager.Instance.ValidateToolExecution(toolName, _provider.Name, model);
        }

        /// <summary>
        /// Gets all models from this provider that support the specified capabilities.
        /// </summary>
        /// <param name="requiredCapabilities">The required capabilities.</param>
        /// <returns>List of compatible models from this provider.</returns>
        public virtual List<ModelCapabilities> GetCompatible(params ModelCapability[] requiredCapabilities)
        {
            var allCompatible = ModelCapabilityManager.Instance.FindCompatibleModels(requiredCapabilities);
            return allCompatible.Where(m => m.Provider.Equals(_provider.Name, StringComparison.OrdinalIgnoreCase)).ToList();
        }
    }
}
