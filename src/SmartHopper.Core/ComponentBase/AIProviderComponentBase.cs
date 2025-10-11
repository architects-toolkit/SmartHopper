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
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using SmartHopper.Infrastructure.AIProviders;

namespace SmartHopper.Core.ComponentBase
{
    /// <summary>
    /// Base class for components that need AI provider selection functionality.
    /// Provides the provider selection context menu and related functionality on top of
    /// the stateful async component functionality.
    /// </summary>
    public abstract class AIProviderComponentBase : StatefulAsyncComponentBase
    {
        /// <summary>
        /// Special value used to indicate that the default provider from settings should be used.
        /// </summary>
        public const string DEFAULT_PROVIDER = "Default";

        /// <summary>
        /// The currently selected AI provider.
        /// </summary>
        private string aiProvider = DEFAULT_PROVIDER;

        /// <summary>
        /// The previously selected AI provider, used for change detection.
        /// </summary>
        private string previousSelectedProvider = DEFAULT_PROVIDER;

        /// <summary>
        /// Initializes a new instance of the <see cref="AIProviderComponentBase"/> class.
        /// </summary>
        /// <param name="name">The name of the component.</param>
        /// <param name="nickname">The nickname of the component.</param>
        /// <param name="description">The description of the component.</param>
        /// <param name="category">The category of the component.</param>
        /// <param name="subcategory">The subcategory of the component.</param>
        protected AIProviderComponentBase(string name, string nickname, string description, string category, string subcategory)
            : base(name, nickname, description, category, subcategory)
        {
        }

        /// <summary>
        /// Appends additional menu items to the component's context menu.
        /// </summary>
        /// <param name="menu">The menu to append to.</param>
        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);

            // Add provider selection submenu
            var providersMenu = new ToolStripMenuItem("Select AI Provider");
            menu.Items.Add(providersMenu);

            // Add the Default option first
            var defaultItem = new ToolStripMenuItem(DEFAULT_PROVIDER)
            {
                Checked = this.aiProvider == DEFAULT_PROVIDER,
                CheckOnClick = true,
                Tag = DEFAULT_PROVIDER,
            };

            defaultItem.Click += (s, e) =>
            {
                var menuItem = s as ToolStripMenuItem;
                if (menuItem != null)
                {
                    // Uncheck all other items
                    foreach (ToolStripMenuItem otherItem in providersMenu.DropDownItems)
                    {
                        if (otherItem != menuItem)
                        {
                            otherItem.Checked = false;
                        }
                    }

                    this.aiProvider = DEFAULT_PROVIDER;
                    this.ExpireSolution(true);
                }
            };

            providersMenu.DropDownItems.Add(defaultItem);

