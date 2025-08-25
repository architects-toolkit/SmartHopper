/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Components.Properties;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Tools;

namespace SmartHopper.Components.Grasshopper
{
    /// <summary>
    /// Component that converts selected or all Grasshopper components to GhJSON format.
    /// Supports optional filtering by runtime messages (errors, warnings, and remarks), component states (selected, enabled, disabled), preview capability (previewcapable, notpreviewcapable), preview state (previewon, previewoff), and classification by object type via Type filter (params, components, input, output, processing, isolated).
    /// </summary>
    public class GhGetComponents : SelectingComponentBase
    {
        private List<string> lastComponentNames = new List<string>();
        private List<string> lastComponentGuids = new List<string>();
        private string lastJsonOutput = "";

        public GhGetComponents()
            : base("Get Components", "GhGet",
                  "Convert Grasshopper components to GhJSON format, with optional filters",
                  "SmartHopper", "Grasshopper")
        {
        }

        public override Guid ComponentGuid => new Guid("E7BB7C92-9565-584C-C1DD-425E77651FD8");

        protected override Bitmap Icon => Resources.ghget;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Type filter", "T", "Optional list of classification tokens with include/exclude syntax: 'params', 'components', 'inputcomponents', 'outputcomponents', 'processingcomponents', 'isolatedcomponents'. Prefix '+' to include, '-' to exclude.", GH_ParamAccess.list, "");
            pManager.AddTextParameter("Attribute Filter", "F", "Optional list of filters by tags: 'error', 'warning', 'remark', 'selected', 'unselected', 'enabled', 'disabled', 'previewon', 'previewoff'. Prefix '+' to include, '-' to exclude.", GH_ParamAccess.list, "");
            pManager.AddIntegerParameter("Connection Depth", "D", "Optional depth of connections to include: 0 = only matching components; 1 = direct connections; higher = further hops.", GH_ParamAccess.item, 0);
            pManager.AddBooleanParameter("Run?", "R", "Run this component?", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Names", "N", "List of names", GH_ParamAccess.list);
            pManager.AddTextParameter("Guids", "G", "List of guids", GH_ParamAccess.list);
            pManager.AddTextParameter("JSON", "J", "Details in JSON format", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // get input run
            object runObject = null;
            if (!DA.GetData(3, ref runObject)) return;

            int connectionDepth = 0;
            DA.GetData(2, ref connectionDepth);

            if (!(runObject is GH_Boolean run))
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Run must be a boolean");
                return;
            }

            if (!run.Value)
            {
                if (this.lastComponentNames.Count > 0)
                {
                    DA.SetDataList(0, this.lastComponentNames);
                    DA.SetDataList(1, this.lastComponentGuids);
                    DA.SetData(2, this.lastJsonOutput);
                }
                else
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Set Run to True to execute the component");
                }

                return;
            }

            // Clear previous results when starting a new run
            this.lastComponentNames.Clear();
            this.lastComponentGuids.Clear();
            this.lastJsonOutput = string.Empty;

            try
            {
                var filters = new List<string>();
                DA.GetDataList(1, filters);
                var typeFilters = new List<string>();
                DA.GetDataList(0, typeFilters);
                var parameters = new JObject
                {
                    ["attrFilters"] = JArray.FromObject(filters),
                    ["typeFilter"] = JArray.FromObject(typeFilters),
                    ["connectionDepth"] = connectionDepth,
                    ["guidFilter"] = JArray.FromObject(this.SelectedObjects.Select(o => o.InstanceGuid.ToString())),
                };

                // Create AIToolCall and execute
                var toolCallInteraction = new AIInteractionToolCall
                {
                    Name = "gh_get",
                    Arguments = parameters,
                    Agent = AIAgent.Assistant,
                };

                var toolCall = new AIToolCall();
                toolCall.Endpoint = "gh_get";
                toolCall.FromToolCallInteraction(toolCallInteraction);

                var aiResult = toolCall.Exec().GetAwaiter().GetResult();
                var toolResultInteraction = aiResult.Body.GetLastInteraction(AIAgent.ToolResult) as AIInteractionToolResult;
                var toolResult = toolResultInteraction?.Result;
                if (toolResult == null)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Tool 'gh_get' did not return a valid result");
                    return;
                }
                var componentNames = toolResult["names"]?.ToObject<List<string>>() ?? new List<string>();
                var componentGuids = toolResult["guids"]?.ToObject<List<string>>() ?? new List<string>();
                var json = toolResult["json"]?.ToString() ?? string.Empty;
                this.lastComponentNames = componentNames;
                this.lastComponentGuids = componentGuids;
                this.lastJsonOutput = json;
                DA.SetDataList(0, componentNames);
                DA.SetDataList(1, componentGuids);
                DA.SetData(2, json);
                return;
            }
            catch (Exception ex)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }
    }
}

