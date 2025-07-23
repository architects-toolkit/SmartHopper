/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

/*
 * SelectingComponentBase - base class for components with a "Select Components" button.
 */

using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Grasshopper;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;

namespace SmartHopper.Core.ComponentBase
{
    /// <summary>
    /// Base for components that support selecting objects via a "Select Components" button.
    /// </summary>
    public abstract class SelectingComponentBase : GH_Component
    {
        /// <summary>Currently selected GH objects.</summary>
        public List<IGH_ActiveObject> SelectedObjects = new();

        private bool inSelectionMode;

        /// <summary>
        /// Initializes a new instance of the <see cref="SelectingComponentBase"/> class.
        /// </summary>
        protected SelectingComponentBase(string name, string nickname, string description, string category, string subcategory)
            : base(name, nickname, description, category, subcategory)
        {
        }

        /// <summary>
        /// Set up custom attributes for the select button.
        /// </summary>
        public override void CreateAttributes()
        {
            this.m_attributes = new SelectingComponentAttributes(this);
        }

        /// <summary>
        /// Enable selection mode to pick GH objects on canvas.
        /// </summary>
        public void EnableSelectionMode()
        {
            this.SelectedObjects.Clear();
            this.inSelectionMode = true;
            var canvas = Instances.ActiveCanvas;
            if (canvas == null)
            {
                return;
            }

            canvas.ContextMenuStrip?.Hide();
            this.CanvasSelectionChanged();
            this.ExpireSolution(true);
        }

        private void CanvasSelectionChanged()
        {
            if (!this.inSelectionMode)
            {
                return;
            }

            var canvas = Instances.ActiveCanvas;
            if (canvas == null)
            {
                return;
            }

            this.SelectedObjects = canvas.Document.SelectedObjects()
                .OfType<IGH_ActiveObject>()
                .ToList();
            this.Message = $"{this.SelectedObjects.Count} selected";
            this.ExpireSolution(true);
        }

        /// <summary>
        /// Adds "Select Components" to the context menu.
        /// </summary>
        protected override void AppendAdditionalComponentMenuItems(System.Windows.Forms.ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
            Menu_AppendItem(menu, "Select Components", (s, e) => this.EnableSelectionMode());
        }
    }

    /// <summary>
    /// Attributes for drawing the select button and highlighting selected objects.
    /// </summary>
    public class SelectingComponentAttributes : GH_ComponentAttributes
    {
        private readonly SelectingComponentBase owner;
        private Rectangle buttonBounds;
        private bool isHovering;
        private bool isClicking;

        public SelectingComponentAttributes(SelectingComponentBase owner)
            : base(owner)
        {
            this.owner = owner;
            this.isHovering = false;
            this.isClicking = false;
        }

        protected override void Layout()
        {
            base.Layout();
            const int margin = 5;
            var width = (int)this.Bounds.Width - (2 * margin);
            var height = 24;
            var x = (int)this.Bounds.X + margin;
            var y = (int)this.Bounds.Bottom;
            this.buttonBounds = new Rectangle(x, y, width, height);
            this.Bounds = new RectangleF(this.Bounds.X, this.Bounds.Y, this.Bounds.Width, this.Bounds.Height + height + margin);
        }

        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            base.Render(canvas, graphics, channel);
            if (channel == GH_CanvasChannel.Objects)
            {
                var palette = this.isClicking ? GH_Palette.White : (this.isHovering ? GH_Palette.Grey : GH_Palette.Black);
                var capsule = GH_Capsule.CreateCapsule(this.buttonBounds, palette);
                capsule.Render(graphics, this.Selected, this.owner.Locked, false);
                capsule.Dispose();

                var font = GH_FontServer.Standard;
                var text = "Select";
                var size = graphics.MeasureString(text, font);
                var tx = this.buttonBounds.X + ((this.buttonBounds.Width - size.Width) / 2);
                var ty = this.buttonBounds.Y + ((this.buttonBounds.Height - size.Height) / 2);
                graphics.DrawString(text, font, (this.isHovering || this.isClicking) ? Brushes.Black : Brushes.White, new PointF(tx, ty));

                if (this.isHovering && this.owner.SelectedObjects.Count > 0)
                {
                    using (var pen = new Pen(Color.DodgerBlue, 2f))
                    {
                        pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                        foreach (var obj in this.owner.SelectedObjects.OfType<IGH_DocumentObject>())
                        {
                            var b = obj.Attributes.Bounds;
                            var pad = 4f;
                            var hb = RectangleF.Inflate(b, pad, pad);
                            graphics.DrawRectangle(pen, hb.X, hb.Y, hb.Width, hb.Height);
                        }
                    }
                }
            }
        }

        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (e.Button == MouseButtons.Left && this.buttonBounds.Contains((int)e.CanvasLocation.X, (int)e.CanvasLocation.Y))
            {
                this.isClicking = true;
                this.owner.ExpireSolution(true);
                this.owner.EnableSelectionMode();
                return GH_ObjectResponse.Handled;
            }

            return base.RespondToMouseDown(sender, e);
        }

        public override GH_ObjectResponse RespondToMouseMove(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            var was = this.isHovering;
            this.isHovering = this.buttonBounds.Contains((int)e.CanvasLocation.X, (int)e.CanvasLocation.Y);
            if (was != this.isHovering)
            {
                this.owner.ExpireSolution(true);
            }

            return base.RespondToMouseMove(sender, e);
        }

        public override GH_ObjectResponse RespondToMouseUp(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (this.isClicking)
            {
                this.isClicking = false;
                this.owner.ExpireSolution(true);
            }

            return base.RespondToMouseUp(sender, e);
        }
    }
}
