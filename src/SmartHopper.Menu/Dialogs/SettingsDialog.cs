/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Eto.Drawing;
using Eto.Forms;
using Rhino;
using SmartHopper.Config.Configuration;
using SmartHopper.Config.Interfaces;
using SmartHopper.Config.Managers;
using SmartHopper.Config.Models;
using SmartHopper.Config.Properties;

namespace SmartHopper.Menu.Dialogs
{
    /// <summary>
    /// Dialog to configure SmartHopper settings, including provider settings and general configuration
    /// </summary>
    internal class SettingsDialog : Dialog
    {
        private static readonly Assembly ConfigAssembly = typeof(providersResources).Assembly;
        private const string IconResourceName = "SmartHopper.Config.Resources.smarthopper.ico";

        private readonly Dictionary<Type, Func<SettingDescriptor, Control>> _controlFactories = new Dictionary<Type, Func<SettingDescriptor, Control>>
        {
            // If descriptor is a string
            [typeof(string)] = descriptor =>
            {
                // If descriptor has allowed values, render a dropdown
                if (descriptor.AllowedValues != null && descriptor.AllowedValues.Any())
                {
                    var drop = new DropDown();
                    foreach (var val in descriptor.AllowedValues)
                        drop.Items.Add(new ListItem { Text = val.ToString() });

                    // Select default value if present
                    if (descriptor.DefaultValue != null)
                    {
                        var def = descriptor.DefaultValue.ToString();
                        for (int i = 0; i < drop.Items.Count; i++)
                        {
                            if (drop.Items[i].Text == def)
                            {
                                drop.SelectedIndex = i;
                                break;
                            }
                        }
                    }
                    return drop;
                }

                // If descriptor is secret, render a password box
                if (descriptor.IsSecret)
                {
                    return new PasswordBox();
                }

                // If descriptor is a string, render a text box
                else
                {
                    return new TextBox();
                }
            },
            // If descriptor is an int, render a numeric stepper
            [typeof(int)] = descriptor => new NumericStepper
            {
                MinValue = 1,
                Value = Convert.ToInt32(descriptor.DefaultValue ?? 0),
            },
        };

        private readonly Dictionary<string, Dictionary<string, Control>> _allControls = new Dictionary<string, Dictionary<string, Control>>();
        private readonly Dictionary<string, Dictionary<string, string>> _originalValues = new Dictionary<string, Dictionary<string, string>>();
        private readonly DropDown _defaultProviderComboBox;
        private readonly NumericStepper _debounceControl;
        private readonly SmartHopperSettings _settings;
        private readonly IEnumerable<IAIProvider> _providers;

