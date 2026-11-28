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
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.ComponentBase.Contracts;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Requests;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.AICall.Metrics;

using SmartHopper.ProviderSdk.Logging;
namespace SmartHopper.Core.ComponentBase
{
    /// <summary>
    /// Metrics and output handling for AIStatefulAsyncComponentBase.
    /// Manages metrics persistence, output finalization, and data tree processing.
    /// </summary>
    public abstract partial class AIStatefulAsyncComponentBase
    {
        #region METRICS

        /// <summary>
        /// Replaces the internal last AIReturn snapshot. Visibility is
        /// <c>protected</c> so only derived components (e.g. batch/output
        /// adapters) can publish a new snapshot — making it symmetric with
        /// <see cref="CurrentAIReturnSnapshot"/>'s reader.
        /// </summary>
        /// <param name="ret">AIReturn snapshot to store. Null is ignored.</param>
        protected virtual void SetAIReturnSnapshot(AIReturn ret)
        {
            if (ret == null)
            {
                return;
            }

            this.AIReturnSnapshot = ret;
        }

        /// <summary>
        /// Gets the current <see cref="AIReturn"/> snapshot stored in this component.
        /// Intended for derived components that need to render outputs (e.g., chat history)
        /// from the same source of truth used for metrics.
        /// </summary>
        protected AIReturn CurrentAIReturnSnapshot => this.AIReturnSnapshot;

        /// <summary>
        /// Sets the metrics output tree. Each branch contains the metric JSON string(s)
        /// for the corresponding processing unit, preserving input topology.
        /// </summary>
        /// <param name="dA">The data access object.</param>
        protected void SetMetricsOutput(IGH_DataAccess dA)
        {
            Logger.For<AIStatefulAsyncComponentBase>().Trace("[AIStatefulComponentBase] SetMetricsOutput - Start");

            // Canonical assembly: rebuild the metrics tree from recorded unit
            // placements so entries align with output positions regardless of
            // completion order. No-op when the run recorded no placements.
            this.RebuildRunMetricsTree();

            if (this._metricsTree != null && this._metricsTree.DataCount > 0)
            {
                this.SetPersistentOutput(WellKnownInputs.Metrics, this._metricsTree, dA);
                Logger.For<AIStatefulAsyncComponentBase>().Trace($"[AIStatefulComponentBase] SetMetricsOutput - Set metrics tree. Paths: {string.Join(", ", this._metricsTree.Paths.Select(p => p.ToString()))}");
                return;
            }

            // Fallback for components that do not populate _metricsTree (legacy / sync paths)
            var metricsList = this._batchState.PersistedMetricsList;
            var fallbackMetrics = metricsList == null ? this.AIReturnSnapshot?.Metrics : null;

            if (metricsList == null && fallbackMetrics == null)
            {
                Logger.For<AIStatefulAsyncComponentBase>().Warning("[AIStatefulComponentBase] Empty metrics, skipping");
                // Clear metrics persisted by a previous run so runs without AI calls emit nothing
                this.SetPersistentOutput(WellKnownInputs.Metrics, null, dA);
                return;
            }

            JToken metricsToken;

            if (metricsList != null && metricsList.Entries.Count > 0)
            {
                if (metricsList.Entries.Count == 1)
                {
                    // Single entry → plain JObject (no breaking change)
                    metricsToken = this.SerializeMetricsEntry(metricsList.Entries[0]);
                }
                else
                {
                    // Multiple entries (multi-branch or multi-provider) → JArray
                    var array = new JArray();
                    foreach (var entry in metricsList.Entries)
                    {
                        array.Add(this.SerializeMetricsEntry(entry));
                    }

                    metricsToken = array;
                }
            }
            else
            {
                // Fallback to single AIMetrics from AIReturn snapshot
                metricsToken = this.SerializeMetricsEntry(fallbackMetrics);
            }

            var metricsJsonString = metricsToken.ToString();
            var ghString = new GH_String(metricsJsonString);
            this.SetPersistentOutput(WellKnownInputs.Metrics, ghString, dA);

            Logger.For<AIStatefulAsyncComponentBase>().Trace($"[AIStatefulComponentBase] SetMetricsOutput - Set metrics output. JSON: {metricsToken}");
        }

