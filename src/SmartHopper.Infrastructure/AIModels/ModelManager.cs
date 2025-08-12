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

        /// <summary>
        /// Gets the current capability registry.
        /// </summary>
        public AIModelCapabilityRegistry Registry => _registry;

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

            this._registry.SetCapabilities(AIModelCapabilities);
            Debug.WriteLine($"[ModelManager] Registered capabilities for {AIModelCapabilities.Provider}.{AIModelCapabilities.Model}");
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
            return this._registry.GetDefaultModel(provider, requiredCapability);
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
