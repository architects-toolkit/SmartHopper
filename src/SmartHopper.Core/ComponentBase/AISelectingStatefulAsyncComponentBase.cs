/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Grasshopper;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Timer = System.Timers.Timer;

namespace SmartHopper.Core.ComponentBase
{
    public abstract class AISelectingStatefulAsyncComponentBase : AIStatefulAsyncComponentBase, ISelectingComponent
    {
        public List<IGH_ActiveObject> SelectedObjects
        {
            get
            {
                this.PruneDeletedSelections();
                return this.selectedObjects;
            }
        }

        private readonly List<IGH_ActiveObject> selectedObjects = new List<IGH_ActiveObject>();
        private readonly SelectingComponentCore selectionCore;

        protected AISelectingStatefulAsyncComponentBase(string name, string nickname, string description, string category, string subCategory)
            : base(name, nickname, description, category, subCategory)
        {
            this.selectionCore = new SelectingComponentCore(this, this);
            Instances.DocumentServer.DocumentAdded += this.OnDocumentAdded;
        }

        public override void RemovedFromDocument(GH_Document document)
        {
            base.RemovedFromDocument(document);
            Instances.DocumentServer.DocumentAdded -= this.OnDocumentAdded;
        }

        public override void CreateAttributes()
        {
            this.m_attributes = new AISelectingComponentAttributes(this, this);
        }

        public void EnableSelectionMode()
        {
            this.selectionCore.EnableSelectionMode();
        }

        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
            Menu_AppendItem(menu, "Select Components", (s, e) => this.EnableSelectionMode());
        }

        public override bool Write(GH_IO.Serialization.GH_IWriter writer)
        {
            if (!base.Write(writer))
            {
                return false;
            }

            return this.selectionCore.Write(writer);
        }

        public override bool Read(GH_IO.Serialization.GH_IReader reader)
        {
            if (!base.Read(reader))
            {
                return false;
            }

            return this.selectionCore.Read(reader);
        }

        private void OnDocumentAdded(GH_DocumentServer sender, GH_Document doc)
        {
            this.selectionCore.OnDocumentAdded(doc);
        }

        private void PruneDeletedSelections()
        {
            if (this.selectedObjects.Count == 0)
            {
                return;
            }

            var canvas = Instances.ActiveCanvas;
            var doc = canvas?.Document;
            if (doc == null)
            {
                return;
            }

            var removedAny = false;

            for (int i = this.selectedObjects.Count - 1; i >= 0; i--)
            {
                if (this.selectedObjects[i] is IGH_DocumentObject docObj)
                {
                    var found = doc.FindObject(docObj.InstanceGuid, true);
                    if (found == null)
                    {
                        this.selectedObjects.RemoveAt(i);
                        removedAny = true;
                    }
                }
            }

            if (removedAny)
            {
                this.Message = $"{this.selectedObjects.Count} selected";
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

        // Cached bounds for selected objects during hover session.
        // Computed once when hover starts, cleared when hover ends.
        private Dictionary<Guid, RectangleF>? cachedSelectedBounds;

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
                SelectingComponentCore.RenderSelectButton(
                    canvas,
                    graphics,
                    this.buttonBounds,
                    this.isHovering,
                    this.isClicking,
                    this.Selected,
                    this.owner.Locked);
            }
            else if (channel == GH_CanvasChannel.Overlay)
            {
                SelectingComponentCore.RenderSelectionOverlay(
                    canvas,
                    graphics,
                    this.buttonBounds,
                    this.cachedSelectedBounds,
                    this.selectAutoHidden);

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

                // Refresh cache after selection completes
                this.CacheSelectedBounds();
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
                    this.CacheSelectedBounds();
                    this.StartSelectDisplayTimer();
                }
                else
                {
                    this.StopSelectDisplayTimer();
                    this.selectAutoHidden = false; // reset for next hover
                    this.cachedSelectedBounds = null; // clear cache on hover end
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
            SelectingComponentCore.RestartSelectDisplayTimer(
                ref this.selectDisplayTimer,
                () =>
                {
                    this.selectAutoHidden = true;
                    try { this.owner?.OnDisplayExpired(false); } catch { /* ignore */ }
                });
        }

        /// <summary>
        /// Stops and disposes the selection display timer if active.
        /// </summary>
        private void StopSelectDisplayTimer()
        {
            SelectingComponentCore.StopSelectDisplayTimer(ref this.selectDisplayTimer);
        }

        /// <summary>
        /// Caches the current bounds of all selected objects.
        /// Called once when hover starts to get fresh positions.
        /// </summary>
        private void CacheSelectedBounds()
        {
            this.cachedSelectedBounds = SelectingComponentCore.BuildSelectedBounds(this.selectingComponent);
        }

        protected override bool ShouldDeferProviderLabelRendering()
        {
            // Defer tooltip rendering so it draws over the Select button UI elements.
            return true;
        }
    }
}
