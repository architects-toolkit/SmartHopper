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
 * TestGhComponentLockComponent: Test component for the gh_component_toggle_lock,
 * gh_component_lock_selected and gh_component_unlock_selected AI tools.
 *
 * Tool: gh_component_toggle_lock
 *   Inputs:  guids (list, required), locked (bool, required)
 *   Outputs: updated (list of GUID strings)
 *
 * Tool: gh_component_lock_selected
 *   Inputs:  (none – acts on canvas selection)
 *   Outputs: updated (list of GUID strings)
 *
 * Tool: gh_component_unlock_selected
 *   Inputs:  (none – acts on canvas selection)
 *   Outputs: updated (list of GUID strings)
 *
 * Uses SelectingComponentBase so the user can pick components via the
 * "Select Components" button. When objects are pinned via the button,
 * gh_component_toggle_lock is called; when none are selected,
 * gh_component_lock_selected or gh_component_unlock_selected is used
 * depending on the Locked input.
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
    /// Test component for the gh_component_toggle_lock, gh_component_lock_selected
    /// and gh_component_unlock_selected AI tools.
    /// Select target components via the "Select Components" button to call
    /// gh_component_toggle_lock; leave the selection empty to act on the canvas
    /// selection (lock_selected or unlock_selected depending on the Locked input).
    /// </summary>
    public class TestGhComponentLockComponent : SelectingComponentBase
    {
        /// <inheritdoc />
        public override Guid ComponentGuid => new Guid("EFC99FCE-9C63-4FF8-AB1B-E70EF75804B2");

        /// <inheritdoc />
        protected override Bitmap Icon => null;

        /// <inheritdoc />
        public override GH_Exposure Exposure => GH_Exposure.hidden;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestGhComponentLockComponent"/> class.
        /// </summary>
        public TestGhComponentLockComponent()
            : base(
                "Test gh_component_lock",
                "TEST-GH-LOCK",
                "Tests gh_component_toggle_lock / gh_component_lock_selected / gh_component_unlock_selected. " +
                "Select components via the button to target them explicitly; leave empty to act on the canvas selection.",
                "SmartHopper Tests",
                "Testing AiTools")
        {
        }

        /// <inheritdoc />
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter(
                "Locked",
                "L",
                "True to lock (disable) components; false to unlock (enable) them.",
                GH_ParamAccess.item,
                true);

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
                "List of component GUID strings whose lock state was changed.",
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

            bool locked = true;
            DA.GetData(0, ref locked);

            var selected = this.SelectedObjects;
            bool hasSelection = selected != null && selected.Count > 0;

            // Choose tool variant
            string toolName;
            if (hasSelection)
            {
                toolName = "gh_component_toggle_lock";
            }
            else
            {
                toolName = locked ? "gh_component_lock_selected" : "gh_component_unlock_selected";
            }

            try
            {
                var parameters = new JObject();

                if (hasSelection)
                {
                    parameters["guids"] = JArray.FromObject(
                        selected.Select(o => o.InstanceGuid.ToString()).ToList());
                    parameters["locked"] = locked;
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

                DA.SetDataList(0, updated);
            }
            catch (Exception ex)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }
    }
}
