/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using SmartHopper.Config.Configuration;
using SmartHopper.Config.Models;
using SmartHopper.Config.Providers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Drawing;
using Rhino;

namespace SmartHopper.Menu.Items
{
    internal static class SettingsMenuItem
    {
        private static readonly Dictionary<Type, Func<SettingDescriptor, Eto.Forms.Control>> ControlFactories = new Dictionary<Type, Func<SettingDescriptor, Eto.Forms.Control>>
        {
            [typeof(string)] = descriptor => 
            {
                // Use PasswordBox for secret fields, TextBox for regular text
                if (descriptor.IsSecret)
                {
                    return new Eto.Forms.PasswordBox();
                }
                else
                {
                    return new Eto.Forms.TextBox();
                }
            },
            [typeof(int)] = descriptor => new Eto.Forms.NumericStepper
            {
                MinValue = 1,
                MaxValue = 4096,
                Value = Convert.ToInt32(descriptor.DefaultValue)
            }
        };

        public static System.Windows.Forms.ToolStripMenuItem Create()
        {
            var item = new System.Windows.Forms.ToolStripMenuItem("Settings");
            item.Click += (sender, e) => ShowSettingsDialog();
            return item;
        }

        private static void ShowSettingsDialog()
        {
            // Use RhinoApp.InvokeOnUiThread to ensure UI operations run on Rhino's main UI thread
            RhinoApp.InvokeOnUiThread(new Action(() =>
            {
                using (var dialog = new Eto.Forms.Dialog())
                {
                    dialog.Title = "SmartHopper Settings";
                    dialog.Size = new Eto.Drawing.Size(500, 400);
                    dialog.MinimumSize = new Eto.Drawing.Size(400, 300);
                    dialog.Resizable = true;
                    dialog.Padding = new Eto.Drawing.Padding(10);
                    
                    // Center the dialog on screen
                    dialog.Location = new Eto.Drawing.Point(
                        (int)((Eto.Forms.Screen.PrimaryScreen.Bounds.Width - dialog.Size.Width) / 2),
                        (int)((Eto.Forms.Screen.PrimaryScreen.Bounds.Height - dialog.Size.Height) / 2)
                    );

                    var providers = SmartHopperSettings.DiscoverProviders().ToArray();
                    var settings = SmartHopperSettings.Load();

                    // Create the main layout
                    var layout = new Eto.Forms.TableLayout
                    {
                        Spacing = new Eto.Drawing.Size(5, 5),
                        Padding = new Eto.Drawing.Padding(10)
                    };

                    var scrollable = new Eto.Forms.Scrollable
                    {
                        Content = layout
                    };

                    // Dictionary to store all controls for later retrieval
                    var allControls = new Dictionary<string, Dictionary<string, Eto.Forms.Control>>();
                    // Dictionary to track original API key values to avoid overwriting unchanged values
                    var originalApiKeys = new Dictionary<string, Dictionary<string, string>>();

                    // Add general settings section
                    layout.Rows.Add(new Eto.Forms.TableRow(
                        new Eto.Forms.TableCell(new Eto.Forms.Label
                        {
                            Text = "General Settings",
                            Font = new Eto.Drawing.Font(Eto.Drawing.SystemFont.Bold, 12),
                            VerticalAlignment = Eto.Forms.VerticalAlignment.Center
                        })
                    ));

                    // Add default provider selection
                    var defaultProviderRow = new Eto.Forms.TableLayout
                    {
                        Spacing = new Eto.Drawing.Size(5, 5),
                        Padding = new Eto.Drawing.Padding(0)
                    };

                    var defaultProviderComboBox = new Eto.Forms.DropDown();
                    
                    // Add all providers to the dropdown
                    foreach (var provider in providers)
                    {
                        defaultProviderComboBox.Items.Add(new Eto.Forms.ListItem { Text = provider.Name });
                    }
                    
                    // Select the current default provider if set
                    if (!string.IsNullOrEmpty(settings.DefaultAIProvider))
                    {
                        for (int i = 0; i < defaultProviderComboBox.Items.Count; i++)
                        {
                            if (defaultProviderComboBox.Items[i].Text == settings.DefaultAIProvider)
                            {
                                defaultProviderComboBox.SelectedIndex = i;
                                break;
                            }
                        }
                    }
                    else if (defaultProviderComboBox.Items.Count > 0)
                    {
                        defaultProviderComboBox.SelectedIndex = 0;
                    }

                    defaultProviderRow.Rows.Add(new Eto.Forms.TableRow(
                        new Eto.Forms.TableCell(new Eto.Forms.Label { Text = "Default AI Provider:", VerticalAlignment = Eto.Forms.VerticalAlignment.Center }),
                        new Eto.Forms.TableCell(defaultProviderComboBox)
                    ));
                    layout.Rows.Add(defaultProviderRow);

                    // Add default provider description
                    layout.Rows.Add(new Eto.Forms.TableRow(
                        new Eto.Forms.TableCell(new Eto.Forms.Label
                        {
                            Text = "The default AI provider to use when 'Default' is selected in components",
                            TextColor = Eto.Drawing.Colors.Gray,
                            Font = new Eto.Drawing.Font(Eto.Drawing.SystemFont.Default, 10)
                        })
                    ));

                    // Add debounce time setting
                    var debounceRow = new Eto.Forms.TableLayout
                    {
                        Spacing = new Eto.Drawing.Size(5, 5),
                        Padding = new Eto.Drawing.Padding(0)
                    };

                    var debounceControl = new Eto.Forms.NumericStepper
                    {
                        MinValue = 1000,
                        MaxValue = 5000,
                        Value = settings.DebounceTime
                    };

                    debounceRow.Rows.Add(new Eto.Forms.TableRow(
                        new Eto.Forms.TableCell(new Eto.Forms.Label { Text = "Debounce Time (ms):", VerticalAlignment = Eto.Forms.VerticalAlignment.Center }),
                        new Eto.Forms.TableCell(debounceControl)
                    ));
                    layout.Rows.Add(debounceRow);

                    // Add debounce description
                    layout.Rows.Add(new Eto.Forms.TableRow(
                        new Eto.Forms.TableCell(new Eto.Forms.Label
                        {
                            Text = "Time to wait before sending a new request (in milliseconds)",
                            TextColor = Eto.Drawing.Colors.Gray,
                            Font = new Eto.Drawing.Font(Eto.Drawing.SystemFont.Default, 10)
                        })
                    ));

                    // Add provider settings
                    foreach (var provider in providers)
                    {
                        var descriptors = provider.GetSettingDescriptors().ToList();

                        // Create a row for the provider header with icon
                        var headerLayout = new Eto.Forms.StackLayout
                        {
                            Orientation = Eto.Forms.Orientation.Horizontal,
                            Spacing = 5,
                            VerticalContentAlignment = Eto.Forms.VerticalAlignment.Center,
                            Padding = new Eto.Drawing.Padding(0, 15, 0, 10)
                        };

                        // Add provider icon
                        if (provider.Icon != null)
                        {
                            // Convert System.Drawing.Image to Eto.Drawing.Image
                            using (var ms = new System.IO.MemoryStream())
                            {
                                provider.Icon.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                                ms.Position = 0;
                                var etoImage = new Eto.Drawing.Bitmap(ms);
                                var imageView = new Eto.Forms.ImageView
                                {
                                    Image = etoImage,
                                    Size = new Eto.Drawing.Size(16, 16)
                                };
                                headerLayout.Items.Add(imageView);
                            }
                        }

                        // Add provider name
                        headerLayout.Items.Add(new Eto.Forms.Label
                        {
                            Text = provider.Name,
                            Font = new Eto.Drawing.Font(Eto.Drawing.SystemFont.Bold, 12),
                            VerticalAlignment = Eto.Forms.VerticalAlignment.Center
                        });

                        layout.Rows.Add(new Eto.Forms.TableRow(new Eto.Forms.TableCell(headerLayout)));

                        var controls = new Dictionary<string, Eto.Forms.Control>();
                        originalApiKeys[provider.Name] = new Dictionary<string, string>();
                        
                        foreach (var descriptor in descriptors)
                        {
                            // Create a row for each setting
                            var settingRow = new Eto.Forms.TableLayout
                            {
                                Spacing = new Eto.Drawing.Size(5, 5),
                                Padding = new Eto.Drawing.Padding(0)
                            };

                            // Create the control based on the descriptor type
                            var control = ControlFactories[descriptor.Type](descriptor);
                            
                            settingRow.Rows.Add(new Eto.Forms.TableRow(
                                new Eto.Forms.TableCell(new Eto.Forms.Label { Text = descriptor.DisplayName + ":", VerticalAlignment = Eto.Forms.VerticalAlignment.Center }),
                                new Eto.Forms.TableCell(control)
                            ));
                            layout.Rows.Add(settingRow);

                            controls[descriptor.Name] = control;

                            // Add description if available
                            if (!string.IsNullOrWhiteSpace(descriptor.Description))
                            {
                                layout.Rows.Add(new Eto.Forms.TableRow(
                                    new Eto.Forms.TableCell(new Eto.Forms.Label
                                    {
                                        Text = descriptor.Description,
                                        TextColor = Eto.Drawing.Colors.Gray,
                                        Font = new Eto.Drawing.Font(Eto.Drawing.SystemFont.Default, 10)
                                    })
                                ));
                            }

                            // Load current value if exists
                            if (settings.ProviderSettings.ContainsKey(provider.Name) &&
                                settings.ProviderSettings[provider.Name].ContainsKey(descriptor.Name))
                            {
                                var value = settings.ProviderSettings[provider.Name][descriptor.Name];
                                
                                if (control is Eto.Forms.TextBox textBox)
                                {
                                    textBox.Text = value?.ToString() ?? "";
                                    
                                    // Store original value for API keys
                                    if (descriptor.IsSecret)
                                    {
                                        originalApiKeys[provider.Name][descriptor.Name] = textBox.Text;
                                    }
                                }
                                else if (control is Eto.Forms.PasswordBox passwordBox)
                                {
                                    passwordBox.Text = value?.ToString() ?? "";
                                    
                                    // Store original value for API keys
                                    originalApiKeys[provider.Name][descriptor.Name] = passwordBox.Text;
                                }
                                else if (control is Eto.Forms.NumericStepper numericStepper && value != null)
                                {
                                    numericStepper.Value = Convert.ToInt32(value);
                                }
                            }
                            else if (descriptor.DefaultValue != null)
                            {
                                if (control is Eto.Forms.TextBox textBox)
                                {
                                    textBox.Text = descriptor.DefaultValue.ToString();
                                }
                                else if (control is Eto.Forms.PasswordBox passwordBox)
                                {
                                    passwordBox.Text = descriptor.DefaultValue.ToString();
                                    
                                    // Store original value for API keys
                                    if (descriptor.IsSecret)
                                    {
                                        originalApiKeys[provider.Name][descriptor.Name] = passwordBox.Text;
                                    }
                                }
                                else if (control is Eto.Forms.NumericStepper numericStepper)
                                {
                                    numericStepper.Value = Convert.ToInt32(descriptor.DefaultValue);
                                }
                            }
                        }

                        allControls[provider.Name] = controls;
                    }

                    // Add a spacer row at the end
                    layout.Rows.Add(Eto.Forms.TableLayout.AutoSized(null));

                    // Create buttons
                    var buttonLayout = new Eto.Forms.StackLayout
                    {
                        Orientation = Eto.Forms.Orientation.Horizontal,
                        Spacing = 5,
                        HorizontalContentAlignment = Eto.Forms.HorizontalAlignment.Right
                    };

                    var saveButton = new Eto.Forms.Button { Text = "Save" };
                    var cancelButton = new Eto.Forms.Button { Text = "Cancel" };

                    buttonLayout.Items.Add(new Eto.Forms.StackLayoutItem(null, true)); // Spacer
                    buttonLayout.Items.Add(saveButton);
                    buttonLayout.Items.Add(cancelButton);

                    // Set up the dialog content
                    var content = new Eto.Forms.DynamicLayout();
                    content.Add(scrollable, yscale: true);
                    content.Add(buttonLayout);

                    dialog.Content = content;
                    dialog.DefaultButton = saveButton;
                    dialog.AbortButton = cancelButton;

                    // Handle button clicks
                    saveButton.Click += (sender, e) =>
                    {
                        // Save settings
                        foreach (var provider in providers)
                        {
                            if (!settings.ProviderSettings.ContainsKey(provider.Name))
                                settings.ProviderSettings[provider.Name] = new Dictionary<string, object>();

                            var controls = allControls[provider.Name];
                            foreach (var descriptor in provider.GetSettingDescriptors())
                            {
                                var control = controls[descriptor.Name];
                                object value = null;

                                // Only update API keys if they've changed
                                if (descriptor.IsSecret)
                                {
                                    string newValue = null;
                                    
                                    if (control is Eto.Forms.TextBox textBox)
                                        newValue = textBox.Text;
                                    else if (control is Eto.Forms.PasswordBox passwordBox)
                                        newValue = passwordBox.Text;
                                    
                                    // Check if API key has changed
                                    if (string.IsNullOrEmpty(newValue))
                                    {
                                        // If empty, don't update (keep existing value)
                                        continue;
                                    }
                                    else if (originalApiKeys[provider.Name].ContainsKey(descriptor.Name) && 
                                             newValue == originalApiKeys[provider.Name][descriptor.Name])
                                    {
                                        // If unchanged, don't update
                                        continue;
                                    }
                                    else
                                    {
                                        // Value has changed, update it
                                        value = newValue;
                                    }
                                }
                                else
                                {
                                    // For non-secret fields, always update
                                    if (control is Eto.Forms.TextBox textBox)
                                        value = textBox.Text;
                                    else if (control is Eto.Forms.PasswordBox passwordBox)
                                        value = passwordBox.Text;
                                    else if (control is Eto.Forms.NumericStepper numericStepper)
                                        value = (int)numericStepper.Value;
                                }

                                // Only update if we have a value to set
                                if (value != null)
                                {
                                    settings.ProviderSettings[provider.Name][descriptor.Name] = value;
                                }
                            }
                        }

                        // Save debounce time
                        settings.DebounceTime = (int)debounceControl.Value;
                        
                        // Save default provider
                        settings.DefaultAIProvider = defaultProviderComboBox.SelectedValue?.ToString() ?? "";
                        if (string.IsNullOrEmpty(settings.DefaultAIProvider) && defaultProviderComboBox.SelectedIndex >= 0)
                        {
                            settings.DefaultAIProvider = defaultProviderComboBox.Items[defaultProviderComboBox.SelectedIndex].Text;
                        }

                        settings.Save();
                        dialog.Close();
                    };

                    cancelButton.Click += (sender, e) => dialog.Close();

                    // Show the dialog
                    dialog.ShowModal();
                }
            }));
        }
    }
}
