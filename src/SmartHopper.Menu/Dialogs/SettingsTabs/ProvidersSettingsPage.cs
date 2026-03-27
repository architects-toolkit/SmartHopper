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
using System.IO;
using Eto.Drawing;
using Eto.Forms;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Infrastructure.Settings;
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
        private readonly DropDown _integrityCheckModeDropDown;
        private readonly NumericStepper _httpTimeoutStepper;
        private readonly NumericStepper _batchHttpTimeoutStepper;

        /// <summary>
        /// Initializes a new instance of the ProvidersSettingsPage class
        /// </summary>
        /// <param name="providers">Available AI providers</param>
        public ProvidersSettingsPage(IAIProvider[] providers)
        {
            this._providers = providers;
            this._providerCheckBoxes = new Dictionary<string, CheckBox>();

            // Create layout
            var layout = new DynamicLayout { Spacing = new Size(5, 5), Padding = new Padding(10) };

            // Add header
            layout.Add(new Label
            {
                Text = "Trusted Providers",
                Font = new Font(SystemFont.Bold, 12),
            });

            layout.Add(new Label
            {
                Text = "Configure which AI providers are trusted and can be used by SmartHopper components.",
                TextColor = Colors.Gray,
                Font = new Font(SystemFont.Default, 10),
                Wrap = WrapMode.Word,
                Width = 500,  // Max width for better text wrapping
            });

            // Add spacing
            layout.Add(new Panel { Height = 10 });

            // Add provider checkboxes
            foreach (var provider in this._providers)
            {
                var providerLayout = new StackLayout
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 10,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Padding = new Padding(0, 5),
                };

                // Create checkbox for this provider
                var checkbox = new CheckBox
                {
                    Text = provider.Name,
                    Font = new Font(SystemFont.Default, 11),
                };
                this._providerCheckBoxes[provider.GetType().Assembly.GetName().Name] = checkbox;

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
                                Size = new Size(16, 16),
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
                layout.Add(providerLayout);
            }

            // Add spacing
            layout.Add(new Panel { Height = 20 });

            // Global Network Settings Section
            layout.Add(new Label
            {
                Text = "Network Settings",
                Font = new Font(SystemFont.Bold, 12),
            });

            layout.Add(new Label
            {
                Text = "Configure global HTTP timeout settings for all AI provider calls.",
                TextColor = Colors.Gray,
                Font = new Font(SystemFont.Default, 10),
                Wrap = WrapMode.Word,
                Width = 500,
            });

            layout.Add(new Panel { Height = 10 });

            // HTTP Timeout for regular calls
            layout.Add(new Label
            {
                Text = "HTTP Timeout (Regular Calls)",
                Font = new Font(SystemFont.Default, 10),
            });

            this._httpTimeoutStepper = new NumericStepper
            {
                Value = 120,
                MinValue = 1,
                MaxValue = 600,
                Width = 100,
            };

            layout.Add(new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Items =
                {
                    this._httpTimeoutStepper,
                    new Label
                    {
                        Text = "seconds (default: 120)",
                        TextColor = Colors.Gray,
                        Font = new Font(SystemFont.Default, 9),
                        VerticalAlignment = VerticalAlignment.Center,
                    },
                },
            });

            layout.Add(new Label
            {
                Text = "Timeout for regular API calls. Increase if you experience timeout errors on slow connections.",
                TextColor = Colors.Gray,
                Font = new Font(SystemFont.Default, 9),
                Wrap = WrapMode.Word,
                Width = 500,
            });

            layout.Add(new Panel { Height = 10 });

            // HTTP Timeout for batch calls
            layout.Add(new Label
            {
                Text = "HTTP Timeout (Batch Calls)",
                Font = new Font(SystemFont.Default, 10),
            });

            this._batchHttpTimeoutStepper = new NumericStepper
            {
                Value = 300,
                MinValue = 1,
                MaxValue = 600,
                Width = 100,
            };

            layout.Add(new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Items =
                {
                    this._batchHttpTimeoutStepper,
                    new Label
                    {
                        Text = "seconds (default: 300)",
                        TextColor = Colors.Gray,
                        Font = new Font(SystemFont.Default, 9),
                        VerticalAlignment = VerticalAlignment.Center,
                    },
                },
            });

            layout.Add(new Label
            {
                Text = "Timeout for batch API operations (file uploads/downloads). Batch operations typically require longer timeouts.",
                TextColor = Colors.Gray,
                Font = new Font(SystemFont.Default, 9),
                Wrap = WrapMode.Word,
                Width = 500,
            });

            layout.Add(new Panel { Height = 20 });

            // Integrity Check Section
            layout.Add(new Label
            {
                Text = "Integrity Check Mode",
                Font = new Font(SystemFont.Bold, 12),
            });

            layout.Add(new Label
            {
                Text = "Configure how provider integrity verification is handled when SHA-256 hash verification encounters issues.",
                TextColor = Colors.Gray,
                Font = new Font(SystemFont.Default, 10),
                Wrap = WrapMode.Word,
                Width = 500,
            });