        /// <summary>
        /// Initializes a new instance of the SettingsDialog
        /// </summary>
        public SettingsDialog()
        {
            // Set window icon from embedded resource
            using (var stream = ConfigAssembly.GetManifestResourceStream(IconResourceName))
            {
                if (stream != null)
                    Icon = new Icon(stream);
            }
            Title = "SmartHopper Settings";
            Size = new Size(500, 400);
            MinimumSize = new Size(500, 400);
            Resizable = true;
            Padding = new Padding(10);

            // Center the dialog on screen
            Location = new Point(
                (int)((Screen.PrimaryScreen.Bounds.Width - Size.Width) / 2),
                (int)((Screen.PrimaryScreen.Bounds.Height - Size.Height) / 2)
            );

            // Load settings and synchronously discover providers on Rhino’s UI thread
            _settings = SmartHopperSettings.Instance;
            IAIProvider[] providers = null;
            RhinoApp.InvokeOnUiThread(() =>
            {
                ProviderManager.Instance.RefreshProviders();
                providers = ProviderManager.Instance.GetProviders().ToArray();
            });
            _providers = providers;

            // Create the main layout
            var layout = new TableLayout { Spacing = new Size(5, 5), Padding = new Padding(10) };
            var scrollable = new Scrollable { Content = layout };

            // Add general settings section
            layout.Rows.Add(new TableRow(
                new TableCell(new Label
                {
                    Text = "General Settings",
                    Font = new Font(SystemFont.Bold, 12),
                    VerticalAlignment = VerticalAlignment.Center,
                })
            ));

            // Add default provider selection
            var defaultProviderRow = new TableLayout { Spacing = new Size(5, 5) };
            _defaultProviderComboBox = new DropDown();

            // Add all providers to the dropdown and select current default
            foreach (var provider in _providers)
            {
                _defaultProviderComboBox.Items.Add(new ListItem { Text = provider.Name });
            }

            if (!string.IsNullOrEmpty(_settings.DefaultAIProvider))
            {
                for (int i = 0; i < _defaultProviderComboBox.Items.Count; i++)
                {
                    if (_defaultProviderComboBox.Items[i].Text == _settings.DefaultAIProvider)
                    {
                        _defaultProviderComboBox.SelectedIndex = i;
                        break;
                    }
                }
            }
            else if (_defaultProviderComboBox.Items.Count > 0)
            {
                _defaultProviderComboBox.SelectedIndex = 0;
            }

            defaultProviderRow.Rows.Add(new TableRow(
                new TableCell(new Label { Text = "Default AI Provider:", VerticalAlignment = VerticalAlignment.Center }),
                new TableCell(_defaultProviderComboBox)
            ));
            layout.Rows.Add(defaultProviderRow);

            // Add default provider description
            layout.Rows.Add(new TableRow(
                new TableCell(new Label
                {
                    Text = "The default AI provider to use",
                    TextColor = Colors.Gray,
                    Font = new Font(SystemFont.Default, 10),
                })
            ));

            // Add debounce time setting
            var debounceRow = new TableLayout { Spacing = new Size(5, 5) };
            _debounceControl = new NumericStepper
            {
                MinValue = 1000,
                MaxValue = 5000,
                Value = _settings.DebounceTime,
            };

            debounceRow.Rows.Add(new TableRow(
                new TableCell(new Label { Text = "Debounce Time (ms):", VerticalAlignment = VerticalAlignment.Center }),
                new TableCell(_debounceControl)
            ));
            layout.Rows.Add(debounceRow);

            // Add debounce description
            layout.Rows.Add(new TableRow(
                new TableCell(new Label
                {
                    Text = "Time to wait for input data to stabilize (no more changes) before sending a request to the AI provider (in milliseconds), specially relevant when run is permanently set to true",
                    TextColor = Colors.Gray,
                    Font = new Font(SystemFont.Default, 10),
                    Wrap = WrapMode.Word,
                    Width = 400,
                })
            ));

            // Add provider settings
            foreach (var provider in _providers)
            {
                var descriptors = provider.GetSettingDescriptors().ToList();
                var controls = new Dictionary<string, Control>();
                _originalValues[provider.Name] = new Dictionary<string, string>();

                // Create provider header with icon
                var headerLayout = new StackLayout
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 5,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Padding = new Padding(0, 15, 0, 10)
                };

                // Add provider icon if available
                if (provider.Icon != null)
                {
                    using (var ms = new MemoryStream())
                    {
                        provider.Icon.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                        ms.Position = 0;
                        headerLayout.Items.Add(new ImageView
                        {
                            Image = new Bitmap(ms),
                            Size = new Size(16, 16)
                        });
                    }
                }

                // Add provider name
                headerLayout.Items.Add(new Label
                {
                    Text = provider.Name,
                    Font = new Font(SystemFont.Bold, 12),
                    VerticalAlignment = VerticalAlignment.Center
                });

                layout.Rows.Add(new TableRow(new TableCell(headerLayout)));

                // Cache settings for this provider
                var providerSettings = _settings.GetProviderSettings(provider.Name);

                // Add settings for this provider
                foreach (var descriptor in descriptors)
                {
                    // Create control for this setting
                    var control = _controlFactories[descriptor.Type](descriptor);
                    controls[descriptor.Name] = control;

                    // Add label and control
                    var settingRow = new TableLayout { Spacing = new Size(5, 5) };
                    settingRow.Rows.Add(new TableRow(
                        new TableCell(new Label { 
                            Text = descriptor.DisplayName + ":", 
                            VerticalAlignment = VerticalAlignment.Center 
                        }),
                        new TableCell(control)
                    ));
                    layout.Rows.Add(settingRow);

                    // Add description if available
                    if (!string.IsNullOrWhiteSpace(descriptor.Description))
                    {
                        layout.Rows.Add(new TableRow(
                            new TableCell(new Label
                            {
                                Text = descriptor.Description,
                                TextColor = Colors.Gray,
                                Font = new Font(SystemFont.Default, 10)
                            })
                        ));
                    }

                    // Load current value (show placeholder for secrets)
                    string currentValue = null;
                    if (descriptor.IsSecret)
                    {
                        bool defined = false;
                        if (providerSettings.TryGetValue(descriptor.Name, out var raw) && raw is string secret && !string.IsNullOrEmpty(secret))
                            defined = true;
                        currentValue = defined ? "<secret-defined>" : string.Empty;
                    }
                    else if (providerSettings.ContainsKey(descriptor.Name))
                    {
                        currentValue = providerSettings[descriptor.Name]?.ToString();
                    }
                    else if (descriptor.DefaultValue != null)
                    {
                        currentValue = descriptor.DefaultValue.ToString();
                    }

                    // Set value to control and store original for comparison
                    if (currentValue != null)
                    {
                        if (control is TextBox textBox)
                            textBox.Text = currentValue;
                        else if (control is PasswordBox passwordBox)
                            passwordBox.Text = currentValue;
                        else if (control is NumericStepper numericStepper)
                            numericStepper.Value = Convert.ToInt32(currentValue);
                        else if (control is DropDown dropDown)
                        {
                            for (int i = 0; i < dropDown.Items.Count; i++)
                            {
                                if (dropDown.Items[i].Text == currentValue)
                                {
                                    dropDown.SelectedIndex = i;
                                    break;
                                }
                            }
                        }

                        // Store original value for comparison
                        _originalValues[provider.Name][descriptor.Name] = currentValue;
                    }
                }

                _allControls[provider.Name] = controls;
            }

