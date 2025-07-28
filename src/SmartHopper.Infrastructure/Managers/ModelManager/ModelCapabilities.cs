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
using Newtonsoft.Json;

namespace SmartHopper.Infrastructure.Managers.ModelManager
{
    /// <summary>
    /// Defines the capabilities that an AI model can support.
    /// </summary>
    [Flags]
    public enum ModelCapability
    {
        None = 0,
        
        // Input capabilities
        TextInput = 1 << 0,
        ImageInput = 1 << 1,
        AudioInput = 1 << 2,
        
        // Output capabilities
        TextOutput = 1 << 3,
        ImageOutput = 1 << 4,
        AudioOutput = 1 << 5,
        
        // Advanced capabilities
        FunctionCalling = 1 << 6,
        StructuredOutput = 1 << 7,
        Reasoning = 1 << 8,
        
        // Composite capabilities for convenience
        BasicChat = TextInput | TextOutput,
        AdvancedChat = BasicChat | FunctionCalling,
        MultiModal = TextInput | TextOutput | ImageInput,
        All = TextInput | ImageInput | AudioInput | TextOutput | ImageOutput | AudioOutput | FunctionCalling | StructuredOutput | Reasoning
    }

    /// <summary>
    /// Represents the capabilities and metadata of a specific AI model.
    /// </summary>
    public class ModelCapabilities
    {
        /// <summary>
        /// The model identifier/name.
        /// </summary>
        [JsonProperty("model")]
        public string Model { get; set; }

        /// <summary>
        /// The provider name that owns this model.
        /// </summary>
        [JsonProperty("provider")]
        public string Provider { get; set; }

        /// <summary>
        /// Bitwise flags representing the model's capabilities.
        /// </summary>
        [JsonProperty("capabilities")]
        public ModelCapability Capabilities { get; set; }

        /// <summary>
        /// Maximum context length in tokens.
        /// </summary>
        [JsonProperty("max_context_length")]
        public int MaxContextLength { get; set; }

        /// <summary>
        /// Whether the model is deprecated.
        /// </summary>
        [JsonProperty("deprecated")]
        public bool IsDeprecated { get; set; }

        /// <summary>
        /// Optional deprecation date.
        /// </summary>
        [JsonProperty("deprecation_date")]
        public DateTime? DeprecationDate { get; set; }

        /// <summary>
        /// Replacement model if deprecated.
        /// </summary>
        [JsonProperty("replacement_model")]
        public string ReplacementModel { get; set; }

        /// <summary>
        /// When this capability information was last updated.
        /// </summary>
        [JsonProperty("last_updated")]
        public DateTime LastUpdated { get; set; }

        /// <summary>
        /// Checks if the model has the specified capability.
        /// </summary>
        /// <param name="capability">The capability to check for.</param>
        /// <returns>True if the model supports the capability.</returns>
        public bool HasCapability(ModelCapability capability)
        {
            return (Capabilities & capability) == capability;
        }

        /// <summary>
        /// Checks if the model has all the specified capabilities.
        /// </summary>
        /// <param name="requiredCapabilities">The capabilities to check for.</param>
        /// <returns>True if the model supports all required capabilities.</returns>
        public bool HasAllCapabilities(params ModelCapability[] requiredCapabilities)
        {
            foreach (var capability in requiredCapabilities)
            {
                if (!HasCapability(capability))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Gets a human-readable list of capabilities.
        /// </summary>
        /// <returns>List of capability names.</returns>
        public List<string> GetCapabilityNames()
        {
            var names = new List<string>();
            foreach (ModelCapability capability in Enum.GetValues(typeof(ModelCapability)))
            {
                if (capability != ModelCapability.None && HasCapability(capability))
                {
                    names.Add(capability.ToString());
                }
            }
            return names;
        }
    }

    /// <summary>
    /// Registry containing capability information for all known models.
    /// </summary>
    public class ModelCapabilityRegistry
    {
        /// <summary>
        /// Dictionary mapping model names to their capabilities.
        /// Key format: "provider:model" (e.g., "openai:gpt-4", "mistral:mistral-large")
        /// </summary>
        [JsonProperty("models")]
        public Dictionary<string, ModelCapabilities> Models { get; set; } = new Dictionary<string, ModelCapabilities>();

        /// <summary>
        /// When this registry was last updated.
        /// </summary>
        [JsonProperty("last_updated")]
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets capabilities for a specific model.
        /// </summary>
        /// <param name="provider">The provider name.</param>
        /// <param name="model">The model name.</param>
        /// <returns>Model capabilities or null if not found.</returns>
        public ModelCapabilities GetCapabilities(string provider, string model)
        {
            var key = $"{provider.ToLower()}:{model.ToLower()}";
            return Models.TryGetValue(key, out var capabilities) ? capabilities : null;
        }

        /// <summary>
        /// Adds or updates capabilities for a specific model.
        /// </summary>
        /// <param name="capabilities">The model capabilities to add/update.</param>
        public void SetCapabilities(ModelCapabilities capabilities)
        {
            var key = $"{capabilities.Provider.ToLower()}:{capabilities.Model.ToLower()}";
            capabilities.LastUpdated = DateTime.UtcNow;
            Models[key] = capabilities;
            LastUpdated = DateTime.UtcNow;
        }

        /// <summary>
        /// Gets all models that support the specified capabilities.
        /// </summary>
        /// <param name="requiredCapabilities">The required capabilities.</param>
        /// <returns>List of matching model capabilities.</returns>
        public List<ModelCapabilities> FindModelsWithCapabilities(params ModelCapability[] requiredCapabilities)
        {
            var matches = new List<ModelCapabilities>();
            foreach (var modelCapability in Models.Values)
            {
                if (modelCapability.HasAllCapabilities(requiredCapabilities))
                {
                    matches.Add(modelCapability);
                }
            }
            return matches;
        }
    }
}
