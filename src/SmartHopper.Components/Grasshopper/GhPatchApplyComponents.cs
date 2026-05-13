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
    /// Grasshopper component for applying a `.ghpatch` to a base GhJSON document.
    /// </summary>
    public class GhPatchApplyComponents : GH_Component
    {
        private string lastResultJson = string.Empty;
        private bool lastSuccess;
        private string lastConflictsSummary = string.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="GhPatchApplyComponents"/> class.
        /// </summary>
        public GhPatchApplyComponents()
            : base(
                "Apply GhPatch",
                "GhPatchApply",
                "Apply a `.ghpatch` patch document to a base GhJSON document. By default, refuses to apply on base checksum mismatch.",
                "SmartHopper",
                "Grasshopper")
        {
        }

        /// <summary>
        /// Gets the unique identifier for this component.
        /// </summary>
        public override Guid ComponentGuid => new Guid("C5E0D4B3-6F2A-4B7C-9E8D-A1F2B3C4D5E6");

        /// <summary>
        /// Gets the component's icon.
        /// </summary>
        protected override Bitmap Icon => Resources.ghmerge;

        /// <summary>
        /// Registers the input parameters for this component.
        /// </summary>
        /// <param name="pManager">The parameter manager to register inputs with.</param>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Base", "B", "Base GhJSON document to apply the patch to.", GH_ParamAccess.item);
            pManager.AddTextParameter("Patch", "P", "GhPatch document.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Verify Base", "V", "Refuse to apply on base checksum mismatch. Defaults to true.", GH_ParamAccess.item, true);
            pManager.AddBooleanParameter("Run?", "X", "Run this component?", GH_ParamAccess.item, false);
        }

        /// <summary>
        /// Registers the output parameters for this component.
        /// </summary>
        /// <param name="pManager">The parameter manager to register outputs with.</param>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Result", "R", "Resulting GhJSON document with the patch applied", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Success", "S", "Whether the apply succeeded with no fatal conflicts", GH_ParamAccess.item);
            pManager.AddTextParameter("Conflicts", "C", "Summary of conflicts encountered during apply", GH_ParamAccess.item);
        }

        /// <summary>
        /// Solves the component for the given data access.
        /// </summary>
        /// <param name="DA">The data access object for input/output operations.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // 1. Read "Run?" switch
            bool run = false;
            if (!DA.GetData(3, ref run))
            {
                return;
            }

            if (!run)
            {
                if (!string.IsNullOrEmpty(this.lastResultJson))
                {
                    DA.SetData(0, this.lastResultJson);
                    DA.SetData(1, this.lastSuccess);
                    DA.SetData(2, this.lastConflictsSummary);
                }
                else
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Set Run to True to apply patch");
                }

                return;
            }

            // 2. Clear previous results
            this.lastResultJson = string.Empty;
            this.lastSuccess = false;
            this.lastConflictsSummary = string.Empty;

            // 3. Get inputs
            string baseJson = null;
            string patchJson = null;
            bool verifyBase = true;

            if (!DA.GetData(0, ref baseJson) || string.IsNullOrEmpty(baseJson))
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Base GhJSON is required");
                return;
            }

            if (!DA.GetData(1, ref patchJson) || string.IsNullOrEmpty(patchJson))
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Patch document is required");
                return;
            }

            DA.GetData(2, ref verifyBase);

            try
            {
                // 4. Call the AI tool
                var parameters = new JObject
                {
                    ["base"] = baseJson,
                    ["patch"] = patchJson,
                    ["verifyBase"] = verifyBase,
                };

                var toolCallInteraction = new AIInteractionToolCall
                {
                    Name = "gh_patch_apply",
                    Arguments = parameters,
                    Agent = AIAgent.Assistant,
                };

                var toolCall = new AIToolCall();
                toolCall.Endpoint = "gh_patch_apply";
                toolCall.FromToolCallInteraction(toolCallInteraction);
                toolCall.SkipMetricsValidation = true;

                var toolResult = ToolCallResult.FromAIReturn(toolCall.Exec().GetAwaiter().GetResult());

                if (toolResult.Result == null)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Tool 'gh_patch_apply' did not return a valid result");
                    return;
                }

                // 5. Extract results
                var resultJson = toolResult["ghjson"]?.ToString() ?? string.Empty;
                var success = toolResult["success"]?.ToObject<bool>() ?? false;
                var conflictsArray = toolResult["conflicts"] as JArray;

                var conflictsSummary = string.Empty;
                if (conflictsArray != null && conflictsArray.Count > 0)
                {
                    var lines = new System.Collections.Generic.List<string>();
                    foreach (var c in conflictsArray)
                    {
                        var kind = c["kind"]?.ToString() ?? "?";
                        var message = c["message"]?.ToString() ?? string.Empty;
                        var path = c["path"]?.ToString() ?? string.Empty;
                        lines.Add(string.IsNullOrEmpty(path) ? $"[{kind}] {message}" : $"[{kind}] {message} ({path})");
                    }

                    conflictsSummary = string.Join("\n", lines);
                }

                this.lastResultJson = resultJson;
                this.lastSuccess = success;
                this.lastConflictsSummary = conflictsSummary;

                if (!success)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Patch apply reported failure ({conflictsArray?.Count ?? 0} conflict(s))");
                }
                else if (conflictsArray != null && conflictsArray.Count > 0)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"{conflictsArray.Count} non-fatal conflict(s)");
                }

                // 6. Set outputs
                DA.SetData(0, resultJson);
                DA.SetData(1, success);
                DA.SetData(2, conflictsSummary);
            }
            catch (Exception ex)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }
    }
}
