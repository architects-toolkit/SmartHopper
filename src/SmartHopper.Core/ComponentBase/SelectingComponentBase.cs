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

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Grasshopper;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Timer = System.Timers.Timer;

namespace SmartHopper.Core.ComponentBase
{
    /// <summary>
    /// Base for components that support selecting objects via a "Select Components" button.
    /// </summary>
    public abstract class SelectingComponentBase : GH_Component, ISelectingComponent
    {
        /// <summary>
        /// Gets the currently selected Grasshopper objects for this component's selection mode.
        /// Exposed as a property to encapsulate internal state while allowing read access.
        /// </summary>
        public List<IGH_ActiveObject> SelectedObjects { get; private set; } = new();

        private readonly SelectingComponentCore selectionCore;

        /// <summary>
        /// Initializes a new instance of the <see cref="SelectingComponentBase"/> class.
        /// </summary>
        protected SelectingComponentBase(string name, string nickname, string description, string category, string subcategory)
            : base(name, nickname, description, category, subcategory)
        {
            this.selectionCore = new SelectingComponentCore(this, this);

            // Subscribe to document events for deferred selection restoration
            Instances.DocumentServer.DocumentAdded += this.OnDocumentAdded;
        }

        /// <summary>
        /// Clean up event subscriptions.
        /// </summary>
        public override void RemovedFromDocument(GH_Document document)
        {
            base.RemovedFromDocument(document);
            Instances.DocumentServer.DocumentAdded -= this.OnDocumentAdded;
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
        protected override void AppendAdditionalComponentMenuItems(System.Windows.Forms.ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
            Menu_AppendItem(menu, "Select Components", (s, e) => this.EnableSelectionMode());
        }

        /// <summary>
        /// Writes the component's persistent data to the Grasshopper file, including selected objects.
        /// </summary>
        /// <param name="writer">The writer to use for serialization.</param>
        /// <returns>True if the write operation succeeds, false if it fails or an exception occurs.</returns>
        public override bool Write(GH_IO.Serialization.GH_IWriter writer)
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
        public override bool Read(GH_IO.Serialization.GH_IReader reader)
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
            this.selectionCore.OnDocumentAdded(doc);
        }
    }

    /// <summary>
    /// Attributes for drawing the select button and highlighting selected objects.
    /// </summary>
    public class SelectingComponentAttributes : GH_ComponentAttributes
    {
        private readonly GH_Component owner;
        private readonly ISelectingComponent selectingComponent;
        private Rectangle buttonBounds;
        private bool isHovering;
        private bool isClicking;

        // Timer-based auto-hide of the visual highlight for selected objects.
        // Purpose: ensure the dashed highlight disappears after 5s even if the cursor stays hovered.
        private Timer? selectDisplayTimer;
        private bool selectAutoHidden;

