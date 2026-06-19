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

using System.Collections.Generic;
using Eto.Drawing;
using Eto.Forms;
using SmartHopper.Infrastructure.AICall.Fallback;

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

        /// <summary>
        /// Initializes a new instance of the <see cref="FallbackSettingsPage"/> class.
        /// </summary>
        public FallbackSettingsPage()
        {
            this._modeDropDown = new DropDown();
            this._modeDropDown.Items.Add(new ListItem { Text = "Disabled", Key = "0" });
            this._modeDropDown.Items.Add(new ListItem { Text = "Configured Provider", Key = "1" });
            this._modeDropDown.Items.Add(new ListItem { Text = "Any Provider", Key = "2" });

            this._pinsContainer = new Panel();

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

            // Provider pins sub-section
            layout.Add(new Label
            {
                Text = "Fallback Model Overrides",
                Font = new Font(SystemFont.Bold, 12),
            });

            layout.Add(new Label
            {
                Text = "Optionally pin a specific provider/model for each fallback conversion. " +
                       "Leave as 'Auto' to let the resolver choose automatically.",
                TextColor = Colors.Gray,
                Font = new Font(SystemFont.Default, 10),
                Wrap = WrapMode.Word,
                Width = 500,
            });

            layout.Add(new Panel { Height = 5 });

            // Build pin rows for registered fallbacks
            ModalityFallbackResolver.EnsureInitialized();
            var fallbacks = ModalityFallbackResolver.GetRegisteredFallbacks();

            var pinsLayout = new DynamicLayout { Spacing = new Size(5, 5) };

            foreach (var fb in fallbacks)
            {
                var providerDropDown = new DropDown();
                providerDropDown.Items.Add(new ListItem { Text = "Auto", Key = "" });
                this._providerDropDowns[fb.Name] = providerDropDown;

                var modelDropDown = new DropDown();
                modelDropDown.Items.Add(new ListItem { Text = "Auto", Key = "" });
                modelDropDown.Enabled = false;
                this._modelDropDowns[fb.Name] = modelDropDown;

                var row = new TableLayout { Spacing = new Size(10, 0) };
                row.Rows.Add(new TableRow(
                    new TableCell(new Label { Text = fb.Description, VerticalAlignment = VerticalAlignment.Center, Width = 250 }, false),
                    new TableCell(providerDropDown, true),
                    new TableCell(modelDropDown, true)));
                pinsLayout.Add(row);
            }

            layout.Add(pinsLayout);
            layout.Add(null); // spacer at bottom

            this.Content = layout;
        }

        /// <summary>
        /// Loads settings into the UI controls.
        /// </summary>
        public void LoadSettings(ModalityFallbackMode mode, Dictionary<string, FallbackProviderPin> pins)
        {
            this._modeDropDown.SelectedIndex = (int)mode;

            if (pins != null)
            {
                foreach (var kvp in pins)
                {
                    if (this._providerDropDowns.TryGetValue(kvp.Key, out var provDD) && kvp.Value != null)
                    {
                        // Try to find the provider in the dropdown; if not, stay at Auto
                        for (int i = 0; i < provDD.Items.Count; i++)
                        {
                            if (provDD.Items[i].Key == (kvp.Value.Provider ?? ""))
                            {
                                provDD.SelectedIndex = i;
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
                var selectedKey = kvp.Value.SelectedKey;
                if (!string.IsNullOrEmpty(selectedKey))
                {
                    var pin = new FallbackProviderPin { Provider = selectedKey };

                    if (this._modelDropDowns.TryGetValue(kvp.Key, out var modelDD) && !string.IsNullOrEmpty(modelDD.SelectedKey))
                    {
                        pin.Model = modelDD.SelectedKey;
                    }

                    pins[kvp.Key] = pin;
                }
            }
        }
    }
}
