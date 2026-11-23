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
using System.Reflection;
using Eto.Drawing;
using Eto.Forms;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Infrastructure.Properties;
using SmartHopper.Infrastructure.Settings;
using SmartHopper.Menu.Dialogs.SettingsTabs;
using SmartHopper.Menu.Dialogs.SettingsTabs.Models;
using SmartHopper.ProviderSdk.AIProviders;
using SmartHopper.ProviderSdk.Hosting;
using SmartHopper.ProviderSdk.Settings;

namespace SmartHopper.Menu.Dialogs
{
    /// <summary>
    /// Identifies which tab <see cref="SettingsDialog"/> shows when opened, so
    /// callers can deep-link users to a specific settings area.
    /// </summary>
    internal enum SettingsTab
    {
        /// <summary>General settings (default).</summary>
        General,

        /// <summary>Canvas Assistant configuration.</summary>
        CanvasAssistant,

        /// <summary>Provider trust and integrity-check controls.</summary>
        Security,

        /// <summary>Telemetry and log sink configuration.</summary>
        Developer,
    }

    /// <summary>
    /// Tabbed dialog to configure SmartHopper settings including general settings, provider management, assistant configuration, and provider-specific settings
    /// </summary>
    internal sealed class SettingsDialog : Dialog
    {
        private static readonly Assembly ConfigAssembly = typeof(providersResources).Assembly;
        private const string IconResourceName = "SmartHopper.Infrastructure.Resources.smarthopper.ico";
        private readonly IAIProvider[] _providers;
        private readonly SmartHopperSettings _settings;

        // Tab pages
        private readonly GeneralSettingsPage _generalPage;
        private readonly ProvidersSettingsPage _providersPage;
        private readonly SecuritySettingsPage _securityPage;
        private readonly AssistantSettingsPage _assistantPage;
        private readonly Dictionary<string, GenericProviderSettingsPage> _providerPages;

        // Navigation between the tabbed view and a single provider's settings view
        private readonly Panel _viewContainer = new Panel();
        private Control _mainView;

        // Settings models
        private readonly GeneralSettings _generalSettings;
        private readonly LogSettings _logSettings;
        private readonly TrustedProvidersSettings _trustedProvidersSettings;
        private readonly AssistantSettings _assistantSettings;
        private ProviderIntegrityCheckMode _providerIntegrityCheckMode;

