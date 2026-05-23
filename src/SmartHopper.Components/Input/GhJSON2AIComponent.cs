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
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Tools;

namespace SmartHopper.Components.Input
{
    /// <summary>
    /// Retrieves selected or all Grasshopper canvas components as GhJSON and wraps into an AIInputPayload.
    /// Uses the gh_get AI tool with SelectingComponentBase for component selection.
    /// </summary>
    public class GhJSON2AIComponent : SelectingStatefulComponentBase
    {
        public override Guid ComponentGuid => new Guid("07254553-A47A-419C-9509-8A97D51A85F9");

        public override GH_Exposure Exposure => GH_Exposure.septenary;

        public GhJSON2AIComponent()
            : base(
                "Canvas to AI",
                "GhJSON2AI",
                "Retrieves selected or all Grasshopper canvas components as GhJSON and wraps into an AIInputPayload.",
                "SmartHopper",
                "Input")
        {
        }

        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Type Filter", "T", "Optional list of classification tokens with include/exclude syntax: 'params', 'components', 'startnodes', 'endnodes', 'middlenodes', 'isolatednodes'. Prefix '+' to include, '-' to exclude.", GH_ParamAccess.list, string.Empty);
            pManager.AddTextParameter("Category Filter", "C", "Optional list of category filters by Grasshopper category or subcategory (e.g. 'Maths', 'Params', 'Script'). Use '+name' to include and '-name' to exclude.", GH_ParamAccess.list, string.Empty);
            pManager.AddTextParameter("Attribute Filter", "F", "Optional list of filters by tags: 'error', 'warning', 'remark', 'selected', 'unselected', 'enabled', 'disabled', 'previewon', 'previewoff'. Prefix '+' to include, '-' to exclude.", GH_ParamAccess.list, string.Empty);
            pManager.AddIntegerParameter("Connection Depth", "D", "Depth of connections to include: 0 = only matching components; 1 = direct connections; higher = further hops.", GH_ParamAccess.item, 0);
            pManager.AddBooleanParameter("Include Metadata", "M", "Include document metadata (timestamps, Rhino/Grasshopper versions, plugin dependencies)", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Include Runtime Data", "Dt", "Include runtime/volatile data (actual values flowing through outputs). This is token-expansive!", GH_ParamAccess.item, false);
        }

        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new AIInputPayloadParameter(), "Input >", ">", "AIInputPayload containing the canvas as GhJSON.", GH_ParamAccess.item);
            pManager.AddTextParameter("GhJSON", "J", "Canvas definition in GhJSON format.", GH_ParamAccess.item);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new GhCanvasWorker(this, this.AddRuntimeMessage);
        }

        private sealed class GhCanvasWorker : AsyncWorkerBase
        {
            private readonly GhJSON2AIComponent parent;

            public GhCanvasWorker(GhJSON2AIComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
                : base(parent, addRuntimeMessage)
            {
                this.parent = parent;
            }

            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                dataCount = 1;
            }

            public override Task DoWorkAsync(CancellationToken token)
            {
                return Task.CompletedTask;
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                message = "Canvas processing complete";
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

        protected override void OnStateProcessing(IGH_DataAccess DA)
        {
            int connectionDepth = 0;
            DA.GetData(3, ref connectionDepth);

            bool includeMetadata = false;
            DA.GetData(4, ref includeMetadata);

            bool includeRuntimeData = false;
            DA.GetData(5, ref includeRuntimeData);

            var typeFilters = new List<string>();
            DA.GetDataList(0, typeFilters);

            var categoryFilters = new List<string>();
            DA.GetDataList(1, categoryFilters);

            var attrFilters = new List<string>();
            DA.GetDataList(2, attrFilters);

            try
            {
                var parameters = new JObject
                {
                    ["typeFilter"] = JArray.FromObject(typeFilters),
                    ["categoryFilter"] = JArray.FromObject(categoryFilters),
                    ["attrFilters"] = JArray.FromObject(attrFilters),
                    ["connectionDepth"] = connectionDepth,
                    ["includeMetadata"] = includeMetadata,
                    ["includeRuntimeData"] = includeRuntimeData,
                    ["guidFilter"] = JArray.FromObject(this.SelectedObjects.Select(o => o.InstanceGuid.ToString())),
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

                var toolResult = ToolCallResult.FromAIReturn(toolCall.Exec().GetAwaiter().GetResult());

                if (toolResult.Result == null)
                {
                    this.SetPersistentRuntimeMessage("gh_get_error", GH_RuntimeMessageLevel.Error, "Tool 'gh_get' returned no result.");
                    return;
                }

                string ghjson = toolResult["ghjson"]?.ToString() ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(ghjson))
                {
                    var payload = AIInputPayload.FromText(ghjson);
                    var ghPayload = new GH_AIInputPayload(payload);

                    this.SetPersistentOutput("Payload", ghPayload, DA);
                    this.SetPersistentOutput("GhJSON", ghjson, DA);

                    DA.SetData(0, ghPayload);
                    DA.SetData(1, ghjson);
                }
                else
                {
                    this.SetPersistentRuntimeMessage("no_content", GH_RuntimeMessageLevel.Warning, "No canvas content was retrieved.");
                }
            }
            catch (Exception ex)
            {
                this.SetPersistentRuntimeMessage("canvas_error", GH_RuntimeMessageLevel.Error, $"Error retrieving canvas: {ex.Message}");
            }
        }
    }
}
