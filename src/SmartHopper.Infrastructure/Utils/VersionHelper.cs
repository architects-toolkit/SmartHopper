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
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;

namespace SmartHopper.Infrastructure.Utils
{
    /// <summary>
    /// Provides version information utilities for SmartHopper.
    /// </summary>
    public static class VersionHelper
    {
        /// <summary>
        /// Gets the full SmartHopper version including prerelease tags and commit hash.
        /// </summary>
        /// <returns>Full version string (e.g., "1.2.3-alpha+abc123")</returns>
        public static string GetFullVersion()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                    .InformationalVersion;
                
                return version ?? "Unknown";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VersionHelper] Error getting full version: {ex.Message}");
                return "Unknown";
            }
        }

        /// <summary>
        /// Gets the SmartHopper version for display purposes (without commit hash).
        /// </summary>
        /// <returns>Display version string (e.g., "1.2.3-alpha")</returns>
        public static string GetDisplayVersion()
        {
            try
            {
                string fullVersion = GetFullVersion();
                
                // Remove commit hash part (everything after '+')
                int plusIndex = fullVersion.IndexOf('+');
                if (plusIndex >= 0)
                {
                    return fullVersion.Substring(0, plusIndex);
                }
                
                return fullVersion;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VersionHelper] Error getting display version: {ex.Message}");
                return "Unknown";
            }
        }

        /// <summary>
        /// Checks if the current version is a development version (dev).
        /// </summary>
        /// <returns>True if version contains a development tag, false otherwise</returns>
        public static bool IsDevelopment()
        {
            string displayVersion = GetDisplayVersion();
            return displayVersion.Contains("-dev");
        }
        
        /// <summary>
        /// Checks if the current version is a prerelease (alpha, beta, or rc).
        /// </summary>
        /// <returns>True if version contains prerelease tag, false otherwise</returns>
        public static bool IsPrerelease()
        {
            string displayVersion = GetDisplayVersion();
            return displayVersion.Contains("-alpha") || 
                   displayVersion.Contains("-beta") || 
                   displayVersion.Contains("-rc");
        }

        /// <summary>
        /// Checks if the current version is a stable release (no prerelease tag).
        /// </summary>
        /// <returns>True if version is stable, false otherwise</returns>
        public static bool IsStable()
        {
            string displayVersion = GetDisplayVersion();
            return !(displayVersion.Contains("-alpha") || 
                   displayVersion.Contains("-beta") || 
                   displayVersion.Contains("-rc") ||
                   displayVersion.Contains("-dev"));
        }

        /// <summary>
        /// Gets the prerelease tag if present (alpha, beta, rc).
        /// </summary>
        /// <returns>Prerelease tag or null if stable version</returns>
        public static string GetPrereleaseTag()
        {
            string displayVersion = GetDisplayVersion();
            
            var match = Regex.Match(displayVersion, @"-(alpha|beta|rc)");
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
            
            return null;
        }
    }
}
