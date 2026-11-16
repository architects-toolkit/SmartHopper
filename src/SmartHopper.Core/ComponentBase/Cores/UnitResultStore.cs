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
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.ComponentBase.Batch;
using SmartHopper.Core.DataTree;
using SmartHopper.Core.IO;
using SmartHopper.ProviderSdk.AICall.Metrics;
using SmartHopper.ProviderSdk.Logging;

namespace SmartHopper.Core.ComponentBase.Cores
{
    /// <summary>
    /// Per-component store that maps processing-unit keys to produced output fragments,
    /// enabling incremental reuse across runs and persistence of the latest snapshot.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A unit key is derived from the semantic fingerprint of the unit's effective
    /// inputs plus the component's reuse context (component type, provider, model and
    /// execution settings). Two scopes exist:
    /// </para>
    /// <list type="bullet">
    ///   <item><b>Committed</b>: results from the last fully completed run. Persisted
    ///   with the document; restored on load.</item>
    ///   <item><b>Run</b>: fragments produced (or copied) during the in-flight run.
    ///   At commit time only fragments without unresolved batch sentinels are kept —
    ///   failed or unresolved units are dropped so they re-execute on the next run.</item>
    /// </list>
    /// <para>
    /// Identical-unit sharing: when enabled, every occurrence of the same unit key maps
    /// to a single fragment (one execution, N placements). When disabled, occurrences
    /// are disambiguated by a deterministic slot index appended to the key.
    /// </para>
    /// </remarks>
    public sealed class UnitResultStore
    {
        private const int FormatVersion = 2;

        /// <summary>
        /// Fragments produced during the in-flight run, keyed by unit key.
        /// Values map output parameter name → produced items.
        /// </summary>
        private readonly Dictionary<string, Dictionary<string, List<IGH_Goo>>> _run =
            new Dictionary<string, Dictionary<string, List<IGH_Goo>>>(StringComparer.Ordinal);

        /// <summary>
        /// Committed fragments from the last fully completed run.
        /// </summary>
        private Dictionary<string, Dictionary<string, List<IGH_Goo>>> _committed =
            new Dictionary<string, Dictionary<string, List<IGH_Goo>>>(StringComparer.Ordinal);

        /// <summary>
        /// Execution provenance recorded during the in-flight run, keyed by unit
        /// key. A unit may hold several metrics entries (multi-call units keep
        /// one entry per provider/tool call, in call order).
        /// </summary>
        private readonly Dictionary<string, List<AIMetrics>> _runMetrics =
            new Dictionary<string, List<AIMetrics>>(StringComparer.Ordinal);

        /// <summary>
        /// Committed provenance for the last fully completed run. Travels with
        /// the fragment so a cache hit replays outputs AND their original
        /// metrics — the Metrics output always describes the complete current
        /// output snapshot, not only the latest run's calls.
        /// </summary>
        private Dictionary<string, List<AIMetrics>> _committedMetrics =
            new Dictionary<string, List<AIMetrics>>(StringComparer.Ordinal);

        /// <summary>
        /// Output placements of units scheduled during the in-flight run, in
        /// scheduling order. Persisted alongside pending fragments so a
        /// save/reopen while a batch is in flight can still rebuild every
        /// output channel from decoded fragments.
        /// </summary>
        private readonly List<UnitPlacement> _runPlacements = new List<UnitPlacement>();

        /// <summary>
        /// Batch customId → unit key, populated while a run is queueing batch calls.
        /// Used to resolve decoded sentinel fragments at batch completion.
        /// </summary>
        private readonly Dictionary<string, string> _customIdToUnitKey =
            new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// Provenance recorded without a unit context (e.g. fallback/passthrough
        /// helper calls outside unit processing). Emitted after placed metrics so
        /// unassociated work still surfaces in the Metrics output.
        /// </summary>
        private readonly List<(AIMetrics Metrics, int[] PathIndices)> _runOrphanMetrics =
            new List<(AIMetrics Metrics, int[] PathIndices)>();

        /// <summary>
        /// Batch customIds whose provenance was already recorded this run. A
        /// sentinel decodes to a complete metric set exactly once — repeated
        /// decode passes (e.g. a component finalizing several outputs through
        /// separate <c>ProcessBatchResults</c> calls) must not duplicate entries.
        /// </summary>
        private readonly HashSet<string> _recordedSentinelMetrics =
            new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// Batch customIds already resolved inside their fragment this run. A
        /// shared result placed at several sentinel seats must resolve exactly
        /// once — later decode passes for the same id are no-ops instead of
        /// re-appending the decoded items as phantom "extra outputs".
        /// </summary>
        private readonly HashSet<string> _resolvedSentinelIds =
            new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// Per-run occurrence counter for identical-input units when sharing is disabled.
        /// </summary>
        private readonly Dictionary<string, int> _occurrenceCounts =
            new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// Serializes all access to the collections below. Workers record
        /// placements/metrics on thread-pool threads while batch finalization
        /// projects the same collections into output trees — unlocked access
        /// produced "Collection was modified" crashes at run completion.
        /// </summary>
        private readonly object _gate = new object();

        private string _contextKey = string.Empty;
        private bool _shareIdentical = true;

        /// <summary>
        /// Gets the number of units scheduled during the current run.
        /// </summary>
        public int RunUnitCount
        {
            get { lock (this._gate) { return this._runUnitCount; } }
            private set { this._runUnitCount = value; }
        }

        private int _runUnitCount;

        /// <summary>
        /// Gets the number of units served from the store during the current run.
        /// </summary>
        public int RunReusedCount
        {
            get { lock (this._gate) { return this._runReusedCount; } }
            private set { this._runReusedCount = value; }
        }

        private int _runReusedCount;

        /// <summary>
        /// Gets a value indicating whether committed fragments exist.
        /// </summary>
        public bool HasCommittedEntries
        {
            get { lock (this._gate) { return this._committed.Count > 0; } }
        }

        /// <summary>
        /// Resets the run-scope store for a new processing cycle.
        /// Does not touch committed entries.
        /// </summary>
        public void BeginRun()
        {
            lock (this._gate)
            {
                this._run.Clear();
                this._runMetrics.Clear();
                this._runPlacements.Clear();
                this._runOrphanMetrics.Clear();
                this._customIdToUnitKey.Clear();
                this._recordedSentinelMetrics.Clear();
                this._resolvedSentinelIds.Clear();
                this._occurrenceCounts.Clear();
                this.RunUnitCount = 0;
                this.RunReusedCount = 0;
            }
        }

        /// <summary>
        /// Sets the reuse context for the current run (component type, provider, model,
        /// execution settings) and the identical-unit sharing policy.
        /// </summary>
        /// <param name="contextKey">Opaque context string; mixed into every unit key.</param>
        /// <param name="shareIdentical">When false, identical units get independent slots.</param>
        public void ConfigureRun(string contextKey, bool shareIdentical)
        {
            lock (this._gate)
            {
                this._contextKey = contextKey ?? string.Empty;
                this._shareIdentical = shareIdentical;
            }
        }

        /// <summary>
        /// Computes the unit key for a set of matched effective inputs.
        /// </summary>
        /// <typeparam name="T">Goo item type.</typeparam>
        /// <param name="inputs">Input name → matched items for this unit.</param>
        /// <returns>The unit key, including a sharing slot suffix when sharing is off.</returns>
        public string ComputeUnitKey<T>(Dictionary<string, List<T>> inputs)
            where T : IGH_Goo
        {
            lock (this._gate)
            {
                this.RunUnitCount++;
                var baseKey = SemanticFingerprint.ComputeShortHash(
                    this._contextKey + "|" + SemanticFingerprint.OfInputs(inputs, strict: true));

                if (this._shareIdentical)
                {
                    return baseKey;
                }

                var slot = this._occurrenceCounts.TryGetValue(baseKey, out var n) ? n : 0;
                this._occurrenceCounts[baseKey] = slot + 1;
                return $"{baseKey}#{slot}";
            }
        }

        /// <summary>
        /// Attempts to retrieve a stored fragment for a unit key. Run entries win over
        /// committed ones; a committed hit is copied into the run store so it survives
        /// the next commit. Callers always receive a clone — the live fragment can be
        /// rewritten in place by <see cref="ResolveSentinel"/> while the caller reads it.
        /// </summary>
        /// <param name="unitKey">The unit key.</param>
        /// <param name="fragment">Output name → items.</param>
        /// <returns>True when a usable fragment was found.</returns>
        public bool TryGetFragment(string unitKey, out Dictionary<string, List<IGH_Goo>> fragment)
            => this.TryGetFragment(unitKey, includeCommitted: true, out fragment);

        /// <summary>
        /// Attempts to retrieve a stored fragment for a unit key, optionally
        /// skipping the committed snapshot. A run that must re-collect live
        /// units — the collect-only import arm — passes
        /// <paramref name="includeCommitted"/> = false so previously committed
        /// results cannot satisfy the recollection, while within-run sharing
        /// between identical units keeps working through the run store.
        /// </summary>
        /// <param name="unitKey">The unit key.</param>
        /// <param name="includeCommitted">Whether the committed snapshot may serve the unit.</param>
        /// <param name="fragment">Output name → items.</param>
        /// <returns>True when a usable fragment was found.</returns>
        public bool TryGetFragment(string unitKey, bool includeCommitted, out Dictionary<string, List<IGH_Goo>> fragment)
        {
            lock (this._gate)
            {
                if (unitKey != null && this._run.TryGetValue(unitKey, out var fromRun))
                {
                    fragment = CloneFragment(fromRun);
                    this.RunReusedCount++;
                    return true;
                }

                if (unitKey != null && includeCommitted && this._committed.TryGetValue(unitKey, out var stored))
                {
                    fragment = CloneFragment(stored);
                    this._run[unitKey] = fragment;
                    if (this._committedMetrics.TryGetValue(unitKey, out var storedMetrics))
                    {
                        this._runMetrics[unitKey] = new List<AIMetrics>(storedMetrics);
                    }

                    this.RunReusedCount++;
                    return true;
                }

                fragment = null;
                return false;
            }
        }

        /// <summary>
        /// Stores a freshly produced fragment for a unit key in the run store.
        /// </summary>
        /// <param name="unitKey">The unit key.</param>
        /// <param name="fragment">Output name → items; may be null.</param>
        public void StoreFragment(string unitKey, Dictionary<string, List<IGH_Goo>> fragment)
        {
            if (unitKey == null)
            {
                return;
            }

            lock (this._gate)
            {
                this._run[unitKey] = fragment ?? new Dictionary<string, List<IGH_Goo>>(StringComparer.Ordinal);
            }
        }

        /// <summary>
        /// Evicts an entry (e.g. when a stored fragment no longer deserializes to the
        /// expected output types).
        /// </summary>
        /// <param name="unitKey">The unit key to drop.</param>
        public void Evict(string unitKey)
        {
            if (unitKey == null)
            {
                return;
            }

            lock (this._gate)
            {
                this._run.Remove(unitKey);
                this._runMetrics.Remove(unitKey);
                this._committed.Remove(unitKey);
                this._committedMetrics.Remove(unitKey);
            }
        }

        /// <summary>
        /// Appends an execution-provenance entry to a unit. Call once per provider
        /// or helper call that contributed to the unit's outputs, in call order.
        /// </summary>
        /// <param name="unitKey">The owning unit key; null keys record an orphan
        /// entry emitted after all placed metrics (see <paramref name="orphanPath"/>).</param>
        /// <param name="metrics">The call's metrics; null entries are ignored.</param>
        /// <param name="orphanIndices">Optional target path indices for orphan entries
        /// recorded without a unit context; defaults to <c>{0}</c> at emit time.</param>
        public void RecordMetrics(string unitKey, AIMetrics metrics, int[] orphanIndices = null)
        {
            if (metrics == null)
            {
                return;
            }

            lock (this._gate)
            {
                if (unitKey == null)
                {
                    this._runOrphanMetrics.Add((metrics, orphanIndices));
                    return;
                }

                if (!this._runMetrics.TryGetValue(unitKey, out var list))
                {
                    list = new List<AIMetrics>();
                    this._runMetrics[unitKey] = list;
                }

                list.Add(metrics);
            }
        }

        /// <summary>
        /// Records the complete provenance set decoded for a batch sentinel by
        /// resolving its customId to the owning unit key. A sentinel records its
        /// metrics exactly once per run — repeated decode passes for the same
        /// customId are no-ops. No-op for unknown ids.
        /// </summary>
        /// <param name="customId">Batch custom identifier.</param>
        /// <param name="metrics">The decoded call's metrics.</param>
        public void RecordMetricsForSentinel(string customId, IReadOnlyList<AIMetrics> metrics)
        {
            lock (this._gate)
            {
                if (string.IsNullOrEmpty(customId)
                    || !this._customIdToUnitKey.TryGetValue(customId, out var unitKey)
                    || !this._recordedSentinelMetrics.Add(customId))
                {
                    return;
                }

                if (metrics == null)
                {
                    return;
                }

                foreach (var entry in metrics)
                {
                    this.RecordMetrics(unitKey, entry);
                }
            }
        }

        /// <summary>
        /// Stamps the batch run's elapsed completion time into stored provenance.
        /// Units with at least one non-reused placement get stamped — including
        /// entries shared with reused seats (the underlying call did execute).
        /// Units placed only as reused keep their original duration, and orphan
        /// entries are always stamped (they belong to this run by definition).
        /// </summary>
        /// <param name="completionTime">Elapsed seconds of the completed batch run.</param>
        public void StampRunCompletionTime(double completionTime)
        {
            lock (this._gate)
            {
                RunMetricsProjection.StampCompletionTime(
                    this._runPlacements, this._runMetrics, this._runOrphanMetrics, completionTime);
            }
        }

        /// <summary>
        /// Returns the provenance recorded for a unit — its own fresh metrics,
        /// or the committed provenance replayed for a cache hit.
        /// </summary>
        /// <param name="unitKey">The unit key.</param>
        /// <param name="metrics">Provenance entries in call order.</param>
        /// <returns>True when provenance exists.</returns>
        public bool TryGetMetrics(string unitKey, out IReadOnlyList<AIMetrics> metrics)
        {
            lock (this._gate)
            {
                if (unitKey != null
                    && (this._runMetrics.TryGetValue(unitKey, out var run)
                        || this._committedMetrics.TryGetValue(unitKey, out run))
                    && run != null)
                {
                    metrics = new List<AIMetrics>(run);
                    return metrics.Count > 0;
                }

                metrics = null;
                return false;
            }
        }

