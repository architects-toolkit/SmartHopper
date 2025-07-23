/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using Grasshopper;
using Grasshopper.Kernel;

namespace SmartHopper.Core.Grasshopper.Utils
{
    public static class GHCanvasUtils
    {
        // Get the current canvas
        public static GH_Document GetCurrentCanvas()
        {
            GH_Document doc = Instances.ActiveCanvas.Document;

            return doc;
        }

        // Get the current objects in the active document
        public static List<IGH_ActiveObject> GetCurrentObjects()
        {
            GH_Document doc = GetCurrentCanvas();
            return doc.ActiveObjects();
        }

        // Add an object to the canvas
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
                    Rhino.RhinoApp.InvokeOnUiThread(() =>
                    {
                        obj.Attributes.Pivot = interp;
                        obj.Attributes.ExpireLayout();
                        // Instances.RedrawCanvas();
                    });
                    await Task.Delay(duration / steps);
                }
                // Final snap to target
                Rhino.RhinoApp.InvokeOnUiThread(() =>
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

            // Start the batch record (this also captures the first object’s action for you)
            var undo = doc.UndoUtil.CreateGenericObjectEvent(
                "[SH] Move Instances",
                moves[0].obj
            );

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
                    Rhino.RhinoApp.InvokeOnUiThread(() =>
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
                Rhino.RhinoApp.InvokeOnUiThread(() =>
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

                    // Once everything’s in place, commit the record as a single undo step
                    doc.UndoUtil.RecordEvent(undo);
                });
            });
            return moved;
        }

        // Identify occupied areas
        public static RectangleF BoundingBox()
        {
            GH_Document doc = GetCurrentCanvas();
            return doc.BoundingBox(false);
        }

        // Determine start point for empty space
        public static PointF StartPoint(int span = 100)
        {
            RectangleF bounds = BoundingBox();

            // return new PointF(bounds.X, bounds.Bottom+span);
            return new PointF(50, bounds.Bottom + span);
        }
    }
}