        /// <summary>
        /// Initializes a new instance of the SettingsDialog with tabbed interface.
        /// </summary>
        /// <param name="initialTab">The tab to select on open.</param>
        public SettingsDialog(SettingsTab initialTab = SettingsTab.General)
        {
            // Set window icon from embedded resource
            using (var stream = ConfigAssembly.GetManifestResourceStream(IconResourceName))
            {
                if (stream != null)
                {
                    this.Icon = new Icon(stream);
                }
            }

            this.Title = "SmartHopper Settings";
            this.Size = new Size(600, 500);
            this.MinimumSize = new Size(600, 400);
            this.Resizable = true;
            this.Padding = new Padding(10);

            // Center the dialog on screen
            this.Location = new Point(
                (int)((Screen.PrimaryScreen.Bounds.Width - this.Size.Width) / 2),
                (int)((Screen.PrimaryScreen.Bounds.Height - this.Size.Height) / 2));

            // Load settings; providers come from the state already discovered at
            // startup. Discovery is intentionally NOT triggered here: a refresh
            // inside the constructor can surface modal integrity/trust dialogs
            // before this dialog is ever shown, leaving Settings unreachable —
            // the very place needed to relax integrity mode. Mid-session rescans
            // use the explicit SmartHopper > Refresh Providers menu item.
            this._settings = SmartHopperSettings.Instance;
            this._providers = ProviderManager.Instance.GetProviders(includeUntrusted: true).ToArray();

            // Initialize settings models from global settings
            this._generalSettings = new GeneralSettings
            {
                DefaultAIProvider = this._settings.DefaultAIProvider,
                DebounceTime = this._settings.DebounceTime,
                BatchPollIntervalSeconds = this._settings.BatchPollIntervalSeconds,
            };

            this._logSettings = new LogSettings
            {
                LiveOutputEnabled = this._settings.LiveOutputEnabled,
                LiveOutputLevel = this._settings.LiveOutputLevel,
                LogFileEnabled = this._settings.LogFileEnabled,
                LogFileLevel = this._settings.LogFileLevel,
                LogRetentionDays = this._settings.LogRetentionDays,
            };

            this._trustedProvidersSettings = new TrustedProvidersSettings(this._settings.TrustedProviders);
            this._providerIntegrityCheckMode = this._settings.ProviderIntegrityCheckMode;

            this._assistantSettings = new AssistantSettings
            {
                EnableCanvasButton = this._settings.SmartHopperAssistant.EnableCanvasButton,
                EnableAIGreeting = this._settings.SmartHopperAssistant.EnableAIGreeting,
                EnableSuggestedPrompts = this._settings.SmartHopperAssistant.EnableSuggestedPrompts,
                AssistantProvider = this._settings.SmartHopperAssistant.AssistantProvider,
                AssistantModel = this._settings.SmartHopperAssistant.AssistantModel,
                MaxAutonomousTimeMinutes = this._settings.SmartHopperAssistant.MaxAutonomousTimeMinutes,
                MaxAutonomousTokens = this._settings.SmartHopperAssistant.MaxAutonomousTokens,
            };

            // Create tab pages
            this._generalPage = new GeneralSettingsPage(this._providers, this.ShowProviderSettings);
            this._assistantPage = new AssistantSettingsPage(this._providers);
            this._providersPage = new ProvidersSettingsPage(this._providers);
            this._securityPage = new SecuritySettingsPage(this._providersPage.SecurityContent);
            this._providerPages = new Dictionary<string, GenericProviderSettingsPage>();

            // Create provider-specific pages (navigated to via each provider's Configure button)
            foreach (var provider in this._providers)
            {
                var providerPage = new GenericProviderSettingsPage(provider);
                this._providerPages[provider.Name] = providerPage;
            }

            // Load settings into pages
            this._generalPage.LoadSettings(this._generalSettings, this._logSettings, this._settings.ModalityFallback, this._settings.FallbackProviderPins);
            this._providersPage.LoadSettings(this._trustedProvidersSettings, this._providerIntegrityCheckMode);
            this._assistantPage.LoadSettings(this._assistantSettings);

            this.CreateTabLayout(initialTab);
        }

        /// <summary>
        /// Creates the tabbed layout with all settings pages.
        /// </summary>
        /// <param name="initialTab">The tab to select on open.</param>
        private void CreateTabLayout(SettingsTab initialTab)
        {
            var tabControl = new TabControl();

            // Add General tab
            var generalTab = new TabPage
            {
                Text = "General",
                Content = this._generalPage,
            };
            tabControl.Pages.Add(generalTab);

            // Add SmartHopper Assistant tab
            var assistantTab = new TabPage
            {
                Text = "Canvas Assistant",
                Content = this._assistantPage,
            };
            tabControl.Pages.Add(assistantTab);

            // Provider configuration is launched from the tile grid on the General
            // tab; trust/network/origin controls all live on the Security tab.
            var securityTab = new TabPage
            {
                Text = "Security",
                Content = this._securityPage,
            };
            tabControl.Pages.Add(securityTab);

            // Log sinks live on the Developer tab.
            var developerTab = new TabPage
            {
                Text = "Developer",
                Content = this._generalPage.DeveloperContent,
            };
            tabControl.Pages.Add(developerTab);

            var selectedTab = initialTab switch
            {
                SettingsTab.CanvasAssistant => assistantTab,
                SettingsTab.Security => securityTab,
                SettingsTab.Developer => developerTab,
                _ => generalTab,
            };
            tabControl.SelectedIndex = tabControl.Pages.IndexOf(selectedTab);

            // Create buttons
            var saveButton = new Button { Text = "Save" };
            var cancelButton = new Button { Text = "Cancel" };

            var buttonLayout = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                HorizontalContentAlignment = HorizontalAlignment.Right,
                Padding = new Padding(0, 10, 0, 0),
            };
            buttonLayout.Items.Add(saveButton);
            buttonLayout.Items.Add(cancelButton);

            // Set up the dialog content: a swappable view container over the shared buttons
            this._mainView = tabControl;
            this._viewContainer.Content = this._mainView;

            var content = new DynamicLayout();
            content.Add(this._viewContainer, yscale: true);
            content.Add(buttonLayout);

            this.Content = content;
            this.DefaultButton = saveButton;
            this.AbortButton = cancelButton;

            // Handle button clicks
            saveButton.Click += (sender, e) => this.SaveSettings();
            cancelButton.Click += (sender, e) => this.Close();
        }

        /// <summary>
        /// Switches the dialog content to the given provider's settings view.
        /// The same Save button still applies — Back is navigation, not commit.
        /// </summary>
        /// <param name="provider">The provider whose settings should be shown.</param>
        private void ShowProviderSettings(IAIProvider provider)
        {
            if (provider == null || !this._providerPages.TryGetValue(provider.Name, out var providerPage))
            {
                return;
            }

            var backButton = new Button { Text = "< Back" };
            backButton.Click += (sender, e) => this.ShowMainView();

            var providerView = new DynamicLayout { Spacing = new Size(5, 5) };
            providerView.Add(new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Items = { backButton },
            });
            providerView.Add(providerPage, yscale: true);

            this._viewContainer.Content = providerView;
            this.Title = $"SmartHopper Settings — {provider.Name}";
        }

        /// <summary>
        /// Restores the main tabbed settings view.
        /// </summary>
        private void ShowMainView()
        {
            this._viewContainer.Content = this._mainView;
            this.Title = "SmartHopper Settings";
        }

        /// <summary>
        /// Saves all settings from all tabs and closes the dialog
        /// </summary>
        private void SaveSettings()
        {
            try
            {
                // Save settings from each tab page
                this._generalPage.SaveSettings(this._generalSettings, this._logSettings, out var fallbackMode, out var fallbackPins);
                this._providersPage.SaveSettings(this._trustedProvidersSettings, out var providerIntegrityCheckMode);
                this._assistantPage.SaveSettings(this._assistantSettings);
                this._securityPage.SaveSettings();

                // Save provider-specific settings
                foreach (var providerPage in this._providerPages.Values)
                {
                    providerPage.SaveSettings();
                }

                // Update global settings from models
                this._settings.DefaultAIProvider = this._generalSettings.DefaultAIProvider;
                this._settings.DebounceTime = this._generalSettings.DebounceTime;
                this._settings.BatchPollIntervalSeconds = this._generalSettings.BatchPollIntervalSeconds;
                this._settings.LiveOutputEnabled = this._logSettings.LiveOutputEnabled;
                this._settings.LiveOutputLevel = this._logSettings.LiveOutputLevel;
                this._settings.LogFileEnabled = this._logSettings.LogFileEnabled;
                this._settings.LogFileLevel = this._logSettings.LogFileLevel;
                this._settings.LogRetentionDays = this._logSettings.LogRetentionDays;
                this._settings.SmartHopperAssistant.EnableCanvasButton = this._assistantSettings.EnableCanvasButton;
                this._settings.SmartHopperAssistant.EnableAIGreeting = this._assistantSettings.EnableAIGreeting;
                this._settings.SmartHopperAssistant.EnableSuggestedPrompts = this._assistantSettings.EnableSuggestedPrompts;
                this._settings.SmartHopperAssistant.AssistantProvider = this._assistantSettings.AssistantProvider;
                this._settings.SmartHopperAssistant.AssistantModel = this._assistantSettings.AssistantModel;
                this._settings.SmartHopperAssistant.MaxAutonomousTimeMinutes = this._assistantSettings.MaxAutonomousTimeMinutes;
                this._settings.SmartHopperAssistant.MaxAutonomousTokens = this._assistantSettings.MaxAutonomousTokens;
                this._settings.TrustedProviders = new Dictionary<string, bool>(this._trustedProvidersSettings);
                this._settings.ProviderIntegrityCheckMode = providerIntegrityCheckMode;

                // Save fallback settings
                this._settings.ModalityFallback = fallbackMode;
                this._settings.FallbackProviderPins = fallbackPins;

                // Persist global settings
                this._settings.Save();

                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Error saving settings: {ex.Message}", "Error", MessageBoxType.Error);
            }
        }
    }
}
