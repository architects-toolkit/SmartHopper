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
using SmartHopper.Core.ComponentBase.Contracts;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.ProviderSdk.AIProviders;

namespace SmartHopper.Core.ComponentBase.Cores
{
    /// <summary>
    /// Instance helper that owns the state required for AI provider selection on a
    /// single component. Mirrors the composition pattern used by
    /// <see cref="SelectingComponentCore"/>: the component owns a core and delegates
    /// menu, persistence, resolution and change-detection to it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Idempotence contract: <see cref="HasPendingChange"/> is a pure query and may be
    /// called any number of times per solve. <see cref="CommitChange"/> is the only
    /// mutator that acknowledges a pending change.
    /// </para>
    /// </remarks>
    public sealed class ProviderSelectionCore
    {
        /// <summary>
        /// Sentinel value stored when the user has not picked a specific provider and
        /// the component should resolve to the global default at use-time.
        /// </summary>
        public const string DEFAULT_PROVIDER = "Default";

        private readonly GH_Component owner;
        private string currentProvider;
        private string committedProvider;

        /// <summary>
        /// Raised whenever the user picks a new provider through the context menu. The
        /// component should react by re-expiring the solution; tighter integrations
        /// (state-machine transitions etc.) can hook in as well.
        /// </summary>
        public event EventHandler ProviderChanged;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProviderSelectionCore"/> class.
        /// </summary>
        /// <param name="owner">The component that owns this core. Used to call
        /// <see cref="GH_Component.ExpireSolution"/> after menu selection.</param>
        public ProviderSelectionCore(GH_Component owner)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.currentProvider = DEFAULT_PROVIDER;
            this.committedProvider = DEFAULT_PROVIDER;
        }

        /// <summary>
        /// Gets the currently selected provider name (may be
        /// <see cref="DEFAULT_PROVIDER"/>).
        /// </summary>
        public string CurrentProvider => this.currentProvider;

        /// <summary>
        /// Sets the currently selected provider name without raising the
        /// <see cref="ProviderChanged"/> event. Intended for programmatic updates such
        /// as tests or scripted workflows. Does not commit the change.
        /// </summary>
        /// <param name="providerName">The provider name to store; <c>null</c> is
        /// normalized to <see cref="DEFAULT_PROVIDER"/>.</param>
        public void SetCurrentProvider(string providerName)
        {
            this.currentProvider = string.IsNullOrWhiteSpace(providerName) ? DEFAULT_PROVIDER : providerName;
        }

        /// <summary>
        /// Pure query: returns <c>true</c> if the current selection differs from the
        /// last committed value. Safe to call any number of times per solve.
        /// </summary>
        public bool HasPendingChange => this.currentProvider != this.committedProvider;

        /// <summary>
        /// Acknowledges the pending change by advancing the commit baseline to the
        /// current selection.
        /// </summary>
        public void CommitChange()
        {
            this.committedProvider = this.currentProvider;
        }

        /// <summary>
        /// Resolves <see cref="CurrentProvider"/> through
        /// <see cref="ProviderManager"/> so <see cref="DEFAULT_PROVIDER"/> becomes the
        /// concrete default-provider name at the moment of the call.
        /// </summary>
        public string GetActualProviderName()
        {
            return ResolveActualProviderName(this.currentProvider);
        }

        /// <summary>
        /// Resolves the current selection to an <see cref="AIProvider"/> instance, or
        /// <c>null</c> if the provider is not registered.
        /// </summary>
        public AIProvider GetActualProvider()
        {
            string actualProviderName = ResolveActualProviderName(this.currentProvider);
            var provider = ProviderManager.Instance.GetProvider(actualProviderName);
            return provider as AIProvider;
        }

