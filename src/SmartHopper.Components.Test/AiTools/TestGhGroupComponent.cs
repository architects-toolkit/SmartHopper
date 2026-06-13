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
 * TestGhGroupComponent: Test component for the gh_group and gh_group_selected AI tools.
 *
 * Tool: gh_group
 *   Inputs:  guids (list, required), groupName (optional), color (optional)
 *   Outputs: group (GUID string), grouped (list of GUID strings)
 *
 * Tool: gh_group_selected
 *   Inputs:  groupName (optional), color (optional)
 *   Outputs: group (GUID string), grouped (list of GUID strings)
 *
 * Uses SelectingComponentBase so the user can pick components with the "Select Components"
 * button. When objects are selected, gh_group is called; when none are selected,
 * gh_group_selected acts on the current canvas selection.
 */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Tools;

namespace SmartHopper.Components.Test.AiTools
{
    /// <summary>
    /// Test component for the gh_group and gh_group_selected AI tools.
    /// Select target components via the "Select Components" button to call gh_group;
    /// leave the selection empty to call gh_group_selected (acts on the canvas selection).
    /// </summary>
    public class TestGhGroupComponent : SelectingComponentBase
    {
        /// <inheritdoc />
        public override Guid ComponentGuid => new Guid("7BFEF496-71D4-47BB-8A5B-C3D9DA564B0F");

        /// <inheritdoc />
        protected override Bitmap Icon => null;

        /// <inheritdoc />
        public override GH_Exposure Exposure => GH_Exposure.hidden;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestGhGroupComponent"/> class.
        /// </summary>
        public TestGhGroupComponent()
            : base(
                "Test gh_group",
                "TEST-GH-GROUP",
                "Tests gh_group and gh_group_selected. Select components via the button to call gh_group; " +
                "leave selection empty to call gh_group_selected (uses the canvas selection).",
                "SmartHopper Tests",
                "Testing AiTools")
        {
        }

        /// <inheritdoc />
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter(
                "Group Name",
                "N",
                "Optional name label for the group.",
                GH_ParamAccess.item,
                string.Empty);
            pManager[0].Optional = true;

            pManager.AddTextParameter(
                "Color",
                "C",
                "Optional group color. Accepts ARGB 'A,R,G,B', RGB 'R,G,B', HTML hex '#RRGGBB', or a known color name (e.g. 'Red').",
                GH_ParamAccess.item,
                string.Empty);
            pManager[1].Optional = true;

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
                "Group GUID",
                "GG",
                "Instance GUID of the newly created group.",
                GH_ParamAccess.item);

            pManager.AddTextParameter(
                "Grouped GUIDs",
                "GR",
                "List of component GUIDs that were added to the group.",
                GH_ParamAccess.list);
        }

        /// <inheritdoc />
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            bool run = false;
            DA.GetData(2, ref run);
            if (!run)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Set Run to True to execute the tool.");
                return;
            }

            string groupName = string.Empty;
            DA.GetData(0, ref groupName);

            string color = string.Empty;
            DA.GetData(1, ref color);

            var selected = this.SelectedObjects;
            bool hasSelection = selected != null && selected.Count > 0;

            // gh_group when objects pinned via the button; gh_group_selected otherwise
            string toolName = hasSelection ? "gh_group" : "gh_group_selected";

            try
            {
                var parameters = new JObject();

                if (hasSelection)
                {
                    parameters["guids"] = JArray.FromObject(
                        selected.Select(o => o.InstanceGuid.ToString()).ToList());
                }

                if (!string.IsNullOrWhiteSpace(groupName))
                {
                    parameters["groupName"] = groupName;
                }

                if (!string.IsNullOrWhiteSpace(color))
                {
                    parameters["color"] = color;
                }

                var toolCallInteraction = new AIInteractionToolCall
                {
                    Name = toolName,
                    Arguments = parameters,
                    Agent = AIAgent.Assistant,
                };

                var toolCall = new AIToolCall();
                toolCall.Endpoint = toolName;
                toolCall.FromToolCallInteraction(toolCallInteraction);
                toolCall.SkipMetricsValidation = true;

                var toolResult = ToolCallResult.FromAIReturn(toolCall.Exec().GetAwaiter().GetResult());

                if (toolResult.Result == null)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Tool '{toolName}' did not return a valid result.");
                    return;
                }

                var groupGuid = toolResult["group"]?.ToString() ?? string.Empty;
                var grouped = (toolResult["grouped"] as JArray)?.Select(t => t.ToString()).ToList()
                              ?? new List<string>();

                DA.SetData(0, groupGuid);
                DA.SetDataList(1, grouped);
            }
            catch (Exception ex)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }
    }
}
