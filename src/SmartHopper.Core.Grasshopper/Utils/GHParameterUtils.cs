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
    /// OBSOLETE: Moved to SmartHopper.Core.Grasshopper.Utils.Canvas.ParameterAccess
    /// </summary>
    [System.Obsolete("This class has been moved to SmartHopper.Core.Grasshopper.Utils.Canvas.ParameterAccess. Please update your references.", false)]
    public static class GHParameterUtils
    {
        public static List<IGH_Param> GetAllInputs(IGH_Component component)
        {
            return ParameterAccess.GetAllInputs(component);
        }

        public static List<IGH_Param> GetAllOutputs(IGH_Component component)
        {
            return ParameterAccess.GetAllOutputs(component);
        }

        public static IGH_Param? GetInputByName(IGH_Component component, string name)
        {
            return ParameterAccess.GetInputByName(component, name);
        }

        public static IGH_Param? GetOutputByName(IGH_Component component, string name)
        {
            return ParameterAccess.GetOutputByName(component, name);
        }

        public static void SetSource(IGH_Param instance, IGH_Param source)
        {
            ParameterAccess.SetSource(instance, source);
        }
    }
}