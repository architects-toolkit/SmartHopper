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
    public abstract class SelectingComponentBase : GH_Component
    {
        /// <summary>
        /// Gets the currently selected Grasshopper objects for this component's selection mode.
        /// Exposed as a property to encapsulate internal state while allowing read access.
        /// </summary>
        public List<IGH_ActiveObject> SelectedObjects { get; private set; } = new();

        private bool inSelectionMode;
        private List<Guid> pendingSelectionGuids = new();
        private bool hasPendingRestore;

        /// <summary>
        /// Initializes a new instance of the <see cref="SelectingComponentBase"/> class.
        /// </summary>
        protected SelectingComponentBase(string name, string nickname, string description, string category, string subcategory)
            : base(name, nickname, description, category, subcategory)
        {
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

            try
            {
                // Store the count of selected objects
                writer.SetInt32("SelectedObjectsCount", this.SelectedObjects.Count);

                // Store each selected object's GUID
                for (int i = 0; i < this.SelectedObjects.Count; i++)
                {
                    if (this.SelectedObjects[i] is IGH_DocumentObject docObj)
                    {
                        writer.SetGuid($"SelectedObject_{i}", docObj.InstanceGuid);
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
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

            try
            {
                // Clear existing selected objects and pending GUIDs
                this.SelectedObjects.Clear();
                this.pendingSelectionGuids.Clear();
                this.hasPendingRestore = false;

                // Read the count of selected objects
                if (reader.ItemExists("SelectedObjectsCount"))
                {
                    int count = reader.GetInt32("SelectedObjectsCount");
                    System.Diagnostics.Debug.WriteLine($"[SelectingComponentBase] Read: Loading {count} selected object GUIDs");

                    // Read each selected object's GUID
                    for (int i = 0; i < count; i++)
                    {
                        string key = $"SelectedObject_{i}";
                        if (reader.ItemExists(key))
                        {
                            Guid objectGuid = reader.GetGuid(key);
                            this.pendingSelectionGuids.Add(objectGuid);
                            System.Diagnostics.Debug.WriteLine($"[SelectingComponentBase] Read: Stored GUID {objectGuid}");
                        }
                    }

                    // Mark that we have pending restoration
                    if (this.pendingSelectionGuids.Count > 0)
                    {
                        this.hasPendingRestore = true;
                        System.Diagnostics.Debug.WriteLine($"[SelectingComponentBase] Read: Marked {this.pendingSelectionGuids.Count} GUIDs for deferred restoration");
                    }

                    // Try immediate restoration (works for copy/paste)
                    this.TryRestoreSelection();
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SelectingComponentBase] Read: Exception - {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Attempts to restore selected objects from pending GUIDs.
        /// </summary>
        private void TryRestoreSelection()
        {
            if (!this.hasPendingRestore || this.pendingSelectionGuids.Count == 0)
            {
                return;
            }

            var canvas = Instances.ActiveCanvas;
            if (canvas?.Document == null)
            {
                System.Diagnostics.Debug.WriteLine("[SelectingComponentBase] TryRestoreSelection: No active canvas/document");
                return;
            }

            int foundCount = 0;
            var remainingGuids = new List<Guid>();

            foreach (var objectGuid in this.pendingSelectionGuids)
            {
                var foundObject = canvas.Document.FindObject(objectGuid, true);
                if (foundObject is IGH_ActiveObject activeObj)
                {
                    if (!this.SelectedObjects.Contains(activeObj))
                    {
                        this.SelectedObjects.Add(activeObj);
                        foundCount++;
                        System.Diagnostics.Debug.WriteLine($"[SelectingComponentBase] TryRestoreSelection: Found object {objectGuid}");
                    }
                }
                else
                {
                    // Object not found yet, keep it for later
                    remainingGuids.Add(objectGuid);
                    System.Diagnostics.Debug.WriteLine($"[SelectingComponentBase] TryRestoreSelection: Object {objectGuid} not found yet");
                }
            }

            // Update pending list
            this.pendingSelectionGuids = remainingGuids;
            this.hasPendingRestore = this.pendingSelectionGuids.Count > 0;

            // Update the message
            this.Message = $"{this.SelectedObjects.Count} selected";

            if (foundCount > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[SelectingComponentBase] TryRestoreSelection: Restored {foundCount} objects, {this.pendingSelectionGuids.Count} still pending");
                this.ExpireSolution(true);
            }
        }

        /// <summary>
        /// Called when a document is added to the DocumentServer.
        /// Used to restore selections after document is fully loaded.
        /// </summary>
        private void OnDocumentAdded(GH_DocumentServer sender, GH_Document doc)
        {
            // Check if this is our document
            if (doc != null && doc == this.OnPingDocument())
            {
                System.Diagnostics.Debug.WriteLine($"[SelectingComponentBase] OnDocumentAdded: Document loaded, attempting deferred restoration");
                
                // Schedule restoration after a short delay to ensure all objects are loaded
                var timer = new Timer(100) { AutoReset = false };
                timer.Elapsed += (s, e) =>
                {
                    try
                    {
                        // Must invoke on UI thread to access canvas
                        Rhino.RhinoApp.InvokeOnUiThread((Action)(() =>
                        {
                            this.TryRestoreSelection();
                        }));
                        timer.Dispose();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SelectingComponentBase] OnDocumentAdded: Exception during deferred restoration - {ex.Message}");
                    }
                };
                timer.Start();
            }
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

        // Timer-based auto-hide of the visual highlight for selected objects.
        // Purpose: ensure the dashed highlight disappears after 5s even if the cursor stays hovered.
        private Timer? selectDisplayTimer;
        private bool selectAutoHidden;

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

                if (this.isHovering && !this.selectAutoHidden && this.owner.SelectedObjects.Count > 0)
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
}
