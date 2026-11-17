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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Components.Properties;
using SmartHopper.Core.ComponentBase;
using SmartHopper.ProviderSdk.AIModels;

using SmartHopper.ProviderSdk.Logging;
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
        /// <summary>
        /// This component's work has side effects or consumes external state
        /// (canvas mutations, chat history, file/web fetches) so per-unit results
        /// are not reusable across runs.
        /// </summary>
        protected override bool SupportsResultReuse => false;

        public override Guid ComponentGuid => new Guid("5FDC1E07-DA09-4051-A89E-7FFCF5DEC6B7");

        /// <inheritdoc/>
        protected override Bitmap Icon => Resources.AIGhReport;

        /// <inheritdoc/>
        public override GH_Exposure Exposure => GH_Exposure.secondary;

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
                  "Generate a structured status report of the current Grasshopper canvas — component and wire counts, topology, groups, scribbles, viewport contents, file metadata, and runtime errors or warnings — with an optional AI summary.",
                  "SmartHopper",
                  "D. GhJSON")
        {
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Include Summary", "S", "When true, asks the selected AI provider to generate a brief summary of what the definition does and what is currently in view.", GH_ParamAccess.item, false);
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Report", "Rpt", "Structured Markdown report of the current Grasshopper canvas.", GH_ParamAccess.item);
            pManager.AddTextParameter("Summary", "Sum", "AI-generated summary of the definition's purpose and current view (empty when Include Summary is false).", GH_ParamAccess.item);
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
                    Logger.For<AIGhReportWorker>().Trace($"[AIGhReportWorker] Starting DoWorkAsync, includeSummary={this.includeSummary}");

                    var parameters = new JObject
                    {
                        ["includeSummary"] = this.includeSummary,
                    };

                    var toolResult = await this.parent.CallAIToolAsync("gh_report", parameters, token).ConfigureAwait(false);

                    if (toolResult == null || !toolResult.Success)
                    {
                        var errorMsg = toolResult?.Messages?.FirstOrDefault(m => m.Severity == SmartHopper.ProviderSdk.Diagnostics.SHRuntimeMessageSeverity.Error);
                        if (errorMsg != null)
                        {
                            this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, errorMsg.Message);
                        }

                        Logger.For<AIGhReportWorker>().Warning($"[AIGhReportWorker] Tool call failed");
                        return;
                    }

                    this.report = toolResult["report"]?.ToString() ?? string.Empty;
                    this.summary = toolResult["aiSummary"]?.ToString() ?? string.Empty;

                    Logger.For<AIGhReportWorker>().Trace($"[AIGhReportWorker] Finished DoWorkAsync - report length={this.report.Length}, summary length={this.summary.Length}");
                }
                catch (Exception ex)
                {
                    Logger.For<AIGhReportWorker>().Exception(ex, $"[AIGhReportWorker] Error: {ex.Message}");
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                }
            }

            /// <inheritdoc/>
            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                this.parent.SetPersistentOutput("Report", new GH_String(this.report), DA);
                this.parent.SetPersistentOutput("Summary", new GH_String(this.summary), DA);
                this.parent.SetMetricsOutput(DA);
                message = string.Empty;
            }
        }
    }
}
