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
 * TestGhRunComponent: Test component for the gh_run AI tool.
 *
 * Tool: gh_run
 *   Inputs:  instanceGuids (list of GUID strings, required)
 *   Outputs: runGuids (list of GUID strings), skippedGuids (object),
 *            protectedGuids (list of GUID strings)
 *
 * Uses SelectingComponentBase so the user can pick the components to re-run
 * via the "Select Components" button.
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
    /// Test component for the gh_run AI tool.
    /// Select target components via the "Select Components" button; each is
    /// re-run through a disable-enable pulse on the Grasshopper UI thread.
    /// </summary>
    public class TestGhRunComponent : SelectingComponentBase
    {
        /// <inheritdoc />
        public override Guid ComponentGuid => new Guid("B5C8D3F4-5E6A-4B7C-9D0E-1F2A3B4C5D6E");

        /// <inheritdoc />
        protected override Bitmap Icon => null;

        /// <inheritdoc />
        public override GH_Exposure Exposure => GH_Exposure.hidden;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestGhRunComponent"/> class.
        /// </summary>
        public TestGhRunComponent()
            : base(
                "Test gh_run",
                "TEST-GH-RUN",
                "Tests the gh_run AI tool. Select components via the button to re-run them " +
                "through a disable-enable pulse, then set Run to True.",
                "SmartHopper Tests",
                "Testing AiTools")
        {
        }

        /// <inheritdoc />
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
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
                "Run",
                "R",
                "List of component GUID strings that were re-run.",
                GH_ParamAccess.list);
        }

        /// <inheritdoc />
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            bool run = false;
            DA.GetData(0, ref run);
            if (!run)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Set Run to True to execute the tool.");
                return;
            }

            var selected = this.SelectedObjects;
            if (selected == null || selected.Count == 0)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Select target components with the 'Select Components' button.");
                return;
            }

            var toolName = "gh_run";

            try
            {
                var parameters = new JObject
                {
                    ["instanceGuids"] = JArray.FromObject(
                        selected.Select(o => o.InstanceGuid.ToString()).ToList()),
                };

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

                var runGuids = (toolResult["runGuids"] as JArray)?.Select(t => t.ToString()).ToList()
                              ?? new List<string>();

                var skipped = toolResult["skippedGuids"] as JObject;
                if (skipped != null && skipped.HasValues)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Skipped: {skipped}");
                }

                DA.SetDataList(0, runGuids);
            }
            catch (Exception ex)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }
    }
}
