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
using System.Diagnostics;
using System.Linq;

namespace SmartHopper.Infrastructure.AIModels
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
            Debug.WriteLine($"[GetCapabilities] Looking for '{key}'");

            // Try exact match first (fastest path)
            if (this.Models.TryGetValue(key, out var capabilities))
            {
                Debug.WriteLine($"[GetCapabilities] Found exact match for '{key}' with capabilities {capabilities.Capabilities.ToDetailedString()}");
                return capabilities;
            }

            Debug.WriteLine($"[GetCapabilities] No exact match for '{key}', trying wildcard matching");

            // Try wildcard matching - look for stored keys ending with '*' that match our model prefix
            var providerPrefix = $"{provider?.ToLower()}.";
            var modelLower = model?.ToLower();
            Debug.WriteLine($"[GetCapabilities] Wildcard search: providerPrefix='{providerPrefix}', modelLower='{modelLower}'");
            Debug.WriteLine($"[GetCapabilities] Registry has {this.Models.Count} total models");

            foreach (var kvp in this.Models)
            {
                var storedKey = kvp.Key;
                Debug.WriteLine($"[GetCapabilities] Checking registry key: '{storedKey}'");

                // Skip null keys (shouldn't happen but defensive programming)
                if (storedKey == null)
                {
                    Debug.WriteLine($"[GetCapabilities] Skipping null key");
                    continue;
                }
                
                // Check if stored key is for same provider and ends with wildcard
                if (storedKey.StartsWith(providerPrefix) && storedKey.EndsWith("*"))
                {
                    Debug.WriteLine($"[GetCapabilities] Found wildcard candidate: '{storedKey}'");
                    // Extract the model part without provider prefix and wildcard suffix
                    var storedModelPrefix = storedKey.Substring(providerPrefix.Length, storedKey.Length - providerPrefix.Length - 1);
                    Debug.WriteLine($"[GetCapabilities] Extracted prefix: '{storedModelPrefix}', checking if '{modelLower}' starts with it");
                    
                    // Check if our model name starts with the stored prefix
                    if (modelLower != null && modelLower.StartsWith(storedModelPrefix))
                    {
                        Debug.WriteLine($"[GetCapabilities] MATCH! Returning capabilities: {kvp.Value.Capabilities.ToDetailedString()}");
                        return kvp.Value;
                    }
                    else
                    {
                        Debug.WriteLine($"[GetCapabilities] No prefix match for '{storedKey}'");
                    }
                }
                else
                {
                    Debug.WriteLine($"[GetCapabilities] Key '{storedKey}' doesn't match wildcard criteria");
                }
            }
            
            Debug.WriteLine($"[GetCapabilities] No wildcard match found for '{key}'");
            
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
        /// Resolves a model name, converting wildcard patterns to actual model names when possible.
        /// </summary>
        /// <param name="modelName">The model name to resolve (may contain wildcards).</param>
        /// <param name="provider">The provider name.</param>
        /// <returns>The resolved model name, or the original if no resolution is possible.</returns>
        private string ResolveModelName(string modelName, string provider)
        {
            if (string.IsNullOrEmpty(modelName) || !modelName.Contains("*"))
            {
                // Not a wildcard pattern, return as-is
                return modelName;
            }

            Debug.WriteLine($"[ModelManager] Resolving wildcard pattern {modelName} for provider {provider}");

            // Extract the prefix (part before the *)
            var wildcardPrefix = modelName.Replace("*", "");
            
            // Find all non-wildcard models from the same provider that match the prefix
            var matchingModels = this.Models.Values
                .Where(m => m != null &&
                            string.Equals(m.Provider, provider, System.StringComparison.OrdinalIgnoreCase) &&
                            !m.Model.Contains("*") && // Exclude wildcard patterns
                            m.Model.ToLower().StartsWith(wildcardPrefix.ToLower()))
                .OrderBy(m => m.Model) // Consistent ordering
                .ToList();

            if (matchingModels.Any())
            {
                var resolvedModel = matchingModels.First().Model;
                Debug.WriteLine($"[ModelManager] Resolved {modelName} to {resolvedModel}");
                return resolvedModel;
            }

            Debug.WriteLine($"[ModelManager] Could not resolve wildcard {modelName}, returning as-is");
            return modelName;  // Fallback to original if no match found
        }

        /// <summary>
        /// Gets the default model for a provider and specific capability.
        /// First looks for concrete model names, then falls back to wildcard patterns.
        /// Prioritizes exact capability matches, then compatible models.
        /// </summary>
        /// <param name="provider">The provider name.</param>
        /// <param name="requiredCapability">The required capability.</param>
        /// <returns>The default model name or null if none found.</returns>
        public string GetDefaultModel(string provider, AICapability requiredCapability = AICapability.BasicChat)
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

            // PRIORITY 1: Concrete model names with exact capability match
            var concreteExactModel = providerModels
                .Where(m => !m.Model.Contains("*"))  // Concrete names only
                .FirstOrDefault(m => (m.Default & requiredCapability) == requiredCapability);

            if (concreteExactModel != null)
            {
                Debug.WriteLine($"[ModelManager] Found concrete exact default model {concreteExactModel.Model} for {provider} with capability {requiredCapability.ToDetailedString()}");
                return concreteExactModel.Model;
            }

            // PRIORITY 2: Concrete model names with compatible capability
            var concreteCompatibleModel = providerModels
                .Where(m => !m.Model.Contains("*"))  // Concrete names only
                .Where(m => m.Default != AICapability.None && m.HasCapability(requiredCapability))
                .FirstOrDefault();

            if (concreteCompatibleModel != null)
            {
                Debug.WriteLine($"[ModelManager] Found concrete compatible default model {concreteCompatibleModel.Model} for {provider} with capability {requiredCapability.ToDetailedString()}");
                return concreteCompatibleModel.Model;
            }

            // PRIORITY 3: Wildcard patterns with exact capability match (resolve to concrete names)
            var wildcardExactModel = providerModels
                .Where(m => m.Model.Contains("*"))  // Wildcard patterns only
                .FirstOrDefault(m => (m.Default & requiredCapability) == requiredCapability);

            if (wildcardExactModel != null)
            {
                Debug.WriteLine($"[ModelManager] Found wildcard exact default model {wildcardExactModel.Model} for {provider} with capability {requiredCapability.ToDetailedString()}, attempting resolution");
                return ResolveModelName(wildcardExactModel.Model, provider);
            }

            // PRIORITY 4: Wildcard patterns with compatible capability (resolve to concrete names)
            var wildcardCompatibleModel = providerModels
                .Where(m => m.Model.Contains("*"))  // Wildcard patterns only
                .Where(m => m.Default != AICapability.None && m.HasCapability(requiredCapability))
                .FirstOrDefault();

            if (wildcardCompatibleModel != null)
            {
                Debug.WriteLine($"[ModelManager] Found wildcard compatible default model {wildcardCompatibleModel.Model} for {provider} with capability {requiredCapability.ToDetailedString()}, attempting resolution");
                return ResolveModelName(wildcardCompatibleModel.Model, provider);
            }

            Debug.WriteLine($"[ModelManager] No default model found for {provider} with capability {requiredCapability.ToDetailedString()}");
            return null;
        }
    }
}