        /// <summary>
        /// Records where a scheduled unit's outputs belong in the current run's
        /// output layout. Call once per scheduled unit after it completes.
        /// </summary>
        /// <param name="unitKey">The owning unit key; null keys are ignored.</param>
        /// <param name="inputPath">The unit's input branch path.</param>
        /// <param name="itemIndex">Item index within the branch, or null for branch-level units.</param>
        /// <param name="targetPaths">Output target paths (primary plus grouped siblings).</param>
        /// <param name="reused">Whether this occurrence was served from the result store.</param>
        public void RecordPlacement(string unitKey, GH_Path inputPath, int? itemIndex, List<GH_Path> targetPaths, bool reused = false)
            => this.RecordPlacementSeats(
                unitKey,
                inputPath?.Indices,
                itemIndex,
                targetPaths?.Select(t => t?.Indices).ToList(),
                reused);

        /// <summary>
        /// Records where a scheduled unit's outputs belong in the current run's
        /// output layout, expressed as raw path indices. This is the storage-level
        /// counterpart of <see cref="RecordPlacement"/> — placements are pure data
        /// (path index sequences) so layout, ordering, and persistence logic stay
        /// decoupled from Grasshopper types; GH_Path is only constructed at the
        /// emission boundary (<see cref="BuildRunTrees"/>).
        /// </summary>
        /// <param name="unitKey">The owning unit key; null keys are ignored.</param>
        /// <param name="inputIndices">The unit's input branch path indices.</param>
        /// <param name="itemIndex">Item index within the branch, or null for branch-level units.</param>
        /// <param name="targetSeats">Output target path indices (primary plus grouped siblings).</param>
        /// <param name="reused">Whether this occurrence was served from the result store.</param>
        public void RecordPlacementSeats(string unitKey, int[] inputIndices, int? itemIndex, List<int[]> targetSeats, bool reused = false)
        {
            if (unitKey == null)
            {
                return;
            }

            lock (this._gate)
            {
                this._runPlacements.Add(new UnitPlacement
                {
                    UnitKey = unitKey,
                    InputIndices = inputIndices,
                    ItemIndex = itemIndex,
                    TargetSeats = targetSeats != null ? new List<int[]>(targetSeats) : new List<int[]>(),
                    Reused = reused,
                });
            }
        }

        /// <summary>
        /// Gets a snapshot of the placements recorded for the in-flight run, in
        /// scheduling order. Snapshot so callers can enumerate without holding
        /// <see cref="_gate"/> while workers keep recording.
        /// </summary>
        public IReadOnlyList<UnitPlacement> RunPlacements
        {
            get { lock (this._gate) { return this._runPlacements.ToArray(); } }
        }

        /// <summary>
        /// Rebuilds every output channel's tree from stored run fragments and
        /// recorded placements. This is the canonical way to assemble mixed
        /// reused/new results after batch completion: fragments hold typed,
        /// heterogeneous channel items and placements hold their target paths,
        /// so every channel — not only the sentinel-bearing primary — is
        /// reconstructed for both executed and cached units.
        /// </summary>
        /// <param name="topology">The component's processing topology.</param>
        /// <returns>
        /// Output name → reconstructed tree, or null when no placements were
        /// recorded (e.g. a legacy pending payload restored without layout data).
        /// </returns>
        public Dictionary<string, GH_Structure<IGH_Goo>> BuildRunTrees(ProcessingTopology topology)
        {
            lock (this._gate)
            {
                if (this._runPlacements.Count == 0)
                {
                    return null;
                }

                var result = new Dictionary<string, GH_Structure<IGH_Goo>>(StringComparer.Ordinal);
                foreach (var placement in this._runPlacements)
                {
                    if (placement?.UnitKey == null
                        || !this._run.TryGetValue(placement.UnitKey, out var fragment)
                        || fragment == null)
                    {
                        continue;
                    }

                    var targets = placement.TargetSeats != null && placement.TargetSeats.Count > 0
                        ? placement.TargetSeats
                        : new List<int[]> { placement.InputIndices };

                    foreach (var seat in targets)
                    {
                        if (seat == null)
                        {
                            continue;
                        }

                        var basePath = new GH_Path(seat);
                        var outPath = topology == ProcessingTopology.ItemGraft && placement.ItemIndex.HasValue
                            ? basePath.AppendElement(placement.ItemIndex.Value)
                            : basePath;

                        foreach (var channel in fragment)
                        {
                            if (!result.TryGetValue(channel.Key, out var tree))
                            {
                                tree = new GH_Structure<IGH_Goo>();
                                result[channel.Key] = tree;
                            }

                            if (channel.Value != null)
                            {
                                tree.AppendRange(channel.Value, outPath);
                            }
                            else if (!tree.PathExists(outPath))
                            {
                                tree.EnsurePath(outPath);
                            }
                        }
                    }
                }

                return result;
            }
        }

