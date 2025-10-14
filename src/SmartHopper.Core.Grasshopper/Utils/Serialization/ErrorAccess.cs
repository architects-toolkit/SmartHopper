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
using System.Linq;
using Grasshopper.Kernel;

namespace SmartHopper.Core.Grasshopper.Utils.Serialization
{
    public static class ErrorAccess
    {
        public static IList<string> GetRuntimeErrors(IGH_ActiveObject obj, string type = "error")
        {
            List<string> errors = new List<string>();

            if (obj != null && obj.RuntimeMessages(GH_RuntimeMessageLevel.Error).Any())
            {
                errors = obj.RuntimeMessages(GH_RuntimeMessageLevel.Error).ToList();
            }

            if (type.ToLowerInvariant() == "warning" && obj != null && obj.RuntimeMessages(GH_RuntimeMessageLevel.Warning).Any())
            {
                errors = obj.RuntimeMessages(GH_RuntimeMessageLevel.Warning).ToList();
            }

            if (type.ToLowerInvariant() == "remark" && obj != null && obj.RuntimeMessages(GH_RuntimeMessageLevel.Remark).Any())
            {
                errors = obj.RuntimeMessages(GH_RuntimeMessageLevel.Remark).ToList();
            }

            return errors;
        }
    }
}