#if DEBUG
            layout.Add(new Panel { Height = 5 });
            layout.Add(new Label
            {
                Text = "⚠ DEBUG BUILD: Integrity check is enforced to Soft mode regardless of the setting below.",
                TextColor = Colors.Orange,
                Font = new Font(SystemFont.Default, 10),
                Wrap = WrapMode.Word,
                Width = 500,
            });
#endif

            layout.Add(new Panel { Height = 10 });

            // Integrity check mode dropdown
            this._integrityCheckModeDropDown = new DropDown
            {
                Width = 500,
            };

            // Add mode options with descriptions
            // Index 0 = Soft, Index 1 = Hard, Index 2 = Strict
            this._integrityCheckModeDropDown.Items.Add(new ListItem
            {
                Text = "Soft - No blocking, just warn about issues (default)"
            });
            this._integrityCheckModeDropDown.Items.Add(new ListItem
            {
                Text = "Hard - Block altered and unknown providers, be permissive when offline"
            });
            this._integrityCheckModeDropDown.Items.Add(new ListItem
            {
                Text = "Strict - Allow only verified providers from official repository (highest security)"
            });

            layout.Add(this._integrityCheckModeDropDown);

            layout.Add(new Panel { Height = 10 });

            layout.Add(new Label
            {
                Text = "• Soft: Warns but allows providers with hash mismatches, unavailable hashes, or custom/third-party providers. " +
                       "Best for development and custom providers.\n\n" +
                       "• Hard: Blocks providers with hash mismatches or unknown providers, " +
                       "but allows when hash repository is unavailable (when offline or network issues). Good balance of security and flexibility.\n\n" +
                       "• Strict: Blocks on all verification failures including hash mismatches, unknown providers, and network unavailability. " +
                       "Requires all providers to have valid published hashes from the official repository and an active internet connection.",
                TextColor = Colors.Gray,
                Font = new Font(SystemFont.Default, 9),
                Wrap = WrapMode.Word,
                Width = 500,
            });

            // Add end spacing
            layout.Add(new Panel { Height = 10 });

            this.Content = new Scrollable { Content = layout };
        }

        /// <summary>
        /// Loads trusted providers settings into the UI controls
        /// </summary>
        /// <param name="settings">Trusted providers settings to load</param>
        /// <param name="integrityCheckMode">Integrity check mode setting</param>
        public void LoadSettings(TrustedProvidersSettings settings, ProviderIntegrityCheckMode integrityCheckMode)
        {
            foreach (var kvp in this._providerCheckBoxes)
            {
                // Default to true if not specified, false if explicitly set to false
                kvp.Value.Checked = !settings.ContainsKey(kvp.Key) || settings[kvp.Key];
            }

            // Select the appropriate dropdown item based on mode
            // Index mapping: 0 = Soft, 1 = Hard, 2 = Strict
            int targetIndex = integrityCheckMode switch
            {
                ProviderIntegrityCheckMode.Soft => 0,
                ProviderIntegrityCheckMode.Hard => 1,
                ProviderIntegrityCheckMode.Strict => 2,
                _ => 0
            };

            if (targetIndex >= 0 && targetIndex < this._integrityCheckModeDropDown.Items.Count)
            {
                this._integrityCheckModeDropDown.SelectedIndex = targetIndex;
            }

            // Load global HTTP timeout settings
            var httpTimeout = SmartHopperSettings.Instance.GetSetting("Global", "HttpTimeoutSeconds");
            if (httpTimeout is int httpTimeoutInt)
            {
                this._httpTimeoutStepper.Value = httpTimeoutInt;
            }

            var batchHttpTimeout = SmartHopperSettings.Instance.GetSetting("Global", "BatchHttpTimeoutSeconds");
            if (batchHttpTimeout is int batchHttpTimeoutInt)
            {
                this._batchHttpTimeoutStepper.Value = batchHttpTimeoutInt;
            }
        }

        /// <summary>
        /// Saves UI control values back to the trusted providers settings model
        /// </summary>
        /// <param name="settings">Trusted providers settings to update</param>
        /// <param name="integrityCheckMode">Integrity check mode setting to update</param>
        public void SaveSettings(TrustedProvidersSettings settings, out ProviderIntegrityCheckMode integrityCheckMode)
        {
            settings.Clear();
            foreach (var kvp in this._providerCheckBoxes)
            {
                settings[kvp.Key] = kvp.Value.Checked ?? false;
            }

            // Get selected mode from dropdown based on index
            // Index mapping: 0 = Soft, 1 = Hard, 2 = Strict
            integrityCheckMode = this._integrityCheckModeDropDown.SelectedIndex switch
            {
                0 => ProviderIntegrityCheckMode.Soft,
                1 => ProviderIntegrityCheckMode.Hard,
                2 => ProviderIntegrityCheckMode.Strict,
                _ => ProviderIntegrityCheckMode.Soft
            };

            // Save global HTTP timeout settings
            SmartHopperSettings.Instance.SetSetting("Global", "HttpTimeoutSeconds", (int)this._httpTimeoutStepper.Value);
            SmartHopperSettings.Instance.SetSetting("Global", "BatchHttpTimeoutSeconds", (int)this._batchHttpTimeoutStepper.Value);
        }
    }
}
