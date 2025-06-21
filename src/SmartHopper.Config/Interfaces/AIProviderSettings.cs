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
using System.Windows.Forms;
using SmartHopper.Config.Dialogs;
using SmartHopper.Config.Managers;

namespace SmartHopper.Config.Interfaces
{
    /// <summary>
    /// Interface for provider settings UI and validation.
    /// </summary>
    public interface IAIProviderSettings
    {
        Control CreateSettingsControl();
    }

    /// <summary>
    /// Base class for provider settings, encapsulating common UI building and persistence logic.
    /// </summary>
    public abstract class AIProviderSettings : IAIProviderSettings
    {
        protected readonly IAIProvider provider;
        protected TextBox apiKeyTextBox;
        protected TextBox modelTextBox;
        protected NumericUpDown maxTokensNumeric;
        protected string decryptedApiKey;

        protected AIProviderSettings(IAIProvider provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public virtual Control CreateSettingsControl()
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
            this.apiKeyTextBox = new TextBox { UseSystemPasswordChar = true, Dock = DockStyle.Fill };
            panel.Controls.Add(this.apiKeyTextBox, 1, 0);

            // Model
            panel.Controls.Add(new Label { Text = "Model:", Dock = DockStyle.Fill }, 0, 1);
            this.modelTextBox = new TextBox { Dock = DockStyle.Fill };
            panel.Controls.Add(this.modelTextBox, 1, 1);

            // Max Tokens
            panel.Controls.Add(new Label { Text = "Max Tokens:", Dock = DockStyle.Fill }, 0, 2);
            this.maxTokensNumeric = new NumericUpDown { Minimum = 1, Maximum = 4096, Value = 150, Dock = DockStyle.Fill };
            panel.Controls.Add(this.maxTokensNumeric, 1, 2);

            return panel;
        }
    }
}
