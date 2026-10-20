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
 * along with this library; if not, see <https://www.gnu.org/licenses/lgpl-3.0.html>.
 */

using System;
using System.IO;

namespace SmartHopper.Core.Grasshopper.Utils.Canvas
{
    /// <summary>
    /// Validates file paths passed to document tools. Only absolute <c>.gh</c>/<c>.ghx</c> paths are accepted,
    /// so tools cannot read or write arbitrary file types. Pure logic, no Rhino/Grasshopper runtime.
    /// </summary>
    public static class GrasshopperFilePath
    {
        /// <summary>
        /// Validates and normalizes a Grasshopper document path.
        /// </summary>
        /// <param name="path">The requested path.</param>
        /// <param name="mustExist">True when the file must already exist (open); false for save targets.</param>
        /// <param name="fullPath">The normalized absolute path when valid.</param>
        /// <param name="error">A user-facing error when invalid.</param>
        /// <returns>True when the path is valid.</returns>
        public static bool TryNormalize(string? path, bool mustExist, out string fullPath, out string error)
        {
            fullPath = string.Empty;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(path))
            {
                error = "A file path is required.";
                return false;
            }

            string normalized;
            try
            {
                if (!Path.IsPathRooted(path))
                {
                    error = "The file path must be absolute.";
                    return false;
                }

                normalized = Path.GetFullPath(path);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
            {
                error = $"Invalid file path: {ex.Message}";
                return false;
            }

            var extension = Path.GetExtension(normalized);
            if (!string.Equals(extension, ".gh", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(extension, ".ghx", StringComparison.OrdinalIgnoreCase))
            {
                error = "Only Grasshopper files (.gh or .ghx) are supported.";
                return false;
            }

            if (mustExist && !File.Exists(normalized))
            {
                error = $"File not found: {normalized}";
                return false;
            }

            fullPath = normalized;
            return true;
        }
    }
}
