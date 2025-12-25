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
using SmartHopper.Core.ComponentBase;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Tools;

namespace SmartHopper.Components.Grasshopper
{
    /// <summary>
    /// Component that arranges selected Grasshopper objects into a tidy grid layout based on dependencies.
    /// </summary>
    public class GhTidyUpComponents : SelectingComponentBase
    {
        private List<string> LastErrors = new List<string>();

        /// <summary>
        /// Initializes a new instance of the <see cref="GhTidyUpComponents"/> class.
        /// </summary>
        public GhTidyUpComponents()
          : base("Tidy Up", "GhTidyUp",
                 "Organize selected components into a tidy grid layout\n\n!!! THIS IS STILL EXPERIMENTAL, IT MIGHT MESS UP YOUR DOCUMENT !!!",
                 "SmartHopper", "Grasshopper")
        {
        }

        /// <summary>
        /// Gets the unique identifier for this component.
        /// </summary>
        public override Guid ComponentGuid => new Guid("D4C8A9E5-B123-4F67-8C90-1234567890AB");

        /// <summary>
        /// Gets the component's icon.
        /// </summary>
        protected override Bitmap Icon => Properties.Resources.tidyup;

        /// <summary>
        /// Enables the selection mode for this component.
        /// </summary>
        public void EnableSelectionMode()
        {
            base.EnableSelectionMode();
            var canvas = Instances.ActiveCanvas;
            if (canvas == null) return;
            canvas.ContextMenuStrip?.Hide();
        }

        /// <summary>
        /// Appends additional menu items to the component's context menu.
        /// </summary>
        /// <param name="menu">The menu to append items to.</param>
        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
        }

        /// <summary>
        /// Registers the input parameters for this component.
        /// </summary>
        /// <param name="pManager">The parameter manager to register inputs with.</param>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Run?", "R", "Run this component?", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers the output parameters for this component.
        /// </summary>
        /// <param name="pManager">The parameter manager to register outputs with.</param>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Errors", "E", "List of errors during tidy up", GH_ParamAccess.list);
        }

        /// <summary>
        /// Solves the component for the given data access.
        /// </summary>
        /// <param name="DA">The data access object for input/output operations.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            object runObj = null;
            if (!DA.GetData(0, ref runObj))
            {
                return;
            }

            if (!(runObj is GH_Boolean run))
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Run must be a boolean");
                return;
            }

            this.AddRuntimeMessage(
                GH_RuntimeMessageLevel.Warning,
                "Heads up: this component is still in alpha and might cheekily mess up your file instead of tidying it... for now!");

            if (!run.Value)
            {
                if (this.LastErrors.Count > 0)
                    DA.SetDataList(0, this.LastErrors);
                else
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Set Run to True to execute tidy up");
                return;
            }

            this.LastErrors.Clear();
            var guids = this.SelectedObjects.Select(o => o.InstanceGuid.ToString()).ToList();
            if (!guids.Any())
            {
                this.LastErrors.Add("No components selected");
                DA.SetDataList(0, this.LastErrors);
                return;
            }

            try
            {
                var parameters = new JObject { ["guids"] = JArray.FromObject(guids) };

                // Create AIToolCall and execute
                var toolCallInteraction = new AIInteractionToolCall
                {
                    Name = "gh_tidy_up",
                    Arguments = parameters,
                    Agent = AIAgent.Assistant,
                };

                var toolCall = new AIToolCall();
                toolCall.Endpoint = "gh_tidy_up";
                toolCall.FromToolCallInteraction(toolCallInteraction);
                toolCall.SkipMetricsValidation = true;

                var aiResult = toolCall.Exec().GetAwaiter().GetResult();
                var toolResultInteraction = aiResult.Body.GetLastInteraction() as AIInteractionToolResult;
                var toolResult = toolResultInteraction?.Result;
                if (toolResult == null)
                {
                    this.LastErrors.Add("Tool 'gh_tidy_up' returned invalid result");
                }
                else if (toolResult["success"]?.ToObject<bool>() == false)
                {
                    // Surface all error messages from the messages array
                    var hasErrors = false;
                    if (toolResult["messages"] is JArray msgs)
                    {
                        foreach (var msg in msgs.Where(m => m["severity"]?.ToString() == "Error"))
                        {
                            var errorText = msg["message"]?.ToString();
                            var origin = msg["origin"]?.ToString();
                            if (!string.IsNullOrEmpty(errorText))
                            {
                                var prefix = !string.IsNullOrEmpty(origin) ? $"[{origin}] " : string.Empty;
                                this.LastErrors.Add($"{prefix}{errorText}");
                                hasErrors = true;
                            }
                        }
                    }

                    if (!hasErrors)
                    {
                        this.LastErrors.Add("Unknown error");
                    }
                }
                else
                {
                    var moved = toolResult["moved"]?.ToObject<List<string>>() ?? new List<string>();
                    var failed = guids.Except(moved);
                    foreach (var g in failed)
                    {
                        this.LastErrors.Add($"Component {g} not moved");
                    }
                }
            }
            catch (Exception ex)
            {
                this.LastErrors.Add(ex.Message);
            }

            DA.SetDataList(0, this.LastErrors);
        }
    }
}
