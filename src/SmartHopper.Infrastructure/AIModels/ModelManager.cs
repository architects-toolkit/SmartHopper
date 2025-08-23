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
using SmartHopper.Infrastructure.AIModels;

namespace SmartHopper.Infrastructure.AIModels
{
    /// <summary>
    /// Unified manager for AI model capabilities with persistent storage.
    /// Combines model discovery, capability management, and validation in a single service.
    /// </summary>
    public class ModelManager
    {
        private static readonly Lazy<ModelManager> _instance = new Lazy<ModelManager>(() => new ModelManager());
        private AIModelCapabilityRegistry _registry;
        private readonly object _syncRoot = new object();

        /// <summary>
        /// Gets the singleton instance of the ModelManager.
        /// </summary>
        public static ModelManager Instance => _instance.Value;

        /// <summary>
        /// Initializes a new instance of the ModelManager.
        /// </summary>
        private ModelManager()
        {
            this._registry = new AIModelCapabilityRegistry();
            Debug.WriteLine("[ModelManager] Initialized with new capability registry");
        }

        #region Capability Management

        /// <summary>
        /// Registers model capabilities for a specific provider and model.
        /// This is the primary method for providers to register their model capabilities.
        /// </summary>
        /// <param name="provider">The provider name.</param>
        /// <param name="modelName">The model name.</param>
        /// <param name="capabilities">The model capabilities.</param>
        /// <param name="defaultFor">The capabilities for which this model should be the default.</param>
        public void RegisterCapabilities(
            string provider,
            string modelName,
            AICapability capabilities,
            AICapability defaultFor = AICapability.None)
        {
            if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(modelName))
            {
                Debug.WriteLine($"[ModelManager] Invalid provider or model name provided for registration");
                return;
            }

            var AIModelCapabilities = new AIModelCapabilities
            {
                Provider = provider.ToLower(),
                Model = modelName,
                Capabilities = capabilities,
                Default = defaultFor,
            };

            this.SetCapabilities(AIModelCapabilities);
        }

        /// <summary>
        /// Sets the capability information for a specific model.
        /// </summary>
        /// <param name="AIModelCapabilities">The model capabilities to store.</param>
        public void SetCapabilities(AIModelCapabilities AIModelCapabilities)
        {
            if (AIModelCapabilities == null) return;

            lock (_syncRoot)
            {
                this._registry.SetCapabilities(AIModelCapabilities);
            }
            Debug.WriteLine($"[ModelManager] Registered capabilities for {AIModelCapabilities.Provider}.{AIModelCapabilities.Model}");
        }

        /// <summary>
        /// Marks a model as the default for the given capabilities. When exclusive is true (default),
        /// clears those capabilities from Default on other models of the same provider to ensure
        /// at most one default per capability.
        /// </summary>
        /// <param name="provider">Provider name.</param>
        /// <param name="model">Model name.</param>
        /// <param name="caps">Capabilities to set as default for this model.</param>
        /// <param name="exclusive">If true, unsets these defaults from other models of the provider.</param>
        public void SetDefault(string provider, string model, AICapability caps, bool exclusive = true)
        {
            if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(model) || caps == AICapability.None)
            {
                return;
            }

            var normalizedProvider = provider.ToLower();

