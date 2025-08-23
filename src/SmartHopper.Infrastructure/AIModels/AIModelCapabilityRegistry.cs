/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SmartHopper.Infrastructure.AIModels
{
    /// <summary>
    /// Registry containing capability information for all known models.
    /// Thread-safe: uses ConcurrentDictionary for reads/writes.
    /// </summary>
    public sealed class AIModelCapabilityRegistry
    {
        /// <summary>
        /// Dictionary of model capabilities keyed by "provider.model".
        /// </summary>
        public ConcurrentDictionary<string, AIModelCapabilities> Models { get; } = new ConcurrentDictionary<string, AIModelCapabilities>();

        /// <summary>
        /// Gets capabilities for a specific model.
        /// Enforces exact name or alias matching only. Wildcard patterns are not supported.
        /// </summary>
        /// <param name="provider">The provider name.</param>
        /// <param name="model">The model name.</param>
        /// <returns>Model capabilities or null if not found.</returns>
        public AIModelCapabilities GetCapabilities(string provider, string model)
        {
            var key = $"{provider?.ToLower()}.{model?.ToLower()}";
            Debug.WriteLine($"[GetCapabilities] Looking for '{key}'");

            // Try exact match first (fastest path)
            if (this.Models.TryGetValue(key, out var capabilities))
            {
                Debug.WriteLine($"[GetCapabilities] Found exact match for '{key}' with capabilities {capabilities.Capabilities.ToDetailedString()}");
                return capabilities;
            }
            
            // Fallback: search by alias within the same provider
            var providerLower = provider?.ToLower();
            var modelLower = model?.ToLower();
            var byAlias = this.Models.Values
                .Where(m => m != null && m.Provider.Equals(providerLower))
                .FirstOrDefault(m => m.Aliases != null && m.Aliases.Any(a => string.Equals(a, modelLower, System.StringComparison.OrdinalIgnoreCase)));

            if (byAlias != null)
            {
                Debug.WriteLine($"[GetCapabilities] Found alias match for '{key}' -> '{byAlias.GetKey()}'");
                return byAlias;
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
        public List<AIModelCapabilities> FindModelsWithCapabilities(AICapability requiredCapabilities)
        {
            if (requiredCapabilities == AICapability.None)
            {
                return this.Models.Values.ToList();
            }

            return this.Models.Values
                .Where(model => model.HasCapability(requiredCapabilities))
                .ToList();
        }

        /// <summary>
        /// Gets the default model for a provider and specific capability.
        /// Exact names only; no wildcard resolution. Prioritizes exact default flag; then any compatible default.
        /// </summary>
        /// <param name="provider">The provider name.</param>
        /// <param name="requiredCapability">The required capability.</param>
        /// <returns>The default model name or null if none found.</returns>
        public string GetDefaultModel(string provider, AICapability requiredCapability = AICapability.Text2Text)
        {
            if (string.IsNullOrEmpty(provider))
            {
                return null;
            }

            var providerModels = this.Models.Values
                .Where(m => m != null && string.Equals(m.Provider, provider, System.StringComparison.OrdinalIgnoreCase))
                .ToList();

            Debug.WriteLine($"[ModelManager] Getting the default model among {providerModels.Count} models for {provider} with capability {requiredCapability.ToDetailedString()}");

            if (!providerModels.Any())
                return null;

            // PRIORITY 1: Exact defaults for the required capability
            var concreteExactModel = providerModels
                .FirstOrDefault(m => (m.Default & requiredCapability) == requiredCapability);

            if (concreteExactModel != null)
            {
                Debug.WriteLine($"[ModelManager] Found concrete exact default model {concreteExactModel.Model} for {provider} with capability {requiredCapability.ToDetailedString()}");
                return concreteExactModel.Model;
            }

            // PRIORITY 2: Any default that is compatible with the required capability
            var concreteCompatibleModel = providerModels
                .Where(m => m.Default != AICapability.None && m.HasCapability(requiredCapability))
                .FirstOrDefault();

            if (concreteCompatibleModel != null)
            {
                Debug.WriteLine($"[ModelManager] Found concrete compatible default model {concreteCompatibleModel.Model} for {provider} with capability {requiredCapability.ToDetailedString()}");
                return concreteCompatibleModel.Model;
            }

            Debug.WriteLine($"[ModelManager] No default model found for {provider} with capability {requiredCapability.ToDetailedString()}");
            return null;
        }
    }
}
