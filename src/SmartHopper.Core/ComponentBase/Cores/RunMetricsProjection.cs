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
using SmartHopper.Core.DataTree;
using SmartHopper.ProviderSdk.AICall.Metrics;

namespace SmartHopper.Core.ComponentBase.Cores
{
    /// <summary>
    /// Where a scheduled processing unit's outputs belong in the current run's
    /// output layout. Pure-data model — paths are raw index sequences so layout,
    /// ordering, and persistence logic stay decoupled from Grasshopper types;
    /// GH_Path is only constructed at the emission boundary.
    /// </summary>
    public sealed class UnitPlacement
    {
        /// <summary>Gets or sets the owning unit key.</summary>
        public string UnitKey { get; set; }

        /// <summary>Gets or sets the unit's input branch path as raw indices.</summary>
        public int[] InputIndices { get; set; }

        /// <summary>Gets or sets the item index within the branch, or null for branch-level units.</summary>
        public int? ItemIndex { get; set; }

        /// <summary>Gets or sets the output target seats (primary plus grouped siblings), each as raw path indices.</summary>
        public List<int[]> TargetSeats { get; set; } = new List<int[]>();

        /// <summary>
        /// Gets or sets a value indicating whether this occurrence was served from
        /// the result store. Reuse is per-occurrence: shared-identical units emit
        /// the same fragment at several seats, only the executor's seat is fresh.
        /// </summary>
        public bool Reused { get; set; }
    }

    /// <summary>
    /// A single metrics emission derived from a <see cref="UnitPlacement"/> and the
    /// unit's recorded provenance, produced by
    /// <see cref="RunMetricsProjection.Build"/> in output order.
    /// </summary>
    public sealed class MetricsPlacement
    {
        /// <summary>Gets or sets the output branch path the metrics entry belongs to, as raw indices.</summary>
        public int[] PathIndices { get; set; }

