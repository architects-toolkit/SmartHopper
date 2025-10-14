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
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Utils.Canvas;

namespace SmartHopper.Core.Grasshopper.Utils
{
    /// <summary>
    /// OBSOLETE: Moved to SmartHopper.Core.Grasshopper.Utils.Canvas.ObjectFactory
    /// </summary>
    [System.Obsolete("This class has been moved to SmartHopper.Core.Grasshopper.Utils.Canvas.ObjectFactory. Please update your references.", false)]
    public static class GHObjectFactory
    {
        public static IGH_ObjectProxy? FindProxy(Guid guid)
        {
            return ObjectFactory.FindProxy(guid);
        }

        public static IGH_ObjectProxy? FindProxy(string name)
        {
            return ObjectFactory.FindProxy(name);
        }

        public static IGH_ObjectProxy? FindProxy(Guid guid, string name)
        {
            return ObjectFactory.FindProxy(guid, name);
        }

        public static IGH_DocumentObject CreateInstance(IGH_ObjectProxy objectProxy)
        {
            return ObjectFactory.CreateInstance(objectProxy);
        }
    }
}