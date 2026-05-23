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
using SmartHopper.Infrastructure.Utilities;

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
        protected override Bitmap Icon => Resources.jsonschema;

        /// <inheritdoc/>
        public override GH_Exposure Exposure => GH_Exposure.quarternary;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonSchemaComponent"/> class.
        /// </summary>
        public JsonSchemaComponent()
            : base(
                "JSON Schema",
                "JsonSchema",
                "Build a JSON Schema from property definitions.\n\nProperty format: \"name:type\" or \"name:type:description\"\nUse dot-notation for nested properties: \"address.city:string:The city name\"\nValid types: string, number, integer, boolean, object, array",
                "SmartHopper",
                "JSON")
        {
        }

        /// <inheritdoc/>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Title", "Ti", "Optional schema title", GH_ParamAccess.item, string.Empty);
            pManager.AddTextParameter("Description", "D", "Optional schema description", GH_ParamAccess.item, string.Empty);
            pManager.AddTextParameter("Properties", "P", "Property definitions. Format: \"name:type\" or \"name:type:description\". Use dot-notation for nested properties: \"address.city:string\". Append :required to mark property as required: \"name:type:description:required\"\nValid types: string, number, integer, boolean, object, array", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Array?", "A", "If true, creates an array schema where items are objects with the defined properties", GH_ParamAccess.item, false);

            pManager[0].Optional = true;
            pManager[1].Optional = true;
            pManager[3].Optional = true;
        }

        /// <inheritdoc/>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Schema", "S", "JSON Schema string", GH_ParamAccess.item);
        }

        /// <inheritdoc/>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string title = string.Empty;
            string description = string.Empty;
            var propertyDefs = new List<string>();
            bool isArray = false;

            DA.GetData("Title", ref title);
            DA.GetData("Description", ref description);
            DA.GetDataList("Properties", propertyDefs);
            DA.GetData("Array?", ref isArray);

            if (propertyDefs == null || propertyDefs.Count == 0)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No properties provided.");
                DA.SetData("Schema", string.Empty);
                return;
            }

            try
            {
                // Property definitions are now processed directly by BuildSchema
                // which handles :required suffix via ParseAndInsertProperty
                var schema = BuildSchema(propertyDefs, isArray, title, description);
                DA.SetData("Schema", JsonFormatHelper.JsonToString(schema));
            }
            catch (Exception ex)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Error building schema: {ex.Message}");
            }
        }

        /// <summary>
        /// Builds a JSON Schema JObject from the property definitions.
        /// Supports dot-notation paths for nested properties and handles required fields at each level.
        /// </summary>
        private static JObject BuildSchema(
            List<string> propertyDefs,
            bool isArray,
            string title,
            string description)
        {
            var schema = new JObject();
            schema["$schema"] = "http://json-schema.org/draft-07/schema#";

            if (!string.IsNullOrWhiteSpace(title))
            {
                schema["title"] = title;
            }

            if (!string.IsNullOrWhiteSpace(description))
            {
                schema["description"] = description;
            }

            // The container that holds properties for the root object (or array items)
            // parentNode is schema itself (object) or the items object (array)
            JObject parentNode;
            if (isArray)
            {
                schema["type"] = "array";
                parentNode = new JObject { ["type"] = "object" };
                schema["items"] = parentNode;
            }
            else
            {
                schema["type"] = "object";
                parentNode = schema;
            }

            var propertiesRoot = new JObject();
            parentNode["properties"] = propertiesRoot;

            foreach (var def in propertyDefs)
            {
                if (string.IsNullOrWhiteSpace(def))
                {
                    continue;
                }

                ParseAndInsertProperty(def.Trim(), parentNode);
            }

            return schema;
        }

        /// <summary>
        /// Parses a property definition and inserts it into the schema tree.
        /// The parentNode is the JObject that owns a "properties" key (root schema or array items object).
        /// Required fields are added directly to the correct parent node as the tree is built.
        /// </summary>
        private static void ParseAndInsertProperty(string def, JObject parentNode)
        {
            var parts = SplitDefinition(def);
            if (parts.Length == 0)
            {
                return;
            }

            string fullPath = parts[0].Trim();
            string rawType = parts.Length > 1 ? parts[1].Trim().ToLowerInvariant() : "string";
            string propDescription = parts.Length > 2 ? parts[2].Trim() : string.Empty;
            bool isRequired = parts.Length > 3 && parts[3].Trim().Equals("required", StringComparison.OrdinalIgnoreCase);

            string propType = rawType;
            string arrayItemsType = "object";
            if (rawType.StartsWith("array[", StringComparison.OrdinalIgnoreCase) && rawType.EndsWith("]"))
            {
                propType = "array";
                arrayItemsType = rawType.Substring(6, rawType.Length - 7);
                if (string.IsNullOrWhiteSpace(arrayItemsType))
                {
                    arrayItemsType = "string";
                }
            }

            var pathSegments = fullPath.Split('.');

            // Navigate/create nodes for each intermediate segment.
            // currentNode always points to the JObject that owns "properties" for this level.
            var currentNode = parentNode;
            for (int i = 0; i < pathSegments.Length - 1; i++)
            {
                string segment = pathSegments[i];
                var props = GetOrCreateProperties(currentNode);

                if (!props.ContainsKey(segment))
                {
                    var nestedObj = new JObject { ["type"] = "object" };
                    nestedObj["properties"] = new JObject();
                    props[segment] = nestedObj;
                }

                var segmentNode = props[segment] as JObject;
                if (segmentNode == null)
                {
                    return;
                }

                // For array[object] segments, descend into items
                if (segmentNode["type"]?.ToString() == "array")
                {
                    if (segmentNode["items"] == null)
                    {
                        segmentNode["items"] = new JObject { ["type"] = "object", ["properties"] = new JObject() };
                    }

                    currentNode = segmentNode["items"] as JObject;
                }
                else
                {
                    currentNode = segmentNode;
                }

                if (currentNode == null)
                {
                    return;
                }
            }

            // Insert the leaf property into the current node's properties
            var leafProps = GetOrCreateProperties(currentNode);
            string leafName = pathSegments[pathSegments.Length - 1];

            var propSchema = new JObject { ["type"] = NormalizeType(propType) };
            if (!string.IsNullOrWhiteSpace(propDescription))
            {
                propSchema["description"] = propDescription;
            }

            if (propType == "object")
            {
                propSchema["properties"] = new JObject();
            }

            if (propType == "array")
            {
                var itemsObj = new JObject { ["type"] = NormalizeType(arrayItemsType) };
                if (arrayItemsType == "object")
                {
                    itemsObj["properties"] = new JObject();
                }

                propSchema["items"] = itemsObj;
            }

            leafProps[leafName] = propSchema;

            // Add to required array on the direct parent node
            if (isRequired)
            {
                AddToRequired(currentNode, leafName);
            }
        }

        /// <summary>
        /// Gets or creates the "properties" JObject on a node.
        /// </summary>
        private static JObject GetOrCreateProperties(JObject node)
        {
            if (node["properties"] is JObject existing)
            {
                return existing;
            }

            var props = new JObject();
            node["properties"] = props;
            return props;
        }

        /// <summary>
        /// Appends a property name to the "required" array on a node, creating it if needed.
        /// </summary>
        private static void AddToRequired(JObject node, string propertyName)
        {
            if (node["required"] is JArray existing)
            {
                if (!existing.Any(t => t.ToString() == propertyName))
                {
                    existing.Add(propertyName);
                }
            }
            else
            {
                node["required"] = new JArray(propertyName);
            }
        }

        /// <summary>
        /// Splits a definition string supporting format:
        /// - "name:type"
        /// - "name:type:description"
        /// - "name:type:description:required" (or any description ending with :required)
        /// Detects :required by checking the last 9 characters, allowing descriptions to contain colons.
        /// </summary>
        private static string[] SplitDefinition(string def)
        {
            string trimmed = def.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                return new string[0];
            }

            const string requiredSuffix = ":required";
            bool isRequired = false;
            string remaining = trimmed;

            // Check if string ends with :required (case insensitive, last 9 chars)
            if (trimmed.EndsWith(requiredSuffix, StringComparison.OrdinalIgnoreCase))
            {
                isRequired = true;
                remaining = trimmed.Substring(0, trimmed.Length - requiredSuffix.Length);
            }

            // Split remaining by first two colons only
            var parts = remaining.Split(new[] { ':' }, 3);

            var result = new string[4];
            result[0] = parts[0].Trim(); // name
            result[1] = parts.Length > 1 ? parts[1].Trim() : "string"; // type (default string)
            result[2] = parts.Length > 2 ? parts[2].Trim() : string.Empty; // description
            result[3] = isRequired ? "required" : string.Empty;

            return result;
        }

        /// <summary>
        /// Normalizes a type string to valid JSON Schema types.
        /// Strips any array[itemsType] encoding before comparing.
        /// </summary>
        private static string NormalizeType(string type)
        {
            if (string.IsNullOrWhiteSpace(type))
            {
                return "string";
            }

            // Strip array[...] encoding if present
            string baseType = type;
            if (type.StartsWith("array[") && type.EndsWith("]"))
            {
                baseType = "array";
            }

            switch (baseType)
            {
                case "string":
                case "number":
                case "integer":
                case "boolean":
                case "object":
                case "array":
                case "null":
                    return baseType;
                default:
                    return "string";
            }
        }
    }
}
