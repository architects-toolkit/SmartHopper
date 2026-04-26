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
using GH_IO.Serialization;
using Grasshopper;
using Grasshopper.Kernel;

namespace SmartHopper.Core.ComponentBase
{
    /// <summary>
    /// Combines SelectingComponentBase functionality with StatefulComponentBase state management.
    /// Provides both selection UI and persistent output handling for components that need to select
    /// Grasshopper objects and maintain state across solve cycles.
    /// </summary>
    public abstract class SelectingStatefulComponentBase : StatefulComponentBase, ISelectingComponent
    {
        /// <summary>
        /// Gets the currently selected Grasshopper objects for this component's selection mode.
        /// Exposed as a property to encapsulate internal state while allowing read access.
        /// Uses <see cref="IGH_DocumentObject"/> to support all object types including scribbles.
        /// </summary>
        public List<IGH_DocumentObject> SelectedObjects
        {
            get
            {
                this.selectionCore.PruneDeletedSelections(this.selectedObjects);
                return this.selectedObjects;
            }
        }

        private readonly List<IGH_DocumentObject> selectedObjects = new List<IGH_DocumentObject>();
        private readonly SelectingComponentCore selectionCore;

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
            this.selectionCore = new SelectingComponentCore(this, this);
            this.selectionCore.SubscribeToDocumentEvents();
        }

        /// <summary>
        /// Clean up event subscriptions.
        /// </summary>
        public override void RemovedFromDocument(GH_Document document)
        {
            base.RemovedFromDocument(document);
            this.selectionCore.UnsubscribeFromDocumentEvents();
        }

        /// <summary>
        /// Set up custom attributes for the select button.
        /// </summary>
        public override void CreateAttributes()
        {
            this.m_attributes = new SelectingComponentAttributes(this, this);
        }

        /// <summary>
        /// Enable selection mode to pick GH objects on canvas.
        /// </summary>
        public void EnableSelectionMode()
        {
            this.selectionCore.EnableSelectionMode();
        }

        /// <summary>
        /// Adds "Select Components" to the context menu.
        /// </summary>
        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
            Menu_AppendItem(menu, "Select Components", (s, e) => this.EnableSelectionMode());
        }

        /// <summary>
        /// Writes the component's persistent data to the Grasshopper file, including selected objects.
        /// </summary>
        /// <param name="writer">The writer to use for serialization.</param>
        /// <returns>True if the write operation succeeds, false if it fails or an exception occurs.</returns>
        public override bool Write(GH_IWriter writer)
        {
            if (!base.Write(writer))
            {
                return false;
            }

            return this.selectionCore.Write(writer);
        }

        /// <summary>
        /// Reads the component's persistent data from the Grasshopper file, including selected objects.
        /// </summary>
        /// <param name="reader">The reader to use for deserialization.</param>
        /// <returns>True if the read operation succeeds, false if it fails, required data is missing, or an exception occurs.</returns>
        public override bool Read(GH_IReader reader)
        {
            if (!base.Read(reader))
            {
                return false;
            }

            return this.selectionCore.Read(reader);
        }

        /// <summary>
        /// Attempts to restore selected objects from pending GUIDs.
        /// </summary>
        private void TryRestoreSelection()
        {
            this.selectionCore.TryRestoreSelection();
        }

        /// <summary>
        /// Called when a document is added to the DocumentServer.
        /// Used to restore selections after document is fully loaded.
        /// </summary>
        private void OnDocumentAdded(GH_DocumentServer sender, GH_Document doc)
        {
            this.selectionCore.OnDocumentAdded(sender, doc);
        }
    }
}
