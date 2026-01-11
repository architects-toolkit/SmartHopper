/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024-2026 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this library; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using System;
using Grasshopper.Kernel;

namespace SmartHopper.Core.Grasshopper.Serialization.GhJson.Shared
{
    /// <summary>
    /// Bidirectional mapper for GH_ParamAccess and string representations.
    /// Provides consistent access mode conversion across serialization and deserialization.
    /// </summary>
    public static class AccessModeMapper
    {
        /// <summary>
        /// Converts a GH_ParamAccess enum to its string representation.
        /// </summary>
        /// <param name="access">The access mode to convert</param>
        /// <returns>String representation ("item", "list", or "tree")</returns>
        public static string ToString(GH_ParamAccess access)
        {
            return access switch
            {
                GH_ParamAccess.item => "item",
                GH_ParamAccess.list => "list",
                GH_ParamAccess.tree => "tree",
                _ => "item"
            };
        }

        /// <summary>
        /// Converts a string representation to GH_ParamAccess enum.
        /// </summary>
        /// <param name="accessString">String representation of access mode</param>
        /// <returns>GH_ParamAccess value, defaults to item if parsing fails</returns>
        public static GH_ParamAccess FromString(string accessString)
        {
            if (string.IsNullOrWhiteSpace(accessString))
                return GH_ParamAccess.item;

            if (Enum.TryParse<GH_ParamAccess>(accessString, true, out var parsedAccess))
                return parsedAccess;

            return GH_ParamAccess.item;
        }

        /// <summary>
        /// Checks if a string represents a valid access mode.
        /// </summary>
        /// <param name="accessString">String to validate</param>
        /// <returns>True if valid access mode string</returns>
        public static bool IsValid(string accessString)
        {
            if (string.IsNullOrWhiteSpace(accessString))
                return false;

            return accessString.Equals("item", StringComparison.OrdinalIgnoreCase) ||
                   accessString.Equals("list", StringComparison.OrdinalIgnoreCase) ||
                   accessString.Equals("tree", StringComparison.OrdinalIgnoreCase);
        }
    }
}
