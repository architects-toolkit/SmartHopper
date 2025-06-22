/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using SmartHopper.Config.Interfaces;
using SmartHopper.Config.Managers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using SmartHopper.Config.Dialogs;

namespace SmartHopper.Providers.Template
{
    /// <summary>
    /// Settings implementation for the Template provider.
    /// This class is responsible for creating the UI controls for configuring the provider
    /// and for managing the provider's settings.
    /// </summary>
    public class TemplateProviderSettings : AIProviderSettings
    {
        private readonly IAIProvider provider;
        private TextBox apiKeyTextBox;
        private TextBox modelTextBox;
        private NumericUpDown maxTokensNumeric;

        /// <summary>
        /// Initializes a new instance of the <see cref="TemplateProviderSettings"/> class.
        /// </summary>
        /// <param name="provider">The provider associated with these settings.</param>
        public TemplateProviderSettings(IAIProvider provider) : base(provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        /// <summary>
        /// Gets the setting descriptors for this provider.
        /// These describe the settings that can be configured in the UI.
        /// </summary>
        /// <returns>A collection of setting descriptors.</returns>
        public override IEnumerable<SettingDescriptor> GetSettingDescriptors()
        {
            // Define the settings that your provider requires
            return new[]
            {
                new SettingDescriptor
                {
                    Name = "ApiKey",
                    DisplayName = "API Key",
                    Description = "Your API key for the Template service",
                    IsSecret = true, // Set to true for sensitive data like API keys
                    Type = typeof(string)
                },
                new SettingDescriptor
                {
                    Name = "Model",
                    DisplayName = "Model",
                    Description = "The model to use for generating responses",
                    Type = typeof(string),
                    DefaultValue = _defaultModel
                },
                new SettingDescriptor
                {
                    Name = "MaxTokens",
                    DisplayName = "Max Tokens",
                    Description = "Maximum number of tokens to generate",
                    Type = typeof(int),
                    DefaultValue = 150
                }
            };
        }

        /// <summary>
        /// Validates the provided settings.
        /// </summary>
        /// <param name="settings">The settings to validate.</param>
        /// <returns>True if the settings are valid, otherwise false.</returns>
        public override bool ValidateSettings(Dictionary<string, object> settings)
        {
            // Only validate settings that are actually provided
            if (settings == null)
                return false;

            // Extract values from settings dictionary
            string apiKey = null;
            string endpoint = null;
            int? maxTokens = null;

            // Get API key if present
            if (settings.TryGetValue("ApiKey", out var apiKeyObj) && apiKeyObj != null)
            {
                apiKey = apiKeyObj.ToString();
                Debug.WriteLine($"[TemplateProvider] API key extracted (length: {apiKey.Length})");

                // Skip API key validation since any value is valid
            }

            // Get endpoint if present
            if (settings.TryGetValue("Endpoint", out var endpointObj) && endpointObj != null)
            {
                endpoint = endpointObj.ToString();
                Debug.WriteLine($"[TemplateProvider] Endpoint extracted: {endpoint}");

                // Check endpoint format if provided
                if (endpoint != null && !string.IsNullOrWhiteSpace(endpoint))
                {
                    // Optional: Add URL format validation
                    if (!endpoint.StartsWith("http://") && !endpoint.StartsWith("https://"))
                    {
                        if (showErrorDialogs)
                        {
                            StyledMessageDialog.ShowError("Endpoint must be a valid URL starting with http:// or https://.", "Validation Error");
                        }
                        return false;
                    }
                }
            }

            // Check max tokens if present
            if (settings.TryGetValue("MaxTokens", out var maxTokensObj) && maxTokensObj != null)
            {
                // Try to parse as integer
                if (int.TryParse(maxTokensObj.ToString(), out int parsedMaxTokens))
                {
                    maxTokens = parsedMaxTokens;
                    Debug.WriteLine($"[TemplateProvider] MaxTokens extracted: {maxTokens}");
                }
                else
                {
                    Debug.WriteLine($"[TemplateProvider] MaxTokens validation failed: not an integer, got {maxTokensObj}");
                    return false;
                }

                // Check max tokens if provided
                if (maxTokens.HasValue && maxTokens.Value <= 0)
                {
                    if (showErrorDialogs)
                    {
                        StyledMessageDialog.ShowError("Max tokens must be a positive number.", "Validation Error");
                    }
                    return false;
                }
            }

            return true;
        }
    }
}
