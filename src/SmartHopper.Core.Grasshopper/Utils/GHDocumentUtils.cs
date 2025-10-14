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
using Grasshopper.Kernel;
using SmartHopper.Core.Grasshopper.Utils.Serialization;
using SmartHopper.Core.Models.Document;

namespace SmartHopper.Core.Grasshopper.Utils
{
    /// <summary>
    /// Utilities for Grasshopper document introspection and manipulation.
    /// </summary>
    /// <remarks>OBSOLETE: Moved to SmartHopper.Core.Grasshopper.Utils.Serialization.DocumentIntrospection</remarks>
    [System.Obsolete("This class has been moved to SmartHopper.Core.Grasshopper.Utils.Serialization.DocumentIntrospection. Please update your references.", false)]
    public static class GHDocumentUtils
    {
        public static IGH_DocumentObject GroupObjects(IList<Guid> guids, string groupName = null, System.Drawing.Color? color = null)
        {
            return DocumentIntrospection.GroupObjects(guids, groupName, color);
        }

        public static GrasshopperDocument GetObjectsDetails(IEnumerable<IGH_ActiveObject> objects)
        {
            return DocumentIntrospection.GetObjectsDetails(objects);
        }

        public static Dictionary<string, object> GetObjectProperties(IGH_DocumentObject obj)
        {
            return DocumentIntrospection.GetObjectProperties(obj);
        }
    }
}