        private JObject SerializeMetricsEntry(AIMetrics metrics, bool reused = false)
        {
            // Shared codec keeps emitted fields identical to provenance
            // persistence and the metrics compose/decompose components.
            return AIMetricsJson.ToJson(
                metrics,
                dataCount: metrics?.DataCount ?? this.DataCount,
                reused: reused);
        }

        /// <summary>
        /// Lightweight read-only context passed to <see cref="PrepareInputs"/> and
        /// <see cref="SentinelTransformOutputs"/> for the current processing unit.
        /// </summary>
        protected readonly struct ProcessingUnitContext
        {
            /// <summary>Gets the data-tree path of the current processing unit.</summary>
            public GH_Path Path { get; init; }

            /// <summary>Gets the item index within the current branch, or null for branch-level processing.</summary>
            public int? ItemIndex { get; init; }

            /// <summary>Gets the sentinel custom ID for batch-mode units, or null in non-batch mode.</summary>
            public string SentinelId { get; init; }
        }

        /// <summary>
        /// Called before the AI tool is invoked for each processing unit.
        /// Override to transform, enrich, or validate inputs before they reach the AI pipeline.
        /// </summary>
        /// <param name="inputs">Mutable input dictionary for the current processing unit.</param>
        /// <param name="context">Read-only context (path, item index, topology) for the current unit.</param>
        protected virtual void PrepareInputs(Dictionary<string, object> inputs, ProcessingUnitContext context)
        {
        }

        /// <summary>
        /// Called after each AI result is decoded, before it is persisted.
        /// Override to split, reshape, or post-process outputs before they reach the canvas.
        /// </summary>
        /// <param name="decodedOutputs">Mutable output dictionary for the current processing unit.</param>
        /// <param name="context">Read-only context (path, item index, sentinel ID) for the current unit.</param>
        /// <returns>The (potentially modified) output dictionary.</returns>
        protected virtual Dictionary<string, IGH_Goo> SentinelTransformOutputs(Dictionary<string, IGH_Goo> decodedOutputs, ProcessingUnitContext context)
            => decodedOutputs;

        /// <summary>
        /// Combines additional metrics into <see cref="_persistedMetrics"/>.
        /// Use this from derived components that need to merge per-slot or per-item metrics
        /// that were not captured by <see cref="ProcessBatchResults{T}"/> (e.g. N→1 grouping).
        /// After calling this, invoke <see cref="SetMetricsOutput"/> to re-emit.
        /// </summary>
        /// <param name="metrics">The metrics to merge in.</param>
        /// <param name="role">Optional role label for the metrics entry (e.g. "main", "fallback", "tool:img2text").</param>
        protected void CombineIntoPersistedMetrics(AIMetrics metrics, string role = null)
        {
            if (metrics == null) return;
            var entry = WithExecutionId(WithRole(metrics, role));
            this._batchState.PersistedMetricsList ??= new ProviderSdk.AICall.Metrics.AIMetricsList();
            this._batchState.PersistedMetricsList.Add(entry);
            this.ResultStore.RecordMetrics(this.CurrentUnitKey, entry);
            this.AppendMetricToTree(entry);
        }

        /// <summary>
        /// Combines additional metrics into the persisted metrics list and appends them to
        /// <see cref="_metricsTree"/> at the specified output branch path. Use this from
        /// derived components that need to merge per-slot or per-item metrics that were
        /// not captured by <see cref="ProcessBatchResults{T}"/> and whose natural output
        /// branch is known (e.g. image slots that belong to the same Markdown document).
        /// After calling this, invoke <see cref="SetMetricsOutput"/> to re-emit.
        /// </summary>
        /// <param name="metrics">The metrics to merge in.</param>
        /// <param name="path">The output branch path the metrics belong to.</param>
        /// <param name="role">Optional role label for the metrics entry.</param>
        protected void CombineIntoPersistedMetricsAtPath(AIMetrics metrics, GH_Path path, string role = null)
        {
            if (metrics == null || path == null) return;
            var entry = WithExecutionId(WithRole(metrics, role));
            this._batchState.PersistedMetricsList ??= new ProviderSdk.AICall.Metrics.AIMetricsList();
            this._batchState.PersistedMetricsList.Add(entry);
            this.ResultStore.RecordMetrics(this.CurrentUnitKey, entry, path?.Indices);
            this.AppendMetricToTree(entry, path);
        }

        /// <summary>
        /// Returns the metrics with <paramref name="role"/> applied, preserving any
        /// role the metrics already carries when no override is supplied.
        /// </summary>
        private static AIMetrics WithRole(AIMetrics metrics, string role)
            => string.IsNullOrEmpty(role) ? metrics : metrics with { Role = role };

        /// <summary>
        /// Ensures the metrics entry carries a stable execution identifier so the
        /// same execution replayed at multiple output placements can be
        /// deduplicated instead of charged once per occurrence. The id is shared
        /// by the persisted list, the unit-result provenance record, and the
        /// emitted metrics tree.
        /// </summary>
        private static AIMetrics WithExecutionId(AIMetrics metrics)
            => string.IsNullOrEmpty(metrics.ExecutionId)
                ? metrics with { ExecutionId = Guid.NewGuid().ToString("N") }
                : metrics;

        /// <summary>
        /// Persists the primary output tree and any additional named outputs, then emits metrics.
        /// Call this from both the non-batch branch of <c>DoWorkAsync</c> and from
        /// <see cref="ProcessBatchResults{T}"/> to ensure a single finalization point.
        /// </summary>
        /// <typeparam name="T">The primary output Grasshopper goo type.</typeparam>
        /// <param name="primaryOutputParamName">Name of the primary output parameter.</param>
        /// <param name="primaryTree">The fully decoded primary output tree.</param>
        /// <param name="additionalOutputs">
        /// Zero or more (name, value) tuples for secondary outputs.
        /// Value routing: <see cref="IGH_Structure"/> → SetDataTree; <see cref="System.Collections.IEnumerable"/> (non-string) → SetDataList;
        /// anything else → SetData via <see cref="GH_Convert.ToGoo"/>.
        /// </param>
        protected void FinishResults<T>(
            string primaryOutputParamName,
            GH_Structure<T> primaryTree,
            params (string name, object value)[] additionalOutputs)
            where T : IGH_Goo
        {
            // Publication boundary: raw batch sentinels are transport-only and must
            // never reach persisted output. Unresolved placeholders (partial decode,
            // orphaned queue) are stripped; the affected units stay uncommitted in
            // the result store and re-execute on the next run.
            var strippedCount = Batch.BatchSentinel.StripFrom(primaryTree);

            // Persist the primary output
            this.SetPersistentOutput(primaryOutputParamName, primaryTree, null);

            // Persist any additional outputs
            if (additionalOutputs != null)
            {
                foreach (var (name, value) in additionalOutputs)
                {
                    if (value is IGH_Structure extraStructure)
                    {
                        strippedCount += Batch.BatchSentinel.StripFrom(extraStructure);
                    }

                    this.SetPersistentOutput(name, value, null);
                }
            }

            if (strippedCount > 0)
            {
                Logger.For<AIStatefulAsyncComponentBase>().Warning($"[AIStatefulAsync] FinishResults: stripped {strippedCount} unresolved batch sentinel(s) from published output");
                this.SetPersistentRuntimeMessage(
                    "batch_unresolved_placeholders",
                    GH_RuntimeMessageLevel.Warning,
                    $"{strippedCount} unresolved batch placeholder(s) were omitted from published output; affected units will re-run.",
                    false);
            }

            // Stamp CompletionTime into every metrics entry (all branches share
            // the same batch completion time; non-batch leaves it null).
            // AIReturn.Metrics is computed fresh on every access — writing to it is a no-op.
            if (this._batchState.CompletionTime.HasValue)
            {
                var list = this._batchState.PersistedMetricsList;
                if (list?.Entries != null && list.Entries.Count > 0)
                {
                    var completionTime = this._batchState.CompletionTime.Value;
                    var newList = new ProviderSdk.AICall.Metrics.AIMetricsList();
                    foreach (var entry in list.Entries)
                    {
                        newList.Add(entry with { CompletionTime = completionTime });
                    }

                    this._batchState.PersistedMetricsList = newList;

                    Logger.For<AIStatefulAsyncComponentBase>().Trace($"[AIStatefulAsync] FinishResults: stamped CompletionTime={completionTime:F2}s into {newList.Entries.Count} metric(s)");
                }

                // Also stamp the stored unit provenance — freshly executed units
                // get the batch elapsed; units served from the store keep their
                // original duration. RebuildRunMetricsTree then emits stamped
                // values on every emit, and committed provenance persists them.
                this.ResultStore.StampRunCompletionTime(this._batchState.CompletionTime.Value);

                // The incrementally assembled tree keeps the stamp too — the
                // placement-driven rebuild replaces it wholesale when placements
                // exist; without placements this is the emitted tree.
                this.StampCompletionTimeIntoMetricsTree(this._batchState.CompletionTime.Value);

                this._batchState.CompletionTime = null;
            }

            // Promote resolved unit results — fragments still carrying sentinels are
            // dropped and will re-execute on the next run. Idempotent: the subsequent
            // OnWorkerCompleted commit converges to the same committed set.
            // Skipped while instance caching is disabled so the untouched run
            // does not replace the committed snapshot with an empty one.
            if (!this.DisableResultCaching)
            {
                this.ResultStore.CommitRun();
            }

            // Always emit metrics (replaces the ShouldEmitMetricsInPostSolve pattern)
            this.SetMetricsOutput(null);
        }