        /// <summary>
        /// Rebuilds the metrics output layout from recorded placements, in output
        /// order. This is the canonical counterpart of <see cref="BuildRunTrees"/>
        /// for the Metrics channel: provenance is emitted at the same paths the
        /// unit's outputs occupy, so mixed reused/fresh runs — where completion
        /// order diverges from output order (e.g. reused units replay during
        /// processing while fresh batch metrics only arrive at poll completion) —
        /// still produce a metrics tree aligned with the result tree.
        /// </summary>
        /// <param name="topology">The component's processing topology.</param>
        /// <returns>
        /// Ordered <see cref="MetricsPlacement"/> entries, or null when no
        /// placements were recorded (e.g. a legacy pending payload restored
        /// without layout data) — callers should keep their append-order
        /// fallback in that case.
        /// </returns>
        public List<MetricsPlacement> BuildRunMetricsPlacements(ProcessingTopology topology)
        {
            lock (this._gate)
            {
                return RunMetricsProjection.Build(this._runPlacements, this._runMetrics, this._runOrphanMetrics, topology);
            }
        }

        /// <summary>
        /// Associates a batch customId with the unit key that queued it, so decoded
        /// results can replace the sentinel inside the stored fragment.
        /// </summary>
        /// <param name="customId">Batch custom identifier.</param>
        /// <param name="unitKey">Owning unit key.</param>
        public void RegisterSentinel(string customId, string unitKey)
        {
            if (!string.IsNullOrEmpty(customId) && !string.IsNullOrEmpty(unitKey))
            {
                lock (this._gate)
                {
                    this._customIdToUnitKey[customId] = unitKey;
                }
            }
        }

        /// <summary>
        /// Replaces the sentinel items produced by a queued batch call with their
        /// decoded results inside the owning unit's stored fragment.
        /// </summary>
        /// <param name="customId">Batch custom identifier of the completed call.</param>
        /// <param name="decoded">Decoded outputs: parameter name → items for this call.</param>
        /// <returns><c>true</c> when the sentinel resolved inside its fragment; <c>false</c> when no fragment could receive it.</returns>
        public bool ResolveSentinel(string customId, IDictionary<string, List<IGH_Goo>> decoded)
        {
            lock (this._gate)
            {
                if (string.IsNullOrEmpty(customId)
                    || !this._customIdToUnitKey.TryGetValue(customId, out var unitKey)
                    || !this._run.TryGetValue(unitKey, out var fragment)
                    || decoded == null)
                {
                    return false;
                }

            // Decode-once: a shared result reaches several sentinel seats; the
            // fragment itself may only be rewritten for the first pass — repeats
            // would append the decoded items again as phantom extra outputs.
            if (!this._resolvedSentinelIds.Add(customId))
            {
                return true;
            }

            foreach (var output in decoded)
            {
                if (!fragment.TryGetValue(output.Key, out var items))
                {
                    fragment[output.Key] = new List<IGH_Goo>(output.Value ?? Enumerable.Empty<IGH_Goo>());
                    continue;
                }

                var sentinelIndex = items.FindIndex(item =>
                    item is GH_String s && BatchSentinel.TryExtract(s.Value, out var id) && id == customId);
                if (sentinelIndex < 0)
                {
                    // Sentinel not in this output — the call produced extra outputs; append them.
                    if (output.Value != null)
                    {
                        items.AddRange(output.Value);
                    }

                    continue;
                }

                var replacements = output.Value ?? new List<IGH_Goo>();
                if (replacements.Count == 0)
                {
                    items.RemoveAt(sentinelIndex);
                }
                else
                {
                    items[sentinelIndex] = replacements[0];
                    for (int i = 1; i < replacements.Count; i++)
                    {
                        items.Insert(sentinelIndex + i, replacements[i]);
                    }
                }
            }

            // Resolution contract: after a customId resolves, no sentinel bearing
            // it may remain in the fragment. Some producers store placeholder
            // sentinels in channels the decode pass does not cover — without this
            // sweep the fragment is dropped at commit as unresolved work, which
            // silently disables cross-run result reuse for that component.
            var swept = 0;
            foreach (var channel in fragment)
            {
                if (channel.Value == null)
                {
                    continue;
                }

                swept += channel.Value.RemoveAll(item =>
                    item is GH_String s && BatchSentinel.TryExtract(s.Value, out var id) && id == customId);
            }

            if (swept > 0)
            {
                Logger.For<UnitResultStore>().Warning(
                    $"[UnitResultStore] ResolveSentinel('{customId}'): removed {swept} unresolvable placeholder sentinel item(s) — producers must not store sentinels in channels the decode pass does not cover");
            }

                return true;
            }
        }

