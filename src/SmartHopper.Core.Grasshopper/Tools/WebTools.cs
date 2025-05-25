/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

/*
 * WebTools.cs
 * Defines AI tool for fetching webpage text content, stripping HTML and respecting robots.txt.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using SmartHopper.Config.Interfaces;
using SmartHopper.Config.Models;

namespace SmartHopper.Core.Grasshopper.Tools
{
    public class WebTools
    {
        private readonly List<string> disallowed = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="RobotsTxtParser"/> class.
        /// Simple robots.txt parser supporting User-agent: * and Disallow directives.
        /// </summary>
        public RobotsTxtParser(string content)
        {
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
                else if (field == "disallow" && appliesToAll)
                {
                    this.disallowed.Add(value);
                }
                else if (field == "allow" && appliesToAll)
                {
                    // Could implement allow rules, but ignored for simplicity
                }
            }
        }

        /// <summary>
        /// Returns true if the given path is allowed to be fetched.
        /// </summary>
        public bool IsAllowed(string path)
        {
            foreach (var rule in this.disallowed)
            {
                if (string.IsNullOrEmpty(rule))
                {
                    continue;
                }

                if (path.StartsWith(rule))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
