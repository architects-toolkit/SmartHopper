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
    /// composed from a list of sub-property definition strings.
    /// Output is compatible with <see cref="JsonSchemaComponent"/> Properties input.
    /// </summary>
    /// <remarks>
    /// Each sub-property string is prefixed with the object property name using dot-notation,
    /// so the entire set can be directly merged into a JsonSchemaComponent Properties list.
    ///
    /// Visual wiring pattern:
    ///   JsonSchemaProp("street", "string") ──┐
    ///   JsonSchemaProp("city",   "string") ──┼─► JsonSchemaPropObject("address") ──► JsonSchemaComponent.Properties
    ///   JsonSchemaProp("zip",    "string") ──┘
    /// </remarks>
    public class JsonSchemaPropObjectComponent : GH_Component
    {
        /// <inheritdoc/>
        public override Guid ComponentGuid => new Guid("E338184C-F82F-41D3-A465-85F20CB0B663");

        /// <inheritdoc/>
        protected override Bitmap Icon => Resources.textgenerate;

        /// <inheritdoc/>
        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonSchemaPropObjectComponent"/> class.
        /// </summary>
        public JsonSchemaPropObjectComponent()
            : base(
                  "JSON Schema Property (Object)",
                  "JsonSchemaPropObj",
                  "Build a JSON Schema object property definition composed of sub-properties.\n\nEach sub-property is prefixed with the object name using dot-notation.\nConnect sub-property outputs from JsonSchemaProp or JsonSchemaPropObject to Sub-Properties.\nThe output list can be merged with other properties and fed directly into JsonSchemaComponent.",
                  "SmartHopper", "JSON")
        {
        }

        /// <inheritdoc/>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "N", "Object property name (e.g. \"address\")", GH_ParamAccess.item);
            pManager.AddTextParameter("Sub-Properties", "P", "List of sub-property definition strings from JsonSchemaProp or JsonSchemaPropObject components", GH_ParamAccess.list);
            pManager.AddTextParameter("Description", "D", "Optional description for this object property", GH_ParamAccess.item, string.Empty);
            pManager.AddTextParameter("Required", "R", "Optional list of required sub-property names within this object", GH_ParamAccess.list);

            pManager[2].Optional = true;
            pManager[3].Optional = true;
        }

        /// <inheritdoc/>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Properties", "P", "List of property definition strings with dot-notation prefixes. Connect to JsonSchemaComponent Properties input.", GH_ParamAccess.list);
            pManager.AddTextParameter("Required Names", "R", "Required sub-property names prefixed with dot-notation (e.g. \"address.city\"). Connect to JsonSchemaComponent Required input alongside other required names.", GH_ParamAccess.list);
        }

        /// <inheritdoc/>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string name = string.Empty;
            var subProperties = new List<string>();
            string description = string.Empty;
            var required = new List<string>();

            DA.GetData("Name", ref name);
            DA.GetDataList("Sub-Properties", subProperties);
            DA.GetData("Description", ref description);
            DA.GetDataList("Required", required);

            if (string.IsNullOrWhiteSpace(name))
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Name is required.");
                return;
            }

            name = name.Trim();

            if (subProperties == null || subProperties.Count == 0)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No sub-properties provided. The object will have no properties.");
            }

            var output = new List<string>();

            // Emit the object property declaration itself
            string objectDeclaration = string.IsNullOrWhiteSpace(description)
                ? $"{name}:object"
                : $"{name}:object:{description.Trim()}";
            output.Add(objectDeclaration);

            // Prefix each sub-property with this object's name
            if (subProperties != null)
            {
                foreach (var sub in subProperties)
                {
                    if (string.IsNullOrWhiteSpace(sub))
                    {
                        continue;
                    }

                    output.Add($"{name}.{sub.Trim()}");
                }
            }

            DA.SetDataList("Properties", output);

            // Emit required sub-property names as dot-prefixed strings
            var requiredOutput = new List<string>();
            if (required != null)
            {
                foreach (var r in required)
                {
                    if (!string.IsNullOrWhiteSpace(r))
                    {
                        requiredOutput.Add($"{name}.{r.Trim()}");
                    }
                }
            }

            DA.SetDataList("Required Names", requiredOutput);
        }
    }
}
