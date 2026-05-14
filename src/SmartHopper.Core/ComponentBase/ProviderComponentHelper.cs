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
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using Grasshopper.Kernel;
using SmartHopper.Infrastructure.AIProviders;

namespace SmartHopper.Core.ComponentBase
{
    /// <summary>
    /// Helper class containing shared logic for AI provider selection, menu generation,
    /// and serialization. Used by both async and non-async provider components.
    /// </summary>
    public static class ProviderComponentHelper
    {
        /// <summary>
        /// Special value used to indicate that the default provider from settings should be used.
        /// </summary>
        public const string DEFAULT_PROVIDER = "Default";

        /// <summary>
        /// Appends provider selection menu items to the component's context menu.
        /// </summary>
        /// <param name="menu">The menu to append to.</param>
        /// <param name="currentProvider">The currently selected provider name.</param>
        /// <param name="onProviderSelected">Callback when a provider is selected.</param>
        public static void AppendProviderMenuItems(
            ToolStripDropDown menu,
            string currentProvider,
            Action<string> onProviderSelected)
        {
            var providersMenu = new ToolStripMenuItem("Select AI Provider");
            menu.Items.Add(providersMenu);

            // Add the Default option first
            var defaultItem = new ToolStripMenuItem(DEFAULT_PROVIDER)
            {
                Checked = currentProvider == DEFAULT_PROVIDER,
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

                    onProviderSelected?.Invoke(DEFAULT_PROVIDER);
                }
            };

            providersMenu.DropDownItems.Add(defaultItem);

            // Get all available providers
            var providers = ProviderManager.Instance.GetProviders();
            foreach (var provider in providers)
            {
                var item = new ToolStripMenuItem(provider.Name)
                {
                    Checked = provider.Name == currentProvider,
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

                        onProviderSelected?.Invoke(menuItem.Tag.ToString());
                    }
                };

                providersMenu.DropDownItems.Add(item);
            }
        }

        /// <summary>
        /// Gets the actual provider name to use for AI processing.
        /// If the selected provider is "Default", returns the default provider from settings.
        /// </summary>
        /// <param name="selectedProvider">The currently selected provider name.</param>
        /// <returns>The actual provider name to use.</returns>
        public static string GetActualProviderName(string selectedProvider)
        {
            if (selectedProvider == DEFAULT_PROVIDER)
            {
                return ProviderManager.Instance.GetDefaultAIProvider();
            }

            return selectedProvider;
        }

        /// <summary>
        /// Gets the currently selected AI provider instance.
        /// </summary>
        /// <param name="selectedProvider">The currently selected provider name.</param>
        /// <returns>The AI provider instance, or null if not available.</returns>
        public static AIProvider GetActualProvider(string selectedProvider)
        {
            string actualProviderName = GetActualProviderName(selectedProvider);
            var provider = ProviderManager.Instance.GetProvider(actualProviderName);

            if (provider is AIProvider concreteProvider)
            {
                return concreteProvider;
            }

            return null;
        }

        /// <summary>
        /// Writes the selected provider to the Grasshopper file.
        /// </summary>
        /// <param name="writer">The writer to use for serialization.</param>
        /// <param name="selectedProvider">The selected provider name.</param>
        /// <returns>True if the write operation succeeds.</returns>
        public static bool WriteProvider(GH_IO.Serialization.GH_IWriter writer, string selectedProvider)
        {
            try
            {
                writer.SetString("AIProvider", selectedProvider);
                Debug.WriteLine($"[ProviderComponentHelper] [Write] Stored AI provider: {selectedProvider}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProviderComponentHelper] [Write] Exception: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Reads the selected provider from the Grasshopper file.
        /// </summary>
        /// <param name="reader">The reader to use for deserialization.</param>
        /// <param name="selectedProvider">Output: the selected provider name.</param>
        /// <returns>True if the read operation succeeds.</returns>
        public static bool ReadProvider(GH_IO.Serialization.GH_IReader reader, out string selectedProvider)
        {
            try
            {
                if (reader.ItemExists("AIProvider"))
                {
                    string storedProvider = reader.GetString("AIProvider");
                    Debug.WriteLine($"[ProviderComponentHelper] [Read] Read stored AI provider: {storedProvider}");

                    // Check if the provider exists in the available providers
                    var providers = ProviderManager.Instance.GetProviders();
                    if (storedProvider == DEFAULT_PROVIDER || providers.Any(p => p.Name == storedProvider))
                    {
                        selectedProvider = storedProvider;
                        Debug.WriteLine($"[ProviderComponentHelper] [Read] Successfully restored AI provider: {selectedProvider}");
                        return true;
                    }
                    else
                    {
                        Debug.WriteLine($"[ProviderComponentHelper] [Read] Stored provider '{storedProvider}' not found, using default");
                        selectedProvider = DEFAULT_PROVIDER;
                        return true;
                    }
                }
                else
                {
                    Debug.WriteLine("[ProviderComponentHelper] [Read] No stored AI provider found, using default");
                    selectedProvider = DEFAULT_PROVIDER;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProviderComponentHelper] [Read] Exception: {ex.Message}");
                selectedProvider = DEFAULT_PROVIDER;
                return false;
            }
        }
    }
}
