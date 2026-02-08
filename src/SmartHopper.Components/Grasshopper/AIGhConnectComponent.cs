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
using SmartHopper.Infrastructure.AIModels;

namespace SmartHopper.Components.Grasshopper
{
    /// <summary>
    /// Component that uses AI to intelligently connect selected Grasshopper components
    /// based on a user-specified purpose. Extends <see cref="AISelectingStatefulAsyncComponentBase"/>
    /// for canvas selection, provider/model selection, async execution, and metrics output.
    /// </summary>
    public class AIGhConnectComponent : AISelectingStatefulAsyncComponentBase
    {
        /// <inheritdoc/>
        public override Guid ComponentGuid => new Guid("E77B49F0-CC4F-4604-8B6C-6310877CCA15");

        /// <inheritdoc/>
        // protected override Bitmap Icon => Resources.ghget;

        /// <inheritdoc/>
        public override GH_Exposure Exposure => GH_Exposure.primary;

        /// <inheritdoc/>
        protected override IReadOnlyList<string> UsingAiTools => new[] { "gh_smart_connect" };

        /// <inheritdoc/>
        protected override AICapability RequiredCapability
        {
            get => base.RequiredCapability | AICapability.TextInput | AICapability.TextOutput;
            set => base.RequiredCapability = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AIGhConnectComponent"/> class.
        /// </summary>
        public AIGhConnectComponent()
            : base(
                  "AI Smart Connect",
                  "AIGhConnect",
                  "Use AI to intelligently connect selected Grasshopper components based on a described purpose.\nSelect components on the canvas, describe the desired wiring goal, and let AI suggest and create the connections.",
                  "SmartHopper",
                  "Grasshopper")
        {
            this.RunOnlyOnInputChanges = false;
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Purpose", "P", "A text description of the desired wiring purpose or goal.", GH_ParamAccess.item);
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Success", "S", "True if all suggested connections were created successfully.", GH_ParamAccess.item);
            pManager.AddTextParameter("Reasoning", "R", "AI explanation of why the suggested connections achieve the purpose.", GH_ParamAccess.item);
            pManager.AddTextParameter("Connections", "C", "JSON summary of the connection results.", GH_ParamAccess.item);
        }

        /// <inheritdoc/>
        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new AIGhConnectWorker(this, this.AddRuntimeMessage);
        }

        /// <summary>
        /// Async worker that executes the gh_smart_connect tool.
        /// </summary>
        private sealed class AIGhConnectWorker : AsyncWorkerBase
        {
            private readonly AIGhConnectComponent parent;
            private string purpose = string.Empty;
            private List<string> guids = new List<string>();
            private bool success;
            private string reasoning = string.Empty;
            private string connectionsJson = string.Empty;

            /// <summary>
            /// Initializes a new instance of the <see cref="AIGhConnectWorker"/> class.
            /// </summary>
            public AIGhConnectWorker(
                AIGhConnectComponent parent,
                Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
                : base(parent, addRuntimeMessage)
            {
                this.parent = parent;
            }

            /// <inheritdoc/>
            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                string purposeInput = string.Empty;
                DA.GetData("Purpose", ref purposeInput);
                this.purpose = purposeInput;

                // Get GUIDs from selection (via selecting button)
                this.guids = this.parent.SelectedObjects
                    .Select(obj => obj.InstanceGuid.ToString())
                    .ToList();

                if (this.guids.Count == 0)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No components selected. Use the selecting button to select components on the canvas.");
                    dataCount = 0;
                    return;
                }

                if (string.IsNullOrWhiteSpace(this.purpose))
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No purpose provided. Describe the desired wiring goal.");
                    dataCount = 0;
                    return;
                }

                dataCount = 1;
            }

            /// <inheritdoc/>
            public override async Task DoWorkAsync(CancellationToken token)
            {
                if (this.guids.Count == 0 || string.IsNullOrWhiteSpace(this.purpose))
                {
                    return;
                }

                try
                {
                    Debug.WriteLine($"[AIGhConnectWorker] Starting with {this.guids.Count} GUIDs, purpose: {this.purpose}");

                    var parameters = new JObject
                    {
                        ["guids"] = JArray.FromObject(this.guids),
                        ["purpose"] = this.purpose,
                    };

                    var toolResult = await this.parent.CallAiToolAsync("gh_smart_connect", parameters).ConfigureAwait(false);

                    if (toolResult == null)
                    {
                        this.success = false;
                        this.reasoning = "Tool returned no result.";
                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "gh_smart_connect returned no result.");
                        return;
                    }

                    this.reasoning = toolResult["reasoning"]?.ToString() ?? string.Empty;

                    // Check connection results
                    var connectionResult = toolResult["connectionResult"];
                    if (connectionResult != null)
                    {
                        var failed = connectionResult["failed"] as JArray;
                        this.success = failed == null || failed.Count == 0;
                        this.connectionsJson = connectionResult.ToString();
                    }
                    else
                    {
                        this.success = false;
                        this.connectionsJson = string.Empty;
                    }

                    Debug.WriteLine($"[AIGhConnectWorker] Completed - success={this.success}, reasoning length={this.reasoning.Length}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AIGhConnectWorker] Error: {ex.Message}");
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                    this.success = false;
                }
            }

            /// <inheritdoc/>
            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                this.parent.SetPersistentOutput("Success", new GH_Boolean(this.success), DA);
                this.parent.SetPersistentOutput("Reasoning", new GH_String(this.reasoning), DA);
                this.parent.SetPersistentOutput("Connections", new GH_String(this.connectionsJson), DA);

                message = this.success ? "Connected successfully" : "Connection completed with issues";
            }
        }
    }
}
