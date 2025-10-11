/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using Eto.Drawing;
using Eto.Forms;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Menu.Dialogs.SettingsTabs.Models;

namespace SmartHopper.Menu.Dialogs.SettingsTabs
{
    /// <summary>
    /// Settings page for SmartHopper Assistant configuration including CanvasButton behavior, greeting generation, and provider/model selection
    /// </summary>
    public class AssistantSettingsPage : Panel
    {
        private readonly DropDown _assistantProviderComboBox;

        private readonly TextBox _assistantModelTextBox;

        private readonly CheckBox _enableCanvasButtonCheckBox;

        private readonly CheckBox _enableAIGreetingCheckBox;

        private readonly IAIProvider[] _providers;

        /// <summary>
        /// Initializes a new instance of the AssistantSettingsPage class
        /// </summary>
        /// <param name="providers">Available AI providers</param>
        public AssistantSettingsPage(IAIProvider[] providers)
        {
            this._providers = providers;

            // Create controls
            this._assistantProviderComboBox = new DropDown();
            this._assistantModelTextBox = new TextBox
            {
                PlaceholderText = "Enter model name or leave empty for default",
            };

            this._enableCanvasButtonCheckBox = new CheckBox
            {
                Text = "Enable canvas button",
            };

            this._enableAIGreetingCheckBox = new CheckBox
            {
                Text = "Enable AI-generated greetings in chat",
            };

            // Populate provider dropdown
            this._assistantProviderComboBox.Items.Add(new ListItem { Text = "(Default)" });
            foreach (var provider in this._providers)
            {
                this._assistantProviderComboBox.Items.Add(new ListItem { Text = provider.Name });
            }

            // Create layout
            var layout = new DynamicLayout { Spacing = new Size(5, 5), Padding = new Padding(10) };

            // Header
            layout.Add(new Label
            {
                Text = "SmartHopper Canvas Assistant",
                Font = new Font(SystemFont.Bold, 12),
            });

            layout.Add(new Label
            {
                Text = "Configure settings for the SmartHopper Canvas Assistant. Talk to it by clicking on the top-right button in the canvas.",
                TextColor = Colors.Gray,
                Font = new Font(SystemFont.Default, 10),
                Wrap = WrapMode.Word,
                Width = 500,  // Max width for better text wrapping
            });

            // Add spacing
            layout.Add(new Panel { Height = 10 });

            // Assistant provider section
            var providerRowLayout = new TableLayout
            {
                Spacing = new Size(10, 0),
            };

            providerRowLayout.Rows.Add(new TableRow(
                new TableCell(new Label { Text = "Assistant Provider:", VerticalAlignment = VerticalAlignment.Center, Width = 150 }, false),
                new TableCell(this._assistantProviderComboBox, true)
            ));

            layout.Add(providerRowLayout);

            layout.Add(new Label
            {
                Text = "AI provider to use specifically for SmartHopper Assistant.",
                TextColor = Colors.Gray,
                Font = new Font(SystemFont.Default, 10),
                Wrap = WrapMode.Word,
                Width = 500,  // Max width for better text wrapping
            });

            // Add spacing
            layout.Add(new Panel { Height = 10 });

            // Assistant model section
            var modelRowLayout = new TableLayout
            {
                Spacing = new Size(10, 0),
            };

            modelRowLayout.Rows.Add(new TableRow(
                new TableCell(new Label { Text = "Assistant Model:", VerticalAlignment = VerticalAlignment.Center, Width = 150 }, false),
                new TableCell(this._assistantModelTextBox, true)
            ));
            layout.Add(modelRowLayout);

            layout.Add(new Label
            {
                Text = "Specific AI model to use for assistant features. Leave empty to use the provider's default model.",
                TextColor = Colors.Gray,
                Font = new Font(SystemFont.Default, 10),
                Wrap = WrapMode.Word,
                Width = 500,  // Max width for better text wrapping
            });

            // Add end spacing
            layout.Add(new Panel { Height = 10 });

            // Canvas Button section
            layout.Add(this._enableCanvasButtonCheckBox);
            layout.Add(new Label
            {
                Text = "When enabled, the canvas button will show at the top-right corner of the canvas.",
                TextColor = Colors.Gray,
                Font = new Font(SystemFont.Default, 10),
                Wrap = WrapMode.Word,
                Width = 500,  // Max width for better text wrapping
            });

            // Add end spacing
            layout.Add(new Panel { Height = 10 });

            // AI Greeting section
            layout.Add(this._enableAIGreetingCheckBox);
            layout.Add(new Label
            {
                Text = "When enabled, the AI assistant will generate personalized greeting messages when starting a new chat conversation. Disable it to prevent extra tokens being used.",
                TextColor = Colors.Gray,
                Font = new Font(SystemFont.Default, 10),
                Wrap = WrapMode.Word,
                Width = 500,  // Max width for better text wrapping
            });

            // Add spacing
            layout.Add(new Panel { Height = 10 });

            this.Content = new Scrollable { Content = layout };
        }

        /// <summary>
        /// Loads assistant settings into the UI controls
        /// </summary>
        /// <param name="settings">Assistant settings to load</param>
        public void LoadSettings(AssistantSettings settings)
        {
            // Set assistant provider
            if (!string.IsNullOrEmpty(settings.AssistantProvider))
            {
                for (int i = 0; i < this._assistantProviderComboBox.Items.Count; i++)
                {
                    if (this._assistantProviderComboBox.Items[i].Text == settings.AssistantProvider)
                    {
                        this._assistantProviderComboBox.SelectedIndex = i;
                        break;
                    }
                }
            }
            else if (this._assistantProviderComboBox.Items.Count > 0)
            {
                this._assistantProviderComboBox.SelectedIndex = 0;
            }

            // Set assistant model in text box
            this._assistantModelTextBox.Text = settings.AssistantModel ?? string.Empty;

            // Set canvas button checkbox
            this._enableCanvasButtonCheckBox.Checked = settings.EnableCanvasButton;

            // Set greeting checkbox
            this._enableAIGreetingCheckBox.Checked = settings.EnableAIGreeting;
        }

        /// <summary>
        /// Saves UI control values back to the assistant settings model
        /// </summary>
        /// <param name="settings">Assistant settings to update</param>
        public void SaveSettings(AssistantSettings settings)
        {
            // Save assistant provider
            if (this._assistantProviderComboBox.SelectedIndex >= 0)
            {
                settings.AssistantProvider = this._assistantProviderComboBox.Items[this._assistantProviderComboBox.SelectedIndex].Text;
            }

            // Save assistant model (trim whitespace)
            settings.AssistantModel = this._assistantModelTextBox.Text?.Trim() ?? string.Empty;

            // Save canvas button setting
            settings.EnableCanvasButton = this._enableCanvasButtonCheckBox.Checked ?? false;

            // Save greeting setting
            settings.EnableAIGreeting = this._enableAIGreetingCheckBox.Checked ?? false;
        }
    }
}
