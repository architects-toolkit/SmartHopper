/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024-2026 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this library; if not, see <https://www.gnu.org/licenses/lgpl-3.0.html>.
 */

using Eto.Drawing;
using Eto.Forms;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Menu.Dialogs.SettingsTabs.Models;

namespace SmartHopper.Menu.Dialogs.SettingsTabs
{
    /// <summary>
    /// Settings page for general SmartHopper configuration including default provider and debounce time
    /// </summary>
    public class GeneralSettingsPage : Panel
    {
        private readonly DropDown _defaultProviderComboBox;
        private readonly NumericStepper _debounceControl;
        private readonly IAIProvider[] _providers;

        /// <summary>
        /// Initializes a new instance of the GeneralSettingsPage class
        /// </summary>
        /// <param name="providers">Available AI providers</param>
        public GeneralSettingsPage(IAIProvider[] providers)
        {
            this._providers = providers;

            // Create controls
            this._defaultProviderComboBox = new DropDown();
            this._debounceControl = new NumericStepper
            {
                MinValue = 1000,
                MaxValue = 5000,
                Increment = 100,
            };

            // Populate provider dropdown
            foreach (var provider in this._providers)
            {
                this._defaultProviderComboBox.Items.Add(new ListItem { Text = provider.Name });
            }

            // Create layout
            var layout = new DynamicLayout { Spacing = new Size(5, 5), Padding = new Padding(10) };

            // Default provider section
            var providerRowLayout = new TableLayout
            {
                Spacing = new Size(10, 0),
            };
            providerRowLayout.Rows.Add(new TableRow(
                new TableCell(new Label { Text = "Default AI Provider:", VerticalAlignment = VerticalAlignment.Center, Width = 150 }, false),
                new TableCell(this._defaultProviderComboBox, true)
            ));
            layout.Add(providerRowLayout);

            layout.Add(new Label
            {
                Text = "The default AI provider to use for new components",
                TextColor = Colors.Gray,
                Font = new Font(SystemFont.Default, 10),
                Wrap = WrapMode.Word,
                Width = 500,  // Max width for better text wrapping
            });

            // Add spacing
            layout.Add(new Panel { Height = 10 });

            // Debounce time section
            var debounceRowLayout = new TableLayout
            {
                Spacing = new Size(10, 0),
            };

            debounceRowLayout.Rows.Add(new TableRow(
                new TableCell(new Label { Text = "Debounce Time (ms):", VerticalAlignment = VerticalAlignment.Center, Width = 150 }, false),
                new TableCell(this._debounceControl, true)
            ));

            layout.Add(debounceRowLayout);

            layout.Add(new Label
            {
                Text = "Time to wait for input data to stabilize before sending requests to AI providers. Especially relevant when run is permanently set to true.",
                TextColor = Colors.Gray,
                Font = new Font(SystemFont.Default, 10),
                Wrap = WrapMode.Word,
                Width = 500,  // Max width for better text wrapping
            });

            // Add end spacing
            layout.Add(new Panel { Height = 10 });

            this.Content = new Scrollable { Content = layout };
        }

        /// <summary>
        /// Loads settings into the UI controls
        /// </summary>
        /// <param name="settings">General settings to load</param>
        public void LoadSettings(GeneralSettings settings)
        {
            // Set default provider
            if (!string.IsNullOrEmpty(settings.DefaultAIProvider))
            {
                for (int i = 0; i < this._defaultProviderComboBox.Items.Count; i++)
                {
                    if (this._defaultProviderComboBox.Items[i].Text == settings.DefaultAIProvider)
                    {
                        this._defaultProviderComboBox.SelectedIndex = i;
                        break;
                    }
                }
            }
            else if (this._defaultProviderComboBox.Items.Count > 0)
            {
                this._defaultProviderComboBox.SelectedIndex = 0;
            }

            // Set debounce time
            this._debounceControl.Value = settings.DebounceTime;
        }

        /// <summary>
        /// Saves UI control values back to the settings model
        /// </summary>
        /// <param name="settings">General settings to update</param>
        public void SaveSettings(GeneralSettings settings)
        {
            // Save default provider
            if (this._defaultProviderComboBox.SelectedIndex >= 0)
            {
                settings.DefaultAIProvider = this._defaultProviderComboBox.Items[this._defaultProviderComboBox.SelectedIndex].Text;
            }

            // Save debounce time
            settings.DebounceTime = (int)this._debounceControl.Value;
        }
    }
}
