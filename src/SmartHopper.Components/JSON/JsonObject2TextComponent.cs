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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartHopper.Components.Properties;

namespace SmartHopper.Components.JSON
{
    /// <summary>
    /// Grasshopper component that serializes a JSON value to a GH string.
    /// </summary>
    public class JsonObject2TextComponent : GH_Component
    {
        /// <inheritdoc/>
        public override Guid ComponentGuid => new Guid("84922418-03DF-4740-9638-C6D7E0B5F16B");

        /// <inheritdoc/>
        protected override Bitmap Icon => Resources.textgenerate;

        /// <inheritdoc/>
        public override GH_Exposure Exposure => GH_Exposure.secondary;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonObject2TextComponent"/> class.
        /// </summary>
        public JsonObject2TextComponent()
            : base(
                "JSON To Text",
                "Json2Text",
                "Serialize a JSON value to a Grasshopper string.\nWhen Pretty is true, outputs human-readable indented JSON.",
                "SmartHopper",
                "JSON")
        {
        }

        /// <inheritdoc/>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("JSON", "J", "JSON string to serialize", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Pretty", "P", "Output indented (pretty-printed) JSON (default: false)", GH_ParamAccess.item, false);
            pManager[1].Optional = true;
        }

        /// <inheritdoc/>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Text", "T", "Serialized JSON string", GH_ParamAccess.item);
        }

        /// <inheritdoc/>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string json = string.Empty;
            bool pretty = false;

            DA.GetData("JSON", ref json);
            DA.GetData("Pretty", ref pretty);

            if (string.IsNullOrWhiteSpace(json))
            {
                DA.SetData("Text", string.Empty);
                return;
            }

            try
            {
                var token = JToken.Parse(json.Trim());
                var formatting = pretty ? Formatting.Indented : Formatting.None;
                DA.SetData("Text", token.ToString(formatting));
            }
            catch (Exception ex)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Error parsing JSON: {ex.Message}");
            }
        }
    }
}