        /// <summary>
        /// Appends the <c>Select AI Provider</c> submenu to <paramref name="menu"/>.
        /// When the user picks an entry the core updates
        /// <see cref="CurrentProvider"/>, raises <see cref="ProviderChanged"/> and
        /// finally calls <see cref="GH_Component.ExpireSolution"/> on the owner.
        /// </summary>
        /// <param name="menu">Target context menu.</param>
        public void AppendMenuItems(ToolStripDropDown menu)
        {
            BuildProviderMenu(
                menu,
                this.currentProvider,
                providerName =>
                {
                    this.currentProvider = providerName;
                    this.ProviderChanged?.Invoke(this, EventArgs.Empty);
                    this.owner.ExpireSolution(true);
                });
        }

        /// <summary>
        /// Persists the current selection to the Grasshopper file.
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <returns><c>true</c> on success.</returns>
        public bool Write(GH_IO.Serialization.GH_IWriter writer)
        {
            try
            {
                writer.SetString(PersistenceKeys.SelectedProvider, this.currentProvider);
                Debug.WriteLine($"[ProviderSelectionCore] [Write] Stored AI provider: {this.currentProvider}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProviderSelectionCore] [Write] Exception: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Restores the selection from the Grasshopper file. After a successful read
        /// the committed baseline is aligned with the restored value so the first
        /// solve after load does not report a phantom change.
        /// </summary>
        /// <param name="reader">The reader.</param>
        /// <returns><c>true</c> on success.</returns>
        public bool Read(GH_IO.Serialization.GH_IReader reader)
        {
            if (!ReadProviderFromReader(reader, out string storedProvider))
            {
                return false;
            }

            this.currentProvider = storedProvider;
            this.committedProvider = storedProvider;
            return true;
        }

        /// <summary>
        /// Resolves a stored provider name (which may be <see cref="DEFAULT_PROVIDER"/>)
        /// to the concrete provider name registered with the
        /// <see cref="ProviderManager"/>.
        /// </summary>
        private static string ResolveActualProviderName(string selectedProvider)
        {
            if (selectedProvider == DEFAULT_PROVIDER)
            {
                return ProviderManager.Instance.GetDefaultAIProvider();
            }

            return selectedProvider;
        }

        /// <summary>
        /// Builds the <c>Select AI Provider</c> submenu and wires the click handlers
        /// to <paramref name="onProviderSelected"/>.
        /// </summary>
        private static void BuildProviderMenu(
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
                if (s is ToolStripMenuItem menuItem)
                {
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
                    if (s is ToolStripMenuItem menuItem)
                    {
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
        /// Reads a stored provider name from <paramref name="reader"/>, falling back to
        /// <see cref="DEFAULT_PROVIDER"/> if the entry is missing or refers to a
        /// provider that is no longer registered.
        /// </summary>
        private static bool ReadProviderFromReader(GH_IO.Serialization.GH_IReader reader, out string selectedProvider)
        {
            try
            {
                if (reader.ItemExists(PersistenceKeys.SelectedProvider))
                {
                    string storedProvider = reader.GetString(PersistenceKeys.SelectedProvider);
                    Debug.WriteLine($"[ProviderSelectionCore] [Read] Read stored AI provider: {storedProvider}");

                    var providers = ProviderManager.Instance.GetProviders();
                    if (storedProvider == DEFAULT_PROVIDER || providers.Any(p => p.Name == storedProvider))
                    {
                        selectedProvider = storedProvider;
                        Debug.WriteLine($"[ProviderSelectionCore] [Read] Successfully restored AI provider: {selectedProvider}");
                        return true;
                    }

                    Debug.WriteLine($"[ProviderSelectionCore] [Read] Stored provider '{storedProvider}' not found, using default");
                    selectedProvider = DEFAULT_PROVIDER;
                    return true;
                }

                Debug.WriteLine("[ProviderSelectionCore] [Read] No stored AI provider found, using default");
                selectedProvider = DEFAULT_PROVIDER;
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProviderSelectionCore] [Read] Exception: {ex.Message}");
                selectedProvider = DEFAULT_PROVIDER;
                return false;
            }
        }
    }
}
