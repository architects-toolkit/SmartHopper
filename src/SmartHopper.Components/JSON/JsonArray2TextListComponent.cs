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

namespace SmartHopper.Components.JSON
{
    /// <summary>
    /// Grasshopper component that parses a JSON array string into a GH text list.
    /// JSON strings have their outer quotes stripped; other types are serialized as-is.
    /// </summary>
    public class JsonArray2TextListComponent : GH_Component
    {
        /// <inheritdoc/>
        public override Guid ComponentGuid => new Guid("7D1F835A-5E41-4310-9813-B3D5F965FCE2");

        /// <inheritdoc/>
        protected override Bitmap Icon => Resources.jsonarraytolist;

        /// <inheritdoc/>
        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonArray2TextListComponent"/> class.
        /// </summary>
        public JsonArray2TextListComponent()
            : base(
                "JSON Array To Text List",
                "JsonArray2Text",
                "Parse a JSON array string into a Grasshopper text list.\nJSON strings have their outer quotes stripped. Numbers, booleans and objects are serialized to their compact string form.",
                "SmartHopper",
                "JSON")
        {
        }

        /// <inheritdoc/>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("JSON", "J", "JSON array string to parse", GH_ParamAccess.item);
        }

        /// <inheritdoc/>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Items", "I", "Parsed text items", GH_ParamAccess.list);
        }

        /// <inheritdoc/>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string json = string.Empty;
            DA.GetData("JSON", ref json);

            if (string.IsNullOrWhiteSpace(json))
            {
                DA.SetDataList("Items", new string[0]);
                return;
            }

            try
            {
                var token = JToken.Parse(json.Trim());

                if (!(token is JArray array))
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Input is not a JSON array.");
                    return;
                }

                var items = new System.Collections.Generic.List<string>(array.Count);
                foreach (var element in array)
                {
                    items.Add(UnwrapToken(element));
                }

                DA.SetDataList("Items", items);
            }
            catch (Exception ex)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Error parsing JSON array: {ex.Message}");
            }
        }

        /// <summary>
        /// Converts a JToken to its string representation, unwrapping JSON strings (stripping outer quotes).
        /// </summary>
        private static string UnwrapToken(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return string.Empty;
            }

            // JSON strings: return the raw string value (no outer quotes)
            if (token.Type == JTokenType.String)
            {
                return token.Value<string>() ?? string.Empty;
            }

            // All other types (number, boolean, object, array): compact serialization
            return token.ToString(Newtonsoft.Json.Formatting.None);
        }
    }
}
