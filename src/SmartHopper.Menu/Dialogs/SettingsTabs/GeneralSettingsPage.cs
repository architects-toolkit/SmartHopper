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

using System;
using System.Collections.Generic;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;
using SmartHopper.Infrastructure.AICall.Fallback;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Menu.Dialogs.SettingsTabs.Models;
using SmartHopper.ProviderSdk.AIModels;
using SmartHopper.ProviderSdk.AIProviders;

namespace SmartHopper.Menu.Dialogs.SettingsTabs
{
    /// <summary>
    /// Settings page for general SmartHopper configuration including default provider, debounce time,
    /// batch poll interval, and modality fallback settings.
    /// </summary>
    public class GeneralSettingsPage : Panel
    {
        private readonly DropDown _defaultProviderComboBox;
        private readonly NumericStepper _debounceControl;
        private readonly NumericStepper _batchPollIntervalControl;
        private readonly IAIProvider[] _providers;

        // Modality fallback controls (AI section)
        private readonly DropDown _fallbackModeDropDown;
        private readonly Panel _fallbackPinsContainer;
        private readonly Dictionary<string, DropDown> _fallbackProviderDropDowns = new();
        private readonly Dictionary<string, DropDown> _fallbackModelDropDowns = new();
        private readonly Dictionary<string, IModalityFallback> _fallbacks = new();

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

            this._batchPollIntervalControl = new NumericStepper
            {
                MinValue = 10,
                MaxValue = 1800,
                Increment = 5,
            };
            foreach (var provider in this._providers)
            {
                this._defaultProviderComboBox.Items.Add(new ListItem { Text = provider.Name });
            }

            this._fallbackModeDropDown = new DropDown();
            this._fallbackModeDropDown.Items.Add(new ListItem { Text = "Disabled", Key = "0" });
            this._fallbackModeDropDown.Items.Add(new ListItem { Text = "Configured Provider", Key = "1" });
            this._fallbackModeDropDown.Items.Add(new ListItem { Text = "Any Provider", Key = "2" });
            this._fallbackModeDropDown.SelectedIndexChanged += this.OnFallbackModeChanged;

            // Create layout
            var layout = new DynamicLayout { Spacing = new Size(5, 5), Padding = new Padding(10) };

