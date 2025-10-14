/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using SmartHopper.Core.Grasshopper.Utils.Internal;

namespace SmartHopper.Core.Grasshopper.Utils
{
    /// <summary>
    /// Provides web-related utility functions, including robots.txt parsing.
    /// </summary>
    /// <remarks>OBSOLETE: Moved to SmartHopper.Core.Grasshopper.Utils.Internal.WebUtilities</remarks>
    [System.Obsolete("This class has been moved to SmartHopper.Core.Grasshopper.Utils.Internal.WebUtilities. Please update your references.", false)]
    internal sealed class WebTools : WebUtilities
    {
        public WebTools(string robotsTxtContent = null) : base(robotsTxtContent)
        {
        }
    }
}