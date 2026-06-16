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
using SmartHopper.Core.ComponentBase.Attributes;
using SmartHopper.Core.ComponentBase.Contracts;
using SmartHopper.Core.ComponentBase.Cores;
using SmartHopper.Infrastructure.AIProviders;

namespace SmartHopper.Core.ComponentBase
{
    /// <summary>
    /// Base class for non-async Grasshopper components that need AI provider selection
    /// functionality. Delegates state, menu wiring and persistence of the provider
    /// choice to a <see cref="ProviderSelectionCore"/> instance so the same logic is
    /// shared with <see cref="AIProviderComponentBase"/>.
    /// </summary>
    public abstract class ProviderComponentBase : GH_Component, IProviderComponent
    {
        /// <summary>
        /// Instance helper that owns the provider selection state.
        /// </summary>
        private readonly ProviderSelectionCore providerCore;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProviderComponentBase"/> class.
        /// </summary>
        /// <param name="name">The name of the component.</param>
        /// <param name="nickname">The nickname of the component.</param>
        /// <param name="description">The description of the component.</param>
        /// <param name="category">The category of the component.</param>
        /// <param name="subcategory">The subcategory of the component.</param>
        protected ProviderComponentBase(string name, string nickname, string description, string category, string subcategory)
            : base(name, nickname, description, category, subcategory)
        {
            this.providerCore = new ProviderSelectionCore(this);

            // Non-async components have no worker lifecycle to commit the change
            // against, so acknowledge and notify derived classes immediately after
            // the user picks a new provider through the menu.
            this.providerCore.ProviderChanged += (sender, e) =>
            {
                this.OnProviderChanged();
                this.providerCore.CommitChange();
            };
        }

        /// <inheritdoc/>
        public string SelectedProviderName => this.providerCore.CurrentProvider;

        /// <inheritdoc/>
        public void SetSelectedProviderName(string providerName)
        {
            this.providerCore.SetCurrentProvider(providerName);
        }

        /// <inheritdoc/>
        public string GetActualAIProviderName() => this.providerCore.GetActualProviderName();

        /// <inheritdoc/>
        public AIProvider GetActualAIProvider() => this.providerCore.GetActualProvider();

        /// <summary>
        /// Appends additional menu items to the component's context menu.
        /// </summary>
        /// <param name="menu">The menu to append to.</param>
        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
            this.providerCore.AppendMenuItems(menu);
        }

        /// <summary>
        /// Called when the provider selection changes. Override to implement custom behavior.
        /// </summary>
        protected virtual void OnProviderChanged()
        {
            // Override in derived classes if needed
        }

        /// <summary>
        /// Creates the custom attributes for this component, which includes the provider logo badge.
        /// </summary>
        public override void CreateAttributes()
        {
            var attrs = new AIProviderComponentAttributes(this)
            {
                ProviderTooltipTemplate = "Settings for %provider%",
            };

            this.m_attributes = attrs;
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

            return this.providerCore.Write(writer);
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

            return this.providerCore.Read(reader);
        }
    }
}
