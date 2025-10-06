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
using System.Globalization;
using System.IO;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Infrastructure.Settings;

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
        }

        /// <summary>
        /// Creates the layout and controls for this provider's settings
        /// </summary>
        private void CreateLayout()
        {
            // Use DynamicLayout for better control over sizing
            var layout = new DynamicLayout { Spacing = new Size(5, 5), Padding = new Padding(10) };
            var descriptors = this._provider.GetSettingDescriptors().ToList();

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
            if (this._controlFactories.ContainsKey(descriptor.Type))
            {
                return this._controlFactories[descriptor.Type](descriptor);
            }

            return null;
        }

        /// <summary>
        /// Creates a string control (TextBox, PasswordBox, or DropDown)
        /// </summary>
        /// <param name="descriptor">Setting descriptor</param>
        /// <returns>Created control</returns>
        private Control CreateStringControl(SettingDescriptor descriptor)
        {
            // If descriptor has allowed values, render a dropdown
            if (descriptor.AllowedValues != null && descriptor.AllowedValues.Any())
            {
                var dropdown = new DropDown();
                foreach (var val in descriptor.AllowedValues)
                {
                    dropdown.Items.Add(new ListItem { Text = val.ToString() });
                }

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
                        var defStr = Convert.ToString(descriptor.DefaultValue, CultureInfo.InvariantCulture) ?? string.Empty;
                        if (double.TryParse(defStr, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var defNum))
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
                        var defStr = Convert.ToString(descriptor.DefaultValue, CultureInfo.InvariantCulture) ?? string.Empty;
                        if (bool.TryParse(defStr, out var defBool))
                        {
                            checkBox.Checked = defBool;
                        }
                    }

                    break;

                case DropDown dropDown:
                    for (int i = 0; i < dropDown.Items.Count; i++)
                    {
                        if (dropDown.Items[i].Text == stringValue)
                        {
                            dropDown.SelectedIndex = i;
                            break;
                        }
                    }

                    // If no match found and there's a default value, try that
                    if (dropDown.SelectedIndex < 0 && descriptor.DefaultValue != null)
                    {
                        var defaultStr = descriptor.DefaultValue.ToString();
                        for (int i = 0; i < dropDown.Items.Count; i++)
                        {
                            if (dropDown.Items[i].Text == defaultStr)
                            {
                                dropDown.SelectedIndex = i;
                                break;
                            }
                        }
                    }

                    break;
            }
        }

        /// <summary>
        /// Saves the current UI control values back to the provider settings
        /// </summary>
        public void SaveSettings()
        {
            var updatedSettings = new Dictionary<string, object>();
            var descriptors = this._provider.GetSettingDescriptors().ToList();

            foreach (var descriptor in descriptors)
            {
                if (!this._controls.ContainsKey(descriptor.Name))
                    continue;

                var control = this._controls[descriptor.Name];
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
                            newValue = dropDown.Items[dropDown.SelectedIndex].Text;
                        }

                        break;
                }

                if (newValue != null)
                {
                    updatedSettings[descriptor.Name] = newValue;
                }
            }

            // Update provider settings via ProviderManager
            if (updatedSettings.Any())
            {
                Infrastructure.AIProviders.ProviderManager.Instance.UpdateProviderSettings(this._provider.Name, updatedSettings);
            }
        }
    }
}
