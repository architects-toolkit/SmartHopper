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

namespace SmartHopper.ProviderSdk.Tests.AICall.Metrics
{
    using SmartHopper.ProviderSdk.AICall.Metrics;
    using Xunit;

    /// <summary>
    /// Tests for <see cref="AIMetricsList"/> role attribution: the list stores a
    /// stamped copy so callers can serialize the same labeled entry they added.
    /// </summary>
    [Collection("ProviderSdk")]
    public class AIMetricsListTests
    {
#if NET7_WINDOWS
        [Fact(DisplayName = "Add_WithRole_StampsEntryWithoutMutatingSource [Windows]")]
#else
        [Fact(DisplayName = "Add_WithRole_StampsEntryWithoutMutatingSource [Core]")]
#endif
        public void Add_WithRole_StampsEntryWithoutMutatingSource()
        {
            var source = new AIMetrics { Provider = "openai", Model = "gpt-test" };
            var list = new AIMetricsList();

            list.Add(source, "tool:text2text");

            Assert.Single(list.Entries);
            Assert.Equal("tool:text2text", list.Entries[0].Role);
            Assert.Null(source.Role);
            Assert.NotSame(source, list.Entries[0]);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Add_WithoutRole_KeepsExistingRole [Windows]")]
#else
        [Fact(DisplayName = "Add_WithoutRole_KeepsExistingRole [Core]")]
#endif
        public void Add_WithoutRole_KeepsExistingRole()
        {
            var stamped = new AIMetrics { Provider = "openai", Model = "gpt-test", Role = "fallback" };
            var list = new AIMetricsList();

            list.Add(stamped);

            Assert.Equal("fallback", list.Entries[0].Role);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Add_AlreadyStampedEntry_RoundTrips [Windows]")]
#else
        [Fact(DisplayName = "Add_AlreadyStampedEntry_RoundTrips [Core]")]
#endif
        public void Add_AlreadyStampedEntry_RoundTrips()
        {
            // CombineIntoPersistedMetrics stamps before Add — the entry must come
            // back with the same role so list and tree views cannot diverge.
            var entry = new AIMetrics { Provider = "openai", Model = "gpt-test" } with { Role = "main" };
            var list = new AIMetricsList();

            list.Add(entry);

            Assert.Equal("main", list.Entries[0].Role);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Add_EntriesNeverMerged [Windows]")]
#else
        [Fact(DisplayName = "Add_EntriesNeverMerged [Core]")]
#endif
        public void Add_EntriesNeverMerged()
        {
            var list = new AIMetricsList();

            list.Add(new AIMetrics { Provider = "openai", Model = "gpt-test" }, "main");
            list.Add(new AIMetrics { Provider = "openai", Model = "gpt-test" }, "tool:text2text");
            list.Add(new AIMetrics { Provider = "openai", Model = "gpt-test" }, "tool:text2text");

            Assert.Equal(3, list.Entries.Count);
            Assert.Equal("main", list.Entries[0].Role);
            Assert.Equal("tool:text2text", list.Entries[1].Role);
            Assert.Equal("tool:text2text", list.Entries[2].Role);
        }
    }
}
