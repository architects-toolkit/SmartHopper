/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using Newtonsoft.Json.Linq;
using RhinoDocObjects = global::Rhino.DocObjects;
using SmartHopper.Core.Grasshopper.Utils.Rhino;

namespace SmartHopper.Core.Grasshopper.Utils
{
    /// <summary>
    /// Utilities for extracting geometry information from the active Rhino document.
    /// </summary>
    /// <remarks>OBSOLETE: Moved to SmartHopper.Core.Grasshopper.Utils.Rhino.DocumentGeometryExtractor</remarks>
    [System.Obsolete("This class has been moved to SmartHopper.Core.Grasshopper.Utils.Rhino.DocumentGeometryExtractor. Please update your references.", false)]
    public static class RhinoGeometryUtils
    {
        public static JObject GetGeometry(
            string filter = "selected",
            string layerName = null,
            RhinoDocObjects.ObjectType? objectType = null,
            bool includeDetails = false,
            int maxObjects = 50)
        {
            return DocumentGeometryExtractor.GetGeometry(filter, layerName, objectType, includeDetails, maxObjects);
        }
    }
}