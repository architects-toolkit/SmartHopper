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
using SmartHopper.Core.Grasshopper.Utils.Components;
using Xunit;

namespace SmartHopper.Core.Grasshopper.Tests.Utils.Components
{
    /// <summary>
    /// Pure ranking tests for <see cref="ComponentSearchRanker"/>: matching tier order,
    /// deterministic tie-breaks, GUID deduplication, and phrase normalization — no Rhino
    /// runtime or component proxies involved.
    /// </summary>
    public sealed class ComponentSearchRankerTests
    {
        private static ComponentSearchEntry Entry(
            string name,
            string? nick = null,
            IReadOnlyList<string>? keywords = null,
            Guid? guid = null)
        {
            return new ComponentSearchEntry(
                guid ?? Guid.NewGuid(),
                name,
                nick,
                keywords,
                "Category",
                "Sub");
        }

        /// <summary>
        /// A keyword hit must surface even when the query does not appear in name or
        /// nickname — the original "AI Settings finds nothing" failure (F4).
        /// </summary>
        [Fact]
        public void Rank_KeywordMatch_FindsComponentWithoutNameHit()
        {
            var settings = Entry("Settings", keywords: new[] { "AI", "SmartHopper", "AI Settings" });
            var unrelated = Entry("Slider");

            var ranked = ComponentSearchRanker.Rank(
                new List<ComponentSearchEntry> { unrelated, settings }, "AI Settings");

            Assert.Same(settings, Assert.Single(ranked));
        }

        /// <summary>
        /// Exact name beats an exact keyword hit; exact keyword beats a substring hit.
        /// </summary>
        [Fact]
        public void Rank_TierOrder_ExactNameThenKeywordThenSubstring()
        {
            var substringOnly = Entry("Settings Panel");
            var keywordExact = Entry("Panel", keywords: new[] { "Settings" });
            var nameExact = Entry("Settings");

            var ranked = ComponentSearchRanker.Rank(
                new List<ComponentSearchEntry> { substringOnly, keywordExact, nameExact }, "settings");

            Assert.Equal(new[] { nameExact, keywordExact, substringOnly }, ranked.ToArray());
        }

        /// <summary>
        /// Prefix matches rank ahead of plain substring matches at the same field level.
        /// </summary>
        [Fact]
        public void Rank_PrefixBeatsSubstring()
        {
            var substring = Entry("A Slider B");
            var prefix = Entry("Slider Control");

            var ranked = ComponentSearchRanker.Rank(
                new List<ComponentSearchEntry> { substring, prefix }, "slider");

            Assert.Equal(new[] { prefix, substring }, ranked.ToArray());
        }

        /// <summary>
        /// Equal-tier ties break deterministically by name, then nickname, then GUID —
        /// registry enumeration order must not leak into results.
        /// </summary>
        [Fact]
        public void Rank_Ties_BreakByNameThenNickNameThenGuid()
        {
            var byGuid1 = Entry("Alpha", nick: "n", guid: new Guid("BBBBBBBB-BBBB-BBBB-BBBB-BBBBBBBBBBBB"));
            var byGuid0 = Entry("Alpha", nick: "n", guid: new Guid("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA"));
            var nickZ = Entry("Alpha", nick: "z");
            var nickA = Entry("Alpha", nick: "a");
            var nickNull = Entry("Alpha");

            var ranked = ComponentSearchRanker.Rank(
                new List<ComponentSearchEntry> { byGuid1, nickZ, nickA, nickNull, byGuid0 }, "alpha");

            // All exact-name tier: name equal, nicknames sort ordinal (null first),
            // identical nicknames fall back to GUID order.
            Assert.Equal(new[] { nickNull, nickA, byGuid0, byGuid1, nickZ }, ranked.ToArray());
        }

        /// <summary>
        /// Within one tier, different names sort alphabetically (ordinal-ignore-case).
        /// </summary>
        [Fact]
        public void Rank_SameTier_OrdersByName()
        {
            var sliderB = Entry("Slider B");
            var sliderA = Entry("Slider A");

            var ranked = ComponentSearchRanker.Rank(
                new List<ComponentSearchEntry> { sliderB, sliderA }, "slider");

            Assert.Equal(new[] { sliderA, sliderB }, ranked.ToArray());
        }

        /// <summary>
        /// Duplicate GUIDs collapse to a single entry even when they carry different
        /// display names (the same component registered twice stays one hit).
        /// </summary>
        [Fact]
        public void Rank_DeduplicatesByGuid()
        {
            var guid = Guid.NewGuid();
            var a = Entry("Panel", guid: guid);
            var b = Entry("Panel (old)", guid: guid);
            var other = Entry("Panel extra");

            var ranked = ComponentSearchRanker.Rank(
                new List<ComponentSearchEntry> { a, b, other }, "panel");

            Assert.Equal(2, ranked.Count);
            Assert.Contains(ranked, r => r.Guid == guid);
        }

        /// <summary>
        /// Empty and whitespace-only queries return every entry in input order —
        /// no ranking perturbation for the unfiltered listing path.
        /// </summary>
        [Fact]
        public void Rank_EmptyQuery_KeepsInputOrder()
        {
            var a = Entry("Zeta");
            var b = Entry("Alpha");

            var ranked = ComponentSearchRanker.Rank(new List<ComponentSearchEntry> { a, b }, "   ");

            Assert.Equal(new[] { a, b }, ranked.ToArray());
        }

        /// <summary>
        /// Multiword queries are one phrase: collapsed whitespace and ordinal
        /// case-insensitive matching, never per-token OR semantics.
        /// </summary>
        [Fact]
        public void Rank_MultiwordQuery_IsASinglePhrase()
        {
            var phrase = Entry("Number Slider");
            var onlyOneWord = Entry("Slider");

            var ranked = ComponentSearchRanker.Rank(
                new List<ComponentSearchEntry> { onlyOneWord, phrase }, "  number   slider  ");

            Assert.Same(phrase, Assert.Single(ranked));
        }

        /// <summary>
        /// Null/empty keyword arrays and non-ASCII names rank without throwing.
        /// </summary>
        [Fact]
        public void Rank_EdgeMetadata_NoThrowAndMatches()
        {
            var unicode = Entry("Courbe de Bézier");
            var nullKeywords = Entry("Panel", keywords: null);
            var emptyKeywords = Entry("Circle", keywords: Array.Empty<string>());

            var ranked = ComponentSearchRanker.Rank(
                new List<ComponentSearchEntry> { nullKeywords, emptyKeywords, unicode }, "bézier");

            Assert.Same(unicode, Assert.Single(ranked));
        }
    }
}
