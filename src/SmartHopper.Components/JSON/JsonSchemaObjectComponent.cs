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
using System.Linq;
using Grasshopper.Kernel;
using SmartHopper.Components.Properties;

namespace SmartHopper.Components.JSON
{
    /// <summary>
    /// Grasshopper component that builds a JSON Schema object property definition,
    /// composed from a list of property definition strings.
    /// Output is compatible with <see cref="JsonSchemaComponent"/> Properties input.
    /// </summary>
    /// <remarks>
    /// Each property string is prefixed with the object property name using dot-notation,
    /// so the entire set can be directly merged into a JsonSchemaComponent Properties list.
    ///
    /// Visual wiring pattern:
    ///   JsonSchemaProp("street", "string") ──┐
    ///   JsonSchemaProp("city",   "string") ──┼─► JsonSchemaObject("address") ──► JsonSchemaComponent.Properties
    ///   JsonSchemaProp("zip",    "string") ──┘
    /// </remarks>
    public class JsonSchemaObjectComponent : GH_Component
    {
        /// <inheritdoc/>
        public override Guid ComponentGuid => new Guid("E338184C-F82F-41D3-A465-85F20CB0B663");

        /// <inheritdoc/>
        protected override Bitmap Icon => Resources.jsonschemaobj;

        /// <inheritdoc/>
        public override GH_Exposure Exposure => GH_Exposure.quarternary;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonSchemaObjectComponent"/> class.
        /// </summary>
        public JsonSchemaObjectComponent()
            : base(
                "JSON Schema Object",
                "JsonSchemaObj",
                "Build a JSON Schema object property definition composed of Properties.\n\nEach property is prefixed with the object name using dot-notation.\nConnect property outputs from JsonSchemaProp to Properties.\nThe output list can be merged with other properties and fed directly into Json Schema component.",
                "SmartHopper",
                "JSON")
        {
        }

        /// <inheritdoc/>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "N", "Object property name (e.g. \"address\")", GH_ParamAccess.item);
            pManager.AddTextParameter("Description", "D", "Optional description for this object property", GH_ParamAccess.item, string.Empty);
            pManager.AddTextParameter("Properties", "P", "List of property definition strings from JsonSchemaProp components", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Array?", "A", "If true, creates an array of objects property (e.g., 'addresses:array[object]')", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Required?", "R", "If true, marks this object property as required in the parent schema", GH_ParamAccess.item, false);

            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
        }

        /// <inheritdoc/>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Properties", "P", "List of property definition strings with dot-notation prefixes. Connect to Json Schema component Properties input. Properties marked with :required suffix are preserved and will be auto-extracted by Json Schema component.", GH_ParamAccess.list);
        }

        /// <inheritdoc/>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string name = string.Empty;
            var properties = new List<string>();
            string description = string.Empty;
            bool isArray = false;
            bool isRequired = false;

            DA.GetData("Name", ref name);
            DA.GetData("Description", ref description);
            DA.GetDataList("Properties", properties);
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
            name = name.Trim();

            if (properties == null || properties.Count == 0)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No Properties provided. The object will have no properties.");
            }

            var output = new List<string>();

            // Emit the object property declaration itself
            string effectiveType = isArray ? "array[object]" : "object";
            string objectDeclaration;
            if (string.IsNullOrWhiteSpace(description))
            {
                objectDeclaration = $"{name}:{effectiveType}";
            }
            else
            {
                objectDeclaration = $"{name}:{effectiveType}:{description.Trim()}";
            }

            // Append :required suffix if marked as required
            if (isRequired)
            {
                objectDeclaration += ":required";
            }

            output.Add(objectDeclaration);

            // Prefix each property with this object's name, preserving :required suffix
            if (properties != null)
            {
                foreach (var sub in properties)
                {
                    if (string.IsNullOrWhiteSpace(sub))
                    {
                        continue;
                    }

                    string trimmedSub = sub.Trim();

                    // Add to properties output (preserve :required suffix if present)
                    output.Add($"{name}.{trimmedSub}");
                }
            }

            DA.SetDataList("Properties", output);
        }
    }
}
