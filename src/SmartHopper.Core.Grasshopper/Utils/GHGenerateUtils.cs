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
using System.Drawing;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Utils.Serialization;

namespace SmartHopper.Core.Grasshopper.Utils
{
    /// <summary>
    /// Utilities for generating Grasshopper component specifications.
    /// </summary>
    /// <remarks>OBSOLETE: Moved to SmartHopper.Core.Grasshopper.Utils.Serialization.ComponentSpecBuilder</remarks>
    [System.Obsolete("This class has been moved to SmartHopper.Core.Grasshopper.Utils.Serialization.ComponentSpecBuilder. Please update your references.", false)]
    public static class GHGenerateUtils
    {
        public static JObject GenerateComponentSpec(
            string componentName,
            Dictionary<string, object> parameters = null,
            PointF? position = null,
            Guid? instanceGuid = null)
        {
            return ComponentSpecBuilder.GenerateComponentSpec(componentName, parameters, position, instanceGuid);
        }

        public static JObject GenerateGhJsonDocument(List<JObject> componentSpecs)
        {
            return ComponentSpecBuilder.GenerateGhJsonDocument(componentSpecs);
        }
    }
}