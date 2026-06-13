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
 * TestGhMoveComponent: Test component for the gh_move AI tool.
 *
 * Tool: gh_move
 *   Inputs:  targets (object mapping GUID → {x, y}), relative (bool)
 *   Outputs: updated (list of moved GUID strings)
 *
 * Uses SelectingComponentBase so the user can pick components via the "Select Components"
 * button. X and Y offsets/positions are provided as flat lists aligned with the selection.
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
    /// Test component for the gh_move AI tool.
    /// Select target components with the "Select Components" button, then supply
    /// X and Y values (one per selected component) and set Run to True.
    /// </summary>
    public class TestGhMoveComponent : SelectingComponentBase
    {
        /// <inheritdoc />
        public override Guid ComponentGuid => new Guid("79F7DE44-9A14-4140-B1CA-C652852068AF");

        /// <inheritdoc />
        protected override Bitmap Icon => null;

        /// <inheritdoc />
        public override GH_Exposure Exposure => GH_Exposure.hidden;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestGhMoveComponent"/> class.
        /// </summary>
        public TestGhMoveComponent()
            : base(
                "Test gh_move",
                "TEST-GH-MOVE",
                "Tests the gh_move AI tool. Select components via the button, then supply X/Y values and run.",
                "SmartHopper Tests",
                "Testing AiTools")
        {
        }

        /// <inheritdoc />
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter(
                "X",
                "X",
                "X coordinates (or offsets when Relative = true). One value per selected component, in selection order.",
                GH_ParamAccess.list);

            pManager.AddNumberParameter(
                "Y",
                "Y",
                "Y coordinates (or offsets when Relative = true). One value per selected component, in selection order.",
                GH_ParamAccess.list);

            pManager.AddBooleanParameter(
                "Relative",
                "Rel",
                "When true, treat X/Y as relative offsets (delta). When false (default), use absolute canvas positions.",
                GH_ParamAccess.item,
                false);

            pManager.AddBooleanParameter(
                "Run?",
                "R",
                "Set to True to execute gh_move.",
                GH_ParamAccess.item,
                false);
        }

        /// <inheritdoc />
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter(
                "Updated",
                "U",
                "List of component GUID strings that were successfully moved.",
                GH_ParamAccess.list);
        }

        /// <inheritdoc />
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            bool run = false;
            DA.GetData(3, ref run);
            if (!run)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Set Run to True to execute gh_move.");
                return;
            }

            var selected = this.SelectedObjects;
            if (selected == null || selected.Count == 0)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No components selected. Use the 'Select Components' button to pick targets.");
                return;
            }

            var xValues = new List<double>();
            var yValues = new List<double>();
            bool relative = false;

            if (!DA.GetDataList(0, xValues) || xValues.Count == 0)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "X values are required.");
                return;
            }

            if (!DA.GetDataList(1, yValues) || yValues.Count == 0)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Y values are required.");
                return;
            }

            if (xValues.Count != selected.Count || yValues.Count != selected.Count)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    $"X and Y lists must each have exactly {selected.Count} value(s) — one per selected component.");
                return;
            }

            DA.GetData(2, ref relative);

            try
            {
                // Build the targets object: { "<guid>": { "x": ..., "y": ... }, ... }
                var targetsObj = new JObject();
                for (int i = 0; i < selected.Count; i++)
                {
                    var guid = selected[i].InstanceGuid.ToString();
                    targetsObj[guid] = new JObject
                    {
                        ["x"] = xValues[i],
                        ["y"] = yValues[i],
                    };
                }

                var parameters = new JObject
                {
                    ["targets"] = targetsObj,
                    ["relative"] = relative,
                };

                var toolCallInteraction = new AIInteractionToolCall
                {
                    Name = "gh_move",
                    Arguments = parameters,
                    Agent = AIAgent.Assistant,
                };

                var toolCall = new AIToolCall();
                toolCall.Endpoint = "gh_move";
                toolCall.FromToolCallInteraction(toolCallInteraction);
                toolCall.SkipMetricsValidation = true;

                var toolResult = ToolCallResult.FromAIReturn(toolCall.Exec().GetAwaiter().GetResult());

                if (toolResult.Result == null)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Tool 'gh_move' did not return a valid result.");
                    return;
                }

                var updatedArray = toolResult["updated"] as JArray;
                var updated = updatedArray?.Select(t => t.ToString()).ToList() ?? new List<string>();

                DA.SetDataList(0, updated);
            }
            catch (Exception ex)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }
    }
}
