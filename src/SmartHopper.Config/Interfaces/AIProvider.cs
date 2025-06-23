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
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Config.Managers;
using SmartHopper.Config.Models;

namespace SmartHopper.Config.Interfaces
{
    public interface IAIProvider
    {
        string Name { get; }

        string DefaultModel { get; }

        /// <summary>
        /// Gets the provider's icon. Should return a 16x16 image suitable for display in the UI.
        /// </summary>
        Image Icon { get; }

        /// <summary>
        /// Gets or sets whether this provider is enabled and should be available for use.
        /// This can be used to disable template or experimental providers.
        /// </summary>
        bool IsEnabled { get; }

        Task<AIResponse> GetResponse(JArray messages, string model, string jsonSchema = "", string endpoint = "", bool includeToolDefinitions = false);

        string GetModel(Dictionary<string, object> settings, string requestedModel = "");

        /// <summary>
        /// Injects decrypted settings for this provider (called by ProviderManager).
        /// </summary>
        void InitializeSettings(Dictionary<string, object> settings);

        IEnumerable<SettingDescriptor> GetSettingDescriptors();
    }

    /// <summary>
    /// Base class for AI providers, encapsulating common logic.
    /// </summary>
    public abstract class AIProvider : IAIProvider
    {
        private Dictionary<string, object> _injectedSettings;

        public abstract string Name { get; }

        public abstract string DefaultModel { get; }

        public abstract bool IsEnabled { get; }

        public abstract Image Icon { get; }

        public abstract Task<AIResponse> GetResponse(JArray messages, string model, string jsonSchema = "", string endpoint = "", bool includeToolDefinitions = false);

        /// <summary>
        /// Initializes the provider with the specified settings.
        /// </summary>
        /// <param name="settings">The decrypted settings to use.</param>
        public void InitializeSettings(Dictionary<string, object> settings)
        {
            _injectedSettings = settings ?? new Dictionary<string, object>();
        }

        /// <summary>
        /// Gets a setting value, with type conversion and fallback to default.
        /// </summary>
        /// <typeparam name="T">The expected type of the setting.</typeparam>
        /// <param name="key">The setting key.</param>
        /// <returns>The setting value, or default if not found.</returns>
        protected T GetSetting<T>(string key)
        {
            if (_injectedSettings == null)
            {
                Debug.WriteLine($"Warning: Settings not initialized for {Name}");
                return default;
            }

            if (!_injectedSettings.TryGetValue(key, out var value) || value == null)
            {
                // Try to get the default value from the descriptor
                var descriptor = GetSettingDescriptors().FirstOrDefault(d => d.Name == key);
                if (descriptor?.DefaultValue != null && descriptor.DefaultValue is T defaultValue)
                {
                    return defaultValue;
                }
                return default;
            }

            // Handle type conversion
            try
            {
                if (value is T typedValue)
                {
                    return typedValue;
                }
                else if (typeof(T) == typeof(int) && int.TryParse(value.ToString(), out var intValue))
                {
                    return (T)(object)intValue;
                }
                else if (typeof(T) == typeof(bool) && bool.TryParse(value.ToString(), out var boolValue))
                {
                    return (T)(object)boolValue;
                }
                else if (typeof(T) == typeof(string))
                {
                    return (T)(object)value.ToString();
                }
                else if (typeof(T) == typeof(double) && double.TryParse(value.ToString(), out var doubleValue))
                {
                    return (T)(object)doubleValue;
                }
                else
                {
                    Debug.WriteLine($"Warning: Failed to convert {key} to {typeof(T).Name} for provider {Name}");
                    return default;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting setting {key} for provider {Name}: {ex.Message}");
                return default;
            }
        }

        /// <summary>
        /// Default model resolution logic.
        /// </summary>
        public virtual string GetModel(Dictionary<string, object> settings, string requestedModel = "")
        {
            if (!string.IsNullOrWhiteSpace(requestedModel))
                return requestedModel;
            if (settings != null && settings.ContainsKey("Model") && !string.IsNullOrWhiteSpace(settings["Model"]?.ToString()))
                return settings["Model"].ToString();
            return DefaultModel;
        }

        /// <summary>
        /// Common tool formatting for function definitions.
        /// </summary>
        protected JArray GetFormattedTools()
        {
            try
            {
                AIToolManager.DiscoverTools();
                var tools = AIToolManager.GetTools();
                if (tools.Count == 0)
                {
                    Debug.WriteLine("No tools available.");
                    return null;
                }

                var toolsArray = new JArray();
                foreach (var tool in tools)
                {
                    var toolObject = new JObject
                    {
                        ["type"] = "function",
                        ["function"] = new JObject
                        {
                            ["name"] = tool.Value.Name,
                            ["description"] = tool.Value.Description,
                            ["parameters"] = JObject.Parse(tool.Value.ParametersSchema)
                        }
                    };
                    toolsArray.Add(toolObject);
                }
                return toolsArray;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error formatting tools: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Returns the SettingDescriptors for this provider by
        /// fetching its IAIProviderSettings instance from ProviderManager.
        /// </summary>
        public virtual IEnumerable<SettingDescriptor> GetSettingDescriptors()
        {
            var ui = ProviderManager.Instance.GetProviderSettings(Name);
            return ui?.GetSettingDescriptors()
                ?? Enumerable.Empty<SettingDescriptor>();
        }
    }
}
