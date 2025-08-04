/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System.Linq;
using Eto.Drawing;
using Eto.Forms;
using SmartHopper.Infrastructure.Interfaces;
using SmartHopper.Infrastructure.Managers.AIProviders;
using SmartHopper.Infrastructure.Managers.ModelManager;
using SmartHopper.Menu.Dialogs.SettingsTabs.Models;

namespace SmartHopper.Menu.Dialogs.SettingsTabs
{
    /// <summary>
    /// Settings page for SmartHopper Assistant configuration including CanvasButton behavior, greeting generation, and provider/model selection
    /// </summary>
    public class AssistantSettingsPage : Panel
    {
        private readonly CheckBox _enableAIGreetingCheckBox;
        private readonly DropDown _assistantProviderComboBox;
        private readonly TextBox _assistantModelTextBox;
        private readonly IAIProvider[] _providers;

        /// <summary>
        /// Initializes a new instance of the AssistantSettingsPage class
        /// </summary>
        /// <param name="providers">Available AI providers</param>
        public AssistantSettingsPage(IAIProvider[] providers)
        {
            _providers = providers;

            // Create controls
            _enableAIGreetingCheckBox = new CheckBox
            {
                Text = "Enable AI-generated greetings in chat"
            };

            _assistantProviderComboBox = new DropDown();
            _assistantModelTextBox = new TextBox
            {
                PlaceholderText = "Enter model name or leave empty for default"
            };

            // Populate provider dropdown
            _assistantProviderComboBox.Items.Add(new ListItem { Text = "(Default)" });
            foreach (var provider in _providers)
            {
                _assistantProviderComboBox.Items.Add(new ListItem { Text = provider.Name });
            }

            // Create layout
            var layout = new TableLayout { Spacing = new Size(5, 5), Padding = new Padding(10) };

            // Header
            layout.Rows.Add(new TableRow(
                new TableCell(new Label
                {
                    Text = "SmartHopper Assistant",
                    Font = new Font(SystemFont.Bold, 12),
                    VerticalAlignment = VerticalAlignment.Center
                })
            ));
            layout.Rows.Add(new TableRow(
                new TableCell(new Label
                {
                    Text = "Configure settings for the SmartHopper Assistant including the CanvasButton and chat behavior.",
                    TextColor = Colors.Gray,
                    Font = new Font(SystemFont.Default, 10),
                    Wrap = WrapMode.Word
                })
            ));

            // Add spacing
            layout.Rows.Add(new TableRow { ScaleHeight = false });

            // AI Greeting section
            layout.Rows.Add(new TableRow(
                new TableCell(_enableAIGreetingCheckBox)
            ));
            layout.Rows.Add(new TableRow(
                new TableCell(new Label
                {
                    Text = "When enabled, the AI assistant will generate personalized greeting messages when starting a new chat conversation.",
                    TextColor = Colors.Gray,
                    Font = new Font(SystemFont.Default, 10),
                    Wrap = WrapMode.Word
                })
            ));

            // Add spacing
            layout.Rows.Add(new TableRow { ScaleHeight = false });

            // Assistant provider section
            layout.Rows.Add(new TableRow(
                new TableCell(new Label { Text = "Assistant Provider:", VerticalAlignment = VerticalAlignment.Center }),
                new TableCell(_assistantProviderComboBox)
            ));
            layout.Rows.Add(new TableRow(
                new TableCell(new Label
                {
                    Text = "AI provider to use specifically for SmartHopper Assistant features like greetings and CanvasButton interactions.",
                    TextColor = Colors.Gray,
                    Font = new Font(SystemFont.Default, 10),
                    Wrap = WrapMode.Word
                })
            ));

            // Add spacing
            layout.Rows.Add(new TableRow { ScaleHeight = false });

            // Assistant model section
            layout.Rows.Add(new TableRow(
                new TableCell(new Label { Text = "Assistant Model:" }),
                new TableCell(_assistantModelTextBox)
            ));
            layout.Rows.Add(new TableRow(
                new TableCell(new Label
                {
                    Text = "Specific AI model to use for assistant features. Leave empty to use the provider's default model.",
                    TextColor = Colors.Gray,
                    Font = new Font(SystemFont.Default, 10),
                    Wrap = WrapMode.Word
                })
            ));

            // Add flexible spacing at the bottom
            layout.Rows.Add(new TableRow { ScaleHeight = true });

            Content = new Scrollable { Content = layout };
        }

        /// <summary>
        /// Loads assistant settings into the UI controls
        /// </summary>
        /// <param name="settings">Assistant settings to load</param>
        public void LoadSettings(AssistantSettings settings)
        {
            // Set greeting checkbox
            _enableAIGreetingCheckBox.Checked = settings.EnableAIGreeting;

            // Set assistant provider
            if (!string.IsNullOrEmpty(settings.AssistantProvider))
            {
                for (int i = 0; i < _assistantProviderComboBox.Items.Count; i++)
                {
                    if (_assistantProviderComboBox.Items[i].Text == settings.AssistantProvider)
                    {
                        _assistantProviderComboBox.SelectedIndex = i;
                        break;
                    }
                }
            }
            else if (_assistantProviderComboBox.Items.Count > 0)
            {
                _assistantProviderComboBox.SelectedIndex = 0;
            }

            // Set assistant model in text box
            _assistantModelTextBox.Text = settings.AssistantModel ?? string.Empty;
        }

        /// <summary>
        /// Saves UI control values back to the assistant settings model
        /// </summary>
        /// <param name="settings">Assistant settings to update</param>
        public void SaveSettings(AssistantSettings settings)
        {
            // Save greeting setting
            settings.EnableAIGreeting = _enableAIGreetingCheckBox.Checked ?? false;

            // Save assistant provider
            if (_assistantProviderComboBox.SelectedIndex >= 0)
            {
                settings.AssistantProvider = _assistantProviderComboBox.Items[_assistantProviderComboBox.SelectedIndex].Text;
            }

            // Save assistant model (trim whitespace)
            settings.AssistantModel = _assistantModelTextBox.Text?.Trim() ?? string.Empty;
        }
    }
}
