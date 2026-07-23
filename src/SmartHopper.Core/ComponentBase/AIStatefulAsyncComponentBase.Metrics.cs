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
        /// Sets the metrics output parameters (input tokens, output tokens, finish reason).
        /// </summary>
        /// <param name="dA">The data access object.</param>
        protected void SetMetricsOutput(IGH_DataAccess dA)
        {
            Debug.WriteLine("[AIStatefulComponentBase] SetMetricsOutput - Start");

            var metrics = this._batchState.PersistedMetrics ?? this.AIReturnSnapshot?.Metrics;
            if (metrics == null)
            {
                Debug.WriteLine("[AIStatefulComponentBase] Empty metrics, skipping");
                return;
            }

            // Create JSON object with metrics
            var metricsJson = new JObject(
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
                new JProperty("context_usage_percent", metrics.ContextUsagePercent),
                new JProperty("data_count", this.DataCount),
                new JProperty("iterations_count", this.ProgressInfo.Total));

            // Convert metricsJson to GH_String
            var metricsJsonString = metricsJson.ToString();
            var ghString = new GH_String(metricsJsonString);

            // Set the metrics output
            this.SetPersistentOutput(WellKnownInputs.Metrics, ghString, dA);

            Debug.WriteLine($"[AIStatefulComponentBase] SetMetricsOutput - Set metrics output. JSON: {metricsJson}");
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
        protected void CombineIntoPersistedMetrics(AIMetrics metrics)
        {
            if (metrics == null) return;
            if (this._batchState.PersistedMetrics == null)
            {
                this._batchState.PersistedMetrics = new AIMetrics
                {
                    Provider = this.GetActualAIProviderName(),
                    Model = this.GetModel(),
                };
            }

            this._batchState.PersistedMetrics.Combine(metrics);
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

            // Stamp CompletionTime into _persistedMetrics (the single authoritative metrics instance).
            // AIReturn.Metrics is computed fresh on every access — writing to it is a no-op.
            if (this._batchState.CompletionTime.HasValue)
            {
                if (this._batchState.PersistedMetrics != null)
                {
                    this._batchState.PersistedMetrics.CompletionTime = this._batchState.CompletionTime.Value;
                    Debug.WriteLine($"[AIStatefulAsync] FinishResults: stamped CompletionTime={this._batchState.CompletionTime.Value:F2}s into _persistedMetrics");
                }

                this._batchState.CompletionTime = null;
            }

            // Always emit metrics (replaces the ShouldEmitMetricsInPostSolve pattern)
            this.SetMetricsOutput(null);
        }

        #endregion
    }
}
