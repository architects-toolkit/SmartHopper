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
using GH_IO.Serialization;
using Grasshopper.Kernel;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Core.ComponentBase.Contracts;
using SmartHopper.Core.ComponentBase.Cores;

namespace SmartHopper.Core.ComponentBase.Mixins
{
    /// <summary>
    /// Composition helper that collapses the ~70-line boilerplate shared by every
    /// selection-enabled component base (<see cref="SelectingComponentBase"/>,
    /// <see cref="SelectingStatefulComponentBase"/>,
    /// <see cref="AISelectingStatefulAsyncComponentBase"/>).
    ///
    /// Each host base stores a single <c>SelectingSupport</c> field and forwards
    /// lifecycle calls (<see cref="OnRemovedFromDocument"/>, <see cref="Write"/>,
    /// <see cref="Read"/>, <see cref="EnableSelectionMode"/>) to it. The list of
    /// selected objects is owned here so the same pruning / restoration rules apply
    /// everywhere.
    /// </summary>
    /// <remarks>
    /// This helper intentionally has no public state beyond the pruned selection
    /// list. Attribute-level rendering remains the responsibility of the host's
    /// <see cref="GH_ComponentAttributes"/> subclass via
    /// <see cref="SelectingButtonBehavior"/>, which is supplied with the same
    /// <see cref="ISelectingComponent"/> reference used here.
    /// </remarks>
    public sealed class SelectingSupport
    {
        private readonly List<IGH_DocumentObject> selected = new List<IGH_DocumentObject>();
        private readonly SelectingComponentCore core;
        private string committedFingerprint;
        private string runFingerprint;

        /// <summary>
        /// Initializes a new instance of the <see cref="SelectingSupport"/> class
        /// and subscribes to the document-added event so selections persisted on
        /// disk can be restored once the host document finishes loading.
        /// </summary>
        /// <param name="owner">The host component; used for <c>ExpireSolution</c>
        /// and <c>OnPingDocument</c> calls.</param>
        /// <param name="selecting">The selection-bearing contract implemented by
        /// the host.</param>
        public SelectingSupport(GH_Component owner, ISelectingComponent selecting)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (selecting == null) throw new ArgumentNullException(nameof(selecting));

            this.core = new SelectingComponentCore(owner, selecting);
            this.core.SubscribeToDocumentEvents();
        }

        /// <summary>
        /// Gets the currently selected document objects, with deleted entries
        /// pruned on every access so callers never see dangling references.
        /// </summary>
        public List<IGH_DocumentObject> SelectedObjects
        {
            get
            {
                this.core.PruneDeletedSelections(this.selected);
                return this.selected;
            }
        }

        /// <summary>
        /// Enters selection mode so the next canvas selection is captured.
        /// </summary>
        public void EnableSelectionMode() => this.core.EnableSelectionMode();

        /// <summary>
        /// Gets a value indicating whether the current selection differs from the
        /// one the last run consumed. The first evaluation adopts the current
        /// selection as the baseline, so a freshly placed or freshly restored
        /// component never reports a phantom selection change.
        /// </summary>
        public bool HasPendingSelectionChange
        {
            get
            {
                var current = this.Fingerprint();
                if (this.committedFingerprint == null)
                {
                    this.committedFingerprint = current;
                    return false;
                }

                return current != this.committedFingerprint;
            }
        }

        /// <summary>
        /// Captures the selection the incoming run is about to consume. Called
        /// when the host enters Processing so a selection change made while work
        /// is in flight stays pending for the next solve.
        /// </summary>
        public void CaptureRunSelection() => this.runFingerprint = this.Fingerprint();

        /// <summary>
        /// Acknowledges the captured run selection after the run finishes,
        /// is cancelled, or errors. Selection changes made mid-run remain
        /// pending because only the run-time fingerprint is committed.
        /// </summary>
        public void CommitRunSelection() =>
            this.committedFingerprint = this.runFingerprint ?? this.Fingerprint();

        /// <summary>
        /// Restores the selection from the given reader and schedules a deferred
        /// restoration pass once the owning document finishes loading.
        /// </summary>
        /// <returns><c>true</c> on success.</returns>
        public bool Read(GH_IReader reader) => this.core.Read(reader);

        private string Fingerprint()
        {
            return string.Join(
                ",",
                this.SelectedObjects
                    .Select(o => o.InstanceGuid.ToString("N"))
                    .OrderBy(g => g, StringComparer.Ordinal));
        }

        /// <summary>
        /// Must be invoked from the host's <see cref="GH_DocumentObject.RemovedFromDocument"/>
        /// override so the document-added subscription is released.
        /// </summary>
        public void OnRemovedFromDocument() => this.core.UnsubscribeFromDocumentEvents();

        /// <summary>
        /// Persists the selection to the given writer.
        /// </summary>
        /// <returns><c>true</c> on success.</returns>
        public bool Write(GH_IWriter writer) => this.core.Write(writer);
    }
}
