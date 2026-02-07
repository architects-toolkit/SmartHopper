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

/*
 * SelectingComponentBase - base class for components with a "Select Components" button.
 */

using System;
using System.Collections.Generic;
using System.Drawing;
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
        /// Uses <see cref="IGH_DocumentObject"/> to support all object types including scribbles.
        /// </summary>
        public List<IGH_DocumentObject> SelectedObjects
        {
            get
            {
                this.PruneDeletedSelections();
                return this.selectedObjects;
            }
        }

        private readonly List<IGH_DocumentObject> selectedObjects = new List<IGH_DocumentObject>();
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
        /// Validates the selected objects list by removing any objects that have been deleted from the document.
        /// Call this before accessing SelectedObjects for execution to ensure all objects are valid.
        /// </summary>
        public void ValidateSelectedObjects()
        {
            this.PruneDeletedSelections();
        }

        /// <summary>
        /// Adds "Select Components" to the context menu.
        /// </summary>
        protected override void AppendAdditionalComponentMenuItems(System.Windows.Forms.ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
            Menu_AppendItem(menu, "Select Components", (s, e) => this.EnableSelectionMode());
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

        // Cached bounds for selected objects during hover session.
        // Computed once when hover starts, cleared when hover ends.
        private Dictionary<Guid, RectangleF>? cachedSelectedBounds;

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
    }
}
