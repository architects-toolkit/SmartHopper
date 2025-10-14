/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using SmartHopper.Core.Grasshopper.Utils.Serialization;

namespace SmartHopper.Core.Grasshopper.Utils
{
    /// <summary>
    /// Grasshopper-specific utilities for validating GhJSON format with Grasshopper component validation.
    /// </summary>
    /// <remarks>OBSOLETE: Moved to SmartHopper.Core.Grasshopper.Utils.Serialization.GhJsonValidator</remarks>
    [System.Obsolete("This class has been moved to SmartHopper.Core.Grasshopper.Utils.Serialization.GhJsonValidator. Please update your references.", false)]
    public static class GHJsonLocal
    {
        public static bool Validate(string json, out string errorMessage)
        {
            return GhJsonValidator.Validate(json, out errorMessage);
        }
    }
}