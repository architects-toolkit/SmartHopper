/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

namespace SmartHopper.Core.ComponentBase
{
    /// <summary>
    /// Base for components that support selecting objects via a "Select Components" button.
    /// </summary>
    public abstract class SelectingComponentBase : GH_Component
    {
        /// <summary>Currently selected GH objects.</summary>
        public List<IGH_ActiveObject> SelectedObjects = new List<IGH_ActiveObject>();

        private bool inSelectionMode = false;

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
            m_attributes = new SelectingComponentAttributes(this);
        }

        /// <summary>
        /// Enable selection mode to pick GH objects on canvas.
        /// </summary>
        public void EnableSelectionMode()
        {
            SelectedObjects.Clear();
            inSelectionMode = true;
            var canvas = Instances.ActiveCanvas;
            if (canvas == null) return;
            canvas.ContextMenuStrip?.Hide();
            CanvasSelectionChanged();
            ExpireSolution(true);
        }

        private void CanvasSelectionChanged()
        {
            if (!inSelectionMode) return;
            var canvas = Instances.ActiveCanvas;
            if (canvas == null) return;
            SelectedObjects = canvas.Document.SelectedObjects()
                .OfType<IGH_ActiveObject>()
                .ToList();
            Message = $"{SelectedObjects.Count} selected";
            ExpireSolution(true);
        }

        /// <summary>
        /// Adds "Select Components" to the context menu.
        /// </summary>
        protected override void AppendAdditionalComponentMenuItems(System.Windows.Forms.ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
            Menu_AppendItem(menu, "Select Components", (s, e) => EnableSelectionMode());
        }
    }

    /// <summary>
    /// Attributes for drawing the select button and highlighting selected objects.
    /// </summary>
    public class SelectingComponentAttributes : GH_ComponentAttributes
    {
        private new readonly SelectingComponentBase Owner;
        private Rectangle ButtonBounds;
        private bool IsHovering;
        private bool IsClicking;

        public SelectingComponentAttributes(SelectingComponentBase owner) : base(owner)
        {
            Owner = owner;
            IsHovering = false;
            IsClicking = false;
        }

        protected override void Layout()
        {
            base.Layout();
            const int margin = 5;
            var width = (int)Bounds.Width - (2 * margin);
            var height = 24;
            var x = (int)Bounds.X + margin;
            var y = (int)Bounds.Bottom;
            ButtonBounds = new Rectangle(x, y, width, height);
            Bounds = new RectangleF(Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height + height + margin);
        }

        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            base.Render(canvas, graphics, channel);
            if (channel == GH_CanvasChannel.Objects)
            {
                var palette = IsClicking ? GH_Palette.White : (IsHovering ? GH_Palette.Grey : GH_Palette.Black);
                var capsule = GH_Capsule.CreateCapsule(ButtonBounds, palette);
                capsule.Render(graphics, Selected, Owner.Locked, false);
                capsule.Dispose();

                var font = GH_FontServer.Standard;
                var text = "Select";
                var size = graphics.MeasureString(text, font);
                var tx = ButtonBounds.X + (ButtonBounds.Width - size.Width) / 2;
                var ty = ButtonBounds.Y + (ButtonBounds.Height - size.Height) / 2;
                graphics.DrawString(text, font, (IsHovering || IsClicking) ? Brushes.Black : Brushes.White, new PointF(tx, ty));

                if (IsHovering && Owner.SelectedObjects.Count > 0)
                {
                    using (var pen = new Pen(Color.DodgerBlue, 2f))
                    {
                        pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                        foreach (var obj in Owner.SelectedObjects.OfType<IGH_DocumentObject>())
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
            if (e.Button == MouseButtons.Left && ButtonBounds.Contains((int)e.CanvasLocation.X, (int)e.CanvasLocation.Y))
            {
                IsClicking = true;
                Owner.ExpireSolution(true);
                Owner.EnableSelectionMode();
                return GH_ObjectResponse.Handled;
            }
            return base.RespondToMouseDown(sender, e);
        }

        public override GH_ObjectResponse RespondToMouseMove(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            var was = IsHovering;
            IsHovering = ButtonBounds.Contains((int)e.CanvasLocation.X, (int)e.CanvasLocation.Y);
            if (was != IsHovering) Owner.ExpireSolution(true);
            return base.RespondToMouseMove(sender, e);
        }

        public override GH_ObjectResponse RespondToMouseUp(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (IsClicking)
            {
                IsClicking = false;
                Owner.ExpireSolution(true);
            }
            return base.RespondToMouseUp(sender, e);
        }
    }
}
