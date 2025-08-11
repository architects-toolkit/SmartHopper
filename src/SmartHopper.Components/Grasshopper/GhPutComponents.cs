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
using System.Drawing;
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;
using SmartHopper.Components.Properties;
using SmartHopper.Infrastructure.AITools;

namespace SmartHopper.Components.Grasshopper
{
    /// <summary>
    /// Grasshopper component for placing components from JSON data.
    /// </summary>
    public class GhPutComponents : GH_Component
    {
        private List<string> lastComponentNames = new ();

        /// <summary>
        /// Initializes a new instance of the <see cref="GhPutComponents"/> class.
        /// </summary>
        public GhPutComponents()
            : base("Place Components", "GhPut", "Convert GhJSON to a Grasshopper components in this file.\n\nNew components will be added at the bottom of the canvas.", "SmartHopper", "Grasshopper")
        {
        }

        /// <summary>
        /// Gets the unique identifier for this component.
        /// </summary>
        public override Guid ComponentGuid => new ("25E07FD9-382C-48C0-8A97-8BFFAEAD8592");

        /// <summary>
        /// Gets the component's icon.
        /// </summary>
        protected override Bitmap Icon => Resources.ghput;

        /// <summary>
        /// Registers the input parameters for this component.
        /// </summary>
        /// <param name="pManager">The parameter manager to register inputs with.</param>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("JSON", "J", "JSON", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Run?", "R", "Run this component?", GH_ParamAccess.item, false);
        }

        /// <summary>
        /// Registers the output parameters for this component.
        /// </summary>
        /// <param name="pManager">The parameter manager to register outputs with.</param>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            // The list of component names output parameter.
            pManager.AddTextParameter("Components", "C", "List of components", GH_ParamAccess.list);
        }

        /// <summary>
        /// Solves the component for the given data access.
        /// </summary>
        /// <param name="DA">The data access object for input/output operations.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // 1. Read "Run?" switch
            bool run = false;
            if (!DA.GetData(1, ref run)) return;
            if (!run)
            {
                if (this.lastComponentNames.Count > 0)
                {
                    DA.SetDataList(0, this.lastComponentNames);
                }
                else
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Set Run to True to place components");
                }
                return;
            }

            // 2. Clear previous results and get JSON input
            this.lastComponentNames.Clear();
            string json = null;
            if (!DA.GetData(0, ref json)) return;

            try
            {
                // 3. Call the AI tool
                var parameters = new JObject { ["json"] = json };
                var toolResult = AIToolManager
                    .ExecuteTool("gh_put", parameters)
                    .GetAwaiter()
                    .GetResult() as JObject;

                var success = toolResult?["success"]?.ToObject<bool>() ?? false;
                var analysis = toolResult?["analysis"]?.ToString();
                // Display analysis messages
                if (!string.IsNullOrEmpty(analysis))
                {
                    GH_RuntimeMessageLevel currentLevel = GH_RuntimeMessageLevel.Remark;
                    foreach (var line in analysis.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var trimmed = line.Trim();
                        if (trimmed == "Errors:")
                        {
                            currentLevel = GH_RuntimeMessageLevel.Error;
                        }
                        else if (trimmed == "Warnings:")
                        {
                            currentLevel = GH_RuntimeMessageLevel.Warning;
                        }
                        else if (trimmed.StartsWith("Information:"))
                        {
                            currentLevel = GH_RuntimeMessageLevel.Remark;
                        }
                        else if (trimmed.StartsWith("- "))
                        {
                            AddRuntimeMessage(currentLevel, trimmed.Substring(2));
                        }
                    }
                }
                if (!success) return;

                // 5. Extract and output component names
                var componentNames = toolResult["components"]
                    ?.ToObject<List<string>>() ?? new List<string>();
                this.lastComponentNames = componentNames;
                DA.SetDataList(0, componentNames);
            }
            catch (Exception ex)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }
    }
}