        /// <summary>
        /// Returns true when <see cref="ResolveSentinel"/> already resolved the
        /// given customId inside its fragment this run. Completion accounting
        /// uses this to count resolved expected work instead of delivered
        /// response bodies — a delivered-but-undecodable item stays unresolved.
        /// </summary>
        /// <param name="customId">Batch custom identifier.</param>
        public bool IsSentinelResolved(string customId)
        {
            lock (this._gate)
            {
                return customId != null && this._resolvedSentinelIds.Contains(customId);
            }
        }

        /// <summary>
        /// Returns true when <see cref="ResolveSentinel"/> could write a decoded
        /// result for the given customId into a live fragment right now: the
        /// live customId → unitKey map and the unit's pending fragment both
        /// exist and the id has not resolved yet. Already-resolved ids report
        /// false — re-resolution is a dedup no-op — and the map plus pending
        /// fragments are only persisted while a submission is in flight, so a
        /// sentinel id surviving on a completed component is stale
        /// bookkeeping. Callers must probe this before trusting an
        /// expected-id match as finalizable in place.
        /// </summary>
        /// <param name="customId">Batch custom identifier.</param>
        public bool CanResolveSentinel(string customId)
        {
            lock (this._gate)
            {
                return SentinelResolution.CanResolve(
                    customId,
                    this._customIdToUnitKey,
                    this._resolvedSentinelIds,
                    unitKey => this._run.ContainsKey(unitKey));
            }
        }

        /// <summary>
        /// Resolves a persisted sentinel against the committed snapshot:
        /// customId → unit key → committed fragment → items for
        /// <paramref name="outputName"/>. Used when a restored output tree still
        /// carries sentinels whose owning run can no longer resume (e.g. a
        /// mid-flight save of non-batch work) — unchanged inputs then republish
        /// the last committed results instead of an empty branch. Committed
        /// fragments are guaranteed sentinel-free (<see cref="CommitRun"/>
        /// drops unresolved work), so returned items never leak placeholders.
        /// The returned list is a copy and may be inserted into output trees.
        /// </summary>
        /// <param name="customId">Custom identifier carried by the sentinel.</param>
        /// <param name="outputName">Output parameter the sentinel occupies.</param>
        /// <param name="items">Committed items for that unit and output.</param>
        /// <returns><c>true</c> when committed items exist for the sentinel's unit and output.</returns>
        public bool TryGetCommittedForSentinel(string customId, string outputName, out List<IGH_Goo> items)
        {
            lock (this._gate)
            {
                items = null;
                if (string.IsNullOrEmpty(customId)
                    || string.IsNullOrEmpty(outputName)
                    || !this._customIdToUnitKey.TryGetValue(customId, out var unitKey)
                    || !this._committed.TryGetValue(unitKey, out var fragment)
                    || !fragment.TryGetValue(outputName, out var stored))
                {
                    return false;
                }

                items = new List<IGH_Goo>(stored);
                return true;
            }
        }

        /// <summary>
        /// Promotes resolved run entries to the committed snapshot. Fragments still
        /// containing batch sentinels are dropped — unresolved work re-executes next run.
        /// </summary>
        public void CommitRun()
        {
            lock (this._gate)
            {
                var next = new Dictionary<string, Dictionary<string, List<IGH_Goo>>>(StringComparer.Ordinal);
                var nextMetrics = new Dictionary<string, List<AIMetrics>>(StringComparer.Ordinal);
                foreach (var kvp in this._run)
                {
                    if (!FragmentHasSentinel(kvp.Value))
                    {
                        next[kvp.Key] = kvp.Value;
                        if (this._runMetrics.TryGetValue(kvp.Key, out var metrics))
                        {
                            nextMetrics[kvp.Key] = metrics;
                        }
                    }
                }

                this._committed = next;
                this._committedMetrics = nextMetrics;
            }
        }

        /// <summary>Clears run and committed entries plus all associations.</summary>
        public void ClearAll()
        {
            lock (this._gate)
            {
                this.BeginRun();
                this._committed.Clear();
                this._committedMetrics.Clear();
            }
        }

        /// <summary>
        /// Serializes committed entries and their provenance as a versioned JSON
        /// object, or null when empty.
        /// </summary>
        public JObject SerializeCommitted()
        {
            lock (this._gate)
            {
                return SerializeEntries(this._committed, this._committedMetrics, null);
            }
        }

        /// <summary>
        /// Serializes run entries (which may contain unresolved batch sentinels),
        /// their provenance, their placements, and orphan helper metrics for
        /// in-flight batch persistence.
        /// </summary>
        public JObject SerializePending()
        {
            lock (this._gate)
            {
                return SerializeEntries(this._run, this._runMetrics, this._runPlacements, this._runOrphanMetrics);
            }
        }

