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
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SmartHopper.Menu.Items
{
    internal static class SettingsMenuItem
    {
        private static readonly Dictionary<Type, Func<SettingDescriptor, Control>> ControlFactories = new Dictionary<Type, Func<SettingDescriptor, Control>>
        {
            [typeof(string)] = descriptor => new TextBox
            {
                UseSystemPasswordChar = descriptor.IsSecret,
                Dock = DockStyle.Fill
            },
            [typeof(int)] = descriptor => new NumericUpDown
            {
                Minimum = 1,
                Maximum = 4096,
                Value = Convert.ToInt32(descriptor.DefaultValue),
                Dock = DockStyle.Fill
            }
        };

        public static ToolStripMenuItem Create()
        {
            var item = new ToolStripMenuItem("Settings");
            item.Click += (sender, e) => ShowSettingsDialog();
            return item;
        }

        private static void ShowSettingsDialog()
        {
            using (var form = new Form())
            {
                form.Text = "SmartHopper Settings";
                form.Size = new Size(500, 400);
                form.StartPosition = FormStartPosition.CenterScreen;
                form.AutoScroll = true;

                var providers = SmartHopperSettings.DiscoverProviders().ToArray();
                var settings = SmartHopperSettings.Load();

                System.Diagnostics.Debug.WriteLine($"Number of providers: {providers.Length}");

                var outerPanel = new Panel
                {
                    Dock = DockStyle.Fill,
                    AutoScroll = true
                };
                form.Controls.Add(outerPanel);

                var panel = new TableLayoutPanel
                {
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    Padding = new Padding(10)
                };
                outerPanel.Controls.Add(panel);

                // Calculate total rows needed
                int totalRows = providers.Sum(p => p.GetSettingDescriptors().Count() * 2 + 1); // *2 for description rows, +1 for provider header
                totalRows += 4; // Add 4 rows: 1 for general header, 3 for default provider selection and debounce time (control + description)
                panel.RowCount = totalRows;
                panel.ColumnCount = 2;

                // Set column widths
                panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));  // First column takes 40% of the width
                panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));  // Second column takes 60% of the width

                var row = 0;
                var allControls = new Dictionary<string, Dictionary<string, Control>>();

                // Add general settings section
                var generalHeader = new Label
                {
                    Text = "General Settings",
                    Font = new Font(form.Font, FontStyle.Bold),
                    Dock = DockStyle.Fill,
                    Padding = new Padding(0, 15, 0, 10),
                    AutoSize = true
                };
                panel.Controls.Add(generalHeader, 0, row);
                panel.SetColumnSpan(generalHeader, 2);
                row++;

                // Add default provider selection
                panel.Controls.Add(new Label
                {
                    Text = "Default AI Provider:",
                    Dock = DockStyle.Fill,
                    AutoSize = true
                }, 0, row);

                var defaultProviderComboBox = new ComboBox
                {
                    Dock = DockStyle.Fill,
                    DropDownStyle = ComboBoxStyle.DropDownList
                };
                
                // Add all providers to the dropdown
                foreach (var provider in providers)
                {
                    defaultProviderComboBox.Items.Add(provider.Name);
                }
                
                // Select the current default provider if set
                if (!string.IsNullOrEmpty(settings.DefaultAIProvider) && 
                    defaultProviderComboBox.Items.Contains(settings.DefaultAIProvider))
                {
                    defaultProviderComboBox.SelectedItem = settings.DefaultAIProvider;
                }
                else if (defaultProviderComboBox.Items.Count > 0)
                {
                    defaultProviderComboBox.SelectedIndex = 0;
                }
                
                panel.Controls.Add(defaultProviderComboBox, 1, row);
                row++;

                // Add default provider description
                var defaultProviderDescription = new Label
                {
                    Text = "The default AI provider to use when 'Default' is selected in components",
                    ForeColor = SystemColors.GrayText,
                    Font = new Font(form.Font.FontFamily, form.Font.Size - 1),
                    Dock = DockStyle.Fill,
                    AutoSize = true,
                    Padding = new Padding(5, 0, 0, 5)
                };
                panel.Controls.Add(defaultProviderDescription, 0, row);
                panel.SetColumnSpan(defaultProviderDescription, 2);
                row++;

                // Add debounce time setting
                panel.Controls.Add(new Label
                {
                    Text = "Debounce Time (ms):",
                    Dock = DockStyle.Fill,
                    AutoSize = true
                }, 0, row);

                var debounceControl = new NumericUpDown
                {
                    Minimum = 1000,
                    Maximum = 5000,
                    Value = settings.DebounceTime,
                    Dock = DockStyle.Fill
                };
                panel.Controls.Add(debounceControl, 1, row);
                row++;

                // Add debounce description
                var debounceDescription = new Label
                {
                    Text = "Time to wait before sending a new request (in milliseconds)",
                    ForeColor = SystemColors.GrayText,
                    Font = new Font(form.Font.FontFamily, form.Font.Size - 1),
                    Dock = DockStyle.Fill,
                    AutoSize = true,
                    Padding = new Padding(5, 0, 0, 5)
                };
                panel.Controls.Add(debounceDescription, 0, row);
                panel.SetColumnSpan(debounceDescription, 2);
                row++;

                foreach (var provider in providers)
                {
                    System.Diagnostics.Debug.WriteLine($"Provider: {provider.Name}");
                    var descriptors = provider.GetSettingDescriptors().ToList();
                    System.Diagnostics.Debug.WriteLine($"Number of descriptors: {descriptors.Count}");

                    // Provider header
                    var header = new Label
                    {
                        Text = provider.Name,
                        Font = new Font(form.Font, FontStyle.Bold),
                        Dock = DockStyle.Fill,
                        Padding = new Padding(0, 15, 0, 10),
                        AutoSize = true
                    };
                    panel.Controls.Add(header, 0, row);
                    panel.SetColumnSpan(header, 2);
                    row++;

                    var controls = new Dictionary<string, Control>();
                    foreach (var descriptor in descriptors)
                    {
                        System.Diagnostics.Debug.WriteLine($"Creating control for: {descriptor.Name} ({descriptor.DisplayName})");

                        // Add label and control
                        panel.Controls.Add(new Label
                        {
                            Text = descriptor.DisplayName + ":",
                            Dock = DockStyle.Fill,
                            AutoSize = true
                        }, 0, row);

                        var control = ControlFactories[descriptor.Type](descriptor);
                        panel.Controls.Add(control, 1, row);
                        controls[descriptor.Name] = control;
                        row++;

                        // Add description
                        if (!string.IsNullOrWhiteSpace(descriptor.Description))
                        {
                            var descriptionLabel = new Label
                            {
                                Text = descriptor.Description,
                                ForeColor = SystemColors.GrayText,
                                Font = new Font(form.Font.FontFamily, form.Font.Size - 1),
                                Dock = DockStyle.Fill,
                                AutoSize = true,
                                Padding = new Padding(5, 0, 0, 5)
                            };
                            panel.Controls.Add(descriptionLabel, 0, row);
                            panel.SetColumnSpan(descriptionLabel, 2);
                            row++;
                        }

                        // Load current value if exists
                        if (settings.ProviderSettings.ContainsKey(provider.Name) &&
                            settings.ProviderSettings[provider.Name].ContainsKey(descriptor.Name))
                        {
                            var value = settings.ProviderSettings[provider.Name][descriptor.Name];
                            if (control is TextBox textBox)
                                textBox.Text = value?.ToString() ?? "";
                            else if (control is NumericUpDown numericUpDown && value != null)
                                numericUpDown.Value = Convert.ToInt32(value);
                        }
                        else if (descriptor.DefaultValue != null)
                        {
                            if (control is TextBox textBox)
                                textBox.Text = descriptor.DefaultValue.ToString();
                            else if (control is NumericUpDown numericUpDown)
                                numericUpDown.Value = Convert.ToInt32(descriptor.DefaultValue);
                        }
                    }

                    allControls[provider.Name] = controls;
                }

                // Add save button at the bottom
                var buttonPanel = new Panel
                {
                    Dock = DockStyle.Bottom,
                    Height = 40,
                    Padding = new Padding(5)
                };

                var saveButton = new Button
                {
                    Text = "Save",
                    DialogResult = DialogResult.OK,
                    Dock = DockStyle.Right
                };
                buttonPanel.Controls.Add(saveButton);
                form.Controls.Add(buttonPanel);
                form.AcceptButton = saveButton;

                if (form.ShowDialog() == DialogResult.OK)
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

                            if (control is TextBox textBox)
                                value = textBox.Text;
                            else if (control is NumericUpDown numericUpDown)
                                value = (int)numericUpDown.Value;

                            settings.ProviderSettings[provider.Name][descriptor.Name] = value;
                        }
                    }

                    // Save debounce time
                    settings.DebounceTime = (int)debounceControl.Value;
                    
                    // Save default provider
                    settings.DefaultAIProvider = defaultProviderComboBox.SelectedItem?.ToString() ?? "";

                    settings.Save();
                }
            }
        }
    }
}
