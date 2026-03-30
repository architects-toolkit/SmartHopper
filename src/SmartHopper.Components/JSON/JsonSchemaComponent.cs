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
        public override GH_Exposure Exposure => GH_Exposure.quarternary;

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
                DA.SetData("Schema", schema.ToString(Newtonsoft.Json.Formatting.Indented));
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

            // Build the properties object (used for both object root and array items)
            var propertiesRoot = new JObject();

            // Track required properties at each nesting level: key = path (e.g., "relationship_N"), value = list of required props
            var requiredAtLevel = new Dictionary<string, List<string>>();

            foreach (var def in propertyDefs)
            {
                if (string.IsNullOrWhiteSpace(def))
                {
                    continue;
                }

                ParseAndInsertProperty(def.Trim(), propertiesRoot, requiredAtLevel);
            }

            // Add required arrays at appropriate levels
            AddRequiredArrays(propertiesRoot, requiredAtLevel, string.Empty);

            if (isArray)
            {
                // Array schema: items are objects with the defined properties
                schema["type"] = "array";
                var items = new JObject { ["type"] = "object" };

                if (propertiesRoot.Count > 0)
                {
                    items["properties"] = propertiesRoot;
                }

                schema["items"] = items;
            }
            else
            {
                // Object schema
                schema["type"] = "object";

                if (propertiesRoot.Count > 0)
                {
                    schema["properties"] = propertiesRoot;
                }
            }

            return schema;
        }

        /// <summary>
        /// Parses a property definition and inserts it into the target properties object.
        /// Supports dot-notation paths for nesting (e.g. "address.city:string:The city name").
        /// Tracks required properties at each nesting level.
        /// </summary>
        private static void ParseAndInsertProperty(string def, JObject targetProperties, Dictionary<string, List<string>> requiredAtLevel)
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

            // Parse optional array item type encoded as "array[itemsType]" by JsonSchemaPropArrayComponent
            string propType = rawType;
            string arrayItemsType = "string";
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

            // Track required at appropriate level
            if (isRequired)
            {
                if (pathSegments.Length == 1)
                {
                    // Top-level property
                    if (!requiredAtLevel.ContainsKey(string.Empty))
                    {
                        requiredAtLevel[string.Empty] = new List<string>();
                    }

                    requiredAtLevel[string.Empty].Add(pathSegments[0]);
                }
                else
                {
                    // Nested property: parent path is all segments except last
                    string parentPath = string.Join(".", pathSegments.Take(pathSegments.Length - 1));
                    string leafName = pathSegments[pathSegments.Length - 1];
                    if (!requiredAtLevel.ContainsKey(parentPath))
                    {
                        requiredAtLevel[parentPath] = new List<string>();
                    }

                    requiredAtLevel[parentPath].Add(leafName);
                }
            }

            // Navigate or create nested objects for intermediate path segments
            var currentProperties = targetProperties;
            for (int i = 0; i < pathSegments.Length - 1; i++)
            {
                string segment = pathSegments[i];
                string remainingPath = string.Join(".", pathSegments.Take(i + 1));
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
            string finalLeafName = pathSegments[pathSegments.Length - 1];
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

            // If type is array, set items type (uses arrayItemsType parsed above, defaults to string)
            if (propType == "array")
            {
                propSchema["items"] = new JObject { ["type"] = NormalizeType(arrayItemsType) };
            }

            currentProperties[finalLeafName] = propSchema;
        }

        /// <summary>
        /// Recursively adds required arrays to nested objects based on the tracking dictionary.
        /// </summary>
        /// <param name="properties">The properties object to process.</param>
        /// <param name="requiredAtLevel">Dictionary mapping paths to required property names.</param>
        /// <param name="currentPath">The current path in the hierarchy (empty string for root).</param>
        private static void AddRequiredArrays(JObject properties, Dictionary<string, List<string>> requiredAtLevel, string currentPath)
        {
            // Add required array at current level if there are any
            if (requiredAtLevel.TryGetValue(currentPath, out var requiredList) && requiredList.Count > 0)
            {
                // Find the parent object that contains these properties
                JObject parentObject = FindParentObjectForPath(currentPath, properties);
                if (parentObject != null)
                {
                    parentObject["required"] = new JArray(requiredList.Distinct().ToArray<object>());
                }
            }

            // Recurse into nested object properties
            foreach (var prop in properties.Properties())
            {
                var propValue = prop.Value as JObject;
                if (propValue == null)
                {
                    continue;
                }

                // If this is an object type with properties, recurse
                if (propValue["type"]?.ToString() == "object" && propValue["properties"] is JObject nestedProps)
                {
                    string newPath = string.IsNullOrEmpty(currentPath) ? prop.Name : $"{currentPath}.{prop.Name}";
                    AddRequiredArrays(nestedProps, requiredAtLevel, newPath);
                }

                // If this is an array of objects, recurse into items
                if (propValue["type"]?.ToString() == "array" && propValue["items"] is JObject itemsObj)
                {
                    var itemsType = itemsObj["type"]?.ToString();
                    if (itemsType == "object" && itemsObj["properties"] is JObject arrayItemProps)
                    {
                        string newPath = string.IsNullOrEmpty(currentPath) ? prop.Name : $"{currentPath}.{prop.Name}";
                        AddRequiredArrays(arrayItemProps, requiredAtLevel, newPath);
                    }
                }
            }
        }

        /// <summary>
        /// Finds the parent JObject for a given path in the properties hierarchy.
        /// </summary>
        private static JObject FindParentObjectForPath(string path, JObject rootProperties)
        {
            if (string.IsNullOrEmpty(path))
            {
                return rootProperties.Parent as JObject; // The root schema object
            }

            var segments = path.Split('.');
            var current = rootProperties;

            foreach (var segment in segments)
            {
                if (current.ContainsKey(segment) && current[segment] is JObject obj)
                {
                    current = obj["properties"] as JObject;
                    if (current == null)
                    {
                        return null;
                    }
                }
                else
                {
                    return null;
                }
            }

            // current is now the properties object, we need its parent
            return current.Parent as JObject;
        }

        /// <summary>
        /// Splits a definition string by ':' supporting format:
        /// - "name:type"
        /// - "name:type:description"
        /// - "name:type:description:required"
        /// </summary>
        private static string[] SplitDefinition(string def)
        {
            var parts = def.Split(':');
            if (parts.Length >= 4)
            {
                // Combine all parts after the first 3 back into description
                var result = new string[4];
                result[0] = parts[0];
                result[1] = parts[1];
                result[2] = string.Join(":", parts.Skip(2).Take(parts.Length - 3));
                result[3] = parts[parts.Length - 1];
                return result;
            }

            return parts;
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
