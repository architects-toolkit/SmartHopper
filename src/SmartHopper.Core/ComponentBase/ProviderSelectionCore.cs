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
using System.Windows.Forms;
using Grasshopper.Kernel;
using SmartHopper.Infrastructure.AIProviders;

namespace SmartHopper.Core.ComponentBase
{
    /// <summary>
    /// Instance helper that owns the state required for AI provider selection on a
    /// single component. Mirrors the composition pattern used by
    /// <see cref="SelectingComponentCore"/>: the component owns a core and delegates
    /// menu, persistence, resolution and change-detection to it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Replaces the previous combination of <c>ProviderComponentHelper</c> (static utils) +
    /// ad-hoc <c>aiProvider</c> / <c>previousSelectedProvider</c> fields duplicated on each
    /// provider base. The low-level utility methods remain available on
    /// <see cref="ProviderComponentHelper"/> for callers that only need menu rendering or
    /// default-name resolution without carrying state.
    /// </para>
    /// <para>
    /// Idempotence contract: <see cref="HasPendingChange"/> is a pure query and may be
    /// called any number of times per solve. <see cref="CommitChange"/> is the only
    /// mutator that acknowledges a pending change. The old <c>HasProviderChanged()</c>
    /// method mutated on read, which made it unsafe to call twice.
    /// </para>
    /// </remarks>
    public sealed class ProviderSelectionCore
    {
        /// <summary>
        /// Sentinel value stored when the user has not picked a specific provider and
        /// the component should resolve to the global default at use-time.
        /// </summary>
        public const string DEFAULT_PROVIDER = ProviderComponentHelper.DEFAULT_PROVIDER;

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
            return ProviderComponentHelper.GetActualProviderName(this.currentProvider);
        }

        /// <summary>
        /// Resolves the current selection to an <see cref="AIProvider"/> instance, or
        /// <c>null</c> if the provider is not registered.
        /// </summary>
        public AIProvider GetActualProvider()
        {
            return ProviderComponentHelper.GetActualProvider(this.currentProvider);
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
            ProviderComponentHelper.AppendProviderMenuItems(
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
            return ProviderComponentHelper.WriteProvider(writer, this.currentProvider);
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
            if (!ProviderComponentHelper.ReadProvider(reader, out string storedProvider))
            {
                return false;
            }

            this.currentProvider = storedProvider;
            this.committedProvider = storedProvider;
            return true;
        }
    }
}
