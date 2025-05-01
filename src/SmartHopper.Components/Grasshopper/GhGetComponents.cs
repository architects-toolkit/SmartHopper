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
using SmartHopper.Config.Managers;
using SmartHopper.Core.ComponentBase;

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
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Run must be a boolean");
                return;
            }

            if (!run.Value)
            {
                if (lastComponentNames.Count > 0)
                {
                    DA.SetDataList(0, lastComponentNames);
                    DA.SetDataList(1, lastComponentGuids);
                    DA.SetData(2, lastJsonOutput);
                }
                else
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Set Run to True to execute the component");
                }
                return;
            }

            // Clear previous results when starting a new run
            lastComponentNames.Clear();
            lastComponentGuids.Clear();
            lastJsonOutput = "";

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
                    ["guidFilter"] = JArray.FromObject(SelectedObjects.Select(o => o.InstanceGuid.ToString())),
                };
                var toolResult = AIToolManager.ExecuteTool("gh_get", parameters, null).GetAwaiter().GetResult() as JObject;
                if (toolResult == null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Tool 'gh_get' did not return a valid result");
                    return;
                }
                var componentNames = toolResult["names"]?.ToObject<List<string>>() ?? new List<string>();
                var componentGuids = toolResult["guids"]?.ToObject<List<string>>() ?? new List<string>();
                var json = toolResult["json"]?.ToString() ?? string.Empty;
                lastComponentNames = componentNames;
                lastComponentGuids = componentGuids;
                lastJsonOutput = json;
                DA.SetDataList(0, componentNames);
                DA.SetDataList(1, componentGuids);
                DA.SetData(2, json);
                return;
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }
    }
}
