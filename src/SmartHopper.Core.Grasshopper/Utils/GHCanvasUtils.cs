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
using SmartHopper.Core.Grasshopper.Utils.Canvas;

namespace SmartHopper.Core.Grasshopper.Utils
{
    /// <summary>
    /// Utility helpers for interacting with the active Grasshopper canvas and document objects.
    /// </summary>
    /// <remarks>OBSOLETE: Moved to SmartHopper.Core.Grasshopper.Utils.Canvas.CanvasAccess</remarks>
    [System.Obsolete("This class has been moved to SmartHopper.Core.Grasshopper.Utils.Canvas.CanvasAccess. Please update your references.", false)]
    public static class GHCanvasUtils
    {
        /// <summary>
        /// Gets the current active Grasshopper document from the canvas.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1024:Use properties where appropriate", Justification = "Ambient UI state access; method communicates non-field-like behavior")]
        public static GH_Document GetCurrentCanvas()
        {
            return CanvasAccess.GetCurrentCanvas();
        }

        /// <summary>
        /// Gets the list of active objects in the current document.
        /// </summary>
        public static List<IGH_ActiveObject> GetCurrentObjects()
        {
            return CanvasAccess.GetCurrentObjects();
        }

        /// <summary>
        /// Adds an object to the active Grasshopper canvas at the specified position.
        /// </summary>
        /// <param name="obj">The Grasshopper document object to add.</param>
        /// <param name="position">The pivot position where the object should be placed.</param>
        /// <param name="redraw">True to redraw the canvas after adding the object.</param>
        public static void AddObjectToCanvas(IGH_DocumentObject obj, PointF position = default, bool redraw = true)
        {
            CanvasAccess.AddObjectToCanvas(obj, position, redraw);
        }

        /// <summary>
        /// Finds a document object instance by its GUID.
        /// </summary>
        /// <param name="guid">The instance GUID.</param>
        /// <returns>The matching document object or null if not found.</returns>
        public static IGH_DocumentObject FindInstance(Guid guid)
        {
            return CanvasAccess.FindInstance(guid);
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
            return CanvasAccess.MoveInstance(guid, position, relative, redraw);
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
            return CanvasAccess.MoveInstance(targets, relative, redraw);
        }

        /// <summary>
        /// Computes the bounding box of all objects in the current document.
        /// </summary>
        public static RectangleF BoundingBox()
        {
            return CanvasAccess.BoundingBox();
        }

        /// <summary>
        /// Determines a starting point to place new objects below the current canvas content.
        /// </summary>
        /// <param name="span">Vertical spacing from the bottom of the bounding box.</param>
        /// <returns>A point suitable for placing new objects.</returns>
        public static PointF StartPoint(int span = 100)
        {
            return CanvasAccess.StartPoint(span);
        }
    }
}
