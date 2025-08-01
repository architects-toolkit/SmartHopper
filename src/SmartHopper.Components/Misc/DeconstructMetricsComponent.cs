/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Drawing;
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;

namespace SmartHopper.Components.Misc
{
    public class DeconstructMetricsComponent : GH_Component
    {
        public DeconstructMetricsComponent()
            : base("Deconstruct SmartHopper Metrics", "DMetrics",
                   "Deconstructs SmartHopper usage metrics into individual values",
                   "SmartHopper", "Utils")
        {
        }

        public override Guid ComponentGuid => new("250D14BA-D96A-4DC0-8703-87468CE2A18D");

        /// <summary>
        /// Gets the component's icon.
        /// </summary>
        protected override Bitmap Icon => Properties.Resources.metrics;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Metrics", "M", "SmartHopper usage metrics in JSON, generated by any AI-powered SmartHopper component", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("AI Provider", "P", "AI provider used", GH_ParamAccess.item);
            pManager.AddTextParameter("AI Model", "M", "AI model used", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Input Tokens", "I", "Number of input tokens", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Output Tokens", "O", "Number of output tokens", GH_ParamAccess.item);
            pManager.AddTextParameter("Finish Reason", "F", "Reason for finishing", GH_ParamAccess.item);
            pManager.AddNumberParameter("Completion Time", "T", "Time taken for completion, in seconds", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Data Count", "DC", "The number of data items that were processed by the component. This may not match the total number of items in your input lists. If the component is configured to process data in batches, this value indicates how many batches (or groups) of results the component needs to process.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Iterations Count", "IC", "The number of times the component ran its calculation. If the component was set to recognize and group identical combinations of input items, it only processed each unique combination once and applied the results to all matching outputs. As a result, the iteration count may be less than the total data count.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string? jsonMetrics = null;
            if (!DA.GetData(0, ref jsonMetrics))
            {
                return;
            }

            try
            {
                var metricsObject = JObject.Parse(jsonMetrics);

                string aiProvider = metricsObject["ai_provider"]?.Value<string>() ?? "Unknown";
                string aiModel = metricsObject["ai_model"]?.Value<string>() ?? "Unknown";
                int inputTokens = metricsObject["tokens_input"]?.Value<int>() ?? 0;
                int outputTokens = metricsObject["tokens_output"]?.Value<int>() ?? 0;
                string finishReason = metricsObject["finish_reason"]?.Value<string>() ?? "Unknown";
                double completionTime = metricsObject["completion_time"]?.Value<double>() ?? 0.0;
                int inputDataCount = metricsObject["data_count"]?.Value<int>() ?? 0;
                int iterationsCount = metricsObject["iterations_count"]?.Value<int>() ?? 0;

                // Checks to see if the values were actually present
                bool hasAIProvider = metricsObject["ai_provider"] != null;
                bool hasAIModel = metricsObject["ai_model"] != null;
                bool hasInputTokens = metricsObject["tokens_input"] != null;
                bool hasOutputTokens = metricsObject["tokens_output"] != null;
                bool hasFinishReason = metricsObject["finish_reason"] != null;
                bool hasCompletionTime = metricsObject["completion_time"] != null;

                // Set the data, potentially with warnings if values were missing
                DA.SetData(0, aiProvider);
                if (!hasAIProvider)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "AI provider not found in JSON");
                }

                DA.SetData(1, aiModel);
                if (!hasAIModel)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "AI model not found in JSON");
                }

                DA.SetData(2, inputTokens);
                if (!hasInputTokens)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Input tokens not found in JSON");
                }

                DA.SetData(3, outputTokens);
                if (!hasOutputTokens)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Output tokens not found in JSON");
                }

                DA.SetData(4, finishReason);
                if (!hasFinishReason)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Finish reason not found in JSON");
                }

                DA.SetData(5, completionTime);
                if (!hasCompletionTime)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Completion time not found in JSON");
                }

                DA.SetData(6, inputDataCount);

                DA.SetData(7, iterationsCount);
            }
            catch (Exception ex)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Failed to parse JSON metrics: {ex.Message}");
            }
        }
    }
}
