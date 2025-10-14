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
using SmartHopper.Core.Grasshopper.Utils.Internal;
using SmartHopper.Core.Models.Document;

namespace SmartHopper.Core.Grasshopper.Utils
{
    /// <summary>
    /// Utility to place deserialized Grasshopper objects onto the canvas.
    /// </summary>
    /// <remarks>OBSOLETE: Moved to SmartHopper.Core.Grasshopper.Utils.Internal.GhJsonPlacer</remarks>
    [System.Obsolete("This class has been moved to SmartHopper.Core.Grasshopper.Utils.Internal.GhJsonPlacer. Please update your references.", false)]
    internal static class Put
    {
        public static List<string> PutObjectsOnCanvas(GrasshopperDocument document, int span = 100)
        {
            return GhJsonPlacer.PutObjectsOnCanvas(document, span);
        }

        public static List<string> PutObjectsOnCanvas(GrasshopperDocument document, PointF startPoint)
        {
            return GhJsonPlacer.PutObjectsOnCanvas(document, startPoint);
        }

        public static string ReplaceIntegerIdsInGhJson(string ghjsonString)
        {
            return GhJsonPlacer.ReplaceIntegerIdsInGhJson(ghjsonString);
        }

        public static GrasshopperDocument ReplaceIntegerIdsInGhJson(GrasshopperDocument document)
        {
            return GhJsonPlacer.ReplaceIntegerIdsInGhJson(document);
        }

        public static Dictionary<Guid, Guid> PutObjectsOnCanvasWithMapping(GrasshopperDocument document, int span = 100)
        {
            return GhJsonPlacer.PutObjectsOnCanvasWithMapping(document, span);
        }

        public static Dictionary<Guid, Guid> PutObjectsOnCanvasWithMapping(GrasshopperDocument document, PointF startPoint)
        {
            return GhJsonPlacer.PutObjectsOnCanvasWithMapping(document, startPoint);
        }
    }
}