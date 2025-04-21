/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using SmartHopper.Config.Configuration;
using SmartHopper.Config.Interfaces;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace SmartHopper.Providers.MistralAI
{
    public class MistralAISettings : IAIProviderSettings
    {
        private TextBox apiKeyTextBox;
        private TextBox modelTextBox;
        private NumericUpDown maxTokensNumeric;
        private readonly MistralAI provider;

        public MistralAISettings(MistralAI provider)
        {
            this.provider = provider;
        }

        public Control CreateSettingsControl()
        {
            var panel = new TableLayoutPanel
            {
                RowCount = 3,
                ColumnCount = 2,
                Dock = DockStyle.Fill,
                Padding = new Padding(5),
                AutoSize = true
            };

            // API Key
            panel.Controls.Add(new Label { Text = "API Key:", Dock = DockStyle.Fill }, 0, 0);
            apiKeyTextBox = new TextBox
            {
                UseSystemPasswordChar = true,
                Dock = DockStyle.Fill
            };
            panel.Controls.Add(apiKeyTextBox, 1, 0);

            // Model Selection
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
                Maximum = 4096,
                Value = 150,
                Dock = DockStyle.Fill
            };
            panel.Controls.Add(maxTokensNumeric, 1, 2);

            LoadSettings();
            return panel;
        }

        public Dictionary<string, object> GetSettings()
        {
            return new Dictionary<string, object>
            {
                ["ApiKey"] = apiKeyTextBox.Text,
                ["Model"] = string.IsNullOrWhiteSpace(modelTextBox.Text) ? provider.DefaultModel : modelTextBox.Text,
                ["MaxTokens"] = (int)maxTokensNumeric.Value
            };
        }

        public void LoadSettings(Dictionary<string, object> settings)
        {
            if (settings.ContainsKey("ApiKey"))
                apiKeyTextBox.Text = settings["ApiKey"].ToString();

            if (settings.ContainsKey("Model"))
                modelTextBox.Text = settings["Model"].ToString();
            else
                modelTextBox.Text = provider.DefaultModel;

            if (settings.ContainsKey("MaxTokens"))
                maxTokensNumeric.Value = Convert.ToInt32(settings["MaxTokens"]);
        }

        private void LoadSettings()
        {
            var settings = SmartHopperSettings.Load();
            if (!settings.ProviderSettings.ContainsKey(provider.Name))
            {
                settings.ProviderSettings[provider.Name] = new Dictionary<string, object>();
            }
            LoadSettings(settings.ProviderSettings[provider.Name]);
        }

        public bool ValidateSettings()
        {
            var settings = GetSettings();
            return provider.ValidateSettings(settings);
        }
    }
}
