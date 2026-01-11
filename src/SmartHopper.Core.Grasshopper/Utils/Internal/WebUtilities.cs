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
using System.Collections.Generic;

namespace SmartHopper.Core.Grasshopper.Utils.Internal
{
    /// <summary>
    /// Provides web-related utility functions, including robots.txt parsing.
    /// </summary>
    internal class WebUtilities
    {
        private readonly List<string> _disallowed = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="WebUtilities"/> class.
        /// </summary>
        /// <param name="robotsTxtContent">The content of the robots.txt file to parse.</param>
        public WebUtilities(string robotsTxtContent = null)
        {
            if (!string.IsNullOrEmpty(robotsTxtContent))
            {
                this.ParseRobotsTxt(robotsTxtContent);
            }
        }

        /// <summary>
        /// Checks if a given path is allowed by the robots.txt rules.
        /// </summary>
        /// <param name="path">The path to check (e.g., "/some/path")</param>
        /// <returns>True if the path is allowed, false if disallowed by robots.txt</returns>
        public bool IsPathAllowed(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return true;
            }

            // Ensure path starts with /
            string normalizedPath = path.StartsWith('/') ? path : "/" + path;

            // Check against all disallowed patterns
            foreach (var pattern in this._disallowed)
            {
                if (string.IsNullOrEmpty(pattern))
                {
                    continue;
                }

                // Simple pattern matching - could be enhanced with regex for full robots.txt spec
                if (normalizedPath.StartsWith(pattern.TrimEnd('*'), StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        private void ParseRobotsTxt(string content)
        {
            this._disallowed.Clear();
            bool appliesToAll = false;

            foreach (var line in content.Split('\n'))
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                {
                    continue;
                }

                var parts = trimmed.Split(':', 2);
                if (parts.Length != 2)
                {
                    continue;
                }

                string field = parts[0].Trim().ToLowerInvariant();
                string value = parts[1].Trim();
                if (field == "user-agent")
                {
                    appliesToAll = value == "*";
                }
                else if (field == "disallow" && appliesToAll && !string.IsNullOrEmpty(value))
                {
                    this._disallowed.Add(value);
                }
            }
        }
    }
}
