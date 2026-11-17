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
using SmartHopper.ProviderSdk.AIProviders;

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
        /// Provider selection captured when a run enters Processing. Completion,
        /// cancellation, and error paths acknowledge this snapshot — not the live
        /// selection — so a provider switch made mid-run stays pending and is
        /// reported as a change afterwards, mirroring the input-hash snapshot.
        /// </summary>
        private string runProviderSelection;

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
        /// Captures the provider selection this run starts with, alongside the
        /// input-hash snapshot in the base implementation.
        /// </summary>
        protected override void OnEnteringProcessing()
        {
            base.OnEnteringProcessing();
            this.runProviderSelection = this.providerCore.CurrentProvider;
        }

        /// <summary>
        /// Commits the run-time provider selection after a successful run so a
        /// switch made while work was in flight stays pending for the next solve.
        /// </summary>
        protected override void OnWorkerCompleted()
        {
            base.OnWorkerCompleted();
            this.CommitRunProviderChange();
        }

        /// <summary>
        /// Commits the run-time provider selection when processing is cancelled —
        /// a switch made mid-run was never consumed by the cancelled work and must
        /// stay pending so <see cref="InputsChanged"/> keeps reporting it.
        /// </summary>
        protected override void OnTasksCancelDetected()
        {
            base.OnTasksCancelDetected();
            this.CommitRunProviderChange();
        }

        /// <summary>
        /// Commits the run-time provider selection on error for the same reason as
        /// <see cref="OnTasksCancelDetected"/>.
        /// </summary>
        protected override void OnEnteringError()
        {
            base.OnEnteringError();
            this.CommitRunProviderChange();
        }

        /// <summary>
        /// Acknowledges the provider selection captured at Processing entry. Falls
        /// back to the current selection when no run snapshot exists (e.g. a run
        /// resumed from file).
        /// </summary>
        private void CommitRunProviderChange()
        {
            this.providerCore.CommitChange(this.runProviderSelection ?? this.SelectedProviderName);
        }

        /// <summary>
        /// Commits the pending provider change outside the worker-completion path.
        /// Batch runs never reach <see cref="OnWorkerCompleted"/>, so callers that
        /// consume the current provider selection without completing a worker (e.g.
        /// batch submission) must commit here to stop <see cref="InputsChanged"/>
        /// from reporting <see cref="WellKnownInputs.AIProvider"/> forever.
        /// </summary>
        protected void CommitProviderChange()
        {
            this.providerCore.CommitChange();
        }

        /// <summary>
        /// Commits an explicit provider selection snapshot rather than the current
        /// selection. Batch submission uses this to acknowledge the selection the
        /// batch was built with, so a provider switch made while submission was in
        /// flight remains pending.
        /// </summary>
        /// <param name="providerSelection">The selection to record as committed.</param>
        protected void CommitProviderChange(string providerSelection)
        {
            this.providerCore.CommitChange(providerSelection);
        }
    }
}