        public SelectingComponentAttributes(GH_Component owner, ISelectingComponent selectingComponent)
            : base(owner)
        {
            this.owner = owner;
            this.selectingComponent = selectingComponent;
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

                if (this.isHovering && !this.selectAutoHidden && this.selectingComponent.SelectedObjects.Count > 0)
                {
                    using (var pen = new Pen(Color.DodgerBlue, 2f))
                    {
                        pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                        foreach (var obj in this.selectingComponent.SelectedObjects.OfType<IGH_DocumentObject>())
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
                this.selectingComponent.EnableSelectionMode();
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
                // Start/stop 5s auto-hide timer based on hover transitions
                if (this.isHovering)
                {
                    this.selectAutoHidden = false;
                    this.StartSelectDisplayTimer();
                }
                else
                {
                    this.StopSelectDisplayTimer();
                    this.selectAutoHidden = false; // reset for next hover
                }

                // Use display invalidation for hover-only visual changes
                this.owner.OnDisplayExpired(false);
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

        /// <summary>
        /// Starts a one-shot 5s timer to auto-hide the selection highlight and request a repaint.
        /// </summary>
        private void StartSelectDisplayTimer()
        {
            this.StopSelectDisplayTimer();
            this.selectDisplayTimer = new Timer(5000) { AutoReset = false };
            this.selectDisplayTimer.Elapsed += (_, __) =>
            {
                this.selectAutoHidden = true;
                try { this.owner?.OnDisplayExpired(false); } catch { /* ignore */ }
                this.StopSelectDisplayTimer();
            };
            this.selectDisplayTimer.Start();
        }

        /// <summary>
        /// Stops and disposes the selection display timer if active.
        /// </summary>
        private void StopSelectDisplayTimer()
        {
            if (this.selectDisplayTimer != null)
            {
                try { this.selectDisplayTimer.Stop(); } catch { /* ignore */ }
                try { this.selectDisplayTimer.Dispose(); } catch { /* ignore */ }
                this.selectDisplayTimer = null;
            }
        }
    }

    public class AISelectingComponentAttributes : ComponentBadgesAttributes
    {
        private readonly AIProviderComponentBase owner;
        private readonly ISelectingComponent selectingComponent;
        private Rectangle buttonBounds;
        private bool isHovering;
        private bool isClicking;

        // Timer-based auto-hide of the visual highlight for selected objects.
        // Purpose: ensure the dashed highlight disappears after 5s even if the cursor stays hovered.
        private Timer? selectDisplayTimer;
        private bool selectAutoHidden;

        public AISelectingComponentAttributes(AIProviderComponentBase owner, ISelectingComponent selectingComponent)
            : base(owner)
        {
            this.owner = owner;
            this.selectingComponent = selectingComponent;
            this.isHovering = false;
            this.isClicking = false;
        }

        protected override void Layout()
        {
            base.Layout();

            const int margin = 5;
            const int buttonHeight = 24;
            var extraHeight = buttonHeight + margin;

            var bounds = this.Bounds;
            bounds.Height += extraHeight;
            this.Bounds = bounds;

            var providerTop = this.Bounds.Bottom - PROVIDERSTRIPHEIGHT;
            var width = (int)this.Bounds.Width - (2 * margin);
            var x = (int)this.Bounds.X + margin;
            var y = (int)(providerTop - margin - buttonHeight);
            this.buttonBounds = new Rectangle(x, y, width, buttonHeight);
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

                if (this.isHovering && !this.selectAutoHidden && this.selectingComponent.SelectedObjects.Count > 0)
                {
                    using (var pen = new Pen(Color.DodgerBlue, 2f))
                    {
                        pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                        foreach (var obj in this.selectingComponent.SelectedObjects.OfType<IGH_DocumentObject>())
                        {
                            var b = obj.Attributes.Bounds;
                            var pad = 4f;
                            var hb = RectangleF.Inflate(b, pad, pad);
                            graphics.DrawRectangle(pen, hb.X, hb.Y, hb.Width, hb.Height);
                        }
                    }
                }

                // Draw the provider tooltip last so it stays above the button and selection overlays.
                this.RenderDeferredProviderLabel(graphics);
            }
        }

        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (e.Button == MouseButtons.Left && this.buttonBounds.Contains((int)e.CanvasLocation.X, (int)e.CanvasLocation.Y))
            {
                this.isClicking = true;
                this.owner.ExpireSolution(true);
                this.selectingComponent.EnableSelectionMode();
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
                // Start/stop 5s auto-hide timer based on hover transitions
                if (this.isHovering)
                {
                    this.selectAutoHidden = false;
                    this.StartSelectDisplayTimer();
                }
                else
                {
                    this.StopSelectDisplayTimer();
                    this.selectAutoHidden = false; // reset for next hover
                }

                // Use display invalidation for hover-only visual changes
                this.owner.OnDisplayExpired(false);
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

        /// <summary>
        /// Starts a one-shot 5s timer to auto-hide the selection highlight and request a repaint.
        /// </summary>
        private void StartSelectDisplayTimer()
        {
            this.StopSelectDisplayTimer();
            this.selectDisplayTimer = new Timer(5000) { AutoReset = false };
            this.selectDisplayTimer.Elapsed += (_, __) =>
            {
                this.selectAutoHidden = true;
                try { this.owner?.OnDisplayExpired(false); } catch { /* ignore */ }
                this.StopSelectDisplayTimer();
            };
            this.selectDisplayTimer.Start();
        }

        /// <summary>
        /// Stops and disposes the selection display timer if active.
        /// </summary>
        private void StopSelectDisplayTimer()
        {
            if (this.selectDisplayTimer != null)
            {
                try { this.selectDisplayTimer.Stop(); } catch { /* ignore */ }
                try { this.selectDisplayTimer.Dispose(); } catch { /* ignore */ }
                this.selectDisplayTimer = null;
            }
        }

        protected override bool ShouldDeferProviderLabelRendering()
        {
            // Defer tooltip rendering so it draws over the Select button UI elements.
            return true;
        }
    }
}
