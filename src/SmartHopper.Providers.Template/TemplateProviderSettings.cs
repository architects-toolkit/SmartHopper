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
using System.Windows.Forms;
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
        /// Internal method for validating Template provider settings.
        /// </summary>
        /// <param name="apiKey">The API key to validate.</param>
        /// <param name="endpoint">The endpoint URL to validate.</param>
        /// <param name="maxTokens">The maximum tokens setting to validate.</param>
        /// <param name="showErrorDialogs">Whether to show error dialogs for validation failures.</param>
        /// <returns>True if all provided settings are valid, otherwise false.</returns>
        internal static bool ValidateSettingsLogic(string apiKey, string endpoint = null, int? maxTokens = null, bool showErrorDialogs = false)
        {
            // Skip API key validation since any value is valid
            
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
            
            // Check max tokens if provided
            if (maxTokens.HasValue && maxTokens.Value <= 0)
            {
                if (showErrorDialogs)
                {
                    StyledMessageDialog.ShowError("Max tokens must be a positive number.", "Validation Error");
                }
                return false;
            }

            return true;
        }
    }
}
