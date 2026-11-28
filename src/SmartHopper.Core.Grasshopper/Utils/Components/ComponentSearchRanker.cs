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
using System.Linq;

namespace SmartHopper.Core.Grasshopper.Utils.Components
{
    /// <summary>
    /// Immutable component-catalogue metadata used for query matching and ranking.
    /// Snapshot on the UI thread so ranking runs off-thread on stable values.
    /// </summary>
    public sealed class ComponentSearchEntry
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ComponentSearchEntry"/> class.
        /// </summary>
        public ComponentSearchEntry(
            Guid guid,
            string? name,
            string? nickName,
            IReadOnlyList<string>? keywords,
            string? category,
            string? subCategory)
        {
            this.Guid = guid;
            this.Name = name;
            this.NickName = nickName;
            this.Keywords = keywords;
            this.Category = category;
            this.SubCategory = subCategory;
        }

        /// <summary>Gets the component type GUID.</summary>
        public Guid Guid { get; }

        /// <summary>Gets the component display name.</summary>
        public string? Name { get; }

        /// <summary>Gets the component nickname.</summary>
        public string? NickName { get; }

        /// <summary>Gets the catalogue keywords, or null when the component exposes none.</summary>
        public IReadOnlyList<string>? Keywords { get; }

        /// <summary>Gets the component category.</summary>
        public string? Category { get; }

        /// <summary>Gets the component subcategory.</summary>
        public string? SubCategory { get; }
    }

    /// <summary>
    /// Deterministic query matching and ranking for component-catalogue search.
    /// The query is treated as a single phrase (trimmed, whitespace collapsed),
    /// matched ordinal case-insensitively against Name, NickName and each keyword —
    /// keywords are what make queries like "AI Settings" find the Settings component.
    /// </summary>
    public static class ComponentSearchRanker
    {
        /// <summary>
        /// Filters and ranks <paramref name="entries"/> by <paramref name="query"/>.
        /// An empty or whitespace-only query returns every entry in input order.
        /// Otherwise entries are ordered by the first matching tier — exact Name,
        /// exact NickName, exact keyword, Name prefix, NickName prefix, keyword
        /// prefix, Name substring, NickName substring, keyword substring — then by
        /// Name, NickName and finally GUID so the output is fully deterministic.
        /// Entries are deduplicated by type GUID.
        /// </summary>
        /// <param name="entries">Candidate catalogue entries.</param>
        /// <param name="query">Raw query text; treated as a phrase.</param>
        /// <returns>Ranked matching entries.</returns>
        public static IReadOnlyList<ComponentSearchEntry> Rank(
            IReadOnlyList<ComponentSearchEntry> entries,
            string? query)
        {
            if (entries == null || entries.Count == 0)
            {
                return Array.Empty<ComponentSearchEntry>();
            }

            var seen = new HashSet<Guid>();
            var distinct = entries.Where(e => e != null && seen.Add(e.Guid)).ToList();

            var normalized = NormalizeQuery(query);
            if (normalized.Length == 0)
            {
                return distinct;
            }

            var scored = new List<(ComponentSearchEntry Entry, int Tier)>(distinct.Count);
            foreach (var entry in distinct)
            {
                var tier = MatchTier(entry, normalized);
                if (tier >= 0)
                {
                    scored.Add((entry, tier));
                }
            }

            return scored
                .OrderBy(s => s.Tier)
                .ThenBy(s => s.Entry.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => s.Entry.NickName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => s.Entry.Guid.ToString("D"), StringComparer.Ordinal)
                .Select(s => s.Entry)
                .ToList();
        }

        /// <summary>
        /// Returns the best matching tier for <paramref name="entry"/> against the
        /// already-normalized <paramref name="query"/>, or -1 when nothing matches.
        /// </summary>
        private static int MatchTier(ComponentSearchEntry entry, string query)
        {
            if (Matches(entry.Name, query, exact: true)) return 0;
            if (Matches(entry.NickName, query, exact: true)) return 1;
            if (KeywordsMatch(entry.Keywords, query, exact: true)) return 2;
            if (Matches(entry.Name, query, prefix: true)) return 3;
            if (Matches(entry.NickName, query, prefix: true)) return 4;
            if (KeywordsMatch(entry.Keywords, query, prefix: true)) return 5;
            if (Matches(entry.Name, query)) return 6;
            if (Matches(entry.NickName, query)) return 7;
            if (KeywordsMatch(entry.Keywords, query)) return 8;
            return -1;
        }

        private static bool Matches(string? candidate, string query, bool exact = false, bool prefix = false)
        {
            if (string.IsNullOrEmpty(candidate))
            {
                return false;
            }

            if (exact)
            {
                return string.Equals(candidate, query, StringComparison.OrdinalIgnoreCase);
            }

            if (prefix)
            {
                return candidate.StartsWith(query, StringComparison.OrdinalIgnoreCase);
            }

            return candidate.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool KeywordsMatch(IReadOnlyList<string>? keywords, string query, bool exact = false, bool prefix = false)
        {
            if (keywords == null)
            {
                return false;
            }

            foreach (var keyword in keywords)
            {
                if (Matches(keyword, query, exact, prefix))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeQuery(string? query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return string.Empty;
            }

            return string.Join(" ", query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
