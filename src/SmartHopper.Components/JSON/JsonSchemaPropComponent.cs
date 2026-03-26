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
using SmartHopper.Components.Properties;

namespace SmartHopper.Components.JSON
{
    /// <summary>
    /// Grasshopper component that builds a single scalar JSON Schema property definition string
    /// compatible with <see cref="JsonSchemaComponent"/> Properties input.
    /// </summary>
    public class JsonSchemaPropComponent : GH_Component
    {
        /// <inheritdoc/>
        public override Guid ComponentGuid => new Guid("0C3FA27C-BA98-4701-8DCA-3C6E647D5245");

        /// <inheritdoc/>
        protected override Bitmap Icon => Resources.textgenerate;

        /// <inheritdoc/>
        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonSchemaPropComponent"/> class.
        /// </summary>
        public JsonSchemaPropComponent()
            : base(
                  "JSON Schema Property",
                  "JsonSchemaProp",
                  "Build a scalar JSON Schema property definition for use with JsonSchemaComponent.\n\nOutputs a string in the format \"name:type\" or \"name:type:description\".\nFor nested properties, prefix the name: e.g. Name = \"address\" and connect as a sub-property to JsonSchemaPropObject.\n\nValid types: string, number, integer, boolean",
                  "SmartHopper", "JSON")
        {
        }

        /// <inheritdoc/>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "N", "Property name", GH_ParamAccess.item);
            pManager.AddTextParameter("Type", "T", "Property type: string, number, integer, boolean (default: string)", GH_ParamAccess.item, "string");
            pManager.AddTextParameter("Description", "D", "Optional description", GH_ParamAccess.item, string.Empty);

            pManager[1].Optional = true;
            pManager[2].Optional = true;
        }

        /// <inheritdoc/>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Property", "P", "Property definition string. Connect to JsonSchemaComponent Properties input.", GH_ParamAccess.item);
        }

        /// <inheritdoc/>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string name = string.Empty;
            string type = "string";
            string description = string.Empty;

            DA.GetData("Name", ref name);
            DA.GetData("Type", ref type);
            DA.GetData("Description", ref description);

            if (string.IsNullOrWhiteSpace(name))
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Name is required.");
                return;
            }

            type = NormalizeScalarType(type);

            string result = string.IsNullOrWhiteSpace(description)
                ? $"{name.Trim()}:{type}"
                : $"{name.Trim()}:{type}:{description.Trim()}";

            DA.SetData("Property", result);
        }

        /// <summary>
        /// Normalizes the type to a valid scalar JSON Schema type.
        /// </summary>
        private static string NormalizeScalarType(string type)
        {
            if (string.IsNullOrWhiteSpace(type))
            {
                return "string";
            }

            switch (type.Trim().ToLowerInvariant())
            {
                case "string":
                case "number":
                case "integer":
                case "boolean":
                    return type.Trim().ToLowerInvariant();
                default:
                    return "string";
            }
        }
    }
}
