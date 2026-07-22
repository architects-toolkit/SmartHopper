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

using System;
using System.Drawing;
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;
using SmartHopper.Components.Properties;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Tools;

namespace SmartHopper.Components.Grasshopper
{
    /// <summary>
    /// Grasshopper component for diffing two GhJSON documents and producing a `.ghpatch`.
    /// </summary>
    public class GhDiffComponents : GH_Component
    {
        private string lastPatchJson = string.Empty;
        private int lastComponentOpCount;
        private int lastConnectionOpCount;
        private int lastGroupOpCount;

        /// <summary>
        /// Initializes a new instance of the <see cref="GhDiffComponents"/> class.
        /// </summary>
        public GhDiffComponents()
            : base(
                "Diff GhJSON",
                "GhDiff",
                "Diff two GhJSON documents and produce a `.ghpatch` document describing the differences. Components are matched by instanceGuid > id > structural fingerprint.",
                "SmartHopper",
                "Grasshopper")
        {
        }

        /// <inheritdoc/>
        public override Guid ComponentGuid => new Guid("B4D9C3A2-5E1F-4A6B-8D7C-9E2F1A5B6D8E");

        /// <inheritdoc/>
        protected override Bitmap Icon => Resources.ghdiff;

        /// <inheritdoc/>
        public override GH_Exposure Exposure => GH_Exposure.secondary;

        /// <inheritdoc/>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Left", "L", "Left (base) GhJSON document.", GH_ParamAccess.item);
            pManager.AddTextParameter("Right", "R", "Right (target) GhJSON document.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Run?", "X", "Run this component?", GH_ParamAccess.item, false);
        }

        /// <summary>
        /// Registers the output parameters for this component.
        /// </summary>
        /// <param name="pManager">The parameter manager to register outputs with.</param>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Patch", "P", "GhPatch document describing left → right differences", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Comp Ops", "C", "Number of component operations", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Conn Ops", "W", "Number of connection operations", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Group Ops", "G", "Number of group operations", GH_ParamAccess.item);
        }

        /// <summary>
        /// Solves the component for the given data access.
        /// </summary>
        /// <param name="DA">The data access object for input/output operations.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // 1. Read "Run?" switch
            bool run = false;
            if (!DA.GetData(2, ref run))
            {
                return;
            }

            if (!run)
            {
                if (!string.IsNullOrEmpty(this.lastPatchJson))
                {
                    DA.SetData(0, this.lastPatchJson);
                    DA.SetData(1, this.lastComponentOpCount);
                    DA.SetData(2, this.lastConnectionOpCount);
                    DA.SetData(3, this.lastGroupOpCount);
                }
                else
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Set Run to True to diff documents");
                }

                return;
            }

            // 2. Clear previous results
            this.lastPatchJson = string.Empty;
            this.lastComponentOpCount = 0;
            this.lastConnectionOpCount = 0;
            this.lastGroupOpCount = 0;

            // 3. Get inputs
            string leftJson = null;
            string rightJson = null;

            if (!DA.GetData(0, ref leftJson) || string.IsNullOrEmpty(leftJson))
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Left GhJSON is required");
                return;
            }

            if (!DA.GetData(1, ref rightJson) || string.IsNullOrEmpty(rightJson))
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Right GhJSON is required");
                return;
            }

            try
            {
                // 4. Call the AI tool
                var parameters = new JObject
                {
                    ["left"] = leftJson,
                    ["right"] = rightJson,
                };

                var toolCallInteraction = new AIInteractionToolCall
                {
                    Name = "gh_diff",
                    Arguments = parameters,
                    Agent = AIAgent.Assistant,
                };

                var toolCall = new AIToolCall();
                toolCall.Endpoint = "gh_diff";
                toolCall.FromToolCallInteraction(toolCallInteraction);
                toolCall.SkipMetricsValidation = true;

                var toolResult = ToolCallResult.FromAIReturn(toolCall.Exec().GetAwaiter().GetResult());

                if (toolResult.Result == null)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Tool 'gh_diff' did not return a valid result");
                    return;
                }

                // 5. Extract results
                var patchJson = toolResult["ghpatch"]?.ToString() ?? string.Empty;
                var hasChanges = toolResult["hasChanges"]?.ToObject<bool>() ?? false;
                var componentOpCount = toolResult["componentOpCount"]?.ToObject<int>() ?? 0;
                var connectionOpCount = toolResult["connectionOpCount"]?.ToObject<int>() ?? 0;
                var groupOpCount = toolResult["groupOpCount"]?.ToObject<int>() ?? 0;

                this.lastPatchJson = patchJson;
                this.lastComponentOpCount = componentOpCount;
                this.lastConnectionOpCount = connectionOpCount;
                this.lastGroupOpCount = groupOpCount;

                if (!hasChanges)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Documents are identical — empty patch produced");
                }

                // 6. Set outputs
                DA.SetData(0, patchJson);
                DA.SetData(1, componentOpCount);
                DA.SetData(2, connectionOpCount);
                DA.SetData(3, groupOpCount);
            }
            catch (Exception ex)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }
    }
}
