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
using SmartHopper.Core.Grasshopper.Utils.Canvas;

namespace SmartHopper.Core.Grasshopper.Utils
{
    /// <summary>
    /// Utilities for connecting Grasshopper components.
    /// </summary>
    /// <remarks>OBSOLETE: Moved to SmartHopper.Core.Grasshopper.Utils.Canvas.ConnectionBuilder</remarks>
    [System.Obsolete("This class has been moved to SmartHopper.Core.Grasshopper.Utils.Canvas.ConnectionBuilder. Please update your references.", false)]
    public static class GHConnectionUtils
    {
        public static bool ConnectComponents(
            Guid sourceGuid,
            Guid targetGuid,
            string sourceParamName = null,
            string targetParamName = null,
            bool redraw = true)
        {
            return ConnectionBuilder.ConnectComponents(sourceGuid, targetGuid, sourceParamName, targetParamName, redraw);
        }
    }
}