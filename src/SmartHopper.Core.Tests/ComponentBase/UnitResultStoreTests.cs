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

namespace SmartHopper.Core.Tests.ComponentBase
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using SmartHopper.Core.ComponentBase.Cores;
    using SmartHopper.Core.DataTree;
    using SmartHopper.ProviderSdk.AICall.Metrics;
    using Xunit;

    /// <summary>
    /// Unit tests for <see cref="RunMetricsProjection"/>, the GH-free placement
    /// layer behind <see cref="UnitResultStore"/>: metrics-placement ordering,
    /// the per-occurrence reused flag, and run completion-time stamping. Path
    /// layout is expressed as raw index seats; Grasshopper types are only
    /// constructed at the emission boundary.
    /// </summary>
    public class UnitResultStoreTests
    {
        private static AIMetrics Metric(string id) => new AIMetrics { ExecutionId = id };

        private static UnitPlacement Seat(string key, int[] input, bool reused = false, params int[][] targets)
            => new UnitPlacement { UnitKey = key, InputIndices = input, TargetSeats = targets.ToList(), Reused = reused };

#if NET7_WINDOWS
        [Fact(DisplayName = "Metrics projection emits in placement order, not record order [Windows]")]
#else
        [Fact(DisplayName = "Metrics projection emits in placement order, not record order [Core]")]
#endif
        public void Build_EmitsInPlacementOrder()
        {
            // D43 regression: reused units replay metrics during processing while
            // fresh batch metrics arrive later — emission must follow placement
            // (output) order, not completion/record order.
            var placements = new List<UnitPlacement>
            {
                Seat("u0", new[] { 0 }, reused: true, new[] { 0 }),
                Seat("u1", new[] { 0 }, reused: false, new[] { 0 }),
                Seat("u2", new[] { 0 }, reused: true, new[] { 0 }),
            };
            var metrics = new Dictionary<string, List<AIMetrics>>
            {
                ["u1"] = new List<AIMetrics> { Metric("fresh") },
                ["u0"] = new List<AIMetrics> { Metric("papa") },
                ["u2"] = new List<AIMetrics> { Metric("romeo") },
            };

            var result = RunMetricsProjection.Build(
                placements, metrics, null, ProcessingTopology.BranchToBranch);

            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
            Assert.Equal("papa", result[0].Metrics.ExecutionId);
            Assert.Equal("fresh", result[1].Metrics.ExecutionId);
            Assert.Equal("romeo", result[2].Metrics.ExecutionId);
            Assert.True(result[0].Reused);
            Assert.False(result[1].Reused);
            Assert.True(result[2].Reused);
            Assert.All(result, p => Assert.False(p.SiblingCopy));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Metrics projection returns null without placements [Windows]")]
#else
        [Fact(DisplayName = "Metrics projection returns null without placements [Core]")]
#endif
        public void Build_NoPlacements_ReturnsNull()
        {
            var metrics = new Dictionary<string, List<AIMetrics>>
            {
                ["u0"] = new List<AIMetrics> { Metric("m") },
            };

            Assert.Null(RunMetricsProjection.Build(
                new List<UnitPlacement>(), metrics, null, ProcessingTopology.BranchToBranch));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Metrics projection marks sibling seats as copies [Windows]")]
#else
        [Fact(DisplayName = "Metrics projection marks sibling seats as copies [Core]")]
#endif
        public void Build_SiblingSeats_SiblingCopy()
        {
            var placements = new List<UnitPlacement>
            {
                Seat("u0", new[] { 0 }, reused: false, new[] { 0 }, new[] { 1 }),
            };
            var metrics = new Dictionary<string, List<AIMetrics>>
            {
                ["u0"] = new List<AIMetrics> { Metric("m") },
            };

            var result = RunMetricsProjection.Build(
                placements, metrics, null, ProcessingTopology.BranchToBranch);

            Assert.Equal(2, result.Count);
            Assert.Equal(new[] { 0 }, result[0].PathIndices);
            Assert.Equal(new[] { 1 }, result[1].PathIndices);
            Assert.False(result[0].SiblingCopy);
            Assert.True(result[1].SiblingCopy);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Metrics projection grafts item index under ItemGraft [Windows]")]
#else
        [Fact(DisplayName = "Metrics projection grafts item index under ItemGraft [Core]")]
#endif
        public void Build_ItemGraft_AppendsElement()
        {
            var placements = new List<UnitPlacement>
            {
                new UnitPlacement { UnitKey = "u0", InputIndices = new[] { 0 }, ItemIndex = 2, TargetSeats = new List<int[]> { new[] { 0 } } },
            };
            var metrics = new Dictionary<string, List<AIMetrics>>
            {
                ["u0"] = new List<AIMetrics> { Metric("m") },
            };

            var result = RunMetricsProjection.Build(
                placements, metrics, null, ProcessingTopology.ItemGraft);

            var entry = Assert.Single(result);
            Assert.Equal(new[] { 0, 2 }, entry.PathIndices);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Metrics projection keeps unplaced and orphan provenance [Windows]")]
#else
        [Fact(DisplayName = "Metrics projection keeps unplaced and orphan provenance [Core]")]
#endif
        public void Build_UnplacedAndOrphans_Appended()
        {
            var placements = new List<UnitPlacement>
            {
                Seat("u0", new[] { 0 }, reused: false, new[] { 0 }),
            };
            var metrics = new Dictionary<string, List<AIMetrics>>
            {
                ["u0"] = new List<AIMetrics> { Metric("placed") },
                ["unplaced"] = new List<AIMetrics> { Metric("noPlacement") },
            };
            var orphans = new List<(AIMetrics Metrics, int[] PathIndices)>
            {
                (Metric("orphan"), new[] { 5 }),
            };

            var result = RunMetricsProjection.Build(
                placements, metrics, orphans, ProcessingTopology.BranchToBranch);

            Assert.Equal(3, result.Count);
            Assert.Equal("placed", result[0].Metrics.ExecutionId);
            Assert.Equal("noPlacement", result[1].Metrics.ExecutionId);
            Assert.Equal(new[] { 0 }, result[1].PathIndices);
            Assert.Equal("orphan", result[2].Metrics.ExecutionId);
            Assert.Equal(new[] { 5 }, result[2].PathIndices);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "StampCompletionTime stamps fresh, skips reused-only units [Windows]")]
#else
        [Fact(DisplayName = "StampCompletionTime stamps fresh, skips reused-only units [Core]")]
#endif
        public void StampCompletionTime_StampsFresh_SkipsReused()
        {
            var placements = new List<UnitPlacement>
            {
                Seat("freshUnit", new[] { 0 }, reused: false, new[] { 0 }),
                Seat("reusedUnit", new[] { 1 }, reused: true, new[] { 1 }),
            };
            var metrics = new Dictionary<string, List<AIMetrics>>
            {
                ["freshUnit"] = new List<AIMetrics> { Metric("f") },
                ["reusedUnit"] = new List<AIMetrics> { new AIMetrics { ExecutionId = "r", CompletionTime = 3.0 } },
            };
            var orphans = new List<(AIMetrics Metrics, int[] PathIndices)>
            {
                (Metric("orphan"), null),
            };

            RunMetricsProjection.StampCompletionTime(placements, metrics, orphans, 42.5);

            Assert.Equal(42.5, metrics["freshUnit"][0].CompletionTime);
            Assert.Equal(3.0, metrics["reusedUnit"][0].CompletionTime);
            Assert.Equal(42.5, orphans[0].Metrics.CompletionTime);
        }

        [Fact(DisplayName = "SentinelResolution: pending map and fragment both required for resolvability")]
        public void CanResolve_RequiresMapAndPendingFragment()
        {
            // The import pipeline gates its direct path on this probe: a matched
            // sentinel id is only finalizable in place when the live customId →
            // unitKey map AND the unit's pending fragment both exist. A sentinel
            // id surviving on a completed component is stale bookkeeping — the
            // pending fragment is not persisted once the run committed.
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            var resolved = new HashSet<string>(StringComparer.Ordinal);
            var pendingUnits = new HashSet<string>(StringComparer.Ordinal);

            bool CanResolve(string id) => SentinelResolution.CanResolve(id, map, resolved, pendingUnits.Contains);

            Assert.False(CanResolve(null));
            Assert.False(CanResolve("sh-x"));

            // Mapped but no pending fragment — the restored-completed state that
            // previously demoted the component to NeedsRun and resubmitted.
            map["sh-x"] = "u0";
            Assert.False(CanResolve("sh-x"));

            // Pending fragment without a map entry — id never queued this run.
            pendingUnits.Add("u1");
            Assert.False(CanResolve("sh-x"));

            // Live run: mapped and pending.
            pendingUnits.Add("u0");
            Assert.True(CanResolve("sh-x"));
            Assert.False(CanResolve("sh-other"));
        }

        [Fact(DisplayName = "SentinelResolution: already-resolved is not freshly resolvable")]
        public void CanResolve_AlreadyResolved_NotResolvable()
        {
            // Re-resolution is a dedup no-op that keeps the first result, so an
            // import targeting an already-resolved id cannot place its data in
            // place — it must take the collect-only path which rebuilds a fresh
            // mapping. This keeps same-session re-imports consistent with the
            // post-restart path instead of reporting success while ignoring
            // the new data.
            var map = new Dictionary<string, string>(StringComparer.Ordinal) { ["sh-x"] = "u0" };
            var resolved = new HashSet<string>(StringComparer.Ordinal) { "sh-x" };
            var pendingUnits = new HashSet<string>(StringComparer.Ordinal) { "u0" };

            Assert.False(SentinelResolution.CanResolve("sh-x", map, resolved, pendingUnits.Contains));
            Assert.False(SentinelResolution.CanResolve("sh-y", map, resolved, pendingUnits.Contains));
        }
    }
}
