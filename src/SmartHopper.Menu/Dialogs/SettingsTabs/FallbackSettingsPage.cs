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
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.ProviderSdk.AIProviders;
using SmartHopper.ProviderSdk.AIModels;

namespace SmartHopper.Menu.Dialogs.SettingsTabs
{
    /// <summary>
    /// Settings page for modality fallback configuration.
    /// </summary>
    public class FallbackSettingsPage : Panel
    {
        private readonly DropDown _modeDropDown;
        private readonly Panel _pinsContainer;
        private readonly Dictionary<string, DropDown> _providerDropDowns = new();
        private readonly Dictionary<string, DropDown> _modelDropDowns = new();
        private readonly Dictionary<string, IModalityFallback> _fallbacks = new();
        private readonly IAIProvider[] _providers;

        /// <summary>
        /// Initializes a new instance of the <see cref="FallbackSettingsPage"/> class.
        /// </summary>
        /// <param name="providers">Available AI providers.</param>
        public FallbackSettingsPage(IAIProvider[] providers)
        {
            this._providers = providers;

            this._modeDropDown = new DropDown();
            this._modeDropDown.Items.Add(new ListItem { Text = "Disabled", Key = "0" });
            this._modeDropDown.Items.Add(new ListItem { Text = "Configured Provider", Key = "1" });
            this._modeDropDown.Items.Add(new ListItem { Text = "Any Provider", Key = "2" });
            this._modeDropDown.SelectedIndexChanged += this.OnModeChanged;

            var layout = new DynamicLayout { Spacing = new Size(5, 5), Padding = new Padding(10) };

            // Mode dropdown
            var modeRowLayout = new TableLayout { Spacing = new Size(10, 0) };
            modeRowLayout.Rows.Add(new TableRow(
                new TableCell(new Label { Text = "Modality Fallback:", VerticalAlignment = VerticalAlignment.Center, Width = 150 }, false),
                new TableCell(this._modeDropDown, true)));
            layout.Add(modeRowLayout);

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

                this._providerDropDowns[fb.Name] = providerDropDown;
                providerDropDown.SelectedIndexChanged += (sender, e) => this.OnProviderChanged(fb.Name);

                var modelDropDown = new DropDown();
                modelDropDown.Items.Add(new ListItem { Text = "Auto", Key = "" });
                modelDropDown.Enabled = false;
                this._modelDropDowns[fb.Name] = modelDropDown;

                var row = new TableLayout { Spacing = new Size(10, 0) };
                row.Rows.Add(new TableRow(
                    new TableCell(new Label { Text = fb.Description, VerticalAlignment = VerticalAlignment.Center, Width = 250 }, false),
                    new TableCell(providerDropDown, true),
                    new TableCell(modelDropDown, true)));
                rowsLayout.Add(row);
            }

            pinsLayout.Add(rowsLayout);
            pinsLayout.Add(null);

            this._pinsContainer = new Panel { Content = pinsLayout };
            layout.Add(this._pinsContainer);
            layout.Add(null);

            this.Content = layout;

            this.UpdatePinsVisibility();
        }

        private void OnModeChanged(object sender, EventArgs e)
        {
            this.UpdatePinsVisibility();
        }

        private void UpdatePinsVisibility()
        {
            var mode = (ModalityFallbackMode)(this._modeDropDown.SelectedIndex >= 0 ? this._modeDropDown.SelectedIndex : 0);
            this._pinsContainer.Visible = mode != ModalityFallbackMode.Disabled;
        }

        private void OnProviderChanged(string fallbackName)
        {
            if (!this._providerDropDowns.TryGetValue(fallbackName, out var providerDD) ||
                !this._modelDropDowns.TryGetValue(fallbackName, out var modelDD) ||
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

            var models = ModelManager.Instance.GetProviderModels(selectedProvider)
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
        /// Loads settings into the UI controls.
        /// </summary>
        public void LoadSettings(ModalityFallbackMode mode, Dictionary<string, FallbackProviderPin> pins)
        {
            this._modeDropDown.SelectedIndex = (int)mode;
            this.UpdatePinsVisibility();

            if (pins != null)
            {
                foreach (var kvp in pins)
                {
                    if (!this._providerDropDowns.TryGetValue(kvp.Key, out var provDD) || kvp.Value == null)
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
                        this._modelDropDowns.TryGetValue(kvp.Key, out var modelDD))
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
        /// Saves settings from UI controls.
        /// </summary>
        public void SaveSettings(out ModalityFallbackMode mode, out Dictionary<string, FallbackProviderPin> pins)
        {
            mode = (ModalityFallbackMode)(this._modeDropDown.SelectedIndex >= 0 ? this._modeDropDown.SelectedIndex : 0);
            pins = new Dictionary<string, FallbackProviderPin>();

            foreach (var kvp in this._providerDropDowns)
            {
                var selectedProviderKey = kvp.Value.SelectedKey;
                if (!string.IsNullOrEmpty(selectedProviderKey))
                {
                    var pin = new FallbackProviderPin { Provider = selectedProviderKey };

                    if (this._modelDropDowns.TryGetValue(kvp.Key, out var modelDD))
                    {
                        var selectedModelKey = modelDD.SelectedKey;
                        if (!string.IsNullOrEmpty(selectedModelKey))
                        {
                            pin.Model = selectedModelKey;
                        }
                    }

                    pins[kvp.Key] = pin;
                }
            }
        }
    }
}
