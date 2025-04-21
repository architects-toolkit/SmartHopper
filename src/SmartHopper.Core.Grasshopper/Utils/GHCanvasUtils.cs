/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using Grasshopper;
using Grasshopper.Kernel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
#if WINDOWS
using System.Drawing;
#else
using Eto.Drawing;
#endif
using System.Linq;

namespace SmartHopper.Core.Grasshopper.Utils
{
    public class GHCanvasUtils
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

#if WINDOWS
            obj.Attributes.Pivot = position;
#else
            obj.Attributes.Pivot = new System.Drawing.PointF(position.X, position.Y);
#endif

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
                //Debug.WriteLine("The object is an IGH_Component.");
                return component;
            }
            else if (obj is IGH_Param)
            {
                IGH_Param param = obj as IGH_Param;
                //Debug.WriteLine("The object is an IGH_Param.");
                return param;
            }
            else
            {
                //Debug.WriteLine("The object is neither an IGH_Component nor an IGH_Param.");
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
            var current = obj.Attributes.Pivot;
            var target = relative
                ? new PointF(current.X + position.X, current.Y + position.Y)
                : position;
#if WINDOWS
            obj.Attributes.Pivot = target;
#else
            obj.Attributes.Pivot = new System.Drawing.PointF(target.X, target.Y);
#endif
            if (redraw)
            {
                obj.Attributes.ExpireLayout();
                Instances.RedrawCanvas();
            }
            return true;
        }

        // Identify occupied areas
        public static RectangleF BoundingBox()
        {
            GH_Document doc = GetCurrentCanvas();
#if WINDOWS
            return doc.BoundingBox(false);
#else
            System.Drawing.RectangleF r = doc.BoundingBox(false);
            return new RectangleF(r.X, r.Y, r.Width, r.Height);
#endif
        }

        // Determine start point for empty space
        public static PointF StartPoint(int span = 100)
        {
            RectangleF bounds = BoundingBox();
            //return new PointF(bounds.X, bounds.Bottom+span);
            return new PointF(50, bounds.Bottom + span);
        }
    }
}
