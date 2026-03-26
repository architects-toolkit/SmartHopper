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
    /// Grasshopper component that builds a JSON Schema array property definition string
    /// compatible with <see cref="JsonSchemaComponent"/> Properties input.
    /// </summary>
    /// <remarks>
    /// Visual wiring pattern:
    ///   JsonSchemaPropArray("tags", "string") ──► JsonSchemaComponent.Properties
    /// </remarks>
    public class JsonSchemaPropArrayComponent : GH_Component
    {
        /// <inheritdoc/>
        public override Guid ComponentGuid => new Guid("A0DEC514-2E3C-4F3F-A65F-AD2C28DA0DCB");

        /// <inheritdoc/>
        protected override Bitmap Icon => Resources.textgenerate;

        /// <inheritdoc/>
        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonSchemaPropArrayComponent"/> class.
        /// </summary>
        public JsonSchemaPropArrayComponent()
            : base(
                  "JSON Schema Property (Array)",
                  "JsonSchemaPropArr",
                  "Build a JSON Schema array property definition for use with JsonSchemaComponent.\n\nOutputs a string in the format \"name:array:description\".\nThe Items Type input defines the type of elements in the array.\n\nValid item types: string, number, integer, boolean, object",
                  "SmartHopper", "JSON")
        {
        }

        /// <inheritdoc/>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "N", "Property name (e.g. \"tags\")", GH_ParamAccess.item);
            pManager.AddTextParameter("Items Type", "IT", "Type of each array element: string, number, integer, boolean, object (default: string)", GH_ParamAccess.item, "string");
            pManager.AddTextParameter("Description", "D", "Optional description for the array property", GH_ParamAccess.item, string.Empty);

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
            string itemsType = "string";
            string description = string.Empty;

            DA.GetData("Name", ref name);
            DA.GetData("Items Type", ref itemsType);
            DA.GetData("Description", ref description);

            if (string.IsNullOrWhiteSpace(name))
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Name is required.");
                return;
            }

            itemsType = NormalizeItemsType(itemsType);

            // The property declaration is "name:array:description"
            // JsonSchemaComponent will set items.type from the stored itemsType
            // We encode it as "name:array[itemsType]:description" and update JsonSchemaComponent to parse it
            string encodedType = $"array[{itemsType}]";

            string result = string.IsNullOrWhiteSpace(description)
                ? $"{name.Trim()}:{encodedType}"
                : $"{name.Trim()}:{encodedType}:{description.Trim()}";

            DA.SetData("Property", result);
        }

        /// <summary>
        /// Normalizes the items type to a valid JSON Schema type.
        /// </summary>
        private static string NormalizeItemsType(string type)
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
                case "object":
                    return type.Trim().ToLowerInvariant();
                default:
                    return "string";
            }
        }
    }
}
