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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;
using SmartHopper.Components.Properties;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Core.Models;
using SmartHopper.Core.Types;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.Diagnostics;

namespace SmartHopper.Components.Input
{
    /// <summary>
    /// Retrieves selected or all Grasshopper canvas components as GhJSON and wraps into an AIInputPayload.
    /// Uses the gh_get AI tool with SelectingComponentBase for component selection.
    /// </summary>
    public class GhJSON2AIComponent : SelectingStatefulComponentBase
    {
        public override Guid ComponentGuid => new Guid("07254553-A47A-419C-9509-8A97D51A85F9");

        protected override Bitmap Icon => Resources.GhJSON2AI;

        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        public GhJSON2AIComponent()
            : base(
                "Canvas to AI",
                "GhJSON2AI",
                "Export the current Grasshopper canvas (or selected/filtered objects) as GhJSON and package it to wire into any AI output component.",
                "SmartHopper",
                "B. Input")
        {
        }

        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Type Filter", "T", "Optional object-type filters ('params', 'components', 'startnodes', 'endnodes', 'middlenodes', 'isolatednodes'). Prefix with '+' to include or '-' to exclude.", GH_ParamAccess.list, string.Empty);
            pManager.AddTextParameter("Category Filter", "C", "Optional Grasshopper category or subcategory filters (e.g., '+Maths', '-Params', '+Script').", GH_ParamAccess.list, string.Empty);
            pManager.AddTextParameter("Attribute Filter", "F", "Optional state and message filters ('error', 'warning', 'remark', 'selected', 'unselected', 'enabled', 'disabled', 'previewon', 'previewoff'). Prefix with '+' to include or '-' to exclude.", GH_ParamAccess.list, string.Empty);
            pManager.AddIntegerParameter("Connection Depth", "D", "How many wire hops of connected neighbors to include around matching components (0 = matching components only, 1 = direct neighbors, higher = further hops).", GH_ParamAccess.item, 0);
            pManager.AddBooleanParameter("Include Metadata", "M", "When true, includes document metadata (timestamps, Rhino/Grasshopper versions, file metadata, and plugin dependencies).", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Include Runtime Data", "Dt", "When true, includes live output values flowing through parameters (can significantly increase document size and token usage).", GH_ParamAccess.item, false);
        }

        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new AIInputPayloadParameter(), "Input >", ">", "Packaged canvas definition (GhJSON) ready to wire into any AI output component's 'Input >' parameter.", GH_ParamAccess.item);
            pManager.AddTextParameter("GhJSON", "J", "Exported Grasshopper definition in GhJSON format.", GH_ParamAccess.item);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new GhCanvasWorker(this, this.AddRuntimeMessage);
        }

        private sealed class GhCanvasWorker : AsyncWorkerBase
        {
            private readonly GhJSON2AIComponent parent;

            private List<string> typeFilters = new List<string>();
            private List<string> categoryFilters = new List<string>();
            private List<string> attrFilters = new List<string>();
            private int connectionDepth;
            private bool includeMetadata;
            private bool includeRuntimeData;
            private List<string> selectedGuids = new List<string>();

            private GH_AIInputPayload payload;
            private string ghjson = string.Empty;

            public GhCanvasWorker(GhJSON2AIComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
                : base(parent, addRuntimeMessage)
            {
                this.parent = parent;
            }

            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                dataCount = 1;
                DA.GetDataList(0, this.typeFilters);
                DA.GetDataList(1, this.categoryFilters);
                DA.GetDataList(2, this.attrFilters);
                DA.GetData(3, ref this.connectionDepth);
                DA.GetData(4, ref this.includeMetadata);
                DA.GetData(5, ref this.includeRuntimeData);
                this.selectedGuids = this.parent.SelectedObjects.Select(o => o.InstanceGuid.ToString()).ToList();
            }

            public override async Task DoWorkAsync(CancellationToken token)
            {
                try
                {
                    var parameters = new JObject
                    {
                        ["typeFilter"] = JArray.FromObject(this.typeFilters),
                        ["categoryFilter"] = JArray.FromObject(this.categoryFilters),
                        ["attrFilters"] = JArray.FromObject(this.attrFilters),
                        ["connectionDepth"] = this.connectionDepth,
                        ["includeMetadata"] = this.includeMetadata,
                        ["includeRuntimeData"] = this.includeRuntimeData,
                        ["instanceGuids"] = JArray.FromObject(this.selectedGuids),
                    };

                    var toolCallInteraction = new AIInteractionToolCall
                    {
                        Name = "gh_get",
                        Arguments = parameters,
                        Agent = AIAgent.Assistant,
                    };

                    var toolCall = new AIToolCall
                    {
                        Endpoint = "gh_get",
                    };

                    toolCall.FromToolCallInteraction(toolCallInteraction);
                    toolCall.SkipMetricsValidation = true;

                    var aiReturn = await toolCall.Exec().ConfigureAwait(false);
                    var toolResult = ToolCallResult.FromAIReturn(aiReturn);

                    if (toolResult.Result == null)
                    {
                        this.CollectMessage(SHRuntimeMessageSeverity.Error, "Tool 'gh_get' returned no result.");
                        return;
                    }

                    foreach (var msg in aiReturn.Messages.Where(m => m?.Severity != SHRuntimeMessageSeverity.Error))
                    {
                        this.CollectMessage(msg.Severity, msg.Message);
                    }

                    string ghjson = toolResult["ghjson"]?.ToString() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(ghjson))
                    {
                        this.CollectMessage(SHRuntimeMessageSeverity.Warning, "No canvas content was retrieved.");
                        return;
                    }

                    this.ghjson = ghjson;
                    this.payload = new GH_AIInputPayload(AIInputPayload.FromText(ghjson));
                }
                catch (Exception ex)
                {
                    this.CollectMessage(SHRuntimeMessageSeverity.Error, $"Error retrieving canvas: {ex.Message}");
                }
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                message = "Canvas processing complete";

                if (this.payload == null)
                {
                    return;
                }

                this.parent.SetPersistentOutput("Payload", this.payload, DA);
                this.parent.SetPersistentOutput("GhJSON", this.ghjson, DA);
                DA.SetData(0, this.payload);
                DA.SetData(1, this.ghjson);
            }
        }

        protected override void OnStateCompleted(IGH_DataAccess DA)
        {
            base.OnStateCompleted(DA);

            var payload = this.GetPersistentOutput<GH_AIInputPayload>("Payload");
            var ghjson = this.GetPersistentOutput<string>("GhJSON", string.Empty);

            if (payload != null)
            {
                DA.SetData(0, payload);
                DA.SetData(1, ghjson);
            }
        }

        protected override void OnStateWaiting(IGH_DataAccess DA)
        {
            base.OnStateWaiting(DA);

            var payload = this.GetPersistentOutput<GH_AIInputPayload>("Payload");
            var ghjson = this.GetPersistentOutput<string>("GhJSON", string.Empty);

            if (payload != null)
            {
                DA.SetData(0, payload);
                DA.SetData(1, ghjson);
            }
        }

    }
}
