/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;
using SmartHopper.Config.Managers;
using SmartHopper.Components.Properties;
using System;
using System.Collections.Generic;
#if WINDOWS
using System.Drawing;
#else
using Eto.Drawing;
#endif

namespace SmartHopper.Components.Grasshopper
{
    /// <summary>
    /// Component that retrieves all installed Grasshopper component types as JSON.
    /// Supports optional filtering by category.
    /// </summary>
    public class GhRetrieveComponents : GH_Component
    {
        private List<string> lastNames = new List<string>();
        private List<string> lastGuids = new List<string>();
        private string lastJson = string.Empty;

        public GhRetrieveComponents()
            : base(
                  "Retrieve Components", "GhRetrieveComponents",
                  "Retrieve all available Grasshopper components in your environment as JSON with optional category filter.",
                  "SmartHopper", "Grasshopper"
                  )
        {
        }

        public override Guid ComponentGuid => new Guid("D2F1E3B4-C5A6-7D8E-9A0B-C1D2E3F4A5B6");

        protected override Bitmap Icon => Resources.ghget;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Category Filter", "C",
                "Optional list of categories with include/exclude syntax. E.g. ['+Math', '-Params'].",
                GH_ParamAccess.list, "");
            pManager.AddBooleanParameter("Run?", "R", "Run this component?", GH_ParamAccess.item, false);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Names", "N", "List of component type names.", GH_ParamAccess.list);
            pManager.AddTextParameter("Guids", "G", "List of component type GUIDs.", GH_ParamAccess.list);
            pManager.AddTextParameter("JSON", "J", "Component type details in JSON format.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            bool run = false;
            if (!DA.GetData(1, ref run)) return;

            var filters = new List<string>();
            DA.GetDataList(0, filters);

            if (!run)
            {
                if (lastNames.Count > 0)
                {
                    DA.SetDataList(0, lastNames);
                    DA.SetDataList(1, lastGuids);
                    DA.SetData(2, lastJson);
                }
                else
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                        "Set Run to True to execute the component");
                }
                return;
            }

            var parameters = new JObject
            {
                ["categoryFilter"] = JArray.FromObject(filters)
            };

            try
            {
                var toolResult = AIToolManager.ExecuteTool("ghretrievecomponents", parameters, null)
                    .GetAwaiter().GetResult() as JObject;
                if (toolResult == null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                        "Tool 'ghretrievecomponents' did not return a valid result");
                    return;
                }

                var names = toolResult["names"]?.ToObject<List<string>>() ?? new List<string>();
                var guids = toolResult["guids"]?.ToObject<List<string>>() ?? new List<string>();
                var json = toolResult["json"]?.ToString() ?? string.Empty;

                lastNames = names;
                lastGuids = guids;
                lastJson = json;

                DA.SetDataList(0, names);
                DA.SetDataList(1, guids);
                DA.SetData(2, json);
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }
    }
}
