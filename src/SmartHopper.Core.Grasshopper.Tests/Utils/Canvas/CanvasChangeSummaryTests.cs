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
using System.IO;
using System.Linq;
using GhJSON.Core.SchemaModels;
using SmartHopper.Core.Grasshopper.Utils.Canvas;
using Xunit;

namespace SmartHopper.Core.Grasshopper.Tests.Utils.Canvas
{
    /// <summary>
    /// Tests for <see cref="CanvasChangeSummary"/>, <see cref="GhJsonStableIdMap"/> and <see cref="ChatCanvasBaselineStore"/>.
    /// </summary>
    public sealed class CanvasChangeSummaryTests
    {
        private static readonly Guid SliderGuid = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid AdditionGuid = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid PanelGuid = Guid.Parse("33333333-3333-3333-3333-333333333333");

        /// <summary>
        /// Verifies that identical snapshots and pivot-only moves produce no summary.
        /// </summary>
        [Fact]
        public void Build_ReturnsNull_WhenOnlyPivotsChanged()
        {
            var baseline = CreateDocument(new[] { CreateComponent(1, SliderGuid, "Number Slider", 0) });
            var moved = CreateDocument(new[] { CreateComponent(1, SliderGuid, "Number Slider", 500) });

            Assert.Null(CanvasChangeSummary.Build(baseline, moved));
        }

        /// <summary>
        /// Verifies deterministic counts and the inlined patch for a small change.
        /// </summary>
        [Fact]
        public void Build_ReportsCountsAndInlinesSmallPatch()
        {
            var baseline = CreateDocument(new[] { CreateComponent(1, SliderGuid, "Number Slider", 0) });
            var current = CreateDocument(
                new[]
                {
                    CreateComponent(1, SliderGuid, "Number Slider", 0),
                    CreateComponent(2, AdditionGuid, "Addition", 100),
                },
                new[] { CreateConnection(1, "Number", 2, "A") });

            var summary = CanvasChangeSummary.Build(baseline, current);

            Assert.NotNull(summary);
            Assert.StartsWith(
                "Canvas changed since the last AI response: 1 component added, 0 removed, 0 modified, 0 input modifiers changed, 0 output modifiers changed; 1 connection added, 0 removed.",
                summary);
            Assert.Contains("```json", summary);
            Assert.Contains("\"name\":\"Addition\"", summary);
        }

        /// <summary>
        /// Verifies that large patches are replaced by a gh_get hint that lists affected and removed components,
        /// and that a replaced component (new instance GUID) is reported as removal plus addition.
        /// </summary>
        [Fact]
        public void Build_ReplacesLargePatchWithGhGetHint()
        {
            var baseline = CreateDocument(new[]
            {
                CreateComponent(1, SliderGuid, "Number Slider", 0),
                CreateComponent(2, PanelGuid, "Panel", 100),
            });
            var current = CreateDocument(new[]
            {
                CreateComponent(1, SliderGuid, "Number Slider", 0),
                CreateComponent(2, AdditionGuid, "Addition", 100),
            });

            var idMap = new GhJsonStableIdMap();
            idMap.Apply(baseline);
            idMap.Apply(current);

            var summary = CanvasChangeSummary.Build(baseline, current, maxInlinePatchLength: 10);

            Assert.NotNull(summary);
            Assert.StartsWith(
                "Canvas changed since the last AI response: 1 component added, 1 removed, 0 modified, 0 input modifiers changed, 0 output modifiers changed; 0 connections added, 0 removed.",
                summary);
            Assert.DoesNotContain("```json", summary);
            Assert.Contains("too large to include", summary);
            Assert.Contains("`gh_get`", summary);
            Assert.Contains("`" + AdditionGuid.ToString("D") + "`", summary);
            Assert.Contains("Removed components: ", summary);
            Assert.Contains(PanelGuid.ToString("D"), summary);
        }

        /// <summary>
        /// Verifies that stable ids prevent spurious connection changes when an earlier component is removed.
        /// </summary>
        [Fact]
        public void StableIdMap_PreventsSpuriousConnectionChanges()
        {
            var idMap = new GhJsonStableIdMap();
            var baseline = CreateDocument(
                new[]
                {
                    CreateComponent(1, PanelGuid, "Panel", 0),
                    CreateComponent(2, SliderGuid, "Number Slider", 100),
                    CreateComponent(3, AdditionGuid, "Addition", 200),
                },
                new[] { CreateConnection(2, "Number", 3, "A") });
            idMap.Apply(baseline);

            // Removing the first component shifts the sequential ids of the remaining ones.
            var current = CreateDocument(
                new[]
                {
                    CreateComponent(1, SliderGuid, "Number Slider", 100),
                    CreateComponent(2, AdditionGuid, "Addition", 200),
                },
                new[] { CreateConnection(1, "Number", 2, "A") });
            idMap.Apply(current);

            var summary = CanvasChangeSummary.Build(baseline, current);

            Assert.NotNull(summary);
            Assert.StartsWith(
                "Canvas changed since the last AI response: 0 components added, 1 removed, 0 modified, 0 input modifiers changed, 0 output modifiers changed; 0 connections added, 0 removed.",
                summary);
            Assert.Equal(2, current.Components.Single(c => c.InstanceGuid == SliderGuid).Id);
            Assert.Equal(2, current.Connections!.Single().From.Id);
        }

        /// <summary>
        /// Verifies baseline round-trip and deletion.
        /// </summary>
        [Fact]
        public void BaselineStore_RoundTripsAndDeletes()
        {
            var folder = Path.Combine(Path.GetTempPath(), "sh-baseline-tests-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new ChatCanvasBaselineStore(folder);
                Assert.Null(store.Load());

                Assert.True(store.Save("{\"components\":[]}"));
                Assert.Equal("{\"components\":[]}", store.Load());
                Assert.StartsWith(folder, store.FilePath);

                store.Delete();
                Assert.Null(store.Load());
            }
            finally
            {
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, true);
                }
            }
        }

        private static GhJsonDocument CreateDocument(
            IEnumerable<GhJsonComponent> components,
            IEnumerable<GhJsonConnection>? connections = null)
        {
            return new GhJsonDocument("1.0", null, components, connections, null);
        }

        private static GhJsonComponent CreateComponent(int id, Guid instanceGuid, string name, int x)
        {
            return new GhJsonComponent
            {
                Id = id,
                InstanceGuid = instanceGuid,
                ComponentGuid = Guid.Parse("3e8f5f24-8e8c-4f11-a538-4dcd67e3590f"),
                Name = name,
                NickName = name,
                Pivot = new GhJsonPivot(x, 100),
            };
        }

        private static GhJsonConnection CreateConnection(int fromId, string fromParam, int toId, string toParam)
        {
            return new GhJsonConnection
            {
                From = new GhJsonConnectionEndpoint { Id = fromId, ParamName = fromParam },
                To = new GhJsonConnectionEndpoint { Id = toId, ParamName = toParam },
            };
        }
    }
}
