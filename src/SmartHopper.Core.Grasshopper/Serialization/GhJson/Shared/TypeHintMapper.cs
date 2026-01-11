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
    /// Handles type hint formatting and conversion for parameter serialization.
    /// Provides consistent type hint representation across serialization and deserialization.
    /// </summary>
    public static class TypeHintMapper
    {
        /// <summary>
        /// Converts a type name to standard C# format with proper access wrapper.
        /// </summary>
        /// <param name="typeName">Base type name (e.g., "Curve", "Point3d")</param>
        /// <param name="access">Parameter access mode</param>
        /// <returns>Formatted type hint (e.g., "List&lt;Curve&gt;", "DataTree&lt;Point3d&gt;")</returns>
        public static string FormatTypeHint(string typeName, GH_ParamAccess access)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                return null;

            return access switch
            {
                GH_ParamAccess.tree => $"DataTree<{typeName}>",
                GH_ParamAccess.list => $"List<{typeName}>",
                GH_ParamAccess.item => typeName,
                _ => typeName
            };
        }

        /// <summary>
        /// Extracts the base type name from a formatted type hint.
        /// </summary>
        /// <param name="typeHint">Formatted type hint (e.g., "List&lt;Curve&gt;")</param>
        /// <returns>Base type name (e.g., "Curve")</returns>
        public static string ExtractBaseType(string typeHint)
        {
            if (string.IsNullOrWhiteSpace(typeHint))
                return null;

            // Check for generic types: List<T> or DataTree<T>
            var startIndex = typeHint.IndexOf('<');
            var endIndex = typeHint.LastIndexOf('>');

            if (startIndex > 0 && endIndex > startIndex)
            {
                return typeHint.Substring(startIndex + 1, endIndex - startIndex - 1).Trim();
            }

            return typeHint;
        }

        /// <summary>
        /// Determines if a type hint represents a collection type.
        /// </summary>
        /// <param name="typeHint">Type hint to check</param>
        /// <returns>True if type hint represents List or DataTree</returns>
        public static bool IsCollectionType(string typeHint)
        {
            if (string.IsNullOrWhiteSpace(typeHint))
                return false;

            return typeHint.StartsWith("List<", StringComparison.OrdinalIgnoreCase) ||
                   typeHint.StartsWith("DataTree<", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Infers access mode from a formatted type hint.
        /// </summary>
        /// <param name="typeHint">Formatted type hint</param>
        /// <returns>Inferred access mode</returns>
        public static GH_ParamAccess InferAccessMode(string typeHint)
        {
            if (string.IsNullOrWhiteSpace(typeHint))
                return GH_ParamAccess.item;

            if (typeHint.StartsWith("DataTree<", StringComparison.OrdinalIgnoreCase))
                return GH_ParamAccess.tree;

            if (typeHint.StartsWith("List<", StringComparison.OrdinalIgnoreCase))
                return GH_ParamAccess.list;

            return GH_ParamAccess.item;
        }

        /// <summary>
        /// Normalizes a type hint string for comparison.
        /// </summary>
        /// <param name="typeHint">Type hint to normalize</param>
        /// <returns>Normalized type hint string</returns>
        public static string Normalize(string typeHint)
        {
            if (string.IsNullOrWhiteSpace(typeHint))
                return null;

            // Remove extra whitespace
            return typeHint.Trim().Replace(" ", string.Empty);
        }
    }
}
