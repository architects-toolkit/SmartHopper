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
using SmartHopper.ProviderSdk.Utilities;

namespace SmartHopper.Components.JSON
{
    /// <summary>
    /// Grasshopper component that merges multiple JSON objects via a shallow merge (last-wins on conflict).
    /// </summary>
    public class JsonMergeComponent : GH_Component
    {
        /// <inheritdoc/>
        public override Guid ComponentGuid => new Guid("E7BCC771-BB43-4144-A88F-666398743023");

        /// <inheritdoc/>
        protected override Bitmap Icon => Resources.jsonmerge;

        /// <inheritdoc/>
        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonMergeComponent"/> class.
        /// </summary>
        public JsonMergeComponent()
            : base(
                "JSON Merge",
                "JsonMerge",
                "Merge multiple JSON objects into one.\nShallow merge: later objects overwrite keys from earlier ones on conflict.",
                "SmartHopper",
                "JSON")
        {
        }

        /// <inheritdoc/>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("JSONs", "J", "List of JSON object strings to merge", GH_ParamAccess.list);
        }

        /// <inheritdoc/>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("JSON", "J", "Merged JSON object string", GH_ParamAccess.item);
        }

        /// <inheritdoc/>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var jsons = new List<string>();
            DA.GetDataList("JSONs", jsons);

            if (jsons == null || jsons.Count == 0)
            {
                DA.SetData("JSON", "{}");
                return;
            }

            try
            {
                var merged = new JObject();

                foreach (var json in jsons)
                {
                    if (string.IsNullOrWhiteSpace(json))
                    {
                        continue;
                    }

                    var token = JToken.Parse(json.Trim());

                    if (!(token is JObject obj))
                    {
                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Skipping non-object JSON: {json.Substring(0, Math.Min(40, json.Length))}...");
                        continue;
                    }

                    // Shallow merge: last-wins
                    merged.Merge(obj, new JsonMergeSettings
                    {
                        MergeArrayHandling = MergeArrayHandling.Replace,
                        MergeNullValueHandling = MergeNullValueHandling.Merge,
                    });
                }

                DA.SetData("JSON", JsonFormatHelper.JsonToString(merged));
            }
            catch (Exception ex)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Error merging JSON objects: {ex.Message}");
            }
        }
    }
}
