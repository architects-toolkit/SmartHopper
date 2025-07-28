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
using SmartHopper.Infrastructure.Models;

namespace SmartHopper.Infrastructure.Managers.ModelManager
{
    /// <summary>
    /// In-memory manager for model capabilities across all AI providers.
    /// Data is lost when the application closes.
    /// </summary>
    public class ModelsManager
    {
        private static readonly Lazy<ModelsManager> _instance = new Lazy<ModelsManager>(() => new ModelsManager());
        private ModelCapabilityRegistry _registry;

        /// <summary>
        /// Gets the singleton instance of the ModelsManager.
        /// </summary>
        public static ModelsManager Instance => _instance.Value;

        /// <summary>
        /// Initializes a new instance of the ModelsManager.
        /// </summary>
        private ModelsManager()
        {
            _registry = new ModelCapabilityRegistry();
            Debug.WriteLine("[ModelsManager] Initialized in-memory model capability registry");
        }

        /// <summary>
        /// Sets the capability information for a specific model.
        /// </summary>
        /// <param name="modelCapabilities">The model capabilities to store.</param>
        public void SetCapabilities(ModelCapabilities modelCapabilities)
        {
            if (modelCapabilities == null) return;
            
            _registry.SetCapabilities(modelCapabilities);
            Debug.WriteLine($"[ModelsManager] Stored capabilities for {modelCapabilities.Provider}.{modelCapabilities.Model}");
        }

        /// <summary>
        /// Checks if a specific model supports the required capabilities.
        /// </summary>
        /// <param name="provider">The provider name.</param>
        /// <param name="model">The model name to check.</param>
        /// <param name="requiredCapabilities">The required capabilities.</param>
        /// <returns>True if the model supports all required capabilities.</returns>
        public bool SupportsCapabilities(string provider, string model, params ModelCapability[] requiredCapabilities)
        {
            var capabilities = GetCapabilities(provider, model);
            return capabilities?.HasAllCapabilities(requiredCapabilities) ?? false;
        }

        /// <summary>
        /// Gets the capability information for a specific model.
        /// </summary>
        /// <param name="provider">The provider name.</param>
        /// <param name="model">The model name.</param>
        /// <returns>Model capabilities or null if not found.</returns>
        public ModelCapabilities GetCapabilities(string provider, string model)
        {
            return _registry.GetCapabilities(provider, model);
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
        public ToolCapabilityValidationResult ValidateToolExecution(string toolName, string provider, string model)
        {
            var capabilities = GetCapabilities(provider, model);
            if (capabilities == null)
            {
                // Soft validation: Allow unregistered models to proceed
                Debug.WriteLine($"[ModelsManager] Model '{model}' from '{provider}' not registered - allowing execution (soft validation)");
                return new ToolCapabilityValidationResult(true, "");
            }

            var requiredCapabilities = GetRequiredCapabilitiesForTool(toolName);
            if (requiredCapabilities.Length == 0)
            {
                return new ToolCapabilityValidationResult(true, ""); // No specific requirements
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
                return new ToolCapabilityValidationResult(false, 
                    $"Model '{model}' does not support required capabilities for tool '{toolName}': {string.Join(", ", missingCapabilities)}");
            }

            return new ToolCapabilityValidationResult(true, "");
        }

        /// <summary>
        /// Gets all models that support the specified capabilities.
        /// </summary>
        /// <param name="requiredCapabilities">The required capabilities.</param>
        /// <returns>List of compatible models.</returns>
        public List<ModelCapabilities> FindCompatibleModels(params ModelCapability[] requiredCapabilities)
        {
            return _registry.FindModelsWithCapabilities(requiredCapabilities);
        }

        /// <summary>
        /// Gets the required capabilities for a specific tool from the tool registry.
        /// </summary>
        private ModelCapability[] GetRequiredCapabilitiesForTool(string toolName)
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
                Debug.WriteLine($"[ModelsManager] Error getting tool capabilities for {toolName}: {ex.Message}");
            }

            // Default: no specific requirements if tool not found
            return new ModelCapability[0];
        }
    }

    /// <summary>
    /// Result of tool capability validation.
    /// </summary>
    public class ToolCapabilityValidationResult
    {
        public bool IsValid { get; }
        public string ErrorMessage { get; }

        public ToolCapabilityValidationResult(bool isValid, string errorMessage)
        {
            IsValid = isValid;
            ErrorMessage = errorMessage ?? "";
        }
    }
}
