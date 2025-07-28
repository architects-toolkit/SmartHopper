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
using SmartHopper.Infrastructure.Managers.AIProviders;

namespace SmartHopper.Infrastructure.Managers.ModelManager
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
        /// <param name="maxContextLength">Maximum context length in tokens.</param>
        /// <param name="isDeprecated">Whether the model is deprecated.</param>
        /// <param name="replacementModel">Replacement model if deprecated.</param>
        public void RegisterCapabilities(
            string provider,
            string modelName,
            AIModelCapability capabilities,
            int maxContextLength = 4096,
            bool isDeprecated = false,
            string replacementModel = null)
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
                MaxContextLength = maxContextLength,
                IsDeprecated = isDeprecated,
                ReplacementModel = replacementModel
            };

            SetCapabilities(AIModelCapabilities);
        }

        /// <summary>
        /// Sets the capability information for a specific model.
        /// </summary>
        /// <param name="AIModelCapabilities">The model capabilities to store.</param>
        public void SetCapabilities(AIModelCapabilities AIModelCapabilities)
        {
            if (AIModelCapabilities == null) return;

            _registry.SetCapabilities(AIModelCapabilities);
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
            return _registry.GetCapabilities(provider, model);
        }

        /// <summary>
        /// Checks if a specific model supports the required capabilities.
        /// </summary>
        /// <param name="provider">The provider name.</param>
        /// <param name="model">The model name to check.</param>
        /// <param name="requiredCapabilities">The required capabilities.</param>
        /// <returns>True if the model supports all required capabilities.</returns>
        public bool SupportsCapabilities(string provider, string model, params AIModelCapability[] requiredCapabilities)
        {
            var capabilities = GetCapabilities(provider, model);
            return capabilities?.HasAllCapabilities(requiredCapabilities) ?? false;
        }

        /// <summary>
        /// Gets all models that support the specified capabilities.
        /// </summary>
        /// <param name="requiredCapabilities">The required capabilities.</param>
        /// <returns>List of compatible models.</returns>
        public List<AIModelCapabilities> FindCompatibleModels(params AIModelCapability[] requiredCapabilities)
        {
            return _registry.FindModelsWithCapabilities(requiredCapabilities);
        }

        #endregion

        #region Tool Validation

        /// <summary>
        /// Validates if a tool can be executed with the given model using soft validation.
        /// Only blocks execution if model is registered but lacks required capabilities.
        /// Unregistered models are allowed to proceed (unknown capabilities).
        /// </summary>
        /// <param name="toolName">The name of the tool.</param>
        /// <param name="provider">The provider name.</param>
        /// <param name="model">The model name.</param>
        /// <returns>Validation result with error message if invalid.</returns>
        public bool ValidateToolExecution(string toolName, string provider, string model)
        {
            var capabilities = GetCapabilities(provider, model);
            if (capabilities == null)
            {
                // Soft validation: Allow unregistered models to proceed
                Debug.WriteLine($"[ModelManager] Model '{model}' from '{provider}' not registered - allowing execution (soft validation)");
                return true;
            }

            var requiredCapabilities = GetRequiredCapabilitiesForTool(toolName);
            if (requiredCapabilities.Length == 0)
            {
                return true; // No specific requirements
            }

            var missingCapabilities = new List<string>();
            foreach (var required in requiredCapabilities)
            {
                if (!capabilities.HasCapability(required))
                {
                    missingCapabilities.Add(required.ToString());
                }
            }

            if (missingCapabilities.Count > 0)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validates if a tool can be executed with the given model using soft validation.
        /// Only blocks execution if model is registered but lacks required capabilities.
        /// Unregistered models are allowed to proceed (unknown capabilities).
        /// </summary>
        /// <param name="toolName">The name of the tool.</param>
        /// <param name="provider">The provider name.</param>
        /// <param name="model">The model name.</param>
        /// <returns>Validation result with error message if invalid.</returns>
        public bool ValidateToolExecution(string toolName, AIProvider provider, string model)
        {
            var providerName = provider.Name;
            return this.ValidateToolExecution(toolName, providerName, model);
        }

        /// <summary>
        /// Gets the required capabilities for a specific tool from the tool registry.
        /// </summary>
        private AIModelCapability[] GetRequiredCapabilitiesForTool(string toolName)
        {
            try
            {
                var tools = AITools.AIToolManager.GetTools();
                if (tools.TryGetValue(toolName, out var tool))
                {
                    return tool.RequiredCapabilities;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ModelManager] Error getting tool capabilities for {toolName}: {ex.Message}");
            }

            // Default: no specific requirements if tool not found
            return new AIModelCapability[0];
        }

        #endregion
    }
}
