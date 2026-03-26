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
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Components.Properties;

namespace SmartHopper.Components.JSON
{
    /// <summary>
    /// Grasshopper component that builds a JSON Schema from property definitions.
    /// Supports nested objects and arrays via dot-notation paths (e.g. "address.city:string").
    /// </summary>
    public class JsonSchemaComponent : GH_Component
    {
        /// <inheritdoc/>
        public override Guid ComponentGuid => new Guid("288B86E1-E748-4503-A9C9-986F4279F8CA");

        /// <inheritdoc/>
        protected override Bitmap Icon => Resources.textgenerate;

        /// <inheritdoc/>
        public override GH_Exposure Exposure => GH_Exposure.secondary;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonSchemaComponent"/> class.
        /// </summary>
        public JsonSchemaComponent()
            : base(
                  "JSON Schema",
                  "JsonSchema",
                  "Build a JSON Schema from property definitions.\n\nProperty format: \"name:type\" or \"name:type:description\"\nUse dot-notation for nested properties: \"address.city:string:The city name\"\nValid types: string, number, integer, boolean, object, array",
                  "SmartHopper", "JSON")
        {
        }

        /// <inheritdoc/>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Properties", "P", "Property definitions. Format: \"name:type\" or \"name:type:description\".\nUse dot-notation for nested properties: \"address.city:string\"", GH_ParamAccess.list);
            pManager.AddTextParameter("Required", "R", "List of required property names (top-level only)", GH_ParamAccess.list, new List<string>());
            pManager.AddTextParameter("Type", "T", "Root schema type: \"object\" or \"array\" (default: object)", GH_ParamAccess.item, "object");
            pManager.AddTextParameter("Title", "Ti", "Optional schema title", GH_ParamAccess.item, string.Empty);
            pManager.AddTextParameter("Description", "D", "Optional schema description", GH_ParamAccess.item, string.Empty);

            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
        }

        /// <inheritdoc/>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Schema", "S", "JSON Schema string", GH_ParamAccess.item);
        }

        /// <inheritdoc/>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var propertyDefs = new List<string>();
            var requiredProps = new List<string>();
            string rootType = "object";
            string title = string.Empty;
            string description = string.Empty;

            DA.GetDataList("Properties", propertyDefs);
            DA.GetDataList("Required", requiredProps);
            DA.GetData("Type", ref rootType);
            DA.GetData("Title", ref title);
            DA.GetData("Description", ref description);

            if (propertyDefs == null || propertyDefs.Count == 0)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No properties provided.");
                DA.SetData("Schema", string.Empty);
                return;
            }

            if (string.IsNullOrWhiteSpace(rootType))
            {
                rootType = "object";
            }

            rootType = rootType.Trim().ToLowerInvariant();
            if (rootType != "object" && rootType != "array")
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Invalid root type '{rootType}'. Using 'object'.");
                rootType = "object";
            }

            try
            {
                var schema = BuildSchema(propertyDefs, requiredProps, rootType, title, description);
                DA.SetData("Schema", schema.ToString(Newtonsoft.Json.Formatting.Indented));
            }
            catch (Exception ex)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Error building schema: {ex.Message}");
            }
        }

        /// <summary>
        /// Builds a JSON Schema JObject from the property definitions.
        /// Supports dot-notation paths for nested properties.
        /// </summary>
        private static JObject BuildSchema(
            List<string> propertyDefs,
            List<string> requiredProps,
            string rootType,
            string title,
            string description)
        {
            var schema = new JObject();
            schema["$schema"] = "http://json-schema.org/draft-07/schema#";
            schema["type"] = rootType;

            if (!string.IsNullOrWhiteSpace(title))
            {
                schema["title"] = title;
            }

            if (!string.IsNullOrWhiteSpace(description))
            {
                schema["description"] = description;
            }

            if (rootType == "object")
            {
                var propertiesRoot = new JObject();
                schema["properties"] = propertiesRoot;

                foreach (var def in propertyDefs)
                {
                    if (string.IsNullOrWhiteSpace(def))
                    {
                        continue;
                    }

                    ParseAndInsertProperty(def.Trim(), propertiesRoot);
                }

                if (requiredProps != null && requiredProps.Count > 0)
                {
                    schema["required"] = new JArray(requiredProps.Where(r => !string.IsNullOrWhiteSpace(r)).ToArray<object>());
                }
            }
            else
            {
                // array root: use first property def (if any) as items schema
                if (propertyDefs.Count > 0)
                {
                    var parts = SplitDefinition(propertyDefs[0]);
                    var itemType = parts.Length > 1 ? parts[1].Trim().ToLowerInvariant() : "string";
                    var items = new JObject { ["type"] = NormalizeType(itemType) };
                    if (parts.Length > 2 && !string.IsNullOrWhiteSpace(parts[2]))
                    {
                        items["description"] = parts[2].Trim();
                    }

                    schema["items"] = items;
                }
            }

            return schema;
        }

        /// <summary>
        /// Parses a property definition and inserts it into the target properties object.
        /// Supports dot-notation paths for nesting (e.g. "address.city:string:The city name").
        /// </summary>
        private static void ParseAndInsertProperty(string def, JObject targetProperties)
        {
            var parts = SplitDefinition(def);
            if (parts.Length == 0)
            {
                return;
            }

            string fullPath = parts[0].Trim();
            string propType = parts.Length > 1 ? parts[1].Trim().ToLowerInvariant() : "string";
            string propDescription = parts.Length > 2 ? parts[2].Trim() : string.Empty;

            var pathSegments = fullPath.Split('.');

            // Navigate or create nested objects for intermediate path segments
            var currentProperties = targetProperties;
            for (int i = 0; i < pathSegments.Length - 1; i++)
            {
                string segment = pathSegments[i];
                if (!currentProperties.ContainsKey(segment))
                {
                    var nestedObj = new JObject { ["type"] = "object" };
                    nestedObj["properties"] = new JObject();
                    currentProperties[segment] = nestedObj;
                }

                var existingNode = currentProperties[segment] as JObject;
                if (existingNode == null)
                {
                    return;
                }

                // Ensure nested object has a properties node
                if (existingNode["properties"] == null)
                {
                    existingNode["properties"] = new JObject();
                }

                currentProperties = existingNode["properties"] as JObject;
                if (currentProperties == null)
                {
                    return;
                }
            }

            // Insert the leaf property
            string leafName = pathSegments[pathSegments.Length - 1];
            var propSchema = new JObject { ["type"] = NormalizeType(propType) };
            if (!string.IsNullOrWhiteSpace(propDescription))
            {
                propSchema["description"] = propDescription;
            }

            // If type is object, add empty properties node
            if (propType == "object")
            {
                propSchema["properties"] = new JObject();
            }

            // If type is array, add default string items schema
            if (propType == "array")
            {
                propSchema["items"] = new JObject { ["type"] = "string" };
            }

            currentProperties[leafName] = propSchema;
        }

        /// <summary>
        /// Splits a definition string by ':' but limits to 3 parts (name, type, description).
        /// </summary>
        private static string[] SplitDefinition(string def)
        {
            var parts = def.Split(new[] { ':' }, 3);
            return parts;
        }

        /// <summary>
        /// Normalizes a type string to valid JSON Schema types.
        /// </summary>
        private static string NormalizeType(string type)
        {
            switch (type)
            {
                case "string":
                case "number":
                case "integer":
                case "boolean":
                case "object":
                case "array":
                case "null":
                    return type;
                default:
                    return "string";
            }
        }
    }
}
