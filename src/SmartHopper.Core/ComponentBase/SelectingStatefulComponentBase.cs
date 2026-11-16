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

using System.Collections.Generic;
using System.Windows.Forms;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using SmartHopper.Core.ComponentBase.Contracts;
using SmartHopper.Core.ComponentBase.Mixins;

namespace SmartHopper.Core.ComponentBase
{
    /// <summary>
    /// Combines <see cref="StatefulComponentBase"/> state management with the
    /// "Select Components" button flow. Selection plumbing is delegated to a
    /// composed <see cref="SelectingSupport"/>; this shell only wires lifecycle
    /// overrides.
    /// </summary>
    public abstract class SelectingStatefulComponentBase : StatefulComponentBase, ISelectingComponent
    {
        private readonly SelectingSupport selection;

        /// <summary>
        /// Initializes a new instance of the <see cref="SelectingStatefulComponentBase"/> class.
        /// </summary>
        protected SelectingStatefulComponentBase(
            string name,
            string nickname,
            string description,
            string category,
            string subCategory)
            : base(name, nickname, description, category, subCategory)
        {
            this.selection = new SelectingSupport(this, this);
        }

        /// <inheritdoc/>
        public List<IGH_DocumentObject> SelectedObjects => this.selection.SelectedObjects;

        /// <inheritdoc/>
        public void EnableSelectionMode() => this.selection.EnableSelectionMode();

        /// <inheritdoc/>
        public override void RemovedFromDocument(GH_Document document)
        {
            base.RemovedFromDocument(document);
            this.selection.OnRemovedFromDocument();
        }

        /// <inheritdoc/>
        public override void CreateAttributes()
        {
            this.m_attributes = new SelectingComponentAttributes(this, this);
        }

        /// <inheritdoc/>
        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
            Menu_AppendItem(menu, "Select Components", (s, e) => this.EnableSelectionMode());
        }

        /// <summary>
        /// Injects the synthetic <see cref="WellKnownInputs.Selection"/> entry when the
        /// canvas selection differs from the one the last run consumed, so a selection
        /// change retriggers processing exactly like a parameter change.
        /// </summary>
        protected override List<string> InputsChanged()
        {
            List<string> changedInputs = base.InputsChanged();
            if (this.selection.HasPendingSelectionChange)
            {
                changedInputs.Add(WellKnownInputs.Selection);
            }

            return changedInputs;
        }

        /// <summary>
        /// Captures the selection this run is about to consume, alongside the
        /// input-hash snapshot in the base implementation.
        /// </summary>
        protected override void OnEnteringProcessing()
        {
            base.OnEnteringProcessing();
            this.selection.CaptureRunSelection();
        }

        /// <summary>
        /// Commits the run-time selection after a successful run so a selection
        /// change made while work was in flight stays pending for the next solve.
        /// </summary>
        protected override void OnWorkerCompleted()
        {
            base.OnWorkerCompleted();
            this.selection.CommitRunSelection();
        }

        /// <summary>
        /// Commits the run-time selection on cancellation — a change made mid-run
        /// was never consumed and must stay pending.
        /// </summary>
        protected override void OnTasksCancelDetected()
        {
            base.OnTasksCancelDetected();
            this.selection.CommitRunSelection();
        }

        /// <summary>
        /// Commits the run-time selection on error for the same reason as
        /// <see cref="OnTasksCancelDetected"/>.
        /// </summary>
        protected override void OnEnteringError()
        {
            base.OnEnteringError();
            this.selection.CommitRunSelection();
        }

        /// <inheritdoc/>
        public override bool Write(GH_IWriter writer)
        {
            return base.Write(writer) && this.selection.Write(writer);
        }

        /// <inheritdoc/>
        public override bool Read(GH_IReader reader)
        {
            return base.Read(reader) && this.selection.Read(reader);
        }
    }
}
