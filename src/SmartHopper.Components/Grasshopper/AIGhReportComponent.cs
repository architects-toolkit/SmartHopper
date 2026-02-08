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
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Components.Properties;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Infrastructure.AIModels;

namespace SmartHopper.Components.Grasshopper
{
    /// <summary>
    /// Component that generates a comprehensive canvas status report using the gh_report AI tool.
    /// Outputs a structured markdown report and an optional AI-generated summary.
    /// Extends <see cref="AIStatefulAsyncComponentBase"/> for provider/model selection, async execution,
    /// metrics output, and badge support.
    /// </summary>
    public class AIGhReportComponent : AIStatefulAsyncComponentBase
    {
        /// <inheritdoc/>
        public override Guid ComponentGuid => new Guid("A841ACAB-491E-45ED-8A4F-5D30A12ACD9A");

        /// <inheritdoc/>
        // protected override Bitmap Icon => Resources.ghget;

        /// <inheritdoc/>
        public override GH_Exposure Exposure => GH_Exposure.primary;

        /// <inheritdoc/>
        protected override IReadOnlyList<string> UsingAiTools => new[] { "gh_report" };

        /// <inheritdoc/>
        protected override AICapability RequiredCapability
        {
            get => base.RequiredCapability | AICapability.TextInput | AICapability.TextOutput;
            set => base.RequiredCapability = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AIGhReportComponent"/> class.
        /// </summary>
        public AIGhReportComponent()
            : base(
                  "AI Canvas Report",
                  "AIGhReport",
                  "Generate a comprehensive canvas status report including object counts, topology, groups, scribbles, viewport contents, metadata, and runtime messages. Optionally includes an AI-generated summary of the file purpose and current view.",
                  "SmartHopper",
                  "Grasshopper")
        {
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Include Summary", "S", "When true, generates an AI-powered brief summary of the file purpose and current view using the selected provider/model.", GH_ParamAccess.item, false);
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Report", "Rpt", "Structured markdown report of the canvas status", GH_ParamAccess.item);
            pManager.AddTextParameter("Summary", "Sum", "AI-generated summary of the file purpose and current view (empty when Include Summary is false)", GH_ParamAccess.item);
        }

        /// <inheritdoc/>
        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new AIGhReportWorker(this, this.AddRuntimeMessage);
        }

        /// <summary>
        /// Async worker that executes the gh_report tool.
        /// </summary>
        private sealed class AIGhReportWorker : AsyncWorkerBase
        {
            private readonly AIGhReportComponent parent;
            private bool includeSummary;
            private string report = string.Empty;
            private string summary = string.Empty;

            /// <summary>
            /// Initializes a new instance of the <see cref="AIGhReportWorker"/> class.
            /// </summary>
            public AIGhReportWorker(
                AIGhReportComponent parent,
                Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
                : base(parent, addRuntimeMessage)
            {
                this.parent = parent;
            }

            /// <inheritdoc/>
            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                bool includeSummaryInput = false;
                DA.GetData("Include Summary", ref includeSummaryInput);
                this.includeSummary = includeSummaryInput;
                dataCount = 1;
            }

            /// <inheritdoc/>
            public override async Task DoWorkAsync(CancellationToken token)
            {
                try
                {
                    Debug.WriteLine($"[AIGhReportWorker] Starting DoWorkAsync, includeSummary={this.includeSummary}");

                    var parameters = new JObject
                    {
                        ["includeSummary"] = this.includeSummary,
                    };

                    var toolResult = await this.parent.CallAiToolAsync("gh_report", parameters).ConfigureAwait(false);

                    this.report = toolResult?["report"]?.ToString() ?? string.Empty;
                    this.summary = toolResult?["aiSummary"]?.ToString() ?? string.Empty;

                    Debug.WriteLine($"[AIGhReportWorker] Finished DoWorkAsync - report length={this.report.Length}, summary length={this.summary.Length}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AIGhReportWorker] Error: {ex.Message}");
                }
            }

            /// <inheritdoc/>
            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                this.parent.SetPersistentOutput("Report", new GH_String(this.report), DA);
                this.parent.SetPersistentOutput("Summary", new GH_String(this.summary), DA);
                message = string.Empty;
            }
        }
    }
}
