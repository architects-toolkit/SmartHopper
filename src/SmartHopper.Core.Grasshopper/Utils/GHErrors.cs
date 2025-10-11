/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System.Collections.Generic;
using System.Diagnostics;
using Grasshopper.Kernel;

namespace SmartHopper.Core.Grasshopper.Utils
{
    public static class GHErrors
    {
        public static IList<string> GetRuntimeErrors(IGH_ActiveObject obj, string type = "error")
        {
            switch (type)
            {
                case "warning":
                    return obj.RuntimeMessages(GH_RuntimeMessageLevel.Warning);
                case "error":
                    return obj.RuntimeMessages(GH_RuntimeMessageLevel.Error);
                case "remark":
                case "info":
                    return obj.RuntimeMessages(GH_RuntimeMessageLevel.Remark);
                default:
                    Debug.WriteLine($"Unknown error type '{type}'. Returning error by default.");
                    return obj.RuntimeMessages(GH_RuntimeMessageLevel.Error);
            }
        }
    }
}
