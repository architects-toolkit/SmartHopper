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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Eto.Drawing;
using Eto.Forms;
using Rhino;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Infrastructure.Properties;
using SmartHopper.Infrastructure.Settings;
using SmartHopper.Menu.Dialogs.SettingsTabs;
using SmartHopper.Menu.Dialogs.SettingsTabs.Models;

namespace SmartHopper.Menu.Dialogs
{
    /// <summary>
    /// Tabbed dialog to configure SmartHopper settings including general settings, provider management, assistant configuration, and provider-specific settings
    /// </summary>
    internal class SettingsDialog : Dialog
    {
        private static readonly Assembly ConfigAssembly = typeof(providersResources).Assembly;
        private const string IconResourceName = "SmartHopper.Infrastructure.Resources.smarthopper.ico";
        private readonly IAIProvider[] _providers;
        private readonly SmartHopperSettings _settings;

        // Tab pages
        private readonly GeneralSettingsPage _generalPage;
        private readonly ProvidersSettingsPage _providersPage;
        private readonly AssistantSettingsPage _assistantPage;
        private readonly List<GenericProviderSettingsPage> _providerPages;

        // Settings models
        private readonly GeneralSettings _generalSettings;
        private readonly TrustedProvidersSettings _trustedProvidersSettings;
        private readonly AssistantSettings _assistantSettings;

        /// <summary>
        /// Initializes a new instance of the SettingsDialog with tabbed interface.
        /// </summary>
        public SettingsDialog()
        {
            // Set window icon from embedded resource
            using (var stream = ConfigAssembly.GetManifestResourceStream(IconResourceName))
            {
                if (stream != null)
                    Icon = new Icon(stream);
            }
            this.Title = "SmartHopper Settings";
            this.Size = new Size(600, 500);
            this.MinimumSize = new Size(600, 400);
            this.Resizable = true;
            this.Padding = new Padding(10);

            // Center the dialog on screen
            Location = new Point(
                (int)((Screen.PrimaryScreen.Bounds.Width - Size.Width) / 2),
                (int)((Screen.PrimaryScreen.Bounds.Height - Size.Height) / 2)
            );

            // Load settings and synchronously discover providers on Rhino's UI thread
            this._settings = SmartHopperSettings.Instance;
            IAIProvider[] providers = null;
            RhinoApp.InvokeOnUiThread(() =>
            {
                Infrastructure.AIProviders.ProviderManager.Instance.RefreshProviders();
                providers = Infrastructure.AIProviders.ProviderManager.Instance.GetProviders(includeUntrusted: true).ToArray();
            });
            this._providers = providers;

            // Initialize settings models from global settings
            this._generalSettings = new GeneralSettings
            {
                DefaultAIProvider = this._settings.DefaultAIProvider,
                DebounceTime = this._settings.DebounceTime,
            };

            this._trustedProvidersSettings = new TrustedProvidersSettings(this._settings.TrustedProviders);

            this._assistantSettings = new AssistantSettings
            {
                EnableAIGreeting = this._settings.SmartHopperAssistant.EnableAIGreeting,
                AssistantProvider = this._settings.SmartHopperAssistant.AssistantProvider,
                AssistantModel = this._settings.SmartHopperAssistant.AssistantModel,
            };

            // Create tab pages
            this._generalPage = new GeneralSettingsPage(this._providers);
            this._assistantPage = new AssistantSettingsPage(this._providers);
            this._providersPage = new ProvidersSettingsPage(this._providers);
            this._providerPages = new List<GenericProviderSettingsPage>();

            // Create provider-specific tabs
            foreach (var provider in this._providers)
            {
                var providerPage = new GenericProviderSettingsPage(provider);
                this._providerPages.Add(providerPage);
            }

            // Load settings into pages
            this._generalPage.LoadSettings(this._generalSettings);
            this._providersPage.LoadSettings(this._trustedProvidersSettings);
            this._assistantPage.LoadSettings(this._assistantSettings);

            this.CreateTabLayout();
        }

        /// <summary>
        /// Creates the tabbed layout with all settings pages
        /// </summary>
        private void CreateTabLayout()
        {
            var tabControl = new TabControl();

            // Add General tab
            tabControl.Pages.Add(new TabPage
            {
                Text = "General",
                Content = _generalPage,
            });

            // Add Providers tab
            tabControl.Pages.Add(new TabPage
            {
                Text = "Providers",
                Content = _providersPage,
            });

            // Add SmartHopper Assistant tab
            tabControl.Pages.Add(new TabPage
            {
                Text = "Canvas Assistant",
                Content = _assistantPage,
            });

            // Add provider-specific tabs
            for (int i = 0; i < this._providers.Length; i++)
            {
                var provider = this._providers[i];
                var providerPage = this._providerPages[i];

                tabControl.Pages.Add(new TabPage
                {
                    Text = provider.Name,
                    Content = providerPage,
                });
            }

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

            // Set up the dialog content
            var content = new DynamicLayout();
            content.Add(tabControl, yscale: true);
            content.Add(buttonLayout);

            this.Content = content;
            this.DefaultButton = saveButton;
            this.AbortButton = cancelButton;

            // Handle button clicks
            saveButton.Click += (sender, e) => SaveSettings();
            cancelButton.Click += (sender, e) => Close();
        }

        /// <summary>
        /// Saves all settings from all tabs and closes the dialog
        /// </summary>
        private void SaveSettings()
        {
            try
            {
                // Save settings from each tab page
                this._generalPage.SaveSettings(this._generalSettings);
                this._providersPage.SaveSettings(this._trustedProvidersSettings);
                this._assistantPage.SaveSettings(this._assistantSettings);

                // Save provider-specific settings
                foreach (var providerPage in this._providerPages)
                {
                    providerPage.SaveSettings();
                }

                // Update global settings from models
                _settings.DefaultAIProvider = _generalSettings.DefaultAIProvider;
                _settings.DebounceTime = _generalSettings.DebounceTime;
                _settings.SmartHopperAssistant.EnableAIGreeting = _assistantSettings.EnableAIGreeting;
                _settings.SmartHopperAssistant.AssistantProvider = _assistantSettings.AssistantProvider;
                _settings.SmartHopperAssistant.AssistantModel = _assistantSettings.AssistantModel;
                _settings.TrustedProviders = new Dictionary<string, bool>(_trustedProvidersSettings);

                // Persist global settings
                _settings.Save();

                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Error saving settings: {ex.Message}", "Error", MessageBoxType.Error);
            }
        }
    }
}