            // Get all available providers
            var providers = ProviderManager.Instance.GetProviders();
            foreach (var provider in providers)
            {
                var item = new ToolStripMenuItem(provider.Name)
                {
                    Checked = provider.Name == this.aiProvider,
                    CheckOnClick = true,
                    Tag = provider.Name,
                };

                item.Click += (s, e) =>
                {
                    var menuItem = s as ToolStripMenuItem;
                    if (menuItem != null)
                    {
                        // Uncheck all other items
                        foreach (ToolStripMenuItem otherItem in providersMenu.DropDownItems)
                        {
                            if (otherItem != menuItem)
                            {
                                otherItem.Checked = false;
                            }
                        }

                        this.aiProvider = menuItem.Tag.ToString();
                        this.ExpireSolution(true);
                    }
                };

                providersMenu.DropDownItems.Add(item);
            }
        }

        /// <summary>
        /// Gets the actual provider name to use for AI processing.
        /// If the selected provider is "Default", returns the default provider from settings.
        /// </summary>
        /// <returns>The actual provider name to use.</returns>
        public string GetActualAIProviderName()
        {
            if (this.aiProvider == DEFAULT_PROVIDER)
            {
                // Use the ProviderManager to get the default provider
                return ProviderManager.Instance.GetDefaultAIProvider();
            }

            return this.aiProvider;
        }

        /// <summary>
        /// Gets the currently selected AI provider instance.
        /// </summary>
        /// <returns>The AI provider instance, or null if not available.</returns>
        protected AIProvider? GetActualAIProvider()
        {
            string actualProviderName = this.GetActualAIProviderName();
            var provider = ProviderManager.Instance.GetProvider(actualProviderName);

            if (provider is AIProvider concreteProvider)
            {
                return concreteProvider;
            }

            return null;
        }

        /// <summary>
        /// Creates the custom attributes for this component, which includes the provider logo badge.
        /// </summary>
        public override void CreateAttributes()
        {
            this.m_attributes = new AIProviderComponentAttributes(this);
        }

        /// <summary>
        /// Writes the component's persistent data to the Grasshopper file.
        /// </summary>
        /// <param name="writer">The writer to use for serialization.</param>
        /// <returns>True if the write operation succeeds, false if it fails or an exception occurs.</returns>
        public override bool Write(GH_IO.Serialization.GH_IWriter writer)
        {
            if (!base.Write(writer))
            {
                return false;
            }

            try
            {
                // Store the selected AI provider
                writer.SetString("AIProvider", this.aiProvider);
                Debug.WriteLine($"[AIProviderComponentBase] [Write] Stored AI provider: {this.aiProvider}");

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AIProviderComponentBase] [Write] Exception: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Reads the component's persistent data from the Grasshopper file.
        /// </summary>
        /// <param name="reader">The reader to use for deserialization.</param>
        /// <returns>True if the read operation succeeds, false if it fails, required data is missing, or an exception occurs.</returns>
        public override bool Read(GH_IO.Serialization.GH_IReader reader)
        {
            if (!base.Read(reader))
            {
                return false;
            }

            try
            {
                // Read the stored AI provider if available
                if (reader.ItemExists("AIProvider"))
                {
                    string storedProvider = reader.GetString("AIProvider");
                    Debug.WriteLine($"[AIProviderComponentBase] [Read] Read stored AI provider: {storedProvider}");

                    // Check if the provider exists in the available providers
                    var providers = ProviderManager.Instance.GetProviders();
                    if (providers.Any(p => p.Name == storedProvider))
                    {
                        this.aiProvider = storedProvider;
                        this.previousSelectedProvider = storedProvider;
                        Debug.WriteLine($"[AIProviderComponentBase] [Read] Successfully restored AI provider: {this.aiProvider}");
                    }
                    else
                    {
                        Debug.WriteLine($"[AIProviderComponentBase] [Read] Stored provider '{storedProvider}' not found, using default");
                        this.aiProvider = DEFAULT_PROVIDER;
                        this.previousSelectedProvider = DEFAULT_PROVIDER;
                    }
                }
                else
                {
                    Debug.WriteLine("[AIProviderComponentBase] [Read] No stored AI provider found, using default");
                    this.aiProvider = DEFAULT_PROVIDER;
                    this.previousSelectedProvider = DEFAULT_PROVIDER;
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AIProviderComponentBase] [Read] Exception: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets the selected AI provider name.
        /// </summary>
        protected string SelectedProviderName => this.aiProvider;

        /// <summary>
        /// Sets the selected AI provider name.
        /// </summary>
        /// <param name="providerName">The provider name to set.</param>
        protected void SetSelectedProviderName(string providerName)
        {
            this.aiProvider = providerName;
        }

        /// <summary>
        /// Checks if the provider selection has changed since last time.
        /// </summary>
        /// <returns>True if the provider selection has changed.</returns>
        protected bool HasProviderChanged()
        {
            if (this.aiProvider != this.previousSelectedProvider)
            {
                this.previousSelectedProvider = this.aiProvider;
                return true;
            }

            return false;
        }

        protected override List<string> InputsChanged()
        {
            List<string> changedInputs = base.InputsChanged();

            if (this.HasProviderChanged())
            {
                changedInputs.Add("AIProvider");
            }

            return changedInputs;
        }
    }
}
