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

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using Grasshopper;
using Grasshopper.Kernel;
using Rhino;

namespace SmartHopper.Core.Grasshopper.Utils.Canvas
{
    /// <summary>
    /// Utility helpers for interacting with the active Grasshopper canvas and document objects.
    /// </summary>
    public static class CanvasAccess
    {
        /// <summary>
        /// Gets the current active Grasshopper document from the canvas.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1024:Use properties where appropriate", Justification = "Ambient UI state access; method communicates non-field-like behavior")]
        public static GH_Document GetCurrentCanvas()
        {
            GH_Document doc = null;
            try
            {
                doc = Instances.ActiveCanvas?.Document;
            }
            catch
            {
                doc = null;
            }

            return doc;
        }

        /// <summary>
        /// Gets the list of active objects in the current document.
        /// </summary>
        public static List<IGH_ActiveObject> GetCurrentObjects()
        {
            GH_Document doc = GetCurrentCanvas();
            if (doc == null)
            {
                return new List<IGH_ActiveObject>();
            }

            try
            {
                return doc.ActiveObjects() ?? new List<IGH_ActiveObject>();
            }
            catch
            {
                return new List<IGH_ActiveObject>();
            }
        }

        /// <summary>
        /// Adds an object to the active Grasshopper canvas at the specified position.
        /// </summary>
        /// <param name="obj">The Grasshopper document object to add.</param>
        /// <param name="position">The pivot position where the object should be placed.</param>
        /// <param name="redraw">True to redraw the canvas after adding the object.</param>
        public static void AddObjectToCanvas(IGH_DocumentObject obj, PointF position = default, bool redraw = true)
        {
            GH_Document doc = GetCurrentCanvas();

            obj.Attributes.Pivot = position;

            doc.AddObject(obj, false);

            if (redraw)
            {
                obj.Attributes.ExpireLayout();
                Instances.RedrawCanvas();
            }
        }

        /// <summary>
        /// Finds a document object instance by its GUID.
        /// </summary>
        /// <param name="guid">The instance GUID.</param>
        /// <returns>The matching document object or null if not found.</returns>
        public static IGH_DocumentObject FindInstance(Guid guid)
        {
            IGH_DocumentObject obj = GetCurrentObjects().FirstOrDefault(o => o.InstanceGuid == guid);

            if (obj is IGH_Component)
            {
                IGH_Component component = obj as IGH_Component;

                // Debug.WriteLine("The object is an IGH_Component.");
                return component;
            }
            else if (obj is IGH_Param)
            {
                IGH_Param param = obj as IGH_Param;

                // Debug.WriteLine("The object is an IGH_Param.");
                return param;
            }
            else
            {
                // Debug.WriteLine("The object is neither an IGH_Component nor an IGH_Param.");
                return obj;
            }
        }

        /// <summary>
        /// Moves an existing instance by setting its Pivot position by GUID.
        /// </summary>
        /// <param name="guid">The GUID of the instance to move.</param>
        /// <param name="position">The new pivot position, absolute or relative.</param>
        /// <param name="relative">True to interpret position as a relative offset; false for absolute.</param>
        /// <param name="redraw">True to redraw canvas after moving.</param>
        /// <returns>True if the instance was found and moved; otherwise false.</returns>
        public static bool MoveInstance(Guid guid, PointF position, bool relative = false, bool redraw = true)
        {
            var obj = FindInstance(guid);
            if (obj == null) return false;

            // Record undo event before moving the instance
            obj.RecordUndoEvent("[SH] Move Instance");
            var current = obj.Attributes.Pivot;
            var target = relative
                ? new PointF(current.X + position.X, current.Y + position.Y)
                : position;

            // Skip movement if initial and target positions are the same
            if (current == target) return false;

            // Animate movement concurrently over 300ms with 15 frames
            Task.Run(async () =>
            {
                const int steps = 15;
                const int duration = 300;
                for (int i = 1; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    var x = current.X + (target.X - current.X) * t;
                    var y = current.Y + (target.Y - current.Y) * t;
                    var interp = new PointF(x, y);
                    RhinoApp.InvokeOnUiThread(() =>
                    {
                        obj.Attributes.Pivot = interp;
                        obj.Attributes.ExpireLayout();

                        // Instances.RedrawCanvas();
                    });
                    await Task.Delay(duration / steps);
                }

                // Final snap to target
                RhinoApp.InvokeOnUiThread(() =>
                {
                    obj.Attributes.Pivot = target;
                    obj.Attributes.ExpireLayout();
                    Instances.RedrawCanvas();
                });
            });

            return true;
        }


