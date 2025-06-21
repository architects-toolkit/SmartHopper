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
using SmartHopper.Config.Dialogs;
using SmartHopper.Config.Interfaces;
using SmartHopper.Config.Models;

namespace SmartHopper.Providers.DeepSeek
{
    /// <summary>
    /// Settings implementation for the Template provider.
    /// This class is responsible for creating the UI controls for configuring the provider
    /// and for managing the provider's settings.
    /// </summary>
    public class DeepSeekProviderSettings : AIProviderSettings
    {
        private readonly IAIProvider provider;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeepSeekProviderSettings"/> class.
        /// </summary>
        /// <param name="provider">The provider associated with these settings.</param>
        public DeepSeekProviderSettings(IAIProvider provider) : base(provider)
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
                    Description = "Your API key for the DeepSeek service",
                    IsSecret = true, // Set to true for sensitive data like API keys
                    Type = typeof(string),
                },
                new SettingDescriptor
                {
                    Name = "Model",
                    DisplayName = "Model",
                    Description = "The model to use for generating responses",
                    Type = typeof(string),
                    DefaultValue = this.provider.DefaultModel,
                },
                new SettingDescriptor
                {
                    Name = "MaxTokens",
                    DisplayName = "Max Tokens",
                    Description = "Maximum number of tokens to generate",
                    Type = typeof(int),
                    DefaultValue = 150,
                },
            };
        }

        /// <summary>
        /// Validates the provided settings.
        /// </summary>
        /// <param name="settings">The settings to validate.</param>
        /// <returns>True if the settings are valid, otherwise false.</returns>
        public override bool ValidateSettings(Dictionary<string, object> settings)
        {
            if (settings == null)
                return false;

            string apiKey = settings.TryGetValue("ApiKey", out var apiKeyObj) && apiKeyObj != null
                ? apiKeyObj.ToString()
                : null;

            string model = settings.TryGetValue("Model", out var modelObj) && modelObj != null
                ? modelObj.ToString()
                : this.provider.DefaultModel;

            int maxTokens = settings.TryGetValue("MaxTokens", out var maxTokensObj) && 
                             int.TryParse(maxTokensObj?.ToString(), out var mt)
                ? mt
                : 0;

            Debug.WriteLine($"Validating DeepSeek settings: API Key: {apiKey}, Model: {model}, Max Tokens: {maxTokens}");

            var showErrorDialogs = true; // Set to false if you don't want to show error dialogs

            // Skip API key validation since any value is valid

            // Skip model validation since any value is valid

            // Ensure max tokens is greater than 0
            if (maxTokens <= 0)
            {
                if (showErrorDialogs)
                {
                    StyledMessageDialog.ShowError("Max tokens must be greater than zero.", "Validation Error");
                }

                return false;
            }

            return true;
        }
    }
}
