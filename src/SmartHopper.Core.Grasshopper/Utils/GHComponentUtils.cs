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
using System.Diagnostics;
using System.Drawing;
using Grasshopper;
using Grasshopper.Kernel;
using SmartHopper.Core.Grasshopper.Utils.Canvas;

namespace SmartHopper.Core.Grasshopper.Utils
{
    /// <summary>
    /// Utilities for manipulating Grasshopper component preview state.
    /// </summary>
    /// <remarks>OBSOLETE: Moved to SmartHopper.Core.Grasshopper.Utils.Canvas.ComponentManipulation</remarks>
    [System.Obsolete("This class has been moved to SmartHopper.Core.Grasshopper.Utils.Canvas.ComponentManipulation. Please update your references.", false)]
    public static class GHComponentUtils
    {
        public static void SetComponentPreview(Guid guid, bool previewOn, bool redraw = true)
        {
            ComponentManipulation.SetComponentPreview(guid, previewOn, redraw);
        }

        public static void SetComponentLock(Guid guid, bool locked, bool redraw = true)
        {
            ComponentManipulation.SetComponentLock(guid, locked, redraw);
        }

        public static RectangleF GetComponentBounds(Guid guid)
        {
            return ComponentManipulation.GetComponentBounds(guid);
        }
    }
}