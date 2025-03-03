/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json;
using SmartHopper.Components.Properties;
using SmartHopper.Core.Grasshopper;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SmartHopper.Components.Grasshopper
{
    public class GhGetComponent : GH_Component
    {
        private List<string> lastComponentNames = new List<string>();
        private List<string> lastComponentGuids = new List<string>();
        private string lastJsonOutput = "";

        public GhGetComponent()
            : base("Get Components", "GhGet", "Convert this Grasshopper file to GhJSON format", "SmartHopper", "Grasshopper")
        {
        }

        public override Guid ComponentGuid => new Guid("E6AA6B91-8454-473B-B0CC-314D66540FC7");

        protected override System.Drawing.Bitmap Icon => Resources.ghget;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
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
            if (!DA.GetData(0, ref runObject)) return;

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
                // Get all objects (components) in the active canvas
                var objects = GHCanvasUtils.GetCurrentObjects();

                // Get the details of each object
                var document = GHDocumentUtils.GetObjectsDetails(objects);

                // Get unique component names
                var componentNames = document.Components.Select(c => c.Name).Distinct().ToList();

                // Get unique component guids
                var componentGuids = document.Components.Select(c => c.ComponentGuid.ToString()).Distinct().ToList();

                // Convert to JSON
                var json = JsonConvert.SerializeObject(document, Formatting.None);

                // Store results
                lastComponentNames = componentNames;
                lastComponentGuids = componentGuids;
                lastJsonOutput = json;

                // Set outputs
                DA.SetDataList(0, componentNames);
                DA.SetDataList(1, componentGuids);
                DA.SetData(2, json);
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }
    }
}
