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
