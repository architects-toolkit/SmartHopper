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
        protected override Bitmap Icon => Resources.jsonschemaprop;

        /// <inheritdoc/>
        public override GH_Exposure Exposure => GH_Exposure.quarternary;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonSchemaPropComponent"/> class.
        /// </summary>
        public JsonSchemaPropComponent()
            : base(
                "JSON Schema Property",
                "JsonSchemaProp",
                "Build a JSON Schema property definition for use with Json Schema component.\n\nOutputs a string in the format \"name:type:description\" or \"name:type:description:required\" when Required? is true.\nSet Array? to true to create an array property with items of the specified Type.\nFor nested properties, prefix the name: e.g. Name = \"address\" and connect as a sub-property to JsonSchemaObject.\n\nValid types: string, number, integer, boolean, object",
                "SmartHopper",
                "JSON")
        {
        }

        /// <inheritdoc/>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "N", "Property name", GH_ParamAccess.item);
            pManager.AddTextParameter("Description", "D", "Optional description", GH_ParamAccess.item, string.Empty);
            pManager.AddTextParameter("Type", "T", "Property type: string, number, integer, boolean, object (default: string). When Array? is true, this defines the items type.", GH_ParamAccess.item, "string");
            pManager.AddBooleanParameter("Array?", "A", "If true, creates an array property with items of the specified Type", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Required?", "R", "If true, marks this property as required in the parent schema", GH_ParamAccess.item, false);

            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
        }

        /// <inheritdoc/>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Property", "P", "Property definition string. Connect to Json Schema component Properties input or Json Schema Object Properties input.", GH_ParamAccess.item);
        }

        /// <inheritdoc/>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string name = string.Empty;
            string description = string.Empty;
            string type = "string";
            bool isArray = false;
            bool isRequired = false;

            DA.GetData("Name", ref name);
            DA.GetData("Description", ref description);
            DA.GetData("Type", ref type);
            DA.GetData("Array?", ref isArray);
            DA.GetData("Required?", ref isRequired);

            if (string.IsNullOrWhiteSpace(name))
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Name is required.");
                return;
            }

            if (name.Contains(":"))
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Name cannot contain colons (:).");
                return;
            }

            // if (description.Contains(":"))
            // {
            //     this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Description cannot contain colons (:).");
            //     return;
            // }
            type = NormalizeType(type);
            name = name.Trim();

            // Build the result string: name:type:description or name:type:description:required
            // For arrays, encode as array[itemsType]
            string effectiveType = isArray ? $"array[{type}]" : type;

            string result;
            if (string.IsNullOrWhiteSpace(description))
            {
                result = $"{name}:{effectiveType}";
            }
            else
            {
                result = $"{name}:{effectiveType}:{description.Trim()}";
            }

            // Append :required suffix if marked as required
            if (isRequired)
            {
                result += ":required";
            }

            DA.SetData("Property", result);
        }

        /// <summary>
        /// Normalizes the type to a valid JSON Schema type.
        /// </summary>
        private static string NormalizeType(string type)
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
