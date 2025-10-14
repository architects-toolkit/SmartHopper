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
using SmartHopper.Core.Grasshopper.Utils.Serialization;

namespace SmartHopper.Core.Grasshopper.Utils
{
    /// <summary>
    /// OBSOLETE: Moved to SmartHopper.Core.Grasshopper.Utils.Serialization.ErrorAccess
    /// </summary>
    [System.Obsolete("This class has been moved to SmartHopper.Core.Grasshopper.Utils.Serialization.ErrorAccess. Please update your references.", false)]
    public static class GHErrors
    {
        public static IList<string> GetRuntimeErrors(IGH_ActiveObject obj, string type = "error")
        {
            return ErrorAccess.GetRuntimeErrors(obj, type);
        }
    }
}