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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Infrastructure.Dialogs;
using SmartHopper.Infrastructure.Settings;
using SmartHopper.ProviderSdk.AIProviders;
using SmartHopper.ProviderSdk.Settings;

namespace SmartHopper.Menu.Dialogs.SettingsTabs
{
    /// <summary>
    /// Generic settings page that dynamically creates UI controls for any AI provider's settings based on their setting descriptors
    /// </summary>
    public class GenericProviderSettingsPage : Panel
    {
        private readonly IAIProvider _provider;
        private readonly Dictionary<string, Control> _controls;
        private readonly Dictionary<string, string> _originalValues;
        private readonly Dictionary<Type, Func<SettingDescriptor, Control>> _controlFactories;
        private UITimer? _refreshTimer;

        // Bound each control's preferred width: drop-downs size to their longest
        // item, and inside the Scrollable every scaled row stretches to the
        // widest row — pushing buttons and pickers past the dialog edge and
        // forcing a horizontal scrollbar. The column keeps a fixed width,
        // matching the fixed 150px label and 500px descriptions.
        private const int ControlWidth = 370;

        /// <summary>
        /// Initializes a new instance of the GenericProviderSettingsPage class
        /// </summary>
        /// <param name="provider">The AI provider whose settings to display</param>
        public GenericProviderSettingsPage(IAIProvider provider)
        {
            this._provider = provider;
            this._controls = new Dictionary<string, Control>();
            this._originalValues = new Dictionary<string, string>();

            // Initialize control factories
            this._controlFactories = new Dictionary<Type, Func<SettingDescriptor, Control>>
            {
                [typeof(string)] = this.CreateStringControl,
                [typeof(int)] = this.CreateNumericControl,
                [typeof(double)] = this.CreateNumericControl,
                [typeof(bool)] = this.CreateBooleanControl,
            };

            this.CreateLayout();

            // Periodically re-read persisted provider settings so controls that change
            // asynchronously (one-shot action buttons, status labels) stay live while
            // the settings page is open.
            this._refreshTimer = new UITimer { Interval = 2 };
            this._refreshTimer.Elapsed += (sender, e) => this.SafeReloadProviderSettings();
            this._refreshTimer.Start();
        }

        /// <summary>
        /// Creates the layout and controls for this provider's settings
        /// </summary>
        private void CreateLayout()
        {
            // Use DynamicLayout for better control over sizing
            var layout = new DynamicLayout { Spacing = new Size(5, 5), Padding = new Padding(10) };
            var descriptors = ProviderSettingDescriptors.Visible(this._provider.GetSettingDescriptors());

            // Add provider title with logo
            var titleLayout = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Padding(0, 0, 0, 10),
            };

            // Add provider icon if available
            if (this._provider.Icon != null)
            {
                try
                {
                    using (var ms = new MemoryStream())
                    {
                        this._provider.Icon.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                        ms.Position = 0;
                        var iconView = new ImageView
                        {
                            Image = new Bitmap(ms),
                            Size = new Size(24, 24),
                        };
                        titleLayout.Items.Add(iconView);
                    }
                }
                catch
                {
                    // Ignore icon loading errors
                }
            }

            // Add provider name title
            titleLayout.Items.Add(new Label
            {
                Text = $"{this._provider.Name} Settings",
                Font = new Font(SystemFont.Bold, 12),
            });

            layout.Add(titleLayout);

