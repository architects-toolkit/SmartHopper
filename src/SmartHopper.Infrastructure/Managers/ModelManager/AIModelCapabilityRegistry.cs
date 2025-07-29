/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System.Collections.Generic;
using System.Linq;

namespace SmartHopper.Infrastructure.Managers.ModelManager
{
    /// <summary>
    /// Registry containing capability information for all known models.
    /// </summary>
    public class AIModelCapabilityRegistry
    {
        /// <summary>
        /// Dictionary of model capabilities keyed by "provider.model".
        /// </summary>
        public Dictionary<string, AIModelCapabilities> Models { get; } = new Dictionary<string, AIModelCapabilities>();

        /// <summary>
        /// Gets capabilities for a specific model.
        /// Supports wildcard matching where models stored with '*' suffix can match more specific model names.
        /// For example, 'o4-mini*' matches 'o4-mini-2025-04-16' and 'o4-mini'.
        /// </summary>
        /// <param name="provider">The provider name.</param>
        /// <param name="model">The model name.</param>
        /// <returns>Model capabilities or null if not found.</returns>
        public AIModelCapabilities GetCapabilities(string provider, string model)
        {
            var key = $"{provider?.ToLower()}.{model?.ToLower()}";
            
            // Try exact match first (fastest path)
            if (this.Models.TryGetValue(key, out var capabilities))
            {
                return capabilities;
            }
            
            // Try wildcard matching - look for stored keys ending with '*' that match our model prefix
            var providerPrefix = $"{provider?.ToLower()}.";
            var modelLower = model?.ToLower();
            
            foreach (var kvp in this.Models)
            {
                var storedKey = kvp.Key;
                
                // Check if stored key is for same provider and ends with wildcard
                if (storedKey.StartsWith(providerPrefix) && storedKey.EndsWith("*"))
                {
                    // Extract the model part without provider prefix and wildcard suffix
                    var storedModelPrefix = storedKey.Substring(providerPrefix.Length, storedKey.Length - providerPrefix.Length - 1);
                    
                    // Check if our model name starts with the stored prefix
                    if (modelLower != null && modelLower.StartsWith(storedModelPrefix))
                    {
                        return kvp.Value;
                    }
                }
            }
            
            return null;
        }

        /// <summary>
        /// Adds or updates capabilities for a specific model.
        /// </summary>
        /// <param name="capabilities">The model capabilities to add/update.</param>
        public void SetCapabilities(AIModelCapabilities capabilities)
        {
            if (capabilities == null) return;

            var key = capabilities.GetKey();
            this.Models[key] = capabilities;
        }

        /// <summary>
        /// Gets all models that support the specified capabilities.
        /// </summary>
        /// <param name="requiredCapabilities">The required capabilities.</param>
        /// <returns>List of matching model capabilities.</returns>
        public List<AIModelCapabilities> FindModelsWithCapabilities(params AIModelCapability[] requiredCapabilities)
        {
            if (requiredCapabilities == null || requiredCapabilities.Length == 0)
                return this.Models.Values.ToList();

            return this.Models.Values
                .Where(model => model.HasAllCapabilities(requiredCapabilities))
                .ToList();
        }

        /// <summary>
        /// Gets the default model for a provider and specific capability.
        /// First looks for models marked as default for the exact capability,
        /// then falls back to models marked as default that support the required capability.
        /// </summary>
        /// <param name="provider">The provider name.</param>
        /// <param name="requiredCapability">The required capability.</param>
        /// <returns>The default model name or null if none found.</returns>
        public string GetDefaultModel(string provider, AIModelCapability requiredCapability = AIModelCapability.BasicChat)
        {
            if (string.IsNullOrEmpty(provider))
                return null;

            var providerModels = this.Models.Values
                .Where(m => string.Equals(m.Provider, provider, System.StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!providerModels.Any())
                return null;

            // First, look for models explicitly marked as default for this capability
            var exactDefaultModel = providerModels
                .FirstOrDefault(m => (m.Default & requiredCapability) == requiredCapability);

            if (exactDefaultModel != null)
                return exactDefaultModel.Model;

            // Fallback: look for any model marked as default that supports the capability
            var compatibleDefaultModel = providerModels
                .Where(m => m.Default != AIModelCapability.None && m.HasCapability(requiredCapability))
                .FirstOrDefault();

            return compatibleDefaultModel?.Model;
        }
    }
}
