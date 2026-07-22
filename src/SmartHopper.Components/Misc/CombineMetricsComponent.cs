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
using System.Drawing;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Metrics;

namespace SmartHopper.Components.Misc
{
    /// <summary>
    /// Combines multiple metrics entries within each branch into a single
    /// aggregated metric. Useful when a component emits several metrics
    /// per branch (e.g. main + fallback) and you want one combined value.
    /// </summary>
    public class CombineMetricsComponent : GH_Component
    {
        public CombineMetricsComponent()
            : base(
                "Combine SmartHopper Metrics",
                "CMetrics",
                "Combines multiple metrics entries within each branch into a single aggregated metric. Preserves branch topology.",
                "SmartHopper",
                "Utils")
        {
        }

        public override Guid ComponentGuid => new ("0B5A6E06-895D-45C7-866F-30A4A0D5D8F5");

        /// <summary>
        /// Gets the component's icon.
        /// </summary>
        protected override Bitmap Icon => null;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Metrics", "M", "SmartHopper usage metrics in JSON. Accepts a tree — all items in each branch are combined into one.", GH_ParamAccess.tree);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Metrics", "M", "Combined metrics in JSON, one item per branch.", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var metricsTree = new GH_Structure<GH_String>();
            if (!DA.GetDataTree(0, out metricsTree))
            {
                return;
            }

            var outputTree = new GH_Structure<GH_String>();

            foreach (var path in metricsTree.Paths)
            {
                var branch = metricsTree.get_Branch(path);
                if (branch == null || branch.Count == 0)
                {
                    continue;
                }

                AIMetrics combined = null;

                foreach (GH_String item in branch)
                {
                    var metric = ParseMetric(item?.Value);
                    if (metric == null)
                    {
                        continue;
                    }

                    if (combined == null)
                    {
                        combined = metric;
                    }
                    else
                    {
                        combined.Combine(metric);
                    }
                }

                if (combined != null)
                {
                    var json = SerializeMetric(combined);
                    outputTree.Append(new GH_String(json), path);
                }
            }

            DA.SetDataTree(0, outputTree);
        }

        /// <summary>
        /// Parses a metrics JSON string into an <see cref="AIMetrics"/> instance.
        /// Maps the property names produced by <see cref="SerializeMetricsEntry"/>.
        /// </summary>
        private static AIMetrics ParseMetric(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                var obj = JObject.Parse(json);
                var metric = new AIMetrics
                {
                    Provider = obj["ai_provider"]?.Value<string>(),
                    Model = obj["ai_model"]?.Value<string>(),
                    InputTokensPrompt = obj["tokens_input_prompt"]?.Value<int>() ?? 0,
                    InputTokensCached = obj["tokens_input_cached"]?.Value<int>() ?? 0,
                    InputTokensCacheWrite = obj["tokens_input_cache_write"]?.Value<int>() ?? 0,
                    OutputTokensReasoning = obj["tokens_output_reasoning"]?.Value<int>() ?? 0,
                    OutputTokensGeneration = obj["tokens_output_generation"]?.Value<int>() ?? 0,
                    FinishReason = obj["finish_reason"]?.Value<string>(),
                    CompletionTime = obj["completion_time"]?.Value<double>() ?? 0.0,
                    Role = obj["role"]?.Value<string>(),
                };

                var dataCount = obj["data_count"]?.Value<int?>();
                if (dataCount.HasValue)
                {
                    metric.DataCount = dataCount.Value;
                }

                var iterationsCount = obj["iterations_count"]?.Value<int?>();
                if (iterationsCount.HasValue)
                {
                    metric.IterationsCount = iterationsCount.Value;
                }

                // ContextUsagePercent is computed, but we can seed LastEffectiveTotalTokens
                // so the next read recomputes correctly.
                var contextUsage = obj["context_usage_percent"]?.Value<double?>();
                if (contextUsage.HasValue && metric.Provider != null && metric.Model != null)
                {
                    // Force a recompute by touching the property
                    _ = metric.ContextUsagePercent;
                }

                return metric;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Serializes an <see cref="AIMetrics"/> instance to JSON using the same
        /// schema as <see cref="AIStatefulAsyncComponentBase.SerializeMetricsEntry"/>.
        /// </summary>
        private static string SerializeMetric(AIMetrics metrics)
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

            if (metrics.IterationsCount.HasValue)
            {
                obj.Add("iterations_count", metrics.IterationsCount.Value);
            }

            return obj.ToString(Newtonsoft.Json.Formatting.None);
        }
    }
}