            // Add a spacer row at the end
            layout.Rows.Add(TableLayout.AutoSized(null));

            // Create buttons
            var buttonLayout = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
                HorizontalContentAlignment = HorizontalAlignment.Right
            };

            var saveButton = new Button { Text = "Save" };
            var cancelButton = new Button { Text = "Cancel" };

            buttonLayout.Items.Add(new StackLayoutItem(null, true)); // Spacer
            buttonLayout.Items.Add(saveButton);
            buttonLayout.Items.Add(cancelButton);

            // Set up the dialog content
            var content = new DynamicLayout();
            content.Add(scrollable, yscale: true);
            content.Add(buttonLayout);

            Content = content;
            DefaultButton = saveButton;
            AbortButton = cancelButton;

            // Handle button clicks
            saveButton.Click += (sender, e) => SaveSettings();
            cancelButton.Click += (sender, e) => Close();
        }

        /// <summary>
        /// Saves the current settings and closes the dialog
        /// </summary>
        private void SaveSettings()
        {
            // Prepare containers for updated values (merge happens in ProviderManager)
            var updatedSettings = new Dictionary<string, Dictionary<string, object>>();
            foreach (var provider in _providers)
            {
                updatedSettings[provider.Name] = new Dictionary<string, object>();
            }
            
            // Update with new values from the UI
            foreach (var provider in _providers)
            {
                if (!updatedSettings.ContainsKey(provider.Name))
                    updatedSettings[provider.Name] = new Dictionary<string, object>();

                var controls = _allControls[provider.Name];
                foreach (var descriptor in provider.GetSettingDescriptors())
                {
                    var control = controls[descriptor.Name];
                    
                    // Get new value from control
                    object newValue = null;
                    if (control is TextBox textBox)
                        newValue = textBox.Text;
                    else if (control is PasswordBox passwordBox)
                        newValue = passwordBox.Text;
                    else if (control is NumericStepper numericStepper)
                        newValue = (int)numericStepper.Value;
                    else if (control is DropDown dropDown)
                    {
                        if (dropDown.SelectedIndex >= 0)
                            newValue = dropDown.Items[dropDown.SelectedIndex].Text;
                    }
                    
                    // For sensitive data, only update if changed and not empty
                    if (descriptor.IsSecret && newValue is string strValue)
                    {
                        if (_originalValues[provider.Name].ContainsKey(descriptor.Name) && 
                            strValue == _originalValues[provider.Name][descriptor.Name])
                            continue; // Skip unchanged values
                    }
                    
                    // Update the setting
                    updatedSettings[provider.Name][descriptor.Name] = newValue;
                }
            }

            // Persist each provider's partial updates via ProviderManager
            foreach (var kvp in updatedSettings)
            {
                ProviderManager.Instance.UpdateProviderSettings(kvp.Key, kvp.Value);
            }

            // Update settings
            _settings.DebounceTime = (int)_debounceControl.Value;
            
            // Save default provider
            if (_defaultProviderComboBox.SelectedIndex >= 0)
                _settings.DefaultAIProvider = _defaultProviderComboBox.Items[_defaultProviderComboBox.SelectedIndex].Text;
            
            // Persist global settings
            _settings.Save();
            Close();
        }
    }
}
