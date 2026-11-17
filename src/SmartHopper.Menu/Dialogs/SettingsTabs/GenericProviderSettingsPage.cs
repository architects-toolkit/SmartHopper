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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eto.Drawing;
using Eto.Forms;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Infrastructure.Dialogs;
using SmartHopper.Infrastructure.Settings;
using SmartHopper.ProviderSdk.AIProviders;
using SmartHopper.ProviderSdk.Logging;
using SmartHopper.ProviderSdk.Settings;

namespace SmartHopper.Menu.Dialogs.SettingsTabs
{
    /// <summary>
    /// Generic settings page that renders a provider's ordered settings surface: persisted
    /// settings (<see cref="BoundSettingElement"/>) bound to the settings store, interleaved
    /// with live chrome (<see cref="ChromeElement"/>: status labels, action buttons, links)
    /// fed by delegates that are re-evaluated on every periodic repaint and never touch the
    /// settings store.
    /// </summary>
    public class GenericProviderSettingsPage : Panel
    {
        private readonly IAIProvider _provider;
        private readonly Dictionary<string, BoundSettingElement> _boundElements;
        private readonly Dictionary<string, Control> _controls;
        private readonly Dictionary<string, Button> _companionControls;
        private readonly Dictionary<BoundSettingElement, (ActionChrome Action, Button Button)> _companionActions;
        private readonly List<ChromeBinding> _chromeBindings;
        private readonly Dictionary<string, string> _originalValues;
        private readonly Dictionary<Type, Func<SettingDescriptor, Control>> _controlFactories;
        private UITimer? _refreshTimer;

        // Bound each control's preferred width: drop-downs size to their longest
        // item (e.g. team display labels), and inside the Scrollable every scaled
        // row stretches to the widest row — pushing buttons and pickers past the
        // dialog edge and forcing a horizontal scrollbar. The budget is ~500px
        // for the widest row (150px label + spacing + control + companion
        // button + layout padding), so the page fits the default 600px dialog
        // even when the vertical scrollbar is showing.
        private const int ControlWidth = 330;
        private const int CompanionControlWidth = 220;

        /// <summary>
        /// Initializes a new instance of the GenericProviderSettingsPage class
        /// </summary>
        /// <param name="provider">The AI provider whose settings to display</param>
        public GenericProviderSettingsPage(IAIProvider provider)
        {
            this._provider = provider;
            this._boundElements = new Dictionary<string, BoundSettingElement>();
            this._controls = new Dictionary<string, Control>();
            this._companionControls = new Dictionary<string, Button>();
            this._companionActions = new Dictionary<BoundSettingElement, (ActionChrome, Button)>();
            this._chromeBindings = new List<ChromeBinding>();
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
            this._refreshTimer = new UITimer { Interval = 2 };
            this._refreshTimer.Elapsed += (sender, e) => this.SafeReloadProviderSettings();

            // Pages for every provider are constructed eagerly by SettingsDialog, but only
            // navigated-to pages are ever loaded. Starting the timer in the constructor leaks
            // a 2-second full-settings sweep for the lifetime of the process for every page
            // that is never shown — gate it on Load/UnLoad instead.
            this.Load += (sender, e) => this._refreshTimer?.Start();
            this.UnLoad += (sender, e) => this._refreshTimer?.Stop();
        }

        /// <summary>
        /// Creates the layout and controls for this provider's settings
        /// </summary>
        private void CreateLayout()
        {
            // Use DynamicLayout for better control over sizing
            var layout = new DynamicLayout { Spacing = new Size(5, 5), Padding = new Padding(10) };
            var surface = (this._provider.GetSurface() ?? Enumerable.Empty<ProviderSurfaceElement>())
                .Where(element => element != null)
                .ToList();

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
                    var icon = ProviderTileIcon.Load(this._provider, 24);
                    if (icon != null)
                    {
                        titleLayout.Items.Add(new ImageView { Image = icon });
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

            if (!surface.Any())
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

                // Render the ordered surface
                foreach (var element in surface)
                {
                    switch (element)
                    {
                        case BoundSettingElement bound:
                            this.AddBoundSettingRow(layout, bound, providerSettings);
                            break;
                        case StatusChrome status:
                            // Status rows carry no description: the live label is the
                            // whole message, and a static copy of it would go stale.
                            this.AddChromeRow(layout, status, this.CreateStatusChromeControl(status), string.Empty);
                            break;
                        case ActionChrome action:
                            this.AddChromeRow(layout, action, this.CreateActionChromeControl(action), SafeEvaluate(action.Description) ?? string.Empty);
                            break;
                        case LinkChrome link:
                            this.AddChromeRow(layout, link, this.CreateLinkChromeControl(link), link.Description ?? string.Empty);
                            break;
                    }
                }
            }

            this.Content = new Scrollable { Content = layout };
        }

