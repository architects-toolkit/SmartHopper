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
using SmartHopper.Core.DataTree;
using SmartHopper.Core.Models;
using SmartHopper.Core.Types;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;

namespace SmartHopper.Components.Grasshopper
{
    /// <summary>
    /// Component that converts selected or all Grasshopper components to GhJSON format.
    /// Supports optional filtering by runtime messages (errors, warnings, and remarks), component states (selected, enabled, disabled), preview capability (previewcapable, notpreviewcapable), preview state (previewon, previewoff), and classification by object type via Type filter (params, components, startnodes, endnodes, middlenodes, isolatednodes).
    /// Optionally includes document metadata (timestamps, Rhino/Grasshopper versions, plugin dependencies).
    /// </summary>
    public class GhGetComponents : SelectingStatefulComponentBase
    {
        public GhGetComponents()
            : base(
                "Get GhJSON",
                "GhGet",
                "Convert Grasshopper components to GhJSON format, with optional filters",
                "SmartHopper",
                "Grasshopper")
        {
            this.RunOnlyOnInputChanges = false;
        }

        public override Guid ComponentGuid => new Guid("E7BB7C92-9565-584C-C1DD-425E77651FD8");

        protected override Bitmap Icon => Resources.ghget;

        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Type filter", "T", "Optional list of classification tokens with include/exclude syntax: 'params', 'components', 'startnodes', 'endnodes', 'middlenodes', 'isolatednodes'. Prefix '+' to include, '-' to exclude.", GH_ParamAccess.list, string.Empty);
            pManager.AddTextParameter("Category Filter", "C", "Optional list of category filters by Grasshopper category or subcategory (e.g. 'Maths', 'Params', 'Script'). Use '+name' to include and '-name' to exclude.", GH_ParamAccess.list, string.Empty);
            pManager.AddTextParameter("Attribute Filter", "F", "Optional list of filters by tags: 'error', 'warning', 'remark', 'selected', 'unselected', 'enabled', 'disabled', 'previewon', 'previewoff'. Prefix '+' to include, '-' to exclude.", GH_ParamAccess.list, string.Empty);
            pManager.AddIntegerParameter("Connection Depth", "D", "Optional depth of connections to include: 0 = only matching components; 1 = direct connections; higher = further hops.", GH_ParamAccess.item, 0);
            pManager.AddIntegerParameter("Count", "Ct", "Maximum number of components to retrieve. Default is 100.", GH_ParamAccess.item, 100);
            pManager.AddBooleanParameter("Include Metadata", "M", "Include document metadata (timestamps, Rhino/Grasshopper versions, plugin dependencies)", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Include Runtime Data", "Dt", "Include runtime/volatile data (actual values flowing through outputs). This is token-expansive!", GH_ParamAccess.item, false);
        }

        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Names", "N", "List of names", GH_ParamAccess.list);
            pManager.AddTextParameter("Guids", "G", "List of guids", GH_ParamAccess.list);
            pManager.AddTextParameter("GhJSON", "J", "Details in GhJSON format", GH_ParamAccess.item);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new GhGetWorker(this, this.AddRuntimeMessage);
        }

        private sealed class GhGetWorker : AsyncWorkerBase
        {
            private readonly GhGetComponents parent;

            public GhGetWorker(GhGetComponents parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
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
                message = "GhGet processing complete";
            }
        }

        protected override void OnStateCompleted(IGH_DataAccess DA)
        {
            base.OnStateCompleted(DA);

            var componentNames = this.GetPersistentOutput<List<string>>("Names", new List<string>());
            var componentGuids = this.GetPersistentOutput<List<string>>("Guids", new List<string>());
            var json = this.GetPersistentOutput<string>("JSON", string.Empty);

            DA.SetDataList(0, componentNames);
            DA.SetDataList(1, componentGuids);
            DA.SetData(2, json);
        }

        protected override void OnStateWaiting(IGH_DataAccess DA)
        {
            base.OnStateWaiting(DA);

            var componentNames = this.GetPersistentOutput<List<string>>("Names", new List<string>());
            var componentGuids = this.GetPersistentOutput<List<string>>("Guids", new List<string>());
            var json = this.GetPersistentOutput<string>("JSON", string.Empty);

            DA.SetDataList(0, componentNames);
            DA.SetDataList(1, componentGuids);
            DA.SetData(2, json);
        }

        protected override void OnStateProcessing(IGH_DataAccess DA)
        {
            int connectionDepth = 0;
            DA.GetData(3, ref connectionDepth);

            private List<string> typeFilters = new List<string>();
            private List<string> categoryFilters = new List<string>();
            private List<string> attrFilters = new List<string>();
            private int connectionDepth;
            private int count;
            private bool includeMetadata;
            private bool includeRuntimeData;

            bool includeRuntimeData = false;
            DA.GetData(5, ref includeRuntimeData);

            try
            {
                try
                {
                    ["attrFilters"] = JArray.FromObject(filters),
                    ["typeFilter"] = JArray.FromObject(typeFilters),
                    ["categoryFilter"] = JArray.FromObject(categoryFilters),
                    ["connectionDepth"] = connectionDepth,
                    ["includeMetadata"] = includeMetadata,
                    ["guidFilter"] = JArray.FromObject(this.SelectedObjects.Select(o => o.InstanceGuid.ToString())),
                    ["includeRuntimeData"] = includeRuntimeData,
                };

                var toolCallInteraction = new AIInteractionToolCall
                {
                    Name = "gh_get",
                    Arguments = parameters,
                    Agent = AIAgent.Assistant,
                };

                    var toolCall = new AIToolCall();
                    toolCall.Endpoint = "gh_get";
                    toolCall.FromToolCallInteraction(toolCallInteraction);
                    toolCall.SkipMetricsValidation = true;

                var toolResult = ToolCallResult.FromAIReturn(toolCall.Exec().GetAwaiter().GetResult());
                if (toolResult.Result == null)
                {
                    this.SetPersistentRuntimeMessage("gh_get_error", GH_RuntimeMessageLevel.Error, "Tool 'gh_get' did not return a valid result");
                    return;
                }

                var componentNames = toolResult["names"]?.ToObject<List<string>>() ?? new List<string>();
                var componentGuids = toolResult["guids"]?.ToObject<List<string>>() ?? new List<string>();
                var json = toolResult["ghjson"]?.ToString() ?? string.Empty;

                this.SetPersistentOutput("Names", componentNames, DA);
                this.SetPersistentOutput("Guids", componentGuids, DA);
                this.SetPersistentOutput("JSON", json, DA);

                DA.SetDataList(0, componentNames);
                DA.SetDataList(1, componentGuids);
                DA.SetData(2, json);
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                this.SetPersistentRuntimeMessage("gh_get_exception", GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }
    }
}
