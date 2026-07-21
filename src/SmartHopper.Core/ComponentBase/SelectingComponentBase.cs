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
using System.Drawing;
using System.Windows.Forms;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using SmartHopper.Core.ComponentBase.Attributes;
using SmartHopper.Core.ComponentBase.Contracts;
using SmartHopper.Core.ComponentBase.Mixins;

namespace SmartHopper.Core.ComponentBase
{
    /// <summary>
    /// Base for components that support selecting objects via a "Select Components" button.
    /// All selection plumbing (event subscription, persistence, pruning) is delegated to a
    /// composed <see cref="SelectingSupport"/>; this shell only wires lifecycle overrides.
    /// </summary>
    public abstract class SelectingComponentBase : GH_Component, ISelectingComponent
    {
        private readonly SelectingSupport selection;

        /// <summary>
        /// Initializes a new instance of the <see cref="SelectingComponentBase"/> class.
        /// </summary>
        protected SelectingComponentBase(string name, string nickname, string description, string category, string subcategory)
            : base(name, nickname, description, category, subcategory)
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
            Menu_AppendSeparator(menu);
            AsyncComponentBase.AppendMcpLockItem(menu, this);
        }

        /// <inheritdoc/>
        public override bool Write(GH_IO.Serialization.GH_IWriter writer)
        {
            return base.Write(writer) && this.selection.Write(writer);
        }

        /// <inheritdoc/>
        public override bool Read(GH_IO.Serialization.GH_IReader reader)
        {
            return base.Read(reader) && this.selection.Read(reader);
        }
    }

    /// <summary>
    /// Attributes for drawing the select button and highlighting selected objects.
    /// Delegates mouse/hover/render state to <see cref="SelectingButtonBehavior"/>
    /// so the AI-flavoured counterpart
    /// (<c>AISelectingComponentAttributes</c>) shares the same logic.
    /// </summary>
    public class SelectingComponentAttributes : GH_ComponentAttributes
    {
        private readonly GH_Component owner;
        private readonly SelectingButtonBehavior behavior;

        /// <summary>
        /// Initializes a new instance of the <see cref="SelectingComponentAttributes"/> class.
        /// </summary>
        /// <param name="owner">The owning component.</param>
        /// <param name="selectingComponent">The selection-bearing contract
        /// implemented by the owner.</param>
        public SelectingComponentAttributes(GH_Component owner, ISelectingComponent selectingComponent)
            : base(owner)
        {
            this.owner = owner;
            this.behavior = new SelectingButtonBehavior(owner, selectingComponent);
        }

        /// <inheritdoc/>
        protected override void Layout()
        {
            base.Layout();
            const int margin = 5;
            var width = (int)this.Bounds.Width - (2 * margin);
            var height = 24;
            var x = (int)this.Bounds.X + margin;
            var y = (int)this.Bounds.Bottom;
            this.behavior.ButtonBounds = new Rectangle(x, y, width, height);
            this.Bounds = new RectangleF(this.Bounds.X, this.Bounds.Y, this.Bounds.Width, this.Bounds.Height + height + margin);
        }

        /// <inheritdoc/>
        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            base.Render(canvas, graphics, channel);
            if (channel == GH_CanvasChannel.Objects)
            {
                this.behavior.RenderButton(canvas, graphics, this.Selected, this.owner.Locked);
            }
            else if (channel == GH_CanvasChannel.Overlay)
            {
                this.behavior.RenderOverlay(canvas, graphics);
            }
        }

        /// <inheritdoc/>
        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            var response = this.behavior.OnMouseDown(e);
            return response == GH_ObjectResponse.Handled ? response : base.RespondToMouseDown(sender, e);
        }

        /// <inheritdoc/>
        public override GH_ObjectResponse RespondToMouseMove(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            this.behavior.OnMouseMove(e);
            return base.RespondToMouseMove(sender, e);
        }

        /// <inheritdoc/>
        public override GH_ObjectResponse RespondToMouseUp(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            this.behavior.OnMouseUp(e);
            return base.RespondToMouseUp(sender, e);
        }
    }
}
