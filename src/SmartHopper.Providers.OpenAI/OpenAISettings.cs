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
using System.Windows.Forms;
using SmartHopper.Config.Configuration;
using SmartHopper.Config.Dialogs;
using SmartHopper.Config.Interfaces;

namespace SmartHopper.Providers.OpenAI
{
    /// <summary>
    /// Settings implementation for the OpenAI provider.
    /// This class is responsible for creating the UI controls for configuring the provider
    /// and for managing the provider's settings.
    /// </summary>
    public class OpenAISettings : AIProviderSettings, IDisposable
    {
        private new readonly OpenAI provider;
        private new TextBox apiKeyTextBox;
        private new TextBox modelTextBox;
        private new NumericUpDown maxTokensNumeric;
        private new ComboBox reasoningEffortComboBox;

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenAISettings"/> class.
        /// </summary>
        /// <param name="provider">The provider associated with these settings.</param>
        public OpenAISettings(OpenAI provider)
            : base(provider)
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
                RowCount = 4,
                ColumnCount = 2,
                Dock = DockStyle.Fill,
                Padding = new Padding(5),
                AutoSize = true,
                ColumnStyles =
                {
                    new ColumnStyle(SizeType.Percent, 30),
                    new ColumnStyle(SizeType.Percent, 70),
                },
            };

            // API Key
            panel.Controls.Add(new Label { Text = "API Key:", Dock = DockStyle.Fill }, 0, 0);
            this.apiKeyTextBox = new TextBox
            {
                UseSystemPasswordChar = true, // Hide the API key for security
                Dock = DockStyle.Fill,
            };
            panel.Controls.Add(this.apiKeyTextBox, 1, 0);

            // Model
            panel.Controls.Add(new Label { Text = "Model:", Dock = DockStyle.Fill }, 0, 1);
            this.modelTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
            };
            panel.Controls.Add(this.modelTextBox, 1, 1);

            // Max Tokens
            panel.Controls.Add(new Label { Text = "Max Tokens:", Dock = DockStyle.Fill }, 0, 2);
            this.maxTokensNumeric = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 100000,
                Value = 150,
                Dock = DockStyle.Fill,
            };
            panel.Controls.Add(this.maxTokensNumeric, 1, 2);
            
            // Reasoning Effort
            panel.Controls.Add(new Label { Text = "Reasoning Effort:", Dock = DockStyle.Fill }, 0, 3);
            this.reasoningEffortComboBox = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            this.reasoningEffortComboBox.Items.AddRange(new object[] { "low", "medium", "high" });
            this.reasoningEffortComboBox.SelectedIndex = 1; // Default to medium
            panel.Controls.Add(this.reasoningEffortComboBox, 1, 3);

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
                ["ApiKey"] = this.apiKeyTextBox.Text,
                ["Model"] = string.IsNullOrWhiteSpace(this.modelTextBox.Text) ? this.provider.DefaultModel : this.modelTextBox.Text,
                ["MaxTokens"] = (int)this.maxTokensNumeric.Value,
                ["ReasoningEffort"] = this.reasoningEffortComboBox.SelectedItem?.ToString() ?? "medium",
            };
        }

        /// <summary>
        /// Loads settings into the UI controls.
        /// </summary>
        /// <param name="settings">The settings to load.</param>
        public void LoadSettings(Dictionary<string, object> settings)
        {
            if (settings == null)
            {
                return;
            }

            try
            {
                // Load API Key
                if (settings.TryGetValue("ApiKey", out object? apiKeyValue))
                {
                    bool defined = apiKeyValue is bool ok && ok;
                    this.apiKeyTextBox.Text = defined ? "<secret-defined>" : string.Empty;
                }

                // Load Model
                if (settings.TryGetValue("Model", out object? modelValue))
                {
                    this.modelTextBox.Text = modelValue.ToString();
                }
                else
                {
                    this.modelTextBox.Text = this.provider.DefaultModel;
                }

                // Load Max Tokens
                if (settings.TryGetValue("MaxTokens", out object? maxTokensValue))
                {
                    if (maxTokensValue is int maxTokens)
                    {
                        this.maxTokensNumeric.Value = maxTokens;
                    }
                    else if (int.TryParse(maxTokensValue.ToString(), out int parsedMaxTokens))
                    {
                        this.maxTokensNumeric.Value = parsedMaxTokens;
                    }
                }
                
                // Load Reasoning Effort
                if (settings.TryGetValue("ReasoningEffort", out object? reasoningEffortValue))
                {
                    string reasoningEffort = reasoningEffortValue.ToString();
                    if (new[] { "low", "medium", "high" }.Contains(reasoningEffort))
                    {
                        this.reasoningEffortComboBox.SelectedItem = reasoningEffort;
                    }
                    else
                    {
                        // Default to medium if invalid value
                        this.reasoningEffortComboBox.SelectedIndex = 1;
                    }
                }
                else
                {
                    // Default to medium if not specified
                    this.reasoningEffortComboBox.SelectedIndex = 1;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading OpenAI provider settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads settings from the SmartHopper configuration.
        /// </summary>
        private void LoadSettings()
        {
            try
            {
                var providerSettings = SmartHopperSettings.Instance.GetProviderSettings(this.provider.Name);
                this.LoadSettings(providerSettings);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading OpenAI provider settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates the current settings from the UI controls.
        /// </summary>
        /// <returns>True if the settings are valid, otherwise false.</returns>
        public bool ValidateSettings()
        {
            string apiKey = this.apiKeyTextBox.Text;
            string model = this.modelTextBox.Text;
            string reasoningEffort = this.reasoningEffortComboBox.SelectedItem?.ToString();
            
            // Use the internal validation method with UI values
            return ValidateSettingsLogic(apiKey, model, reasoningEffort, showErrorDialogs: true);
        }

        /// <summary>
        /// Internal method for validating OpenAI settings.
        /// </summary>
        /// <param name="apiKey">The API key to validate.</param>
        /// <param name="model">The model name to validate.</param>
        /// <param name="reasoningEffort">The reasoning effort setting to validate.</param>
        /// <param name="showErrorDialogs">Whether to show error dialogs for validation failures.</param>
        /// <returns>True if all provided settings are valid, otherwise false.</returns>
        internal static bool ValidateSettingsLogic(string apiKey, string model, string reasoningEffort, bool showErrorDialogs = false)
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

            // Check if the model is provided
            if (string.IsNullOrWhiteSpace(model))
            {
                if (showErrorDialogs)
                {
                    StyledMessageDialog.ShowError("Model is required.", "Validation Error");
                }
                return false;
            }
            
            // Check if reasoning effort is valid
            if (string.IsNullOrWhiteSpace(reasoningEffort) || !new[] { "low", "medium", "high" }.Contains(reasoningEffort))
            {
                if (showErrorDialogs)
                {
                    StyledMessageDialog.ShowError("Reasoning effort must be low, medium, or high.", "Validation Error");
                }
                return false;
            }

            return true;
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