        /// <summary>
        /// Rebuilds <see cref="_metricsTree"/> from recorded unit placements so the
        /// metrics items align with the output items at the same paths, regardless
        /// of the order units completed in. Reused occurrences emit
        /// <c>reused: true</c> with zeroed data/iterations counts; grouped sibling
        /// copies carry <c>data_count: 0</c>; provenance without a unit context is
        /// appended last. No-op when the run recorded no placements (legacy
        /// pending payloads keep the incrementally assembled tree, including the
        /// batch completion-time stamp).
        /// </summary>
        private void RebuildRunMetricsTree()
        {
            var placements = this.ResultStore.BuildRunMetricsPlacements(this.ComponentProcessingOptions.Topology);
            if (placements == null)
            {
                return;
            }

            var rebuilt = new GH_Structure<GH_String>();
            foreach (var placement in placements)
            {
                var stamped = placement.Reused
                    ? placement.Metrics with { DataCount = 0, IterationsCount = 0 }
                    : placement.SiblingCopy
                        ? placement.Metrics with { DataCount = 0 }
                        : placement.Metrics;
                var obj = this.SerializeMetricsEntry(stamped, reused: placement.Reused);
                if (obj != null)
                {
                    rebuilt.Append(
                        new GH_String(obj.ToString(Newtonsoft.Json.Formatting.None)),
                        new GH_Path(placement.PathIndices ?? Array.Empty<int>()));
                }
            }

            // Keep the incrementally assembled tree when the rebuild produced
            // nothing — covers runs that recorded placements but no provenance.
            if (rebuilt.DataCount > 0)
            {
                this._metricsTree = rebuilt;
            }
        }

        /// <summary>
        /// Updates every serialized metric in <see cref="_metricsTree"/> with the given completion time.
        /// Parses each JSON string, overwrites the "completion_time" property, and re-serializes.
        /// </summary>
        private void StampCompletionTimeIntoMetricsTree(double completionTime)
        {
            if (this._metricsTree == null) return;

            var updatedTree = new GH_Structure<GH_String>();
            foreach (var path in this._metricsTree.Paths)
            {
                var branch = this._metricsTree.get_Branch(path);
                if (branch == null) continue;
                foreach (GH_String item in branch)
                {
                    try
                    {
                        var obj = JObject.Parse(item.Value);

                        // Replayed provenance keeps its original duration — the
                        // current run's batch elapsed time does not apply.
                        if (obj["reused"]?.Value<bool>() == true)
                        {
                            updatedTree.Append(new GH_String(obj.ToString(Newtonsoft.Json.Formatting.None)), path);
                            continue;
                        }

                        obj["completion_time"] = completionTime;
                        updatedTree.Append(new GH_String(obj.ToString(Newtonsoft.Json.Formatting.None)), path);
                    }
                    catch
                    {
                        // If parsing fails, keep the original
                        updatedTree.Append(item, path);
                    }
                }
            }

            this._metricsTree = updatedTree;
        }

        #endregion
    }
}
