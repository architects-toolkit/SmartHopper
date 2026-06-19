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
using System.Windows.Forms;
using SmartHopper.Core.ComponentBase.Attributes;
using SmartHopper.Core.ComponentBase.Contracts;
using SmartHopper.Core.ComponentBase.Cores;
using SmartHopper.Infrastructure.AIProviders;

namespace SmartHopper.Core.ComponentBase
{
    /// <summary>
    /// Base class for async components that need AI provider selection functionality.
    /// Delegates state, menu wiring and persistence of the provider choice to a
    /// <see cref="ProviderSelectionCore"/> instance so the same logic is shared with
    /// <see cref="ProviderComponentBase"/>.
    /// </summary>
    public abstract class AIProviderComponentBase : StatefulComponentBase, IProviderComponent
    {
        /// <summary>
        /// Special value used to indicate that the default provider from settings should be used.
        /// </summary>
        public const string DEFAULT_PROVIDER = ProviderSelectionCore.DEFAULT_PROVIDER;

        /// <summary>
        /// Instance helper that owns the provider selection state.
        /// </summary>
        private readonly ProviderSelectionCore providerCore;

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
            this.providerCore = new ProviderSelectionCore(this);
        }

        /// <summary>
        /// Appends additional menu items to the component's context menu.
        /// </summary>
        /// <param name="menu">The menu to append to.</param>
        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
            this.providerCore.AppendMenuItems(menu);
        }

        /// <inheritdoc/>
        public string GetActualAIProviderName() => this.providerCore.GetActualProviderName();

        /// <inheritdoc/>
        public AIProvider GetActualAIProvider() => this.providerCore.GetActualProvider();

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

        /// <inheritdoc/>
        public string SelectedProviderName => this.providerCore.CurrentProvider;

        /// <inheritdoc/>
        public void SetSelectedProviderName(string providerName)
        {
            this.providerCore.SetCurrentProvider(providerName);
        }

        /// <summary>
        /// Injects the synthetic <see cref="WellKnownInputs.AIProvider"/> entry when the
        /// user picked a new provider since the last solve. Idempotent: calling this
        /// method multiple times per solve returns the same result. The pending change
        /// is acknowledged from <see cref="OnWorkerCompleted"/>.
        /// </summary>
        protected override List<string> InputsChanged()
        {
            List<string> changedInputs = base.InputsChanged();

            if (this.providerCore.HasPendingChange)
            {
                changedInputs.Add(WellKnownInputs.AIProvider);
            }

            return changedInputs;
        }

        /// <summary>
        /// Commits the pending provider change after a successful run so subsequent
        /// solves do not keep reporting the same change.
        /// </summary>
        protected override void OnWorkerCompleted()
        {
            base.OnWorkerCompleted();
            this.providerCore.CommitChange();
        }

        /// <summary>
        /// Commits the pending provider change when processing is cancelled. Without this,
        /// <see cref="InputsChanged"/> would keep reporting <see cref="WellKnownInputs.AIProvider"/>
        /// for every subsequent solve, permanently marking the component as dirty.
        /// </summary>
        protected override void OnTasksCancelDetected()
        {
            base.OnTasksCancelDetected();
            this.providerCore.CommitChange();
        }

        /// <summary>
        /// Commits the pending provider change on error for the same reason as
        /// <see cref="OnTasksCancelDetected"/>: the user's selection has taken effect on
        /// the attempted run and should not resurface as "changed" on the next solve.
        /// </summary>
        protected override void OnEnteringError()
        {
            base.OnEnteringError();
            this.providerCore.CommitChange();
        }
    }
}
