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
using System.Collections.Generic;

namespace SmartHopper.Core.Grasshopper.Utils
{
    /// <summary>
    /// Provides web-related utility functions, including robots.txt parsing.
    /// </summary>
    internal sealed class WebTools
    {
        private readonly List<string> _disallowed = new ();

        /// <summary>
        /// Initializes a new instance of the <see cref="WebTools"/> class.
        /// </summary>
        /// <param name="robotsTxtContent">The content of the robots.txt file to parse.</param>
        public WebTools(string robotsTxtContent = null)
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