        /// <summary>
        /// Adds a persisted-setting row: label, editor (with optional companion action)
        /// and description, bound to the settings store.
        /// </summary>
        private void AddBoundSettingRow(DynamicLayout layout, BoundSettingElement element, Dictionary<string, object> providerSettings)
        {
            var descriptor = element.Descriptor;
            if (descriptor == null || descriptor.IsHidden)
            {
                return;
            }

            // Register before control creation so live option/default hooks resolve.
            this._boundElements[descriptor.Name] = element;

            var control = this.CreateControlForDescriptor(descriptor);
            if (control == null)
            {
                this._boundElements.Remove(descriptor.Name);
                return;
            }

            // Honor the live enable-state hook, falling back to the static flag
            control.Enabled = SafeEvaluateValue(element.Enabled) ?? descriptor.Enabled;

            // Companion-button rows share the cell, so the control gets a narrower bound
            var hasCompanion = element.Companion != null || descriptor.CompanionDescriptor != null;
            control.Width = hasCompanion ? CompanionControlWidth : ControlWidth;

            this._controls[descriptor.Name] = control;

            // Load current value
            this.LoadSettingValue(element, control, providerSettings);

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

            Control controlCell = control;
            if (element.Companion != null)
            {
                var companionButton = this.CreateActionChromeControl(element.Companion);
                this._companionActions[element] = (element.Companion, companionButton);
                var stack = new StackLayout
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    VerticalContentAlignment = VerticalAlignment.Center,
                };
                stack.Items.Add(control);
                stack.Items.Add(companionButton);
                controlCell = stack;
            }
            else if (descriptor.CompanionDescriptor != null)
            {
                var companion = descriptor.CompanionDescriptor;
                var companionButton = (Button)this.CreateButtonControl(companion);
                companionButton.Enabled = companion.Enabled;
                this._companionControls[companion.Name] = companionButton;
                var stack = new StackLayout
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    VerticalContentAlignment = VerticalAlignment.Center,
                };
                stack.Items.Add(control);
                stack.Items.Add(companionButton);
                controlCell = stack;
            }

            // Create row with fixed label width and expandable control
            rowLayout.Rows.Add(new TableRow(
                new TableCell(label, false),  // Fixed size
                new TableCell(controlCell, true)));   // Expandable

            layout.Add(rowLayout);

            // Add description if available - spans full width below the label+control
            if (!string.IsNullOrEmpty(descriptor.Description))
            {
                layout.Add(SettingsText.DescriptionLabel(descriptor.Description));
            }

