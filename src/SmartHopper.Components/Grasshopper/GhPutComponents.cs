/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using Newtonsoft.Json.Linq;
using SmartHopper.Components.Properties;
using SmartHopper.Core.Graph;
using SmartHopper.Core.Grasshopper.Utils;
using SmartHopper.Core.Models.Serialization;
using SmartHopper.Core.Models.Document;
using SmartHopper.Config.Managers;

namespace SmartHopper.Components.Grasshopper
{
    public class GhPutComponents : GH_Component
    {
        private List<string> lastComponentNames = new();

        public GhPutComponents()
            : base("Place Components", "GhPut", "Convert GhJSON to a Grasshopper definition in this file", "SmartHopper", "Grasshopper")
        {
        }

        public override Guid ComponentGuid => new("25E07FD9-382C-48C0-8A97-8BFFAEAD8592");

        protected override Bitmap Icon => Resources.ghput;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("JSON", "J", "JSON", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Run?", "R", "Run this component?", GH_ParamAccess.item, false);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Components", "C", "List of components", GH_ParamAccess.list);
        }

       protected override void SolveInstance(IGH_DataAccess DA)
        {
            // 1. Read “Run?” switch
            bool run = false;
            if (!DA.GetData(1, ref run)) return;
            if (!run)
            {
                if (lastComponentNames.Count > 0)
                    DA.SetDataList(0, lastComponentNames);
                else
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                        "Set Run to True to place components");
                return;
            }

            // 2. Clear previous results and get JSON input
            lastComponentNames.Clear();
            string json = null;
            if (!DA.GetData(0, ref json)) return;

            try
            {
                // 3. Call the AI tool
                var parameters = new JObject { ["json"] = json };
                var toolResult = AIToolManager
                    .ExecuteTool("gh_put", parameters, null)
                    .GetAwaiter()
                    .GetResult() as JObject;

                // 4. Handle errors
                if (toolResult == null || !(toolResult["success"]?.ToObject<bool>() ?? false))
                {
                    var err = toolResult?["error"]?.ToString() ?? "Unknown error";
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                        $"Tool 'gh_put' failed: {err}");
                    return;
                }

                // 5. Extract and output component names
                var componentNames = toolResult["components"]
                    ?.ToObject<List<string>>() ?? new List<string>();
                lastComponentNames = componentNames;
                DA.SetDataList(0, componentNames);
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }
    }
}