        /// <summary>
        /// Moves instances to specific targets by GUID mapping, with optional relative offsets, batching into one undo event.
        /// </summary>
        /// <param name="targets">A mapping of instance GUIDs to target positions.</param>
        /// <param name="relative">If true, interprets target positions as offsets from current positions.</param>
        /// <param name="redraw">True to redraw the canvas after moving objects.</param>
        /// <returns>The list of GUIDs that were successfully moved.</returns>
        public static List<Guid> MoveInstance(IDictionary<Guid, PointF> targets, bool relative = false, bool redraw = true)
        {
            var doc = GetCurrentCanvas();
            var moved = new List<Guid>();
            var moves = new List<(IGH_DocumentObject obj, PointF start, PointF target)>();

            // Collect valid moves
            foreach (var kvp in targets)
            {
                var obj = FindInstance(kvp.Key);
                if (obj == null) continue;
                var start = obj.Attributes.Pivot;
                var targetPos = relative
                    ? new PointF(start.X + kvp.Value.X, start.Y + kvp.Value.Y)
                    : kvp.Value;
                if (start == targetPos) continue;
                moves.Add((obj, start, targetPos));
                moved.Add(kvp.Key);
            }

            if (!moves.Any())
            {
                return moved;
            }

            // Start the batch record (this also captures the first object's action for you)
            var undo = doc.UndoUtil.CreateGenericObjectEvent(
                "[SH] Move Instances",
                moves[0].obj);

            // For every other object, grab its auto-generated action and append it
            foreach (var (obj, _, _) in moves.Skip(1))
            {
                obj.RecordUndoEvent(undo);
            }

            // Animate all moves
            Task.Run(async () =>
            {
                const int steps = 15;
                const int duration = 300;
                for (int i = 1; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    RhinoApp.InvokeOnUiThread(() =>
                    {
                        foreach (var (obj, start, targetPos) in moves)
                        {
                            var ix = start.X + (targetPos.X - start.X) * t;
                            var iy = start.Y + (targetPos.Y - start.Y) * t;
                            var interp = new PointF(ix, iy);
                            obj.Attributes.Pivot = interp;
                            obj.Attributes.ExpireLayout();
                        }
                    });

                    await Task.Delay(duration / steps);
                }

                // Final snap to target
                RhinoApp.InvokeOnUiThread(() =>
                {
                    foreach (var (obj, _, targetPos) in moves)
                    {
                        obj.Attributes.Pivot = targetPos;
                        obj.Attributes.ExpireLayout();
                    }

                    if (redraw)
                    {
                        Instances.RedrawCanvas();
                    }

                    // Once everything's in place, commit the record as a single undo step
                    doc.UndoUtil.RecordEvent(undo);
                });
            });
            return moved;
        }

        /// <summary>
        /// Computes the bounding box of all objects in the current document.
        /// </summary>
        public static RectangleF BoundingBox()
        {
            GH_Document doc = GetCurrentCanvas();
            return doc.BoundingBox(false);
        }

        /// <summary>
        /// Determines a starting point to place new objects below the current canvas content.
        /// </summary>
        /// <param name="span">Vertical spacing from the bottom of the bounding box.</param>
        /// <returns>A point suitable for placing new objects.</returns>
        public static PointF StartPoint(int span = 100)
        {
            RectangleF bounds = BoundingBox();

            // return new PointF(bounds.X, bounds.Bottom+span);
            return new PointF(50, bounds.Bottom + span);
        }

        /// <summary>
        /// Pans the canvas view to position a component at a specified horizontal location.
        /// If the component is already within the central 2/3 of the viewport, no panning occurs.
        /// </summary>
        /// <param name="instanceGuid">The GUID of the component to focus on.</param>
        /// <param name="horizontalPosition">
        /// Horizontal position in the viewport where the component should be placed.
        /// 0 = left edge, 0.5 = center, 1 = right edge. Default is 0.5 (centered).
        /// </param>
        /// <returns>True if the canvas was panned, false if no panning was needed or an error occurred.</returns>
        public static bool CenterViewOnComponent(Guid instanceGuid, float horizontalPosition = 0.5f)
        {
            if (instanceGuid == Guid.Empty)
            {
                return false;
            }

            // Clamp horizontal position to valid range
            horizontalPosition = Math.Max(0f, Math.Min(1f, horizontalPosition));

            try
            {
                var canvas = Instances.ActiveCanvas;
                if (canvas?.Document == null)
                {
                    System.Diagnostics.Debug.WriteLine("[CanvasAccess] CenterViewOnComponent: No active canvas");
                    return false;
                }

                var component = canvas.Document.FindObject(instanceGuid, true);
                if (component == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[CanvasAccess] CenterViewOnComponent: Component not found: {instanceGuid}");
                    return false;
                }

                // Get component bounds in canvas coordinates
                var componentBounds = component.Attributes.Bounds;
                var componentCenter = new PointF(
                    componentBounds.X + (componentBounds.Width / 2),
                    componentBounds.Y + (componentBounds.Height / 2));

                // Get current viewport bounds in canvas coordinates
                var viewportBounds = canvas.Viewport.VisibleRegion;

                // Calculate the central 2/3 region for the "already visible" check
                var centralWidth = viewportBounds.Width * 2f / 3f;
                var centralHeight = viewportBounds.Height * 2f / 3f;
                var centralLeft = viewportBounds.X + ((viewportBounds.Width - centralWidth) / 2f);
                var centralTop = viewportBounds.Y + ((viewportBounds.Height - centralHeight) / 2f);
                var centralRegion = new RectangleF(centralLeft, centralTop, centralWidth, centralHeight);

                // Check if component is already within the central 2/3
                if (centralRegion.Contains(componentCenter))
                {
                    System.Diagnostics.Debug.WriteLine("[CanvasAccess] Component already in central region, no pan needed");
                    return false;
                }

                // Calculate the target X position in the viewport based on horizontalPosition
                // horizontalPosition: 0 = left edge, 0.5 = center, 1 = right edge
                var targetViewportX = viewportBounds.X + (viewportBounds.Width * horizontalPosition);

                // Calculate the offset needed to position the component at the target horizontal position
                // and center it vertically
                var viewportCenterY = viewportBounds.Y + (viewportBounds.Height / 2);

                var offsetX = componentCenter.X - targetViewportX;
                var offsetY = componentCenter.Y - viewportCenterY;

                // Pan the viewport
                var newMidpoint = new PointF(
                    canvas.Viewport.MidPoint.X + offsetX,
                    canvas.Viewport.MidPoint.Y + offsetY);

                canvas.Viewport.MidPoint = newMidpoint;
                canvas.Refresh();

                System.Diagnostics.Debug.WriteLine($"[CanvasAccess] Positioned component {instanceGuid} at horizontal {horizontalPosition:P0}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CanvasAccess] Error centering canvas: {ex.Message}");
                return false;
            }
        }
    }
}