            // Default provider section
            var providerRowLayout = new TableLayout
            {
                Spacing = new Size(10, 0),
            };
            providerRowLayout.Rows.Add(new TableRow(
                new TableCell(new Label { Text = "Default AI Provider:", VerticalAlignment = VerticalAlignment.Center, Width = 150 }, false),
                new TableCell(this._defaultProviderComboBox, true)));
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
                new TableCell(this._debounceControl, true)));

            layout.Add(debounceRowLayout);

            layout.Add(new Label
            {
                Text = "Time to wait for input data to stabilize before sending requests to AI providers. Especially relevant when run is permanently set to true.",
                TextColor = Colors.Gray,
                Font = new Font(SystemFont.Default, 10),
                Wrap = WrapMode.Word,
                Width = 500,  // Max width for better text wrapping
            });

            // Add spacing
            layout.Add(new Panel { Height = 10 });

            // Batch poll interval section
            var batchPollRowLayout = new TableLayout
            {
                Spacing = new Size(10, 0),
            };

            batchPollRowLayout.Rows.Add(new TableRow(
                new TableCell(new Label { Text = "Batch Poll Interval (s):", VerticalAlignment = VerticalAlignment.Center, Width = 150 }, false),
                new TableCell(this._batchPollIntervalControl, true)));

            layout.Add(batchPollRowLayout);

            layout.Add(new Label
            {
                Text = "Minimum interval between batch status poll requests. Applies to all providers that support batch processing.",
                TextColor = Colors.Gray,
                Font = new Font(SystemFont.Default, 10),
                Wrap = WrapMode.Word,
                Width = 500,
            });

            // Add end spacing
            layout.Add(new Panel { Height = 10 });

            // Modality fallback mode
            var fallbackModeRowLayout = new TableLayout { Spacing = new Size(10, 0) };
            fallbackModeRowLayout.Rows.Add(new TableRow(
                new TableCell(new Label { Text = "Modality Fallback:", VerticalAlignment = VerticalAlignment.Center, Width = 150 }, false),
                new TableCell(this._fallbackModeDropDown, true)));
            layout.Add(fallbackModeRowLayout);

            layout.Add(new Label
            {
                Text = "Controls what happens when an AI model does not support the required input modality (e.g. images sent to a text-only model). " +
                       "Disabled: unsupported modalities produce an error. " +
                       "Configured Provider: convert using the component's configured provider. " +
                       "Any Provider: convert using whichever configured provider can handle the conversion.",
                TextColor = Colors.Gray,
                Font = new Font(SystemFont.Default, 10),
                Wrap = WrapMode.Word,
                Width = 500,
            });

            layout.Add(new Panel { Height = 10 });

            // Build the pins container with title, description and rows
            var pinsLayout = new DynamicLayout { Spacing = new Size(5, 5) };

            pinsLayout.Add(new Label
            {
                Text = "Fallback Model Overrides",
                Font = new Font(SystemFont.Bold, 12),
            });

            pinsLayout.Add(new Label
            {
                Text = "Optionally pin a specific provider/model for each fallback conversion. " +
                       "Leave as 'Auto' to let the resolver choose automatically.",
                TextColor = Colors.Gray,
                Font = new Font(SystemFont.Default, 10),
                Wrap = WrapMode.Word,
                Width = 500,
            });

            pinsLayout.Add(new Panel { Height = 5 });

            // Build pin rows for registered fallbacks
            ModalityFallbackResolver.EnsureInitialized();
            var fallbacks = ModalityFallbackResolver.GetRegisteredFallbacks();

            var rowsLayout = new DynamicLayout { Spacing = new Size(5, 5) };

            foreach (var fb in fallbacks)
            {
                this._fallbacks[fb.Name] = fb;

                var providerDropDown = new DropDown();
                providerDropDown.Items.Add(new ListItem { Text = "Auto", Key = "" });

                foreach (var provider in this._providers.OrderBy(p => p.Name))
                {
                    if (provider.IsEnabled && fb.IsAvailable(provider.Name))
                    {
                        providerDropDown.Items.Add(new ListItem { Text = provider.Name, Key = provider.Name });
                    }
                }

                this._fallbackProviderDropDowns[fb.Name] = providerDropDown;
                providerDropDown.SelectedIndexChanged += (sender, e) => this.OnFallbackProviderChanged(fb.Name);

                var modelDropDown = new DropDown();
                modelDropDown.Items.Add(new ListItem { Text = "Auto", Key = "" });
                modelDropDown.Enabled = false;
                this._fallbackModelDropDowns[fb.Name] = modelDropDown;

                var row = new TableLayout { Spacing = new Size(10, 0) };
                row.Rows.Add(new TableRow(
                    new TableCell(new Label { Text = fb.Description, VerticalAlignment = VerticalAlignment.Center, Width = 250 }, false),
                    new TableCell(providerDropDown, true),
                    new TableCell(modelDropDown, true)));
                rowsLayout.Add(row);
            }

            pinsLayout.Add(rowsLayout);
            pinsLayout.Add(null);

            this._fallbackPinsContainer = new Panel { Content = pinsLayout };
            layout.Add(this._fallbackPinsContainer);
            layout.Add(null);

            this.Content = new Scrollable { Content = layout };

            this.UpdateFallbackPinsVisibility();
        }

        private void OnFallbackModeChanged(object sender, EventArgs e)
        {
            this.UpdateFallbackPinsVisibility();
        }

        private void UpdateFallbackPinsVisibility()
        {
            var mode = (ModalityFallbackMode)(this._fallbackModeDropDown.SelectedIndex >= 0 ? this._fallbackModeDropDown.SelectedIndex : 0);
            this._fallbackPinsContainer.Visible = mode != ModalityFallbackMode.Disabled;
        }

        private void OnFallbackProviderChanged(string fallbackName)
        {
            if (!this._fallbackProviderDropDowns.TryGetValue(fallbackName, out var providerDD) ||
                !this._fallbackModelDropDowns.TryGetValue(fallbackName, out var modelDD) ||
                !this._fallbacks.TryGetValue(fallbackName, out var fallback))
            {
                return;
            }

            var selectedProvider = providerDD.SelectedKey;

            modelDD.Items.Clear();
            modelDD.Items.Add(new ListItem { Text = "Auto", Key = "" });

            if (string.IsNullOrEmpty(selectedProvider))
            {
                modelDD.Enabled = false;
                modelDD.SelectedIndex = 0;
                return;
            }

            modelDD.Enabled = true;

            var models = AIModelCapabilityRegistry.Instance.GetProviderModels(selectedProvider)
                .Where(m => m.HasCapability(fallback.RequiresCapability))
                .OrderBy(m => m.Model)
                .ToList();

            foreach (var model in models)
            {
                modelDD.Items.Add(new ListItem { Text = model.Model, Key = model.Model });
            }

            modelDD.SelectedIndex = 0;
        }

        /// <summary>
        /// Loads settings into the UI controls
        /// </summary>
        /// <param name="settings">General settings to load</param>
        /// <param name="fallbackMode">Modality fallback mode to load</param>
        /// <param name="fallbackPins">Fallback provider pins to load</param>
        public void LoadSettings(GeneralSettings settings, ModalityFallbackMode fallbackMode, Dictionary<string, FallbackProviderPin> fallbackPins)
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

            // Set batch poll interval
            this._batchPollIntervalControl.Value = settings.BatchPollIntervalSeconds;

            // Set fallback mode and pins
            this._fallbackModeDropDown.SelectedIndex = (int)fallbackMode;
            this.UpdateFallbackPinsVisibility();

            if (fallbackPins != null)
            {
                foreach (var kvp in fallbackPins)
                {
                    if (!this._fallbackProviderDropDowns.TryGetValue(kvp.Key, out var provDD) || kvp.Value == null)
                    {
                        continue;
                    }

                    for (int i = 0; i < provDD.Items.Count; i++)
                    {
                        if (provDD.Items[i].Key == (kvp.Value.Provider ?? ""))
                        {
                            provDD.SelectedIndex = i;
                            break;
                        }
                    }

                    if (!string.IsNullOrEmpty(kvp.Value.Model) &&
                        this._fallbackModelDropDowns.TryGetValue(kvp.Key, out var modelDD))
                    {
                        for (int i = 0; i < modelDD.Items.Count; i++)
                        {
                            if (modelDD.Items[i].Key == kvp.Value.Model)
                            {
                                modelDD.SelectedIndex = i;
                                break;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Saves UI control values back to the settings model
        /// </summary>
        /// <param name="settings">General settings to update</param>
        /// <param name="fallbackMode">Resulting modality fallback mode</param>
        /// <param name="fallbackPins">Resulting fallback provider pins</param>
        public void SaveSettings(GeneralSettings settings, out ModalityFallbackMode fallbackMode, out Dictionary<string, FallbackProviderPin> fallbackPins)
        {
            // Save default provider
            if (this._defaultProviderComboBox.SelectedIndex >= 0)
            {
                settings.DefaultAIProvider = this._defaultProviderComboBox.Items[this._defaultProviderComboBox.SelectedIndex].Text;
            }

            // Save debounce time
            settings.DebounceTime = (int)this._debounceControl.Value;

            // Save batch poll interval
            settings.BatchPollIntervalSeconds = (int)this._batchPollIntervalControl.Value;

            // Save fallback mode and pins
            fallbackMode = (ModalityFallbackMode)(this._fallbackModeDropDown.SelectedIndex >= 0 ? this._fallbackModeDropDown.SelectedIndex : 0);
            fallbackPins = new Dictionary<string, FallbackProviderPin>();

            foreach (var kvp in this._fallbackProviderDropDowns)
            {
                var selectedProviderKey = kvp.Value.SelectedKey;
                if (!string.IsNullOrEmpty(selectedProviderKey))
                {
                    var pin = new FallbackProviderPin { Provider = selectedProviderKey };

                    if (this._fallbackModelDropDowns.TryGetValue(kvp.Key, out var modelDD))
                    {
                        var selectedModelKey = modelDD.SelectedKey;
                        if (!string.IsNullOrEmpty(selectedModelKey))
                        {
                            pin.Model = selectedModelKey;
                        }
                    }

                    fallbackPins[kvp.Key] = pin;
                }
            }
        }
    }
}