            // Add spacing between settings
            layout.Add(new Panel { Height = 10 });
        }

        /// <summary>
        /// Adds a chrome row (status, action, link): label, control and description, and
        /// registers it for live delegate repaints. Chrome never produces store keys.
        /// </summary>
        private void AddChromeRow(DynamicLayout layout, ChromeElement element, Control control, string description)
        {
            var label = new Label
            {
                Text = (string.IsNullOrEmpty(element.Title) ? element.GetType().Name : element.Title) + ":",
                VerticalAlignment = VerticalAlignment.Center,
                Width = 150,
            };

            // Chrome rows have no explicitly scaled column, so Eto auto-scales the
            // last column and would stretch a bare control (e.g. the Connect button)
            // to the full row width. A non-scaling stack keeps the control at its
            // natural size inside the stretched cell.
            var cell = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                VerticalContentAlignment = VerticalAlignment.Center,
                Items = { control },
            };

            var rowLayout = new TableLayout { Spacing = new Size(10, 0) };
            rowLayout.Rows.Add(new TableRow(
                new TableCell(label, false),
                new TableCell(cell, false)));

            layout.Add(rowLayout);

            var descriptionLabel = SettingsText.DescriptionLabel(description);
            descriptionLabel.Visible = !string.IsNullOrEmpty(description);
            layout.Add(descriptionLabel);
            layout.Add(new Panel { Height = 10 });

            this._chromeBindings.Add(new ChromeBinding(element, control, descriptionLabel));
        }

        /// <summary>
        /// Evaluates a live surface delegate, falling back on failure so a misbehaving
        /// provider hook cannot break the settings page.
        /// </summary>
        private static T? SafeEvaluate<T>(Func<T>? provider)
            where T : class
        {
            if (provider == null)
            {
                return null;
            }

            try
            {
                return provider();
            }
            catch (Exception ex)
            {
                Logger.For<GenericProviderSettingsPage>().Exception(ex, "[SettingsPage] Live surface delegate failed");
                return null;
            }
        }

        /// <summary>
        /// Evaluates a live value-type surface delegate (e.g. enabled flags), falling back
        /// on failure so a misbehaving provider hook cannot break the settings page.
        /// </summary>
        private static T? SafeEvaluateValue<T>(Func<T>? provider)
            where T : struct
        {
            if (provider == null)
            {
                return null;
            }

            try
            {
                return provider();
            }
            catch (Exception ex)
            {
                Logger.For<GenericProviderSettingsPage>().Exception(ex, "[SettingsPage] Live surface delegate failed");
                return null;
            }
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

            statusLabel.TextColor = LevelColor(level);
        }

        /// <summary>Maps a semantic status level to the status-label colour.</summary>
        private static Color LevelColor(string? level)
            => level switch
            {
                "ok" => Colors.Green,
                "busy" => Colors.Orange,
                "error" => Colors.Red,
                _ => Colors.Gray,
            };

        /// <summary>
        /// Creates a hyperlink that opens the configured URL in the browser.
        /// </summary>
        private Control CreateLinkControl(SettingDescriptor descriptor)
        {
            var linkParams = (LinkSettingDescriptorControl)descriptor.ControlParams!;
            return CreateLinkButton(linkParams.Url, linkParams.Text);
        }

        /// <summary>
        /// Creates a hyperlink that opens the configured URL in the browser.
        /// </summary>
        private static LinkButton CreateLinkButton(string url, string? text)
        {
            var link = new LinkButton
            {
                Text = !string.IsNullOrWhiteSpace(text) ? text : url,
            };
            link.Click += (sender, e) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = url,
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
        /// Creates a read-only status label bound to a <see cref="StatusChrome"/> live
        /// text and level delegates.
        /// </summary>
        private Control CreateStatusChromeControl(StatusChrome status)
        {
            return new Label
            {
                Text = SafeEvaluate(status.Text) ?? string.Empty,
                TextColor = LevelColor(SafeEvaluate(status.Level)),
                Wrap = WrapMode.Word,
                Width = ControlWidth,   // Bound preferred width: a wrapping label reports its full unwrapped text
                Font = new Font(SystemFont.Bold),
                VerticalAlignment = VerticalAlignment.Center,
            };
        }

        /// <summary>
        /// Creates a button bound to an <see cref="ActionChrome"/> live text, description
        /// and enabled delegates; clicking invokes the action with the current form state.
        /// </summary>
        private Button CreateActionChromeControl(ActionChrome action)
        {
            var button = new Button
            {
                Text = SafeEvaluate(action.Text) ?? action.Title,
                Enabled = SafeEvaluateValue(action.Enabled) ?? true,
            };
            button.Click += (sender, e) => this.RunChromeActionAsync(action);
            return button;
        }

        /// <summary>
        /// Creates a hyperlink bound to a <see cref="LinkChrome"/>.
        /// </summary>
        private Control CreateLinkChromeControl(LinkChrome link)
        {
            return CreateLinkButton(link.Url, link.Text);
        }

        /// <summary>
        /// Validates and persists the current form, then invokes a chrome action with the
        /// validated settings. Validation failure blocks the action, matching the legacy
        /// pseudo-setting flow.
        /// </summary>
        private async void RunChromeActionAsync(ActionChrome action)
        {
            try
            {
                var form = this.CollectSettings() ?? new Dictionary<string, object>();
                if (form.Count > 0)
                {
                    var settingsUi = Infrastructure.AIProviders.ProviderManager.Instance.GetProviderSettings(this._provider.Name);
                    if (settingsUi?.ValidateSettings(form) == false)
                    {
                        return;
                    }

                    Infrastructure.AIProviders.ProviderManager.Instance.UpdateProviderSettings(this._provider.Name, form);
                }

                if (action.InvokeAsync != null)
                {
                    await action.InvokeAsync(form, CancellationToken.None).ConfigureAwait(true);
                }

                this.SafeReloadProviderSettings();
            }
            catch (Exception ex)
            {
                Logger.For<GenericProviderSettingsPage>().Exception(ex, $"[SettingsPage] Surface action failed for {this._provider?.Name}");
                StyledMessageDialog.ShowError($"Action failed: {ex.Message}", "Error");
            }
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
            if (descriptor.AllowedValues != null || this.HasLiveAllowedValues(descriptor))
            {
                var dropdown = new DropDown();
                SyncDropDownItems(this.AllowedValuesFor(descriptor), dropdown);
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

        /// <summary>Whether the bound element for <paramref name="descriptor"/> supplies live options.</summary>
        private bool HasLiveAllowedValues(SettingDescriptor descriptor)
            => this._boundElements.TryGetValue(descriptor.Name, out var element) && element.AllowedValues != null;

        /// <summary>Resolves dropdown options for a bound setting: live hook first, static list as fallback.</summary>
        private IEnumerable<object>? AllowedValuesFor(SettingDescriptor descriptor)
            => this._boundElements.TryGetValue(descriptor.Name, out var element)
                ? (SafeEvaluate(element.AllowedValues) ?? descriptor.AllowedValues)
                : descriptor.AllowedValues;

        /// <summary>Resolves the default value for a bound setting: live hook first, static default as fallback.</summary>
        private object? DefaultValueFor(SettingDescriptor descriptor)
            => this._boundElements.TryGetValue(descriptor.Name, out var element)
                ? (SafeEvaluate(element.DefaultValue) ?? descriptor.DefaultValue)
                : descriptor.DefaultValue;

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
        /// <param name="element">Bound surface element (descriptor plus live hooks)</param>
        /// <param name="control">Control to load value into</param>
        /// <param name="providerSettings">Current provider settings</param>
        private void LoadSettingValue(BoundSettingElement element, Control control, Dictionary<string, object> providerSettings)
        {
            var descriptor = element.Descriptor;
            var currentValue = providerSettings.ContainsKey(descriptor.Name)
                ? providerSettings[descriptor.Name]
                : this.DefaultValueFor(descriptor);

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
                    else
                    {
                        var defaultValue = this.DefaultValueFor(descriptor);
                        if (defaultValue != null)
                        {
                            // Try direct numeric conversion first, then fall back to string parsing
                            if (defaultValue is double defNum ||
                                double.TryParse(defaultValue.ToString(), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out defNum))
                            {
                                numericStepper.Value = defNum;
                            }
                        }
                    }

                    break;

                case CheckBox checkBox:
                    if (bool.TryParse(stringValue, out var boolValue))
                    {
                        checkBox.Checked = boolValue;
                    }
                    else
                    {
                        var defaultValue = this.DefaultValueFor(descriptor);
                        if (defaultValue != null)
                        {
                            // Try direct bool conversion first, then fall back to string parsing
                            if (defaultValue is bool defBool ||
                                bool.TryParse(defaultValue.ToString(), out defBool))
                            {
                                checkBox.Checked = defBool;
                            }
                        }
                    }

                    break;

                case DropDown dropDown:
                    SyncDropDownItems(this.AllowedValuesFor(descriptor), dropDown);
                    dropDown.SelectedIndex = IndexOfDropDownKey(dropDown, stringValue);
                    if (dropDown.SelectedIndex < 0)
                    {
                        var defaultValue = this.DefaultValueFor(descriptor);
                        if (defaultValue != null)
                        {
                            dropDown.SelectedIndex = IndexOfDropDownKey(dropDown, defaultValue.ToString());
                        }
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
        /// Replaces a dropdown's items when the descriptor's allowed values changed,
        /// preserving the current selection when its key still exists.
        /// </summary>
        private static void SyncDropDownItems(IEnumerable<object>? allowedValues, DropDown dropDown)
        {
            var options = (allowedValues ?? Enumerable.Empty<object>())
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

        /// <summary>
        /// Collects the current UI control values into a settings dictionary.
        /// Chrome rows contribute nothing — they never reach the store.
        /// </summary>
        /// <returns>A dictionary of setting values from the UI controls.</returns>
        private Dictionary<string, object> CollectSettings()
        {
            var updatedSettings = new Dictionary<string, object>();

            foreach (var (name, element) in this._boundElements)
            {
                if (!this._controls.TryGetValue(name, out var control))
                {
                    continue;
                }

                var descriptor = element.Descriptor;
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
                Logger.For<GenericProviderSettingsPage>().Exception(ex, $"[SettingsPage] Periodic settings reload failed for {this._provider?.Name}");
            }
        }

        /// <summary>
        /// Reloads the UI control values: persisted values are re-read for bound settings,
        /// and every live surface delegate (options, enabled flags, companion and chrome
        /// state) is re-evaluated so dynamic rows stay current without storage traffic.
        /// </summary>
        private void ReloadProviderSettings()
        {
            var providerSettings = SmartHopper.Infrastructure.Settings.SmartHopperSettings.Instance.GetProviderSettings(this._provider.Name);

            foreach (var (name, element) in this._boundElements)
            {
                var descriptor = element.Descriptor;
                if (this._controls.TryGetValue(name, out var control))
                {
                    control.Enabled = SafeEvaluateValue(element.Enabled) ?? descriptor.Enabled;

                    switch (control)
                    {
                        case Button button:
                            button.Text = ButtonText(descriptor);
                            break;
                        case Label statusLabel:
                            this.LoadSettingValue(element, statusLabel, providerSettings);
                            break;
                        case DropDown dropDown:
                            // Repopulate changed option lists without disturbing the user's selection.
                            SyncDropDownItems(this.AllowedValuesFor(descriptor), dropDown);
                            if (dropDown.SelectedIndex < 0)
                            {
                                var defaultValue = this.DefaultValueFor(descriptor);
                                if (defaultValue != null)
                                {
                                    dropDown.SelectedIndex = IndexOfDropDownKey(dropDown, defaultValue.ToString());
                                }
                            }

                            break;
                    }
                }

                if (this._companionActions.TryGetValue(element, out var companionAction))
                {
                    companionAction.Button.Enabled = SafeEvaluateValue(companionAction.Action.Enabled) ?? true;
                    companionAction.Button.Text = SafeEvaluate(companionAction.Action.Text) ?? companionAction.Action.Title;
                }

                var companion = descriptor.CompanionDescriptor;
                if (companion != null && this._companionControls.TryGetValue(companion.Name, out var legacyCompanionButton))
                {
                    legacyCompanionButton.Enabled = companion.Enabled;
                    legacyCompanionButton.Text = ButtonText(companion);
                }
            }

            foreach (var binding in this._chromeBindings)
            {
                switch (binding.Element)
                {
                    case StatusChrome status when binding.Control is Label statusLabel:
                        statusLabel.Text = SafeEvaluate(status.Text) ?? string.Empty;
                        statusLabel.TextColor = LevelColor(SafeEvaluate(status.Level));
                        break;
                    case ActionChrome action when binding.Control is Button actionButton:
                        actionButton.Text = SafeEvaluate(action.Text) ?? action.Title;
                        actionButton.Enabled = SafeEvaluateValue(action.Enabled) ?? true;
                        if (binding.DescriptionLabel != null)
                        {
                            var description = SafeEvaluate(action.Description) ?? string.Empty;
                            binding.DescriptionLabel.Text = description;
                            binding.DescriptionLabel.Visible = !string.IsNullOrEmpty(description);
                        }

                        break;
                }
            }
        }

        private static string ButtonText(SettingDescriptor descriptor)
        {
            var buttonParams = descriptor.ControlParams as ButtonSettingDescriptorControl;
            return !string.IsNullOrWhiteSpace(buttonParams?.Text)
                ? buttonParams.Text
                : (!string.IsNullOrEmpty(descriptor.DisplayName) ? descriptor.DisplayName : descriptor.Name);
        }

        /// <summary>A chrome control plus its optional description label, tracked for live repaints.</summary>
        private sealed class ChromeBinding
        {
            public ChromeBinding(ChromeElement element, Control control, Label? descriptionLabel)
            {
                this.Element = element;
                this.Control = control;
                this.DescriptionLabel = descriptionLabel;
            }

            public ChromeElement Element { get; }

            public Control Control { get; }

            public Label? DescriptionLabel { get; }
        }
    }
}
