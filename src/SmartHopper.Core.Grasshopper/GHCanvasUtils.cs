﻿/*
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
using System.Drawing;

namespace SmartHopper.Core.Grasshopper
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
        public static void AddObjectToCanvas(IGH_DocumentObject obj, PointF position = default, bool live = false)
        {
            GH_Document doc = GetCurrentCanvas();

            obj.Attributes.Pivot = position;

            doc.AddObject(obj, false);

            if (live)
            {
                Instances.RedrawCanvas();
            }
        }

        public static IGH_DocumentObject FindInstance(Guid guid)
        {
            GH_Document doc = GetCurrentCanvas();

            IGH_DocumentObject obj = doc.FindObject(guid, true);

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
            //return new PointF(bounds.X, bounds.Bottom+span);
            return new PointF(50, bounds.Bottom + span);
        }
    }
}
