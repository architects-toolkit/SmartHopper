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
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;
using SmartHopper.Components.Properties;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;

namespace SmartHopper.Components.Grasshopper
{
    /// <summary>
    /// Component that retrieves all installed Grasshopper component types as JSON.
    /// Supports optional filtering by category.
    /// </summary>
    public class GhRetrieveComponents : GH_Component
    {
        private List<string> lastNames = new List<string>();
        private List<string> lastGuids = new List<string>();
        private string lastJson = string.Empty;

        public GhRetrieveComponents()
            : base(
                "Retrieve Components",
                "GhRetrieveComponents",
                "List all component types installed in your Grasshopper environment as JSON, with optional category filtering.",
                "SmartHopper",
                "D. GhJSON")
        {
        }

        public override Guid ComponentGuid => new Guid("D2F1E3B4-C5A6-7D8E-9A0B-C1D2E3F4A5B6");

        /// <summary>
        /// Gets the exposure level of this component in the ribbon.
        /// </summary>
        public override GH_Exposure Exposure => GH_Exposure.quarternary | GH_Exposure.obscure;

        protected override Bitmap Icon => Resources.GhRetrieve;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Category Filter", "C",
                "Optional Grasshopper category filters using '+Category' to include or '-Category' to exclude (e.g., '+Maths', '-Params').",
                GH_ParamAccess.list, string.Empty);
            pManager.AddBooleanParameter("Run?", "R", "Set to true (or connect a Button) to retrieve the installed components.", GH_ParamAccess.item, false);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Names", "N", "Display names of the matching component types.", GH_ParamAccess.list);
            pManager.AddTextParameter("Guids", "G", "Component type GUIDs of the matching components.", GH_ParamAccess.list);
            pManager.AddTextParameter("JSON", "J", "Detailed component type definitions (inputs, outputs, categories, and descriptions) in JSON format.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            bool run = false;
            if (!DA.GetData(1, ref run)) return;

            var filters = new List<string>();
            DA.GetDataList(0, filters);

            if (!run)
            {
                if (this.lastNames.Count > 0)
                {
                    DA.SetDataList(0, this.lastNames);
                    DA.SetDataList(1, this.lastGuids);
                    DA.SetData(2, this.lastJson);
                }
                else
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Set Run to True to execute the component");
                }

                return;
            }

            var parameters = new JObject
            {
                ["categoryFilter"] = JArray.FromObject(filters),
            };

            try
            {
                // Create AIToolCall and execute
                var toolCallInteraction = new AIInteractionToolCall
                {
                    Name = "gh_list_components",
                    Arguments = parameters,
                    Agent = AIAgent.Assistant,
                };

                var toolCall = new AIToolCall();
                toolCall.Origin = "gh_list_components";
                toolCall.FromToolCallInteraction(toolCallInteraction);
                toolCall.SkipMetricsValidation = true;

                var toolResult = ToolCallResult.FromAIReturn(toolCall.Exec().GetAwaiter().GetResult());
                if (toolResult.Result == null)
                {
                    this.AddRuntimeMessage(
                        GH_RuntimeMessageLevel.Error,
                        "Tool 'gh_list_components' did not return a valid result");
                    return;
                }

                var names = toolResult["names"]?.ToObject<List<string>>() ?? new List<string>();
                var guids = toolResult["guids"]?.ToObject<List<string>>() ?? new List<string>();
                var json = toolResult["ghjson"]?.ToString() ?? string.Empty;

                this.lastNames = names;
                this.lastGuids = guids;
                this.lastJson = json;

                DA.SetDataList(0, names);
                DA.SetDataList(1, guids);
                DA.SetData(2, json);
            }
            catch (Exception ex)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }
    }
}
