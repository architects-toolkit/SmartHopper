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

namespace SmartHopper.Components.Misc
{
    /// <summary>
    /// Deconstructs SmartHopper usage metrics into per-call outputs: the stable
    /// metadata fields come out as plain values while the open-ended bags —
    /// measured <c>quantities</c> and per-currency <c>costs</c> — stay JSON
    /// objects for the JSON deconstruction tools.
    /// </summary>
    public class DeconstructMetricsComponent : GH_Component
    {
        public DeconstructMetricsComponent()
            : base(
                "Deconstruct SmartHopper Metrics",
                "DMetrics",
                "Deconstruct SmartHopper usage metrics: provider, model, finish reason, timing and call counts as values; measured quantities and per-currency costs as JSON objects (use the JSON tools to read them).",
                "SmartHopper",
                "A. AI")
        {
        }

        public override Guid ComponentGuid => new ("D412F6FD-656A-45BD-AEB1-02F5D6CCC9E0");

        /// <summary>
        /// Gets the exposure level of this component in the ribbon.
        /// </summary>
        public override GH_Exposure Exposure => GH_Exposure.quarternary;

        /// <summary>
        /// Gets the component's icon.
        /// </summary>
        protected override Bitmap Icon => Properties.Resources.DeconstructMetrics;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Metrics", "M", "Usage metrics JSON from any AI-powered SmartHopper component.", GH_ParamAccess.tree);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("AI Provider", "P", "Name of the AI provider that processed the request.", GH_ParamAccess.tree);
            pManager.AddTextParameter("AI Model", "M", "Name of the AI model that processed the request.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Finish Reason", "F", "Reason the model stopped generating (e.g., 'stop', 'length', 'tool_calls').", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Completion Time", "T", "Elapsed time in seconds for the AI call.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Context Usage", "CU", "Percentage (0–100) of the model's context window used by the call (empty when the model's context limit is unknown).", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Data Count", "DC", "Total number of input data units processed by the component.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Iterations Count", "IC", "Number of unique AI calls executed (may be lower than Data Count when identical inputs are deduplicated).", GH_ParamAccess.tree);
            pManager.AddTextParameter("Role", "R", "Call role label when multiple calls occur in a branch (e.g., primary call, modality fallback, or inner tool call).", GH_ParamAccess.tree);
            pManager.AddTextParameter("Quantities", "Q", "Measured usage as a JSON object — '<kind>/<unit>' → value (e.g. 'text_input/token', 'audio_input/second', 'tool:web_search/invocation'). Deconstruct with the JSON tools.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Costs", "C", "Costs as a JSON object — '{estimated:{<CURRENCY>: amount}, reported:{<CURRENCY>: amount}}'. Deconstruct with the JSON tools.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Completed At", "CA", "UTC completion date-time of the call (ISO 8601; empty for legacy metrics that predate timestamp capture).", GH_ParamAccess.tree);
            pManager.AddTextParameter("Execution Id", "EID", "Stable identifier of the execution that produced this metric — shared-result replays carry the same id.", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("Reused", "RU", "True when this metric is replayed provenance from a cached result rather than work executed in the latest run.", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var metricsTree = new GH_Structure<GH_String>();
            if (!DA.GetDataTree(0, out metricsTree))
            {
                return;
            }

            var providers = new GH_Structure<GH_String>();
            var models = new GH_Structure<GH_String>();
            var finishReasons = new GH_Structure<GH_String>();
            var completionTimes = new GH_Structure<GH_Number>();
            var contextUsages = new GH_Structure<GH_Number>();
            var dataCounts = new GH_Structure<GH_Integer>();
            var iterationsCounts = new GH_Structure<GH_Integer>();
            var roles = new GH_Structure<GH_String>();
            var quantities = new GH_Structure<GH_String>();
            var costs = new GH_Structure<GH_String>();
            var completedAts = new GH_Structure<GH_String>();
            var executionIds = new GH_Structure<GH_String>();
            var reusedFlags = new GH_Structure<GH_Boolean>();

            foreach (var path in metricsTree.Paths)
            {
                var branch = metricsTree.get_Branch(path);
                if (branch == null) continue;

                foreach (GH_String item in branch)
                {
                    try
                    {
                        var token = JToken.Parse(item?.Value ?? "{}");
                        var entries = new List<JObject>();

                        if (token is JArray array)
                        {
                            foreach (var arrItem in array)
                            {
                                if (arrItem is JObject obj) entries.Add(obj);
                            }
                        }
                        else if (token is JObject singleObj)
                        {
                            entries.Add(singleObj);
                        }

                        foreach (var metricsObject in entries)
                        {
                            providers.Append(new GH_String(metricsObject["ai_provider"]?.Value<string>() ?? "Unknown"), path);
                            models.Append(new GH_String(metricsObject["ai_model"]?.Value<string>() ?? "Unknown"), path);
                            finishReasons.Append(new GH_String(metricsObject["finish_reason"]?.Value<string>() ?? "Unknown"), path);
                            completionTimes.Append(new GH_Number(metricsObject["completion_time"]?.Value<double>() ?? 0.0), path);

                            var contextUsagePercent = metricsObject["context_usage_percent"]?.Value<double?>();
                            contextUsages.Append(new GH_Number(contextUsagePercent.HasValue ? contextUsagePercent.Value * 100.0 : 0.0), path);

                            dataCounts.Append(new GH_Integer(metricsObject["data_count"]?.Value<int>() ?? 0), path);
                            iterationsCounts.Append(new GH_Integer(metricsObject["iterations_count"]?.Value<int>() ?? 0), path);
                            roles.Append(new GH_String(metricsObject["role"]?.Value<string>()), path);

                            var quantitiesJson = metricsObject["quantities"] as JObject ?? new JObject();
                            var costsJson = metricsObject["costs"] as JObject ?? new JObject();
                            quantities.Append(new GH_String(quantitiesJson.ToString(Newtonsoft.Json.Formatting.None)), path);
                            costs.Append(new GH_String(costsJson.ToString(Newtonsoft.Json.Formatting.None)), path);

                            completedAts.Append(new GH_String(
                                SmartHopper.ProviderSdk.AICall.Metrics.AIMetricsJson
                                    .ReadCompletedAt(metricsObject["completed_at"])
                                    ?.UtcDateTime.ToString("o")), path);
                            executionIds.Append(new GH_String(metricsObject["execution_id"]?.Value<string>()), path);
                            reusedFlags.Append(new GH_Boolean(metricsObject["reused"]?.Value<bool>() ?? false), path);
                        }
                    }
                    catch (Exception ex)
                    {
                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Failed to parse JSON metrics at {path}: {ex.Message}");
                    }
                }
            }

            DA.SetDataTree(0, providers);
            DA.SetDataTree(1, models);
            DA.SetDataTree(2, finishReasons);
            DA.SetDataTree(3, completionTimes);
            DA.SetDataTree(4, contextUsages);
            DA.SetDataTree(5, dataCounts);
            DA.SetDataTree(6, iterationsCounts);
            DA.SetDataTree(7, roles);
            DA.SetDataTree(8, quantities);
            DA.SetDataTree(9, costs);
            DA.SetDataTree(10, completedAts);
            DA.SetDataTree(11, executionIds);
            DA.SetDataTree(12, reusedFlags);
        }
    }
}
