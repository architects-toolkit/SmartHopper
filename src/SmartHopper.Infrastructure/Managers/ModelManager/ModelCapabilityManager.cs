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
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SmartHopper.Infrastructure.Managers.AIProviders;
using SmartHopper.Infrastructure.Models;
using SmartHopper.Infrastructure.Settings;

namespace SmartHopper.Infrastructure.Managers.ModelManager
{
    /// <summary>
    /// Manages model capabilities for all AI providers, including loading, updating, and validation.
    /// </summary>
    public class ModelCapabilityManager
    {
        private static readonly Lazy<ModelCapabilityManager> _instance = new Lazy<ModelCapabilityManager>(() => new ModelCapabilityManager());
        private ModelCapabilityRegistry _registry;
        private readonly string _capabilitiesFilePath;

        /// <summary>
        /// Gets the singleton instance of the ModelCapabilityManager.
        /// </summary>
        public static ModelCapabilityManager Instance => _instance.Value;

        /// <summary>
        /// Initializes a new instance of the ModelCapabilityManager.
        /// </summary>
        private ModelCapabilityManager()
        {
            _capabilitiesFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Grasshopper", "SmartHopperCapabilites.json");
            this.LoadCapabilities();
        }

        /// <summary>
        /// Gets the current capability registry.
        /// </summary>
        public ModelCapabilityRegistry Registry => _registry;

        /// <summary>
        /// Loads capabilities from the local file, falling back to default capabilities if the file doesn't exist.
        /// Then automatically updates capabilities for all registered providers.
        /// </summary>
        private void LoadCapabilities()
        {
            try
            {
                if (File.Exists(_capabilitiesFilePath))
                {
                    var json = File.ReadAllText(_capabilitiesFilePath);
                    _registry = JsonConvert.DeserializeObject<ModelCapabilityRegistry>(json) ?? new ModelCapabilityRegistry();
                    Debug.WriteLine($"[ModelCapabilityManager] Loaded capabilities for {_registry.Models.Count} models");
                }
                else
                {
                    Debug.WriteLine("[ModelCapabilityManager] Capabilities file not found, creating empty registry");
                    _registry = new ModelCapabilityRegistry();
                    SaveCapabilities();
                }

                // Update capabilities from all registered providers
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var providers = ProviderManager.Instance.GetProviders();
                        foreach (var provider in providers)
                        {
                            try
                            {
                                Debug.WriteLine($"[ModelCapabilityManager] Updating capabilities for provider: {provider.Name}");
                                await provider.Models.UpdateCapabilities();
                            }
                            catch (Exception providerEx)
                            {
                                Debug.WriteLine($"[ModelCapabilityManager] Error updating capabilities for {provider.Name}: {providerEx.Message}");
                            }
                        }
                        Debug.WriteLine("[ModelCapabilityManager] Completed capability updates for all providers");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[ModelCapabilityManager] Error during provider capability updates: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ModelCapabilityManager] Error loading capabilities: {ex.Message}");
                _registry = new ModelCapabilityRegistry();
            }
        }

        /// <summary>
        /// Saves the current capability registry to the local file.
        /// </summary>
        public void SaveCapabilities()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_capabilitiesFilePath));
                var json = JsonConvert.SerializeObject(_registry, Formatting.Indented);
                File.WriteAllText(_capabilitiesFilePath, json);
                Debug.WriteLine($"[ModelCapabilityManager] Saved capabilities for {_registry.Models.Count} models");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ModelCapabilityManager] Error saving capabilities: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets capabilities for a specific model.
        /// </summary>
        /// <param name="provider">The provider name.</param>
        /// <param name="model">The model name.</param>
        /// <returns>Model capabilities or null if not found.</returns>
        public ModelCapabilities GetCapabilities(string provider, string model)
        {
            return _registry.GetCapabilities(provider, model);
        }

        /// <summary>
        /// Checks if a model supports the required capabilities.
        /// </summary>
        /// <param name="provider">The provider name.</param>
        /// <param name="model">The model name.</param>
        /// <param name="requiredCapabilities">The required capabilities.</param>
        /// <returns>True if the model supports all required capabilities.</returns>
        public bool SupportsCapabilities(string provider, string model, params ModelCapability[] requiredCapabilities)
        {
            var capabilities = GetCapabilities(provider, model);
            return capabilities?.HasAllCapabilities(requiredCapabilities) ?? false;
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
                Debug.WriteLine($"[ModelCapabilityManager] Model '{model}' from '{provider}' not registered - allowing execution (soft validation)");
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
                Debug.WriteLine($"[ModelCapabilityManager] Error getting tool capabilities for {toolName}: {ex.Message}");
            }

            // Default: no specific requirements if tool not found
            return new ModelCapability[0];
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
