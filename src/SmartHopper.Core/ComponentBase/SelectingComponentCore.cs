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
 * along with this library; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

/*
 * SelectingComponentCore - shared logic for selection-enabled components.
 */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Grasshopper;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using SmartHopper.Core.UI;
using Timer = System.Timers.Timer;

namespace SmartHopper.Core.ComponentBase
{
    internal sealed class SelectingComponentCore
    {
        private readonly GH_Component owner;
        private readonly ISelectingComponent selectingComponent;
        private bool inSelectionMode;
        private List<Guid> pendingSelectionGuids = new List<Guid>();
        private bool hasPendingRestore;

        public SelectingComponentCore(GH_Component owner, ISelectingComponent selectingComponent)
        {
            this.owner = owner;
            this.selectingComponent = selectingComponent;
        }

        public void EnableSelectionMode()
        {
            this.selectingComponent.SelectedObjects.Clear();
            this.inSelectionMode = true;
            var canvas = Instances.ActiveCanvas;
            if (canvas == null)
            {
                return;
            }

            canvas.ContextMenuStrip?.Hide();
            this.CanvasSelectionChanged();
            this.owner.ExpireSolution(true);
        }

        public bool Write(GH_IO.Serialization.GH_IWriter writer)
        {
            try
            {
                writer.SetInt32("SelectedObjectsCount", this.selectingComponent.SelectedObjects.Count);

                for (int i = 0; i < this.selectingComponent.SelectedObjects.Count; i++)
                {
                    if (this.selectingComponent.SelectedObjects[i] is IGH_DocumentObject docObj)
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

        public bool Read(GH_IO.Serialization.GH_IReader reader)
        {
            try
            {
                this.selectingComponent.SelectedObjects.Clear();
                this.pendingSelectionGuids.Clear();
                this.hasPendingRestore = false;

                if (reader.ItemExists("SelectedObjectsCount"))
                {
                    int count = reader.GetInt32("SelectedObjectsCount");
                    System.Diagnostics.Debug.WriteLine($"[SelectingComponentCore] Read: Loading {count} selected object GUIDs");

                    for (int i = 0; i < count; i++)
                    {
                        string key = $"SelectedObject_{i}";
                        if (reader.ItemExists(key))
                        {
                            Guid objectGuid = reader.GetGuid(key);
                            this.pendingSelectionGuids.Add(objectGuid);
                            System.Diagnostics.Debug.WriteLine($"[SelectingComponentCore] Read: Stored GUID {objectGuid}");
                        }
                    }

                    if (this.pendingSelectionGuids.Count > 0)
                    {
                        this.hasPendingRestore = true;
                        System.Diagnostics.Debug.WriteLine($"[SelectingComponentCore] Read: Marked {this.pendingSelectionGuids.Count} GUIDs for deferred restoration");
                    }

                    this.TryRestoreSelection();
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SelectingComponentCore] Read: Exception - {ex.Message}");
                return false;
            }
        }

        public void TryRestoreSelection()
        {
            if (!this.hasPendingRestore || this.pendingSelectionGuids.Count == 0)
            {
                return;
            }

            var canvas = Instances.ActiveCanvas;
            if (canvas?.Document == null)
            {
                System.Diagnostics.Debug.WriteLine("[SelectingComponentCore] TryRestoreSelection: No active canvas/document");
                return;
            }

            int foundCount = 0;
            var remainingGuids = new List<Guid>();

            foreach (var objectGuid in this.pendingSelectionGuids)
            {
                var foundObject = canvas.Document.FindObject(objectGuid, true);
                if (foundObject is IGH_ActiveObject activeObj)
                {
                    if (!this.selectingComponent.SelectedObjects.Contains(activeObj))
                    {
                        this.selectingComponent.SelectedObjects.Add(activeObj);
                        foundCount++;
                        System.Diagnostics.Debug.WriteLine($"[SelectingComponentCore] TryRestoreSelection: Found object {objectGuid}");
                    }
                }
                else
                {
                    remainingGuids.Add(objectGuid);
                    System.Diagnostics.Debug.WriteLine($"[SelectingComponentCore] TryRestoreSelection: Object {objectGuid} not found yet");
                }
            }

            this.pendingSelectionGuids = remainingGuids;
            this.hasPendingRestore = this.pendingSelectionGuids.Count > 0;

            this.owner.Message = $"{this.selectingComponent.SelectedObjects.Count} selected";

            if (foundCount > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[SelectingComponentCore] TryRestoreSelection: Restored {foundCount} objects, {this.pendingSelectionGuids.Count} still pending");
                this.owner.ExpireSolution(true);
            }
        }

        public void OnDocumentAdded(GH_Document doc)
        {
            if (doc != null && doc == this.owner.OnPingDocument())
            {
                System.Diagnostics.Debug.WriteLine("[SelectingComponentCore] OnDocumentAdded: Document loaded, attempting deferred restoration");

                var timer = new Timer(100) { AutoReset = false };
                timer.Elapsed += (s, e) =>
                {
                    try
                    {
                        Rhino.RhinoApp.InvokeOnUiThread((Action)(() =>
                        {
                            this.TryRestoreSelection();
                        }));
                        timer.Dispose();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SelectingComponentCore] OnDocumentAdded: Exception during deferred restoration - {ex.Message}");
                    }
                };
                timer.Start();
            }
        }

        private void CanvasSelectionChanged()
        {
            if (!this.inSelectionMode)
            {
                return;
            }

            var canvas = Instances.ActiveCanvas;
            if (canvas?.Document == null)
            {
                return;
            }

            this.selectingComponent.SelectedObjects.Clear();
            this.selectingComponent.SelectedObjects.AddRange(
                canvas.Document.SelectedObjects()
                    .OfType<IGH_ActiveObject>()
                    .Where(obj => obj is IGH_Component ||
                                  obj is IGH_Param ||
                                  obj is Grasshopper.Kernel.Special.GH_Group ||
                                  obj.GetType().Name.Contains("Scribble", StringComparison.Ordinal) ||
                                  obj.GetType().Name.Contains("Panel", StringComparison.Ordinal)));

            this.owner.Message = $"{this.selectingComponent.SelectedObjects.Count} selected";
            this.owner.ExpireSolution(true);
        }

        internal static Dictionary<Guid, RectangleF> BuildSelectedBounds(ISelectingComponent selectingComponent)
        {
            return selectingComponent.SelectedObjects
                .OfType<IGH_DocumentObject>()
                .Where(obj => obj.Attributes != null)
                .ToDictionary(obj => obj.InstanceGuid, obj => obj.Attributes.Bounds);
        }

        internal static void RenderSelectButton(
            GH_Canvas canvas,
            Graphics graphics,
            Rectangle buttonBounds,
            bool isHovering,
            bool isClicking,
            bool isSelected,
            bool isLocked)
        {
            var palette = isClicking ? GH_Palette.White : (isHovering ? GH_Palette.Grey : GH_Palette.Black);
            var capsule = GH_Capsule.CreateCapsule(buttonBounds, palette);
            capsule.Render(graphics, isSelected, isLocked, false);
            capsule.Dispose();

            var font = GH_FontServer.Standard;
            var text = "Select";
            var size = graphics.MeasureString(text, font);
            var tx = buttonBounds.X + ((buttonBounds.Width - size.Width) / 2);
            var ty = buttonBounds.Y + ((buttonBounds.Height - size.Height) / 2);
            graphics.DrawString(text, font, (isHovering || isClicking) ? Brushes.Black : Brushes.White, new PointF(tx, ty));
        }

        internal static void RenderSelectionOverlay(
            GH_Canvas canvas,
            Graphics graphics,
            Rectangle buttonBounds,
            Dictionary<Guid, RectangleF>? cachedSelectedBounds,
            bool selectAutoHidden)
        {
            if (selectAutoHidden || cachedSelectedBounds == null || cachedSelectedBounds.Count == 0)
            {
                return;
            }

            var highlightColor = DialogCanvasLink.DefaultLineColor;
            var highlightWidth = 2f;

            using (var pen = new Pen(highlightColor, highlightWidth))
            {
                pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;

                var buttonMidY = buttonBounds.Y + (buttonBounds.Height / 2f);
                var buttonCenterX = buttonBounds.X + (buttonBounds.Width / 2f);
                var buttonLeft = new PointF(buttonBounds.Left, buttonMidY);
                var buttonRight = new PointF(buttonBounds.Right, buttonMidY);

                foreach (var bounds in cachedSelectedBounds.Values)
                {
                    var pad = 4f;
                    var hb = RectangleF.Inflate(bounds, pad, pad);
                    graphics.DrawRectangle(pen, hb.X, hb.Y, hb.Width, hb.Height);

                    var compCenterX = hb.X + (hb.Width / 2f);
                    var compCenterY = hb.Y + (hb.Height / 2f);
                    var isLeftOfButton = compCenterX < buttonCenterX;

                    var start = isLeftOfButton
                        ? new PointF(hb.Right, compCenterY)
                        : new PointF(hb.Left, compCenterY);

                    var end = isLeftOfButton ? buttonLeft : buttonRight;

                    DialogCanvasLink.DrawLinkOnCanvas(canvas, graphics, start, end, highlightColor, highlightWidth);
                }
            }
        }

        internal static void RestartSelectDisplayTimer(ref Timer? selectDisplayTimer, Action onElapsed)
        {
            StopSelectDisplayTimer(ref selectDisplayTimer);

            var timer = new Timer(5000) { AutoReset = false };
            selectDisplayTimer = timer;

            timer.Elapsed += (_, __) =>
            {
                onElapsed();
                try
                {
                    timer.Stop();
                }
                catch (ObjectDisposedException)
                {
                    // Timer already disposed, ignore
                }

                try
                {
                    timer.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // Timer already disposed, ignore
                }
            };

            timer.Start();
        }

        internal static void StopSelectDisplayTimer(ref Timer? selectDisplayTimer)
        {
            if (selectDisplayTimer != null)
            {
                try
                {
                    selectDisplayTimer.Stop();
                }
                catch (ObjectDisposedException)
                {
                    // Timer already disposed, ignore
                }

                try
                {
                    selectDisplayTimer.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // Timer already disposed, ignore
                }

                selectDisplayTimer = null;
            }
        }
    }
}