        /// <summary>
        /// Serializes the customId → unitKey map for in-flight batch persistence.
        /// </summary>
        public JObject SerializeCustomIdMap()
        {
            lock (this._gate)
            {
                if (this._customIdToUnitKey.Count == 0)
                {
                    return null;
                }

                var obj = new JObject();
                foreach (var kvp in this._customIdToUnitKey)
                {
                    obj[kvp.Key] = kvp.Value;
                }

                return obj;
            }
        }

        /// <summary>
        /// Restores committed entries from a previously serialized snapshot.
        /// Malformed payloads degrade to an empty store.
        /// </summary>
        public void RestoreCommitted(JObject payload)
        {
            var metrics = new Dictionary<string, List<AIMetrics>>(StringComparer.Ordinal);
            var entries = DeserializeEntries(payload, metrics, null);
            if (entries != null)
            {
                lock (this._gate)
                {
                    this._committed = entries;
                    this._committedMetrics = metrics;
                }
            }
        }

        /// <summary>
        /// Restores pending (in-flight batch) run entries, provenance, and placements.
        /// </summary>
        public void RestorePending(JObject payload)
        {
            var metrics = new Dictionary<string, List<AIMetrics>>(StringComparer.Ordinal);
            var placements = new List<UnitPlacement>();
            var entries = DeserializeEntries(payload, metrics, placements);
            if (entries == null)
            {
                return;
            }

            lock (this._gate)
            {
                foreach (var kvp in entries)
                {
                    this._run[kvp.Key] = kvp.Value;
                }

                foreach (var kvp in metrics)
                {
                    this._runMetrics[kvp.Key] = kvp.Value;
                }

                this._runPlacements.AddRange(placements);

                // Orphan helper metrics (e.g. media preparation charged before
                // submission) travel with the pending snapshot so a resumed batch
                // keeps the complete provenance picture after save/reopen.
                if (payload["om"] is JArray orphans)
                {
                    foreach (var token in orphans)
                    {
                        if (token is not JObject obj)
                        {
                            continue;
                        }

                        var m = AIMetricsJson.FromJson(obj["m"]);
                        var indices = obj["p"] is JArray arr
                            ? arr.Values<int>().ToArray()
                            : Array.Empty<int>();
                        if (m != null)
                        {
                            this._runOrphanMetrics.Add((m, indices));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Restores the customId → unitKey map for a resumed batch.
        /// </summary>
        public void RestoreCustomIdMap(JObject payload)
        {
            if (payload == null)
            {
                return;
            }

            lock (this._gate)
            {
                foreach (var prop in payload.Properties())
                {
                    this._customIdToUnitKey[prop.Name] = prop.Value?.ToString();
                }
            }
        }

        private static JObject SerializeEntries(
            Dictionary<string, Dictionary<string, List<IGH_Goo>>> entries,
            Dictionary<string, List<AIMetrics>> metrics,
            List<UnitPlacement> placements,
            List<(AIMetrics Metrics, int[] PathIndices)> orphanMetrics = null)
        {
            if (entries == null || entries.Count == 0)
            {
                return null;
            }

            var units = new JObject();
            foreach (var unit in entries)
            {
                var outputs = new JObject();
                foreach (var output in unit.Value)
                {
                    var encoded = new JArray();
                    if (output.Value != null)
                    {
                        foreach (var item in output.Value)
                        {
                            try
                            {
                                encoded.Add(item == null ? JValue.CreateNull() : SafeGooCodec.Encode(item));
                            }
                            catch (Exception ex)
                            {
                                Logger.For<UnitResultStore>().Warning($"[UnitResultStore] Skipping unencodable item in output '{output.Key}': {ex.Message}");
                            }
                        }
                    }

                    outputs[output.Key] = encoded;
                }

                units[unit.Key] = outputs;
            }

            var result = new JObject { ["v"] = FormatVersion, ["e"] = units };

            if (metrics != null && metrics.Count > 0)
            {
                var provenance = new JObject();
                foreach (var unit in metrics)
                {
                    if (unit.Value == null || unit.Value.Count == 0)
                    {
                        continue;
                    }

                    var arr = new JArray();
                    foreach (var m in unit.Value)
                    {
                        var encoded = AIMetricsJson.ToJson(m);
                        if (encoded != null)
                        {
                            arr.Add(encoded);
                        }
                    }

                    if (arr.Count > 0)
                    {
                        provenance[unit.Key] = arr;
                    }
                }

                if (provenance.Count > 0)
                {
                    result["p"] = provenance;
                }
            }

            if (placements != null && placements.Count > 0)
            {
                var arr = new JArray();
                foreach (var placement in placements)
                {
                    if (placement?.UnitKey == null)
                    {
                        continue;
                    }

                    var placementObj = new JObject
                    {
                        ["k"] = placement.UnitKey,
                        ["p"] = new JArray(placement.InputIndices ?? Array.Empty<int>()),
                        ["i"] = placement.ItemIndex.HasValue ? new JValue(placement.ItemIndex.Value) : JValue.CreateNull(),
                        ["t"] = new JArray((placement.TargetSeats ?? new List<int[]>())
                            .Select(t => (object)new JArray(t ?? Array.Empty<int>()))
                            .ToArray()),
                    };
                    if (placement.Reused)
                    {
                        placementObj["r"] = true;
                    }

                    arr.Add(placementObj);
                }

                if (arr.Count > 0)
                {
                    result["pl"] = arr;
                }
            }

            if (orphanMetrics != null && orphanMetrics.Count > 0)
            {
                var orphans = new JArray();
                foreach (var (metric, indices) in orphanMetrics)
                {
                    var encoded = AIMetricsJson.ToJson(metric);
                    if (encoded == null)
                    {
                        continue;
                    }

                    orphans.Add(new JObject
                    {
                        ["m"] = encoded,
                        ["p"] = new JArray(indices ?? Array.Empty<int>()),
                    });
                }

                if (orphans.Count > 0)
                {
                    result["om"] = orphans;
                }
            }

            return result;
        }

        private static Dictionary<string, Dictionary<string, List<IGH_Goo>>> DeserializeEntries(
            JObject payload,
            Dictionary<string, List<AIMetrics>> metrics,
            List<UnitPlacement> placements)
        {
            var unitsToken = payload?["e"] as JObject;
            if (unitsToken == null)
            {
                return null;
            }

            if (metrics != null && payload["p"] is JObject provenance)
            {
                foreach (var prop in provenance.Properties())
                {
                    if (!(prop.Value is JArray arr))
                    {
                        continue;
                    }

                    var list = new List<AIMetrics>();
                    foreach (var token in arr)
                    {
                        var m = AIMetricsJson.FromJson(token);
                        if (m != null)
                        {
                            list.Add(m);
                        }
                    }

                    if (list.Count > 0)
                    {
                        metrics[prop.Name] = list;
                    }
                }
            }

            if (placements != null && payload["pl"] is JArray placementsArr)
            {
                foreach (var token in placementsArr)
                {
                    if (!(token is JObject obj))
                    {
                        continue;
                    }

                    var key = obj["k"]?.ToString();
                    if (string.IsNullOrEmpty(key))
                    {
                        continue;
                    }

                    var placement = new UnitPlacement
                    {
                        UnitKey = key,
                        InputIndices = (obj["p"] as JArray)?.Values<int>().ToArray() ?? Array.Empty<int>(),
                        ItemIndex = obj["i"]?.Type == JTokenType.Integer ? obj["i"].Value<int>() : (int?)null,
                        TargetSeats = (obj["t"] as JArray)?
                            .OfType<JArray>()
                            .Select(t => t.Values<int>().ToArray())
                            .ToList() ?? new List<int[]>(),
                        Reused = obj["r"]?.Value<bool>() == true,
                    };
                    placements.Add(placement);
                }
            }

            var entries = new Dictionary<string, Dictionary<string, List<IGH_Goo>>>(StringComparer.Ordinal);
            foreach (var unitProp in unitsToken.Properties())
            {
                if (!(unitProp.Value is JObject outputsObj))
                {
                    continue;
                }

                var outputs = new Dictionary<string, List<IGH_Goo>>(StringComparer.Ordinal);
                foreach (var outProp in outputsObj.Properties())
                {
                    var items = new List<IGH_Goo>();
                    if (outProp.Value is JArray arr)
                    {
                        foreach (var token in arr)
                        {
                            if (token.Type == JTokenType.Null)
                            {
                                items.Add(null);
                                continue;
                            }

                            try
                            {
                                if (SafeGooCodec.TryDecode(token.ToString(), out var goo, out var warning) && goo != null)
                                {
                                    items.Add(goo);
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.For<UnitResultStore>().Warning($"[UnitResultStore] Failed to decode cached item for '{outProp.Name}': {ex.Message}");
                            }
                        }
                    }

                    outputs[outProp.Name] = items;
                }

                entries[unitProp.Name] = outputs;
            }

            return entries;
        }

        private static bool FragmentHasSentinel(Dictionary<string, List<IGH_Goo>> fragment)
        {
            if (fragment == null)
            {
                return false;
            }

            foreach (var list in fragment.Values)
            {
                if (list == null)
                {
                    continue;
                }

                foreach (var item in list)
                {
                    if (item is GH_String s && BatchSentinel.Is(s.Value))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static Dictionary<string, List<IGH_Goo>> CloneFragment(Dictionary<string, List<IGH_Goo>> fragment)
        {
            var clone = new Dictionary<string, List<IGH_Goo>>(StringComparer.Ordinal);
            foreach (var kvp in fragment)
            {
                clone[kvp.Key] = kvp.Value == null ? null : new List<IGH_Goo>(kvp.Value);
            }

            return clone;
        }
    }
}
