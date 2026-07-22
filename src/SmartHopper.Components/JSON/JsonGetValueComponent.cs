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
using Newtonsoft.Json.Linq;
using SmartHopper.Components.Properties;
using SmartHopper.ProviderSdk.Utilities;

namespace SmartHopper.Components.JSON
{
    /// <summary>
    /// Grasshopper component that extracts a nested value from a JSON object using dot-notation or bracket notation paths.
    /// Supports paths like "address.city", "results[0].name", or "items[5]".
    /// </summary>
    public class JsonGetValueComponent : GH_Component
    {
        /// <inheritdoc/>
        public override Guid ComponentGuid => new Guid("703A62A1-CDEA-4BFE-AA26-B6DF3F04D0C0");

        /// <inheritdoc/>
        protected override Bitmap Icon => Resources.textgenerate;

        /// <inheritdoc/>
        public override GH_Exposure Exposure => GH_Exposure.secondary;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonGetValueComponent"/> class.
        /// </summary>
        public JsonGetValueComponent()
            : base(
                "JSON Get Value",
                "JsonGetValue",
                "Extract a nested value from a JSON object using dot-notation path.\nExample: \"address.city\" extracts the city from the address object.",
                "SmartHopper",
                "JSON")
        {
        }

        /// <inheritdoc/>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("JSON", "J", "JSON string (object or array)", GH_ParamAccess.item);
            pManager.AddTextParameter("Path", "P", "Dot-notation path to the value (e.g. \"address.city\")", GH_ParamAccess.item);
        }

        /// <inheritdoc/>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Value", "V", "Extracted value as a string. JSON strings have outer quotes stripped; objects/arrays are compact JSON.", GH_ParamAccess.item);
        }

        /// <inheritdoc/>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string json = string.Empty;
            string path = string.Empty;

            DA.GetData("JSON", ref json);
            DA.GetData("Path", ref path);

            if (string.IsNullOrWhiteSpace(json))
            {
                DA.SetData("Value", string.Empty);
                return;
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Path is empty. Returning full JSON.");
                DA.SetData("Value", json.Trim());
                return;
            }

            try
            {
                var token = JToken.Parse(json.Trim());
                var result = NavigatePath(token, path.Trim());

                if (result == null)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"JSON Path '{path}' not found in JSON.");
                    DA.SetData("Value", string.Empty);
                    return;
                }

                DA.SetData("Value", TokenToString(result));
            }
            catch (Exception ex)
            {
                string errorMessage = JsonPathHelper.FormatJsonPathError(path, ex.Message);
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, errorMessage);
            }
        }

        /// <summary>
        /// Navigates a path within a JToken tree, supporting both dot notation (e.g., "address.city")
        /// and bracket notation (e.g., "results[0].name" or "items[5]").
        /// </summary>
        private static JToken NavigatePath(JToken root, string path)
        {
            if (root == null || string.IsNullOrWhiteSpace(path))
            {
                return root;
            }

            JToken current = root;
            var segments = ParsePathSegments(path);

            foreach (var segment in segments)
            {
                if (current == null)
                {
                    return null;
                }

                if (segment.IsArrayIndex)
                {
                    if (current is JArray arr)
                    {
                        current = segment.Index >= 0 && segment.Index < arr.Count ? arr[segment.Index] : null;
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    if (current is JObject obj)
                    {
                        current = obj[segment.Name];
                    }
                    else
                    {
                        return null;
                    }
                }
            }

            return current;
        }

        /// <summary>
        /// Parses a path string into segments, handling both dot notation and bracket notation.
        /// Examples: "address.city" → ["address", "city"]
        ///           "results[0].name" → ["results", "[0]", "name"]
        ///           "items[5]" → ["items", "[5]"]
        /// </summary>
        private static System.Collections.Generic.List<PathSegment> ParsePathSegments(string path)
        {
            var segments = new System.Collections.Generic.List<PathSegment>();
            var current = new System.Text.StringBuilder();

            for (int i = 0; i < path.Length; i++)
            {
                char ch = path[i];

                if (ch == '.')
                {
                    if (current.Length > 0)
                    {
                        segments.Add(new PathSegment { Name = current.ToString() });
                        current.Clear();
                    }
                }
                else if (ch == '[')
                {
                    if (current.Length > 0)
                    {
                        segments.Add(new PathSegment { Name = current.ToString() });
                        current.Clear();
                    }

                    int endBracket = path.IndexOf(']', i);
                    if (endBracket > i + 1)
                    {
                        string indexStr = path.Substring(i + 1, endBracket - i - 1);
                        if (int.TryParse(indexStr, out int index))
                        {
                            segments.Add(new PathSegment { IsArrayIndex = true, Index = index });
                        }

                        i = endBracket;
                    }
                }
                else
                {
                    current.Append(ch);
                }
            }

            if (current.Length > 0)
            {
                segments.Add(new PathSegment { Name = current.ToString() });
            }

            return segments;
        }

        /// <summary>
        /// Represents a single segment in a JSON path.
        /// </summary>
        private class PathSegment
        {
            /// <summary>
            /// Gets or sets the property name (for object navigation).
            /// </summary>
            public string Name { get; set; } = string.Empty;

            /// <summary>
            /// Gets or sets a value indicating whether this segment is an array index.
            /// </summary>
            public bool IsArrayIndex { get; set; }

            /// <summary>
            /// Gets or sets the array index (when IsArrayIndex is true).
            /// </summary>
            public int Index { get; set; }
        }

        /// <summary>
        /// Converts a JToken to its string representation,
        /// stripping outer quotes for JSON strings.
        /// </summary>
        private static string TokenToString(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return string.Empty;
            }

            if (token.Type == JTokenType.String)
            {
                return token.Value<string>() ?? string.Empty;
            }

            return token.ToString(Newtonsoft.Json.Formatting.None);
        }
    }
}
