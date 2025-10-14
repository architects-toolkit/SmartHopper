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
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using RhinoDocObjects = global::Rhino.DocObjects;
using RhinoFileIO = global::Rhino.FileIO;
using RhinoGeometry = global::Rhino.Geometry;
using SmartHopper.Core.Grasshopper.Utils.Rhino;

namespace SmartHopper.Core.Grasshopper.Utils
{
    /// <summary>
    /// Utilities for reading and analyzing Rhino 3DM files.
    /// </summary>
    /// <remarks>OBSOLETE: Moved to SmartHopper.Core.Grasshopper.Utils.Rhino.File3dmReader</remarks>
    [System.Obsolete("This class has been moved to SmartHopper.Core.Grasshopper.Utils.Rhino.File3dmReader. Please update your references.", false)]
    public static class RhinoFileUtils
    {
        /// <summary>
        /// Reads a 3DM file and extracts metadata, summary statistics, and layer information.
        /// </summary>
        /// <param name="filePath">Path to the .3dm file.</param>
        /// <param name="includeObjectDetails">Whether to include detailed object information.</param>
        /// <param name="maxObjects">Maximum number of objects to include in details.</param>
        /// <param name="typeFilter">Optional filter for object types.</param>
        /// <returns>JObject containing file analysis results, or null if failed.</returns>
        public static JObject Read3dmFile(
            string filePath,
            bool includeObjectDetails = false,
            int maxObjects = 100,
            HashSet<RhinoDocObjects.ObjectType> typeFilter = null)
        {
            return File3dmReader.Read3dmFile(filePath, includeObjectDetails, maxObjects, typeFilter);
        }
    }
}