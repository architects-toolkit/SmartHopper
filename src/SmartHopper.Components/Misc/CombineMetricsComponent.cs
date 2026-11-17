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
using System.Drawing;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.ProviderSdk.AICall.Metrics;

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
                "Aggregate multiple SmartHopper usage metrics within each data-tree branch into a single combined metrics JSON object.",
                "SmartHopper",
                "A. AI")
        {
        }

        public override Guid ComponentGuid => new ("0B5A6E06-895D-45C7-866F-30A4A0D5D8F5");

        /// <summary>
        /// Gets the exposure level of this component in the ribbon.
        /// </summary>
        public override GH_Exposure Exposure => GH_Exposure.quarternary | GH_Exposure.obscure;

        /// <summary>
        /// Gets the component's icon.
        /// </summary>
        protected override Bitmap Icon => Properties.Resources.CombineMetrics;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Metrics", "M", "SmartHopper usage metrics JSON strings. All items within each branch are summed into one combined metric.", GH_ParamAccess.tree);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Metrics", "M", "Combined usage metrics in JSON format (one item per branch).", GH_ParamAccess.tree);
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
                var seenExecutionIds = new HashSet<string>(StringComparer.Ordinal);

                foreach (GH_String item in branch)
                {
                    var metric = ParseMetric(item?.Value);
                    if (metric == null)
                    {
                        continue;
                    }

                    // The same execution replayed at several placements carries the
                    // same execution_id — count its usage/cost AND its execution
                    // counters only once. Zeroing just the money/usage fields left
                    // data_count/iterations_count summed per seat, inflating the
                    // combined view for shared results.
                    if (!string.IsNullOrEmpty(metric.ExecutionId) && !seenExecutionIds.Add(metric.ExecutionId))
                    {
                        metric = metric with
                        {
                            Quantities = new Dictionary<string, double>(),
                            EstimatedCosts = new Dictionary<string, decimal>(),
                            ReportedCosts = new Dictionary<string, decimal>(),
                            CompletionTime = 0.0,
                            DataCount = 0,
                            IterationsCount = 0,
                        };
                    }

                    if (combined == null)
                    {
                        combined = metric;
                    }
                    else
                    {
                        combined = combined.WithCombined(metric);
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
                return AIMetricsJson.FromJson(JObject.Parse(json));
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Serializes an <see cref="AIMetrics"/> instance to JSON using the shared
        /// metrics schema (<see cref="AIMetricsJson"/>).
        /// </summary>
        private static string SerializeMetric(AIMetrics metrics)
            => AIMetricsJson.ToJson(metrics)?.ToString(Newtonsoft.Json.Formatting.None) ?? string.Empty;
    }
}
