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
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Metrics;

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
            Debug.WriteLine("[AIStatefulComponentBase] SetMetricsOutput - Start");

            if (this._metricsTree != null && this._metricsTree.DataCount > 0)
            {
                this.SetPersistentOutput(WellKnownInputs.Metrics, this._metricsTree, dA);
                Debug.WriteLine($"[AIStatefulComponentBase] SetMetricsOutput - Set metrics tree. Paths: {string.Join(", ", this._metricsTree.Paths.Select(p => p.ToString()))}");
                return;
            }

            // Fallback for components that do not populate _metricsTree (legacy / sync paths)
            var metricsList = this._batchState.PersistedMetricsList;
            var fallbackMetrics = metricsList == null ? this.AIReturnSnapshot?.Metrics : null;

            if (metricsList == null && fallbackMetrics == null)
            {
                Debug.WriteLine("[AIStatefulComponentBase] Empty metrics, skipping");
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

            Debug.WriteLine($"[AIStatefulComponentBase] SetMetricsOutput - Set metrics output. JSON: {metricsToken}");
        }

        private JObject SerializeMetricsEntry(AIMetrics metrics)
        {
            var obj = new JObject(
                new JProperty("ai_provider", metrics.Provider),
                new JProperty("ai_model", metrics.Model),
                new JProperty("tokens_input", metrics.InputTokens),
                new JProperty("tokens_input_prompt", metrics.InputTokensPrompt),
                new JProperty("tokens_input_cached", metrics.InputTokensCached),
                new JProperty("tokens_input_cache_write", metrics.InputTokensCacheWrite),
                new JProperty("tokens_output", metrics.OutputTokens),
                new JProperty("tokens_output_reasoning", metrics.OutputTokensReasoning),
                new JProperty("tokens_output_generation", metrics.OutputTokensGeneration),
                new JProperty("finish_reason", metrics.FinishReason),
                new JProperty("completion_time", metrics.CompletionTime),
                new JProperty("context_usage_percent", metrics.ContextUsagePercent));

            if (metrics.Role != null)
            {
                obj.Add("role", metrics.Role);
            }

            if (metrics.DataCount.HasValue)
            {
                obj.Add("data_count", metrics.DataCount.Value);
            }
            else
            {
                obj.Add("data_count", this.DataCount);
            }

            obj.Add("iterations_count", metrics.IterationsCount ?? 0);

            return obj;
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
        /// <param name="role">Optional role label for the metrics entry (e.g. "main", "fallback:ImageToText").</param>
        protected void CombineIntoPersistedMetrics(AIMetrics metrics, string role = null)
        {
            if (metrics == null) return;
            this._batchState.PersistedMetricsList ??= new Infrastructure.AICall.Metrics.AIMetricsList();
            this._batchState.PersistedMetricsList.Add(metrics, role);
            this.AppendMetricToTree(metrics);
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
            this._batchState.PersistedMetricsList ??= new Infrastructure.AICall.Metrics.AIMetricsList();
            this._batchState.PersistedMetricsList.Add(metrics, role);
            this.AppendMetricToTree(metrics, path);
        }

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
            // Persist the primary output
            this.SetPersistentOutput(primaryOutputParamName, primaryTree, null);

            // Persist any additional outputs
            if (additionalOutputs != null)
            {
                foreach (var (name, value) in additionalOutputs)
                {
                    this.SetPersistentOutput(name, value, null);
                }
            }

            // Stamp CompletionTime into every metrics entry (all branches share
            // the same batch completion time; non-batch leaves it null).
            // AIReturn.Metrics is computed fresh on every access — writing to it is a no-op.
            if (this._batchState.CompletionTime.HasValue)
            {
                var entries = this._batchState.PersistedMetricsList?.Entries;
                if (entries != null && entries.Count > 0)
                {
                    foreach (var entry in entries)
                    {
                        entry.CompletionTime = this._batchState.CompletionTime.Value;
                    }

                    Debug.WriteLine($"[AIStatefulAsync] FinishResults: stamped CompletionTime={this._batchState.CompletionTime.Value:F2}s into {entries.Count} metric(s)");
                }

                // Also stamp into the metrics tree by updating the serialized JSON strings
                if (this._metricsTree != null)
                {
                    this.StampCompletionTimeIntoMetricsTree(this._batchState.CompletionTime.Value);
                }

                this._batchState.CompletionTime = null;
            }

            // Always emit metrics (replaces the ShouldEmitMetricsInPostSolve pattern)
            this.SetMetricsOutput(null);
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
