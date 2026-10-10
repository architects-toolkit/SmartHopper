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
 * TestGhSetValueComponent: Test component for the gh_set_value AI tool.
 *
 * Tool: gh_set_value
 *   Inputs:  instanceGuid (string, required), value (scalar, required),
 *            param (string, optional – component input parameter)
 *   Outputs: updated (list of GUID strings), failed (object, optional)
 *
 * Uses SelectingComponentBase so the user can pick the target object via the
 * "Select Components" button. The Value input is provided as text and parsed
 * by the tool according to the target type (boolean for toggles, number for
 * sliders, item name/index for value lists, raw text for panels).
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
    /// Test component for the gh_set_value AI tool.
    /// Select the target object via the "Select Components" button; the first
    /// selected object receives the value. Optionally name an input parameter
    /// to set a component's persistent input default instead.
    /// </summary>
    public class TestGhSetValueComponent : SelectingComponentBase
    {
        /// <inheritdoc />
        public override Guid ComponentGuid => new Guid("A3B7C1E2-4D5F-4A6B-8C9D-0E1F2A3B4C5D");

        /// <inheritdoc />
        protected override Bitmap Icon => null;

        /// <inheritdoc />
        public override GH_Exposure Exposure => GH_Exposure.hidden;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestGhSetValueComponent"/> class.
        /// </summary>
        public TestGhSetValueComponent()
            : base(
                "Test gh_set_value",
                "TEST-GH-SETVAL",
                "Tests the gh_set_value AI tool. Select a target object via the button, " +
                "supply a value (and optionally an input parameter name), then set Run to True.",
                "SmartHopper Tests",
                "Testing AiTools")
        {
        }

        /// <inheritdoc />
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter(
                "Value",
                "V",
                "Value to apply (text, number, boolean, or value-list item name/index).",
                GH_ParamAccess.item);

            pManager.AddTextParameter(
                "Param",
                "P",
                "Optional input parameter name or index when the target is a component.",
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
                "Updated",
                "U",
                "List of object GUID strings whose value was set.",
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

            string value = string.Empty;
            string param = string.Empty;
            DA.GetData(0, ref value);
            DA.GetData(1, ref param);

            var selected = this.SelectedObjects;
            if (selected == null || selected.Count == 0)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Select a target object with the 'Select Components' button.");
                return;
            }

            var toolName = "gh_set_value";

            try
            {
                var parameters = new JObject
                {
                    ["instanceGuid"] = selected[0].InstanceGuid.ToString(),
                    ["value"] = value,
                };
                if (!string.IsNullOrWhiteSpace(param))
                {
                    parameters["param"] = param;
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

                var updated = (toolResult["updated"] as JArray)?.Select(t => t.ToString()).ToList()
                              ?? new List<string>();

                var failed = toolResult["failed"] as JObject;
                if (failed != null && failed.HasValues)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Set value failed: {failed}");
                }

                DA.SetDataList(0, updated);
            }
            catch (Exception ex)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }
    }
}
