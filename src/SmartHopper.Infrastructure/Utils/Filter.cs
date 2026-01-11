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
using System.Diagnostics;
using System.Linq;

namespace SmartHopper.Infrastructure.Utils
{
    /// <summary>
    /// Encapsulates include/exclude rules parsed from a filter string
    /// (e.g. "a,b -c -d", "*" or "-*").
    /// </summary>
    public sealed class Filter
    {
        private Filter(
            bool excludeAll,
            bool includeAll,
            HashSet<string> includeSet,
            HashSet<string> excludeSet)
        {
            this.ExcludeAll = excludeAll;
            this.IncludeAll = includeAll;
            this.IncludeSet = includeSet;
            this.ExcludeSet = excludeSet;
        }

        public bool ExcludeAll { get; }

        public bool IncludeAll { get; }

        public IReadOnlySet<string> IncludeSet { get; }

        public IReadOnlySet<string> ExcludeSet { get; }

        /// <summary>
        /// Parse a filter expression. Null/empty ⇒ include-all; "-*" ⇒ exclude-all.
        /// </summary>
        public static Filter Parse(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new Filter(false, true, new HashSet<string>(StringComparer.OrdinalIgnoreCase), new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            }

            var parts = raw
                .Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .ToList();

            if (parts.Any(p => p == "-*"))
            {
                return new Filter(
                    excludeAll: true,
                    includeAll: false,
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            }

            var includes = parts.Where(p => !p.StartsWith("-")).ToList();
            var excludes = parts.Where(p => p.StartsWith("-")).Select(p => p.Substring(1)).Where(p => p != "*").ToList();

            bool includeAll = includes.Contains("*") || includes.Count == 0;
            var includeSet = new HashSet<string>(includes.Where(p => p != "*"), StringComparer.OrdinalIgnoreCase);
            var excludeSet = new HashSet<string>(excludes, StringComparer.OrdinalIgnoreCase);

            Debug.WriteLine($"[Filter] raw='{raw}', includeAll={includeAll}, include=[{string.Join(",", includeSet)}], exclude=[{string.Join(",", excludeSet)}]");

            return new Filter(false, includeAll, includeSet, excludeSet);
        }

        /// <summary>
        /// Return true if key passes include/exclude rules.
        /// </summary>
        public bool ShouldInclude(string key)
        {
            if (this.ExcludeAll)
            {
                return false;
            }

            if (this.ExcludeSet.Contains(key))
            {
                return false;
            }

            if (this.IncludeAll)
            {
                return true;
            }

            return this.IncludeSet.Contains(key);
        }
    }

    /// <summary>
    /// Static helper methods for filtering.
    /// </summary>
    public static class Filtering
    {
        public static Filter Parse(string raw) => Filter.Parse(raw);

        public static bool ShouldInclude(string key, Filter f) => f.ShouldInclude(key);
    }
}
