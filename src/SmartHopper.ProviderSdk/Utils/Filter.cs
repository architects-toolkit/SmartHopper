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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
namespace SmartHopper.ProviderSdk.Utils
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

            var includes = parts.Where(p => !p.StartsWith("-")).Select(p => p.StartsWith("+") ? p.Substring(1) : p).ToList();
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

        /// <summary>
        /// Return true if a dot-separated hierarchical path (e.g. "Canvas.Query")
        /// passes include/exclude rules. A token matches the path when it equals
        /// the path itself or any of its ancestor segments: token "Canvas"
        /// matches "Canvas.Query"; token "Canvas.Query" matches only that leaf.
        /// </summary>
        public bool ShouldIncludePath(string path)
        {
            if (this.ExcludeAll)
            {
                return false;
            }

            if (PathMatchesAny(path, this.ExcludeSet))
            {
                return false;
            }

            if (this.IncludeAll)
            {
                return true;
            }

            return PathMatchesAny(path, this.IncludeSet);
        }

        private static bool PathMatchesAny(string path, IReadOnlySet<string> tokens)
        {
            foreach (var token in tokens)
            {
                var t = token;
                if (t.EndsWith(".*", StringComparison.Ordinal))
                {
                    t = t.Substring(0, t.Length - 2);
                }
                else if (t.EndsWith("*", StringComparison.Ordinal))
                {
                    t = t.Substring(0, t.Length - 1);
                }

                if (t.Length == 0)
                {
                    continue;
                }

                if (string.Equals(path, t, StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith(t + ".", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
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
