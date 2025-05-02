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
using System.Windows.Forms;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Config.Managers;
using SmartHopper.Core.ComponentBase;

namespace SmartHopper.Components.Grasshopper
{
    /// <summary>
    /// Component that arranges selected Grasshopper objects into a tidy grid layout based on dependencies.
    /// </summary>
    public class GhTidyUpComponents : SelectingComponentBase
    {
        private List<string> LastErrors = new List<string>();

        public GhTidyUpComponents()
          : base("Tidy Up", "GhTidyUp",
                 "Organize selected components into a tidy grid layout",
                 "SmartHopper", "Grasshopper")
        {
        }

        public override Guid ComponentGuid => new Guid("D4C8A9E5-B123-4F67-8C90-1234567890AB");
        
        protected override Bitmap Icon => null;

        public void EnableSelectionMode()
        {
            base.EnableSelectionMode();
            var canvas = Instances.ActiveCanvas;
            if (canvas == null) return;
            canvas.ContextMenuStrip?.Hide();
        }

        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Run?", "R", "Run this component?", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Errors", "E", "List of errors during tidy up", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            object runObj = null;
            if (!DA.GetData(0, ref runObj)) return;
            if (!(runObj is GH_Boolean run))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Run must be a boolean");
                return;
            }
            if (!run.Value)
            {
                if (LastErrors.Count > 0)
                    DA.SetDataList(0, LastErrors);
                else
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Set Run to True to execute tidy up");
                return;
            }
            LastErrors.Clear();
            var guids = SelectedObjects.Select(o => o.InstanceGuid.ToString()).ToList();
            if (!guids.Any())
            {
                LastErrors.Add("No components selected");
                DA.SetDataList(0, LastErrors);
                return;
            }
            try
            {
                var parameters = new JObject { ["guids"] = JArray.FromObject(guids) };
                var result = AIToolManager.ExecuteTool("gh_tidy_up", parameters, null)
                                  .GetAwaiter().GetResult() as JObject;
                if (result == null)
                    LastErrors.Add("Tool 'gh_tidy_up' returned invalid result");
                else if (result["success"]?.ToObject<bool>() == false)
                    LastErrors.Add(result["error"]?.ToString() ?? "Unknown error");
                else
                {
                    var moved = result["moved"]?.ToObject<List<string>>() ?? new List<string>();
                    var failed = guids.Except(moved);
                    foreach (var g in failed)
                        LastErrors.Add($"Component {g} not moved");
                }
            }
            catch (Exception ex)
            {
                LastErrors.Add(ex.Message);
            }
            DA.SetDataList(0, LastErrors);
        }
    }
}
