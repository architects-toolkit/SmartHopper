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
using SmartHopper.Config.Dialogs;
using SmartHopper.Config.Interfaces;

namespace SmartHopper.Providers.OpenAI
{
    /// <summary>
    /// Settings implementation for the OpenAI provider.
    /// This class is responsible for creating the UI controls for configuring the provider
    /// and for managing the provider's settings.
    /// </summary>
    public class OpenAIProviderSettings : AIProviderSettings, IDisposable
    {
        private new readonly OpenAIProvider provider;
        private new TextBox apiKeyTextBox;
        private new TextBox modelTextBox;
        private new NumericUpDown maxTokensNumeric;
        private new ComboBox reasoningEffortComboBox;

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenAIProviderSettings"/> class.
        /// </summary>
        /// <param name="provider">The provider associated with these settings.</param>
        public OpenAIProviderSettings(OpenAIProvider provider)
            : base(provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        /// <summary>
        /// Internal method for validating OpenAI settings.
        /// </summary>
        /// <param name="apiKey">The API key to validate.</param>
        /// <param name="model">The model name to validate.</param>
        /// <param name="maxTokens">The max tokens setting to validate.</param>
        /// <param name="reasoningEffort">The reasoning effort setting to validate.</param>
        /// <param name="showErrorDialogs">Whether to show error dialogs for validation failures.</param>
        /// <returns>True if all provided settings are valid, otherwise false.</returns>
        internal static bool ValidateSettingsLogic(string apiKey, string model, int maxTokens, string reasoningEffort, bool showErrorDialogs = false)
        {
            Debug.WriteLine($"Validating OpenAI settings: API Key: {apiKey}, Model: {model}, Max Tokens: {maxTokens}, Reasoning Effort: {reasoningEffort}");

            // Skip API key validation since any value is valid

            // Skip model validation since any value is valid

            // Ensure max tokens is greater than 0
            if (maxTokens <= 0)
            {
                if (showErrorDialogs)
                {
                    StyledMessageDialog.ShowError("Max Tokens must be greater than 0.", "Validation Error");
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
