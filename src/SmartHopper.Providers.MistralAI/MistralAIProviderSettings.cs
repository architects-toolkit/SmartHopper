/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using SmartHopper.Config.Dialogs;
using SmartHopper.Config.Interfaces;

namespace SmartHopper.Providers.MistralAI
{
    public class MistralAIProviderSettings : AIProviderSettings, IDisposable
    {
        private new readonly MistralAIProvider provider;
        private new TextBox apiKeyTextBox;
        private new TextBox modelTextBox;
        private new NumericUpDown maxTokensNumeric;

        public MistralAIProviderSettings(MistralAIProvider provider)
            : base(provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public Control CreateSettingsControl()
        {
            var panel = new TableLayoutPanel
            {
                RowCount = 3,
                ColumnCount = 2,
                Dock = DockStyle.Fill,
                Padding = new Padding(5),
                AutoSize = true,
            };

            // API Key
            panel.Controls.Add(new Label { Text = "API Key:", Dock = DockStyle.Fill }, 0, 0);
            this.apiKeyTextBox = new TextBox
            {
                UseSystemPasswordChar = true, // Hide the API key for security
                Dock = DockStyle.Fill,
            };
            panel.Controls.Add(this.apiKeyTextBox, 1, 0);

            // Model Selection
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
                Value = 500,
                Dock = DockStyle.Fill,
            };
            panel.Controls.Add(this.maxTokensNumeric, 1, 2);

            return panel;
        }


        /// <summary>
        /// Internal method for validating MistralAI settings.
        /// </summary>
        /// <param name="apiKey">The API key to validate.</param>
        /// <param name="model">The model name to validate.</param>
        /// <param name="maxTokens">The maximum tokens setting to validate.</param>
        /// <param name="showErrorDialogs">Whether to show error dialogs for validation failures.</param>
        /// <returns>True if all provided settings are valid, otherwise false.</returns>
        internal static bool ValidateSettingsLogic(string apiKey, string model, int? maxTokens = null, bool showErrorDialogs = false)
        {
            Debug.WriteLine($"Validating MistralAI settings: API Key: {apiKey}, Model: {model}, Max Tokens: {maxTokens}");

            // Skip API key validation since any value is valid

            // Skip model validation since any value is valid

            // Ensure max tokens is greater than 0
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

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
