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

/*
 * TestGhSelectComponent: Test component for the gh_select AI tool.
 *
 * Tool: gh_select
 *   Inputs:  guids (list), mode (set|add|remove|clear)
 *   Outputs: selected (list of GUID strings), changed (list of GUID strings)
 *
 * Uses SelectingComponentBase so the user can pin components with the "Select
 * Components" button; pinned objects are sent as the 'guids' argument. With no
 * pinned selection the call still works, which is useful for the 'clear' mode.
 */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;

namespace SmartHopper.Components.Test.AiTools
{
    /// <summary>
    /// Test component for the gh_select AI tool.
    /// Select target components via the "Select Components" button; the pinned
    /// objects are passed as the 'guids' argument for modes set/add/remove.
    /// </summary>
    public class TestGhSelectComponent : SelectingComponentBase
    {
        /// <inheritdoc />
        public override Guid ComponentGuid => new Guid("969D3770-0657-494E-874C-5A1D37226C50");

        /// <inheritdoc />
        protected override Bitmap Icon => null;

        /// <inheritdoc />
        public override GH_Exposure Exposure => GH_Exposure.hidden;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestGhSelectComponent"/> class.
        /// </summary>
        public TestGhSelectComponent()
            : base(
                "Test gh_select",
                "TEST-GH-SELECT",
                "Tests gh_select. Pin components via the button and choose a mode (set, add, remove or clear).",
                "SmartHopper Tests",
                "Testing AiTools")
        {
        }

        /// <inheritdoc />
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter(
                "Mode",
                "M",
                "Selection mode: 'set', 'add', 'remove' or 'clear'.",
                GH_ParamAccess.item,
                "set");

            pManager.AddBooleanParameter(
                "Run?",
                "R",
                "Set to True to execute the tool.",
                GH_ParamAccess.item,
                false);
        }

        /// <inheritdoc />
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter(
                "Selected",
                "S",
                "Instance GUIDs selected on the canvas after the call.",
                GH_ParamAccess.list);

            pManager.AddTextParameter(
                "Changed",
                "C",
                "Instance GUIDs whose selection flag changed.",
                GH_ParamAccess.list);
        }

        /// <inheritdoc />
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            bool run = false;
            DA.GetData(1, ref run);
            if (!run)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Set Run to True to execute the tool.");
                return;
            }

            string mode = string.Empty;
            DA.GetData(0, ref mode);

            var selected = this.SelectedObjects;

            try
            {
                var parameters = new JObject();

                if (!string.IsNullOrWhiteSpace(mode))
                {
                    parameters["mode"] = mode;
                }

                if (selected != null && selected.Count > 0)
                {
                    parameters["guids"] = JArray.FromObject(
                        selected.Select(o => o.InstanceGuid.ToString()).ToList());
                }

                var toolCallInteraction = new AIInteractionToolCall
                {
                    Name = "gh_select",
                    Arguments = parameters,
                    Agent = AIAgent.Assistant,
                };

                var toolCall = new AIToolCall();
                toolCall.Endpoint = "gh_select";
                toolCall.FromToolCallInteraction(toolCallInteraction);
                toolCall.SkipMetricsValidation = true;

                var toolResult = ToolCallResult.FromAIReturn(toolCall.Exec().GetAwaiter().GetResult());

                if (toolResult.Result == null)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Tool 'gh_select' did not return a valid result.");
                    return;
                }

                var selectedGuids = (toolResult["selected"] as JArray)?.Select(t => t.ToString()).ToList()
                                    ?? new List<string>();
                var changedGuids = (toolResult["changed"] as JArray)?.Select(t => t.ToString()).ToList()
                                   ?? new List<string>();

                var skippedProtected = (toolResult["skippedProtected"] as JArray)?.Select(t => t.ToString()).ToList();
                if (skippedProtected != null && skippedProtected.Count > 0)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Skipped protected: {string.Join(", ", skippedProtected)}");
                }

                var missingGuids = (toolResult["missingGuids"] as JArray)?.Select(t => t.ToString()).ToList();
                if (missingGuids != null && missingGuids.Count > 0)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Missing GUIDs: {string.Join(", ", missingGuids)}");
                }

                DA.SetDataList(0, selectedGuids);
                DA.SetDataList(1, changedGuids);
            }
            catch (Exception ex)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }
    }
}