        /// <summary>Gets or sets the stored provenance entry.</summary>
        public AIMetrics Metrics { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the unit occurrence was served
        /// from the store — emitted entries carry <c>reused: true</c> and zeroed
        /// data/iterations counts.
        /// </summary>
        public bool Reused { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the entry targets a grouped
        /// sibling seat rather than the placement's primary target — emitted
        /// entries carry <c>data_count: 0</c>.
        /// </summary>
        public bool SiblingCopy { get; set; }
    }

    /// <summary>
    /// Pure projection from run placements + recorded provenance to the ordered
    /// metrics emission. Extracted from <see cref="UnitResultStore"/> so the
    /// ordering/reuse/stamping policy is testable without Grasshopper runtime
    /// types: the projection never constructs GH_Path — emission layers map
    /// <see cref="MetricsPlacement.PathIndices"/> to real paths.
    /// </summary>
    public static class RunMetricsProjection
    {
        /// <summary>
        /// Rebuilds the metrics output layout from recorded placements, in output
        /// order. This is the canonical counterpart of the output-tree rebuild for
        /// the Metrics channel: provenance is emitted at the same paths the unit's
        /// outputs occupy, so mixed reused/fresh runs — where completion order
        /// diverges from output order (e.g. reused units replay during processing
        /// while fresh batch metrics only arrive at poll completion) — still
        /// produce a metrics layout aligned with the result tree.
        /// </summary>
        /// <param name="placements">Run placements in scheduling order.</param>
        /// <param name="unitMetrics">Recorded provenance keyed by unit key.</param>
        /// <param name="orphans">Provenance recorded without a unit context.</param>
        /// <param name="topology">The component's processing topology.</param>
        /// <returns>
        /// Ordered <see cref="MetricsPlacement"/> entries, or null when no
        /// placements were recorded (e.g. a legacy pending payload restored
        /// without layout data) — callers should keep their append-order
        /// fallback in that case.
        /// </returns>
        public static List<MetricsPlacement> Build(
            IReadOnlyList<UnitPlacement> placements,
            IReadOnlyDictionary<string, List<AIMetrics>> unitMetrics,
            IReadOnlyList<(AIMetrics Metrics, int[] PathIndices)> orphans,
            ProcessingTopology topology)
        {
            if (placements == null || placements.Count == 0)
            {
                return null;
            }

            var result = new List<MetricsPlacement>();
            var placed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var placement in placements)
            {
                if (placement?.UnitKey == null)
                {
                    continue;
                }

                placed.Add(placement.UnitKey);
                if (unitMetrics == null
                    || !unitMetrics.TryGetValue(placement.UnitKey, out var metrics)
                    || metrics == null)
                {
                    continue;
                }

                var targets = placement.TargetSeats != null && placement.TargetSeats.Count > 0
                    ? placement.TargetSeats
                    : new List<int[]> { placement.InputIndices };

                var first = true;
                foreach (var seat in targets)
                {
                    if (seat == null)
                    {
                        continue;
                    }

                    var outIndices = topology == ProcessingTopology.ItemGraft && placement.ItemIndex.HasValue
                        ? WithAppendedElement(seat, placement.ItemIndex.Value)
                        : seat;

                    foreach (var m in metrics)
                    {
                        result.Add(new MetricsPlacement
                        {
                            PathIndices = outIndices,
                            Metrics = m,
                            Reused = placement.Reused,
                            SiblingCopy = !first,
                        });
                    }

                    first = false;
                }
            }

            // Units that recorded provenance but were never placed keep their
            // metrics visible rather than dropping silently — appended at {0}.
            if (unitMetrics != null)
            {
                foreach (var kvp in unitMetrics)
                {
                    if (placed.Contains(kvp.Key) || kvp.Value == null)
                    {
                        continue;
                    }

                    foreach (var m in kvp.Value)
                    {
                        result.Add(new MetricsPlacement
                        {
                            PathIndices = new[] { 0 },
                            Metrics = m,
                            Reused = false,
                            SiblingCopy = false,
                        });
                    }
                }
            }

            // Provenance recorded without a unit context (helper/fallback calls)
            // still surfaces — appended after placed entries at its own path.
            if (orphans != null)
            {
                foreach (var orphan in orphans)
                {
                    if (orphan.Metrics == null)
                    {
                        continue;
                    }

                    result.Add(new MetricsPlacement
                    {
                        PathIndices = orphan.PathIndices ?? new[] { 0 },
                        Metrics = orphan.Metrics,
                        Reused = false,
                        SiblingCopy = false,
                    });
                }
            }

            return result;
        }

        /// <summary>
        /// Stamps the batch run's elapsed completion time into recorded provenance,
        /// in place. Units with at least one non-reused placement get stamped —
        /// including entries shared with reused seats (the underlying call did
        /// execute). Units placed only as reused keep their original duration, and
        /// orphan entries are always stamped (they belong to this run by
        /// definition).
        /// </summary>
        /// <param name="placements">Run placements in scheduling order.</param>
        /// <param name="unitMetrics">Recorded provenance keyed by unit key (mutated).</param>
        /// <param name="orphans">Orphan entries (mutated).</param>
        /// <param name="completionTime">Elapsed seconds of the completed batch run.</param>
        public static void StampCompletionTime(
            IReadOnlyList<UnitPlacement> placements,
            IDictionary<string, List<AIMetrics>> unitMetrics,
            IList<(AIMetrics Metrics, int[] PathIndices)> orphans,
            double completionTime)
        {
            var freshByKey = new Dictionary<string, bool>(StringComparer.Ordinal);
            if (placements != null)
            {
                foreach (var placement in placements)
                {
                    if (placement?.UnitKey == null)
                    {
                        continue;
                    }

                    freshByKey[placement.UnitKey] = freshByKey.TryGetValue(placement.UnitKey, out var fresh)
                        ? fresh || !placement.Reused
                        : !placement.Reused;
                }
            }

            if (unitMetrics != null)
            {
                foreach (var kvp in unitMetrics)
                {
                    // Units never placed are fresh by definition (reused units always
                    // carry a placement); units with at least one fresh seat get
                    // stamped; units placed only as reused keep original durations.
                    if (kvp.Value == null
                        || (freshByKey.TryGetValue(kvp.Key, out var fresh) && !fresh))
                    {
                        continue;
                    }

                    for (int i = 0; i < kvp.Value.Count; i++)
                    {
                        kvp.Value[i] = kvp.Value[i] with { CompletionTime = completionTime };
                    }
                }
            }

            if (orphans != null)
            {
                for (int i = 0; i < orphans.Count; i++)
                {
                    var orphan = orphans[i];
                    orphans[i] = (orphan.Metrics with { CompletionTime = completionTime }, orphan.PathIndices);
                }
            }
        }

        private static int[] WithAppendedElement(int[] indices, int element)
        {
            var combined = new int[(indices?.Length ?? 0) + 1];
            if (indices != null)
            {
                Array.Copy(indices, combined, indices.Length);
            }

            combined[combined.Length - 1] = element;
            return combined;
        }
    }
}