            if (!descriptors.Any())
            {
                // No settings available
                layout.Add(new Label
                {
                    Text = "No configurable settings available for this provider.",
                    TextColor = Colors.Gray,
                    Font = new Font(SystemFont.Default, 10),
                });
            }
            else
            {
                // Load current provider settings
                var providerSettings = SmartHopper.Infrastructure.Settings.SmartHopperSettings.Instance.GetProviderSettings(this._provider.Name);

                // Create controls for each setting
                foreach (var descriptor in descriptors)
                {
                    var control = this.CreateControlForDescriptor(descriptor);
                    if (control != null)
                    {
                        // Honor descriptor-level enable/disable flag
                        control.Enabled = descriptor.Enabled;

                        // Bound the preferred width: drop-downs size to their longest
                        // item and inside the Scrollable every scaled row stretches
                        // to the widest row, pushing controls past the dialog edge.
                        control.Width = ControlWidth;

                        this._controls[descriptor.Name] = control;

                        // Load current value
                        this.LoadSettingValue(descriptor, control, providerSettings);

                        // Create a horizontal layout for label and control
                        var labelText = !string.IsNullOrEmpty(descriptor.DisplayName) ? descriptor.DisplayName : descriptor.Name;
                        var label = new Label
                        {
                            Text = labelText + ":",
                            VerticalAlignment = VerticalAlignment.Center,
                            Width = 150,  // Fixed width to prevent overflow
                        };

                        var rowLayout = new TableLayout
                        {
                            Spacing = new Size(10, 0),
                        };

                        // Create row with fixed label width and expandable control
                        rowLayout.Rows.Add(new TableRow(
                            new TableCell(label, false),  // Fixed size
                            new TableCell(control, true)));   // Expandable

                        layout.Add(rowLayout);

                        // Add description if available - spans full width below the label+control
                        if (!string.IsNullOrEmpty(descriptor.Description))
                        {
                            layout.Add(new Label
                            {
                                Text = descriptor.Description,
                                TextColor = Colors.Gray,
                                Font = new Font(SystemFont.Default, 10),
                                Wrap = WrapMode.Word,
                                Width = 500,  // Max width for better text wrapping
                            });
                        }

                        // Add spacing between settings
                        layout.Add(new Panel { Height = 10 });
                    }
                }
            }

            this.Content = new Scrollable { Content = layout };
        }

        /// <summary>
        /// Creates a control for the specified setting descriptor
        /// </summary>
        /// <param name="descriptor">Setting descriptor</param>
        /// <returns>Created control or null if type not supported</returns>
        private Control CreateControlForDescriptor(SettingDescriptor descriptor)
        {
            if (descriptor.ControlParams is ButtonSettingDescriptorControl)
            {
                return this.CreateButtonControl(descriptor);
            }

            if (descriptor.ControlParams is StatusSettingDescriptorControl)
            {
                return this.CreateStatusControl(descriptor);
            }

            if (descriptor.ControlParams is LinkSettingDescriptorControl)
            {
                return this.CreateLinkControl(descriptor);
            }

            if (this._controlFactories.ContainsKey(descriptor.Type))
            {
                return this._controlFactories[descriptor.Type](descriptor);
            }

            return null;
        }

        /// <summary>
        /// Creates a read-only status label whose colour follows a sibling level
        /// setting ("ok", "busy", "error", anything else = neutral).
        /// </summary>
        private Control CreateStatusControl(SettingDescriptor descriptor)
        {
            return new Label
            {
                Wrap = WrapMode.Word,
                Font = new Font(SystemFont.Bold),
                VerticalAlignment = VerticalAlignment.Center,
            };
        }

        /// <summary>
        /// Applies the status colour mapped from the sibling level setting.
        /// </summary>
        private void ApplyStatusColor(SettingDescriptor descriptor, Label statusLabel, Dictionary<string, object> providerSettings)
        {
            var level = string.Empty;
            if (descriptor.ControlParams is StatusSettingDescriptorControl statusParams
                && !string.IsNullOrWhiteSpace(statusParams.LevelSettingName)
                && providerSettings.TryGetValue(statusParams.LevelSettingName, out var levelValue))
            {
                level = levelValue?.ToString() ?? string.Empty;
            }

            statusLabel.TextColor = level switch
            {
                "ok" => Colors.Green,
                "busy" => Colors.Orange,
                "error" => Colors.Red,
                _ => Colors.Gray,
            };
        }

