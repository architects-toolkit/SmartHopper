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
        /// Creates a control for configuring the provider settings.
        /// This control will be displayed in the settings dialog.
        /// </summary>
        /// <returns>A control for configuring the provider settings.</returns>
        public Control CreateSettingsControl()
        {
            // Create a table layout panel for the settings
            var panel = new TableLayoutPanel
            {
                RowCount = 3,
                ColumnCount = 2,
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                ColumnStyles =
                {
                    new ColumnStyle(SizeType.Percent, 30),
                    new ColumnStyle(SizeType.Percent, 70)
                }
            };

            // API Key
            panel.Controls.Add(new Label { Text = "API Key:", Dock = DockStyle.Fill }, 0, 0);
            apiKeyTextBox = new TextBox
            {
                PasswordChar = '*', // Hide the API key for security
                Dock = DockStyle.Fill
            };
            panel.Controls.Add(apiKeyTextBox, 1, 0);

            // Model
            panel.Controls.Add(new Label { Text = "Model:", Dock = DockStyle.Fill }, 0, 1);
            modelTextBox = new TextBox
            {
                Dock = DockStyle.Fill
            };
            panel.Controls.Add(modelTextBox, 1, 1);

            // Max Tokens
            panel.Controls.Add(new Label { Text = "Max Tokens:", Dock = DockStyle.Fill }, 0, 2);
            maxTokensNumeric = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 100000,
                Value = 150,
                Dock = DockStyle.Fill
            };
            panel.Controls.Add(maxTokensNumeric, 1, 2);

            // Load any existing settings
            LoadSettings();
            
            return panel;
        }

        /// <summary>
        /// Gets the current settings from the UI controls.
        /// </summary>
        /// <returns>A dictionary containing the current settings.</returns>
        public Dictionary<string, object> GetSettings()
        {
            return new Dictionary<string, object>
            {
                ["ApiKey"] = apiKeyTextBox.Text,
                ["Model"] = string.IsNullOrWhiteSpace(modelTextBox.Text) ? provider.DefaultModel : modelTextBox.Text,
                ["MaxTokens"] = (int)maxTokensNumeric.Value
            };
        }

        /// <summary>
        /// Loads settings into the UI controls.
        /// </summary>
        /// <param name="settings">The settings to load.</param>
        public void LoadSettings(Dictionary<string, object> settings)
        {
            if (settings == null)
                return;
            try
            {
                // Load API Key (show placeholder only)
                if (settings.ContainsKey("ApiKey"))
                {
                    bool defined = settings["ApiKey"] is bool ok && ok;
                    apiKeyTextBox.Text = defined ? "<secret-defined>" : string.Empty;
                }

                // Load Model
                if (settings.ContainsKey("Model"))
                    modelTextBox.Text = settings["Model"].ToString();
                else
                    modelTextBox.Text = provider.DefaultModel;

                // Load Max Tokens
                if (settings.ContainsKey("MaxTokens") && settings["MaxTokens"] is int maxTokens)
                    maxTokensNumeric.Value = maxTokens;
                else if (settings.ContainsKey("MaxTokens") && int.TryParse(settings["MaxTokens"].ToString(), out int parsedMaxTokens))
                    maxTokensNumeric.Value = parsedMaxTokens;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading Template provider settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads settings from the SmartHopper configuration.
        /// </summary>
        private void LoadSettings()
        {
            try
            {
                var settings = ProviderManager.Instance.LoadProviderSettings(provider.Name);
                LoadSettings(settings);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading Template provider settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates the current settings.
        /// </summary>
        /// <returns>True if the settings are valid, otherwise false.</returns>
        public bool ValidateSettings()
        {
            string apiKey = this.apiKeyTextBox.Text;
            string endpoint = null; // No endpointTextBox in this class
            int? maxTokens = this.maxTokensNumeric != null ? (int?)this.maxTokensNumeric.Value : null;
            
            // Use the centralized validation method with UI values
            return ValidateSettingsLogic(apiKey, endpoint, maxTokens, showErrorDialogs: true);
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
            // Check if the API key is provided
            if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "<secret-defined>")
            {
                if (showErrorDialogs)
                {
                    StyledMessageDialog.ShowError("API Key is required.", "Validation Error");
                }
                return false;
            }
            
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
