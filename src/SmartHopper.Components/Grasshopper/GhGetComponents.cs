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
using SmartHopper.ProviderSdk.Diagnostics;

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
            pManager.AddBooleanParameter("Viewport Only", "V", "Only include components visible in the current canvas viewport", GH_ParamAccess.item, false);
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

            private List<string> typeFilters = new List<string>();
            private List<string> categoryFilters = new List<string>();
            private List<string> attrFilters = new List<string>();
            private int connectionDepth;
            private int count;
            private bool includeMetadata;
            private bool includeRuntimeData;

            /// <summary>Whether to restrict results to components visible in the canvas viewport.</summary>
            private bool viewportOnly;
            private List<IGH_DocumentObject> selectedObjects = new List<IGH_DocumentObject>();

            private List<string> names = new List<string>();
            private List<string> guids = new List<string>();
            private string json = string.Empty;

            public GhGetWorker(GhGetComponents parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
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
                DA.GetData(4, ref this.count);
                DA.GetData(5, ref this.includeMetadata);
                DA.GetData(6, ref this.includeRuntimeData);
                DA.GetData(7, ref this.viewportOnly);
                this.selectedObjects = new List<IGH_DocumentObject>(this.parent.SelectedObjects);
            }

            public override async Task DoWorkAsync(CancellationToken token)
            {
                try
                {
                    var parameters = new JObject
                    {
                        ["attrFilters"] = JArray.FromObject(this.attrFilters),
                        ["typeFilter"] = JArray.FromObject(this.typeFilters),
                        ["categoryFilter"] = JArray.FromObject(this.categoryFilters),
                        ["connectionDepth"] = this.connectionDepth,
                        ["pageSize"] = this.count,
                        ["page"] = 1,
                        ["includeMetadata"] = this.includeMetadata,
                        ["guidFilter"] = JArray.FromObject(this.selectedObjects.Select(o => o.InstanceGuid.ToString())),
                        ["includeRuntimeData"] = this.includeRuntimeData,
                        ["viewportOnly"] = this.viewportOnly,
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

                    var aiReturn = await toolCall.Exec();
                    var toolResult = ToolCallResult.FromAIReturn(aiReturn);
                    if (toolResult.Result == null)
                    {
                        this.CollectMessage(SHRuntimeMessageSeverity.Error, "Tool 'gh_get' did not return a valid result");
                        return;
                    }

                    // Surface any warning/info messages from the tool (e.g. viewportOnly with no canvas)
                    foreach (var msg in aiReturn.Messages.Where(m => m?.Severity != SHRuntimeMessageSeverity.Error))
                    {
                        this.CollectMessage(msg.Severity, msg.Message);
                    }

                    // Surface pagination info so users know when the output is truncated
                    var pagination = toolResult["pagination"] as JObject;
                    if (pagination != null)
                    {
                        var returned = pagination["returnedComponents"]?.ToObject<int?>() ?? 0;
                        var total = pagination["totalComponents"]?.ToObject<int?>() ?? 0;
                        var page = pagination["page"]?.ToObject<int?>() ?? 1;
                        var pageCount = pagination["pageCount"]?.ToObject<int?>() ?? 1;
                        if (total > returned && total > 0)
                        {
                            this.CollectMessage(SHRuntimeMessageSeverity.Info, $"Returning {returned} of {total} components (page {page} of {pageCount}). Boundary connections to components outside this page are preserved.");
                        }
                    }

                    this.names = toolResult["names"]?.ToObject<List<string>>() ?? new List<string>();
                    this.guids = toolResult["guids"]?.ToObject<List<string>>() ?? new List<string>();
                    this.json = toolResult["ghjson"]?.ToString() ?? string.Empty;
                }
                catch (Exception ex)
                {
                    this.CollectMessage(SHRuntimeMessageSeverity.Error, $"gh_get failed: {ex.Message}");
                }
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                message = "GhGet processing complete";

                this.parent.SetPersistentOutput("Names", this.names, DA);
                this.parent.SetPersistentOutput("Guids", this.guids, DA);
                this.parent.SetPersistentOutput("GhJSON", this.json, DA);

                DA.SetDataList(0, this.names);
                DA.SetDataList(1, this.guids);
                DA.SetData(2, this.json);
            }
        }
    }
}