        /// <summary>
        /// Creates a hyperlink that opens the configured URL in the browser.
        /// </summary>
        private Control CreateLinkControl(SettingDescriptor descriptor)
        {
            var linkParams = (LinkSettingDescriptorControl)descriptor.ControlParams!;
            var link = new LinkButton
            {
                Text = !string.IsNullOrWhiteSpace(linkParams.Text) ? linkParams.Text : linkParams.Url,
            };
            link.Click += (sender, e) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = linkParams.Url,
                        UseShellExecute = true,
                    });
                }
                catch (Exception ex)
                {
                    StyledMessageDialog.ShowError($"Failed to open URL: {ex.Message}", "Error");
                }
            };
            return link;
        }

        /// <summary>
        /// Creates a string control (TextBox, PasswordBox, or DropDown)
        /// </summary>
        /// <param name="descriptor">Setting descriptor</param>
        /// <returns>Created control</returns>
        private Control CreateStringControl(SettingDescriptor descriptor)
        {
            // If descriptor has allowed values, render a dropdown (possibly empty;
            // items are repopulated on refresh when the list changes).
            if (descriptor.AllowedValues != null)
            {
                var dropdown = new DropDown();
                SyncDropDownItems(descriptor, dropdown);
                return dropdown;
            }

            // If descriptor is secret, render a password box
            if (descriptor.IsSecret)
            {
                return new PasswordBox();
            }

            // Default to text box
            return new TextBox();
        }

        /// <summary>
        /// Creates a numeric control (NumericStepper)
        /// </summary>
        /// <param name="descriptor">Setting descriptor</param>
        /// <returns>Created control</returns>
        private Control CreateNumericControl(SettingDescriptor descriptor)
        {
            var numericParams = descriptor.ControlParams as NumericSettingDescriptorControl;
            return new NumericStepper
            {
                MinValue = numericParams?.Min ?? (descriptor.Type == typeof(int) ? int.MinValue : double.MinValue),
                MaxValue = numericParams?.Max ?? (descriptor.Type == typeof(int) ? int.MaxValue : double.MaxValue),
                Increment = numericParams?.Step ?? 1,
                DecimalPlaces = descriptor.Type == typeof(double) ? 2 : 0,
            };
        }

        /// <summary>
        /// Creates a boolean control (CheckBox)
        /// </summary>
        /// <param name="descriptor">Setting descriptor</param>
        /// <returns>Created control</returns>
        private Control CreateBooleanControl(SettingDescriptor descriptor)
        {
            return new CheckBox
            {
                Text = !string.IsNullOrEmpty(descriptor.DisplayName) ? descriptor.DisplayName : descriptor.Name,
            };
        }

        /// <summary>
        /// Creates a button control that immediately validates and saves the provider
        /// settings with the associated setting set to true.
        /// </summary>
        /// <param name="descriptor">Setting descriptor</param>
        /// <returns>Created control</returns>
        private Control CreateButtonControl(SettingDescriptor descriptor)
        {
            var button = new Button
            {
                Text = ButtonText(descriptor),
            };

            button.Click += (sender, e) => this.RunButtonAction(descriptor.Name);
            return button;
        }

        /// <summary>
        /// Validates and saves the provider settings with the given one-shot action
        /// set to true, then refreshes all rendered values.
        /// </summary>
        private void RunButtonAction(string settingName)
        {
            var updatedSettings = this.CollectSettings();
            if (updatedSettings != null)
            {
                updatedSettings[settingName] = true;
                Infrastructure.AIProviders.ProviderManager.Instance.UpdateProviderSettings(this._provider.Name, updatedSettings);
                this.ReloadProviderSettings();
            }
        }

        /// <summary>
        /// Loads the current setting value into the control
        /// </summary>
        /// <param name="descriptor">Setting descriptor</param>
        /// <param name="control">Control to load value into</param>
        /// <param name="providerSettings">Current provider settings</param>
        private void LoadSettingValue(SettingDescriptor descriptor, Control control, Dictionary<string, object> providerSettings)
        {
            var currentValue = providerSettings.ContainsKey(descriptor.Name)
                ? providerSettings[descriptor.Name]
                : descriptor.DefaultValue;

            var stringValue = currentValue?.ToString() ?? string.Empty;
            this._originalValues[descriptor.Name] = stringValue;

            switch (control)
            {
                case TextBox textBox:
                    textBox.Text = stringValue;
                    break;

                case PasswordBox passwordBox:
                    passwordBox.Text = stringValue;
                    break;

                case NumericStepper numericStepper:
                    if (double.TryParse(stringValue, out var numValue))
                    {
                        numericStepper.Value = numValue;
                    }
                    else if (descriptor.DefaultValue != null)
                    {
                        // Try direct numeric conversion first, then fall back to string parsing
                        if (descriptor.DefaultValue is double defNum ||
                            double.TryParse(descriptor.DefaultValue.ToString(), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out defNum))
                        {
                            numericStepper.Value = defNum;
                        }
                    }

                    break;

                case CheckBox checkBox:
                    if (bool.TryParse(stringValue, out var boolValue))
                    {
                        checkBox.Checked = boolValue;
                    }
                    else if (descriptor.DefaultValue != null)
                    {
                        // Try direct bool conversion first, then fall back to string parsing
                        if (descriptor.DefaultValue is bool defBool ||
                            bool.TryParse(descriptor.DefaultValue.ToString(), out defBool))
                        {
                            checkBox.Checked = defBool;
                        }
                    }

                    break;

                case DropDown dropDown:
                    SyncDropDownItems(descriptor, dropDown);
                    dropDown.SelectedIndex = IndexOfDropDownKey(dropDown, stringValue);
                    if (dropDown.SelectedIndex < 0 && descriptor.DefaultValue != null)
                    {
                        dropDown.SelectedIndex = IndexOfDropDownKey(dropDown, descriptor.DefaultValue.ToString());
                    }

                    break;

                case Label label:
                    label.Text = stringValue;
                    break;
            }

            if (control is Label statusLabel && descriptor.ControlParams is StatusSettingDescriptorControl)
            {
                this.ApplyStatusColor(descriptor, statusLabel, providerSettings);
            }
        }

        /// <summary>
        /// Collects the current UI control values into a settings dictionary.
        /// </summary>
        /// <returns>A dictionary of setting values from the UI controls.</returns>
        private Dictionary<string, object> CollectSettings()
        {
            var updatedSettings = new Dictionary<string, object>();
            var descriptors = ProviderSettingDescriptors.Visible(this._provider.GetSettingDescriptors());

            foreach (var descriptor in descriptors)
            {
                if (!this._controls.ContainsKey(descriptor.Name))
                {
                    continue;
                }

                var control = this._controls[descriptor.Name];
                if (!control.Enabled)
                {
                    // Disabled controls are read-only and do not submit values.
                    continue;
                }

                object newValue = null;

                switch (control)
                {
                    case TextBox textBox:
                        newValue = textBox.Text;
                        break;
                    case PasswordBox passwordBox:
                        newValue = passwordBox.Text;

                        // For sensitive data, only update if changed and not empty
                        if (descriptor.IsSecret && this._originalValues.ContainsKey(descriptor.Name) &&
                            passwordBox.Text == this._originalValues[descriptor.Name])
                        {
                            continue; // Skip unchanged values
                        }

                        break;
                    case NumericStepper numericStepper:
                        newValue = descriptor.Type == typeof(int) ? (int)numericStepper.Value : numericStepper.Value;
                        break;
                    case CheckBox checkBox:
                        newValue = checkBox.Checked ?? false;
                        break;
                    case DropDown dropDown:
                        if (dropDown.SelectedIndex >= 0)
                        {
                            newValue = dropDown.Items[dropDown.SelectedIndex].Key;
                        }

                        break;
                    case Button _:
                    case Label _:
                    case LinkButton _:
                        continue;
                }

                if (newValue != null)
                {
                    updatedSettings[descriptor.Name] = newValue;
                }
            }

            return updatedSettings;
        }

        /// <summary>
        /// Saves the current UI control values back to the provider settings
        /// </summary>
        public void SaveSettings()
        {
            var updatedSettings = this.CollectSettings();

            // Update provider settings via ProviderManager
            if (updatedSettings.Any())
            {
                Infrastructure.AIProviders.ProviderManager.Instance.UpdateProviderSettings(this._provider.Name, updatedSettings);
            }
        }

        /// <summary>
        /// Timer entry point that keeps the periodic refresh alive even when a single
        /// reload fails; failures are logged instead of surfacing on the UI thread.
        /// </summary>
        private void SafeReloadProviderSettings()
        {
            try
            {
                this.ReloadProviderSettings();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsPage] Periodic settings reload failed for {this._provider?.Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Reloads the UI control values from the currently persisted provider settings.
        /// Also re-applies descriptor-level enabled flags, button texts and status
        /// colours so dynamic descriptors stay live while the page is open.
        /// </summary>
        private void ReloadProviderSettings()
        {
            var providerSettings = SmartHopper.Infrastructure.Settings.SmartHopperSettings.Instance.GetProviderSettings(this._provider.Name);
            var descriptors = ProviderSettingDescriptors.Visible(this._provider.GetSettingDescriptors());

            foreach (var descriptor in descriptors)
            {
                if (this._controls.TryGetValue(descriptor.Name, out var control))
                {
                    control.Enabled = descriptor.Enabled;

                    switch (control)
                    {
                        case Button button:
                            button.Text = ButtonText(descriptor);
                            break;
                        case Label statusLabel:
                            this.LoadSettingValue(descriptor, statusLabel, providerSettings);
                            break;
                        case DropDown dropDown:
                            // Repopulate changed option lists without disturbing the user's selection.
                            SyncDropDownItems(descriptor, dropDown);
                            if (dropDown.SelectedIndex < 0 && descriptor.DefaultValue != null)
                            {
                                dropDown.SelectedIndex = IndexOfDropDownKey(dropDown, descriptor.DefaultValue.ToString());
                            }

                            break;
                        default:
                            this.LoadSettingValue(descriptor, control, providerSettings);
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Replaces a dropdown's items when the descriptor's allowed values changed,
        /// preserving the current selection when its key still exists.
        /// </summary>
        private static void SyncDropDownItems(SettingDescriptor descriptor, DropDown dropDown)
        {
            var options = (descriptor.AllowedValues ?? Enumerable.Empty<object>())
                .Select(value => value is SettingOption option
                    ? (Key: option.Value?.ToString() ?? string.Empty, Label: option.Label)
                    : (Key: value?.ToString() ?? string.Empty, Label: value?.ToString() ?? string.Empty))
                .ToList();

            if (dropDown.Items.Select(item => item.Key).SequenceEqual(options.Select(option => option.Key)))
            {
                return;
            }

            var selectedKey = dropDown.SelectedIndex >= 0 ? dropDown.Items[dropDown.SelectedIndex].Key : null;
            dropDown.Items.Clear();
            foreach (var option in options)
            {
                dropDown.Items.Add(new ListItem { Text = option.Label, Key = option.Key });
            }

            dropDown.SelectedIndex = IndexOfDropDownKey(dropDown, selectedKey);
        }

        /// <summary>Finds a dropdown item index by key, or -1 when absent.</summary>
        private static int IndexOfDropDownKey(DropDown dropDown, string? key)
        {
            for (var i = 0; i < dropDown.Items.Count; i++)
            {
                if (dropDown.Items[i].Key == key)
                {
                    return i;
                }
            }

            return -1;
        }

        private static string ButtonText(SettingDescriptor descriptor)
        {
            var buttonParams = descriptor.ControlParams as ButtonSettingDescriptorControl;
            return !string.IsNullOrWhiteSpace(buttonParams?.Text)
                ? buttonParams.Text
                : (!string.IsNullOrEmpty(descriptor.DisplayName) ? descriptor.DisplayName : descriptor.Name);
        }
    }
}