            lock (_syncRoot)
            {
                // Optionally clear the bits from other models in the same provider
                if (exclusive)
                {
                    var all = this.GetProviderModels(normalizedProvider);
                    foreach (var m in all)
                    {
                        if (!string.Equals(m.Model, model, StringComparison.Ordinal))
                        {
                            var before = m.Default;
                            m.Default &= ~caps; // clear the specified capability bits
                            if (m.Default != before)
                            {
                                this._registry.SetCapabilities(m);
                            }
                        }
                    }
                }

                // Ensure the target model exists (create or update)
                var target = this.GetCapabilities(normalizedProvider, model);
                if (target == null)
                {
                    target = new AIModelCapabilities
                    {
                        Provider = normalizedProvider,
                        Model = model,
                        Capabilities = AICapability.None,
                        Default = AICapability.None,
                    };
                }

                target.Default |= caps;
                this._registry.SetCapabilities(target);
            }
            Debug.WriteLine($"[ModelManager] Set default for {normalizedProvider}.{model} -> {caps}");
        }

        /// <summary>
        /// Gets the capability information for a specific model.
        /// </summary>
        /// <param name="provider">The provider name.</param>
        /// <param name="model">The model name.</param>
        /// <returns>Model capabilities or null if not found.</returns>
        public AIModelCapabilities GetCapabilities(string provider, string model)
        {
            return this._registry.GetCapabilities(provider, model);
        }

        /// <summary>
        /// Gets the default model for a provider and specific capability.
        /// </summary>
        /// <param name="provider">The provider name.</param>
        /// <param name="requiredCapability">The required capability.</param>
        /// <returns>The default model name or null if none found.</returns>
        public string GetDefaultModel(string provider, AICapability requiredCapability)
        {
            if (string.IsNullOrWhiteSpace(provider)) return string.Empty;

            // Snapshot of provider models
            var providerModels = this.GetProviderModels(provider);
            if (providerModels.Count == 0) return string.Empty;

            // Exact defaults for the capability
            var exact = providerModels
                .Where(m => (m.Default & requiredCapability) == requiredCapability)
                .OrderByDescending(m => m.Verified)
                .ThenByDescending(m => m.Rank)
                .ThenBy(m => m.Deprecated)
                .ThenBy(m => m.Model, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (exact != null) return exact.Model;

            // Any default that is compatible
            var compatible = providerModels
                .Where(m => m.Default != AICapability.None && m.HasCapability(requiredCapability))
                .OrderByDescending(m => m.Verified)
                .ThenByDescending(m => m.Rank)
                .ThenBy(m => m.Deprecated)
                .ThenBy(m => m.Model, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (compatible != null) return compatible.Model;

            return string.Empty;
        }

        /// <summary>
        /// Checks if a provider has any registered model capabilities.
        /// </summary>
        /// <param name="provider">The provider name to check.</param>
        /// <returns>True if the provider has any registered capabilities.</returns>
        public bool HasProviderCapabilities(string provider)
        {
            if (string.IsNullOrWhiteSpace(provider))
                return false;

            return this._registry.Models.Keys.Any(key => key.StartsWith($"{provider}.", StringComparison.OrdinalIgnoreCase));
        }

        #endregion

        #region Model Listing and Selection

        /// <summary>
        /// Returns all registered models for a provider.
        /// </summary>
        public List<AIModelCapabilities> GetProviderModels(string provider)
        {
            if (string.IsNullOrWhiteSpace(provider)) return new List<AIModelCapabilities>();

            var models = this._registry.Models.Values
                .Where(m => m != null && string.Equals(m.Provider, provider, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return models;
        }

        /// <summary>
        /// DO NOT CALL THIS DIRECTLY. USE AIProvider.SelectModel() INSTEAD.
        /// Centralized model selection and fallback.
        /// - If user specified a model:
        ///   * If known and capable -> use it.
        ///   * If unknown -> allow it to pass through.
        ///   * If known but not capable -> fallback.
        /// - If no user model or fallback needed:
        ///   * Prefer preferredDefault when capable.
        ///   * Then provider defaults with exact capability.
        ///   * Then other defaults compatible with capability.
        ///   * Then other registered models ordered by Verified, Rank, and not Deprecated.
        /// </summary>
        /// <param name="provider">Provider name.</param>
        /// <param name="userModel">User-specified model (optional).</param>
        /// <param name="requiredCapability">Required capability flags.</param>
        /// <param name="preferredDefault">A preferred default (e.g., settings default) to try first when falling back.</param>
        /// <returns>Selected model name or empty when none.</returns>
        public string SelectBestModel(string provider, string userModel, AICapability requiredCapability, string preferredDefault = null)
        {
            if (string.IsNullOrWhiteSpace(provider) || requiredCapability == AICapability.None)
            {
                Debug.WriteLine($"[ModelManager.SelectBestModel] Invalid args -> provider='{provider}', requiredCapability='{requiredCapability.ToDetailedString()}'");
                return string.Empty;
            }

            Debug.WriteLine($"[ModelManager.SelectBestModel] provider='{provider}', userModel='{userModel}', required='{requiredCapability.ToDetailedString()}', preferredDefault='{preferredDefault}'");

            // If user specified a model, validate capability if known; allow unknown to pass
            if (!string.IsNullOrWhiteSpace(userModel))
            {
                var known = this.GetCapabilities(provider, userModel);
                if (known == null)
                {
                    // Unknown model -> pass through
                    Debug.WriteLine($"[ModelManager.SelectBestModel] User model '{userModel}' is unknown for provider '{provider}'. Passing through without override.");
                    return userModel;
                }

                if (known.HasCapability(requiredCapability))
                {
                    // Known and capable
                    Debug.WriteLine($"[ModelManager.SelectBestModel] Using user model '{userModel}' (known and capable: {known.Capabilities.ToDetailedString()})");
                    return userModel;
                }
                // else: fall through to fallback selection
                Debug.WriteLine($"[ModelManager.SelectBestModel] User model '{userModel}' is known but NOT capable of {requiredCapability.ToDetailedString()} (has {known.Capabilities.ToDetailedString()}). Falling back.");
            }

            // Build candidate list of models that support the capability
            var candidates = this.GetProviderModels(provider)
                .Where(m => m.HasCapability(requiredCapability))
                .ToList();

            // Try preferred default first if it is capable
            if (!string.IsNullOrWhiteSpace(preferredDefault))
            {
                var preferredCaps = this.GetCapabilities(provider, preferredDefault);
                if (preferredCaps != null && preferredCaps.HasCapability(requiredCapability))
                {
                    Debug.WriteLine($"[ModelManager.SelectBestModel] Using preferredDefault '{preferredDefault}' (capable: {preferredCaps.Capabilities.ToDetailedString()})");
                    return preferredDefault;
                }
                else
                {
                    Debug.WriteLine($"[ModelManager.SelectBestModel] preferredDefault '{preferredDefault}' is not capable or unknown. Continuing selection.");
                }
            }

            if (candidates.Count == 0)
            {
                Debug.WriteLine($"[ModelManager.SelectBestModel] No candidates found for provider '{provider}' with capability {requiredCapability.ToDetailedString()}.");
                return string.Empty;
            }

            // 1) Defaults with exact capability
            var defaultExact = candidates
                .Where(m => (m.Default & requiredCapability) == requiredCapability)
                .OrderByDescending(m => m.Verified)
                .ThenByDescending(m => m.Rank)
                .ThenBy(m => m.Deprecated)
                .ThenBy(m => m.Model, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (defaultExact != null)
            {
                Debug.WriteLine($"[ModelManager.SelectBestModel] Selecting provider default (exact) '{defaultExact.Model}'.");
                return defaultExact.Model;
            }

            // 2) Other defaults that are compatible
            var defaultCompatible = candidates
                .Where(m => m.Default != AICapability.None)
                .OrderByDescending(m => m.Verified)
                .ThenByDescending(m => m.Rank)
                .ThenBy(m => m.Deprecated)
                .ThenBy(m => m.Model, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (defaultCompatible != null)
            {
                Debug.WriteLine($"[ModelManager.SelectBestModel] Selecting provider default (compatible) '{defaultCompatible.Model}'.");
                return defaultCompatible.Model;
            }

            // 3) Any other registered model, ordered by quality
            var best = candidates
                .OrderByDescending(m => m.Verified)
                .ThenByDescending(m => m.Rank)
                .ThenBy(m => m.Deprecated)
                .ThenBy(m => m.Model, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (best != null)
            {
                Debug.WriteLine($"[ModelManager.SelectBestModel] Selecting best available '{best.Model}'.");
                return best.Model;
            }

            Debug.WriteLine($"[ModelManager.SelectBestModel] Failed to select a model; returning empty.");
            return string.Empty;
        }

        #endregion

        #region Tool Validation

        /// <summary>
        /// Validates if a model has the required capabilities.
        /// </summary>
        /// <param name="provider">The provider name.</param>
        /// <param name="model">The model name.</param>
        /// <param name="requiredCapability">The required capability.</param>
        /// <returns>True if the model has the required capabilities.</returns>
        public bool ValidateCapabilities(string provider, string model, AICapability requiredCapability)
        {
            var capabilities = this.GetCapabilities(provider, model);
            if (capabilities == null)
            {
                // Do not pass validation if model is unregistered
                Debug.WriteLine($"[ModelManager] Model '{model}' from '{provider}' not registered");
                return false;
            }

            Debug.WriteLine($"[ModelManager] Model '{model}' from '{provider}' has capabilities {capabilities.Capabilities.ToDetailedString()}");
            return capabilities.HasCapability(requiredCapability);
        }

        #endregion
    }
}
