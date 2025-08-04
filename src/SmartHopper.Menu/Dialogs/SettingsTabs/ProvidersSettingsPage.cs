/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;
using SmartHopper.Infrastructure.Interfaces;
using SmartHopper.Menu.Dialogs.SettingsTabs.Models;

namespace SmartHopper.Menu.Dialogs.SettingsTabs
{
    /// <summary>
    /// Settings page for managing trusted AI providers - enable/disable providers and edit trust settings
    /// </summary>
    public class ProvidersSettingsPage : Panel
    {
        private readonly Dictionary<string, CheckBox> _providerCheckBoxes;
        private readonly IAIProvider[] _providers;

        /// <summary>
        /// Initializes a new instance of the ProvidersSettingsPage class
        /// </summary>
        /// <param name="providers">Available AI providers</param>
        public ProvidersSettingsPage(IAIProvider[] providers)
        {
            _providers = providers;
            _providerCheckBoxes = new Dictionary<string, CheckBox>();

            // Create layout
            var layout = new TableLayout { Spacing = new Size(5, 5), Padding = new Padding(10) };

            // Add header
            layout.Rows.Add(new TableRow(
                new TableCell(new Label
                {
                    Text = "Trusted Providers",
                    Font = new Font(SystemFont.Bold, 12),
                    VerticalAlignment = VerticalAlignment.Center
                })
            ));
            layout.Rows.Add(new TableRow(
                new TableCell(new Label
                {
                    Text = "Configure which AI providers are trusted and can be used by SmartHopper components.",
                    TextColor = Colors.Gray,
                    Font = new Font(SystemFont.Default, 10),
                    Wrap = WrapMode.Word
                })
            ));

            // Add spacing
            layout.Rows.Add(new TableRow { ScaleHeight = false });

            // Add provider checkboxes
            foreach (var provider in _providers)
            {
                var providerLayout = new StackLayout
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 10,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Padding = new Padding(0, 5)
                };

                // Create checkbox for this provider
                var checkbox = new CheckBox
                {
                    Text = provider.Name,
                    Font = new Font(SystemFont.Default, 11)
                };
                _providerCheckBoxes[provider.GetType().FullName] = checkbox;

                // Add provider icon if available
                if (provider.Icon != null)
                {
                    try
                    {
                        using (var ms = new MemoryStream())
                        {
                            provider.Icon.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                            ms.Position = 0;
                            var iconView = new ImageView
                            {
                                Image = new Bitmap(ms),
                                Size = new Size(16, 16)
                            };
                            providerLayout.Items.Insert(0, iconView);
                        }
                    }
                    catch
                    {
                        // Ignore icon loading errors
                    }
                }

                providerLayout.Items.Add(checkbox);

                // Provider descriptions could be added here if needed in the future

                layout.Rows.Add(new TableRow(new TableCell(providerLayout)));
            }

            // Add flexible spacing at the bottom
            layout.Rows.Add(new TableRow { ScaleHeight = true });

            Content = new Scrollable { Content = layout };
        }

        /// <summary>
        /// Loads trusted providers settings into the UI controls
        /// </summary>
        /// <param name="settings">Trusted providers settings to load</param>
        public void LoadSettings(TrustedProvidersSettings settings)
        {
            foreach (var kvp in _providerCheckBoxes)
            {
                // Default to true if not specified, false if explicitly set to false
                kvp.Value.Checked = !settings.ContainsKey(kvp.Key) || settings[kvp.Key];
            }
        }

        /// <summary>
        /// Saves UI control values back to the trusted providers settings model
        /// </summary>
        /// <param name="settings">Trusted providers settings to update</param>
        public void SaveSettings(TrustedProvidersSettings settings)
        {
            settings.Clear();
            foreach (var kvp in _providerCheckBoxes)
            {
                settings[kvp.Key] = kvp.Value.Checked ?? false;
            }
        }
    }
}
