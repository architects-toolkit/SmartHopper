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
using System.Text;
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;
using SmartHopper.Components.Properties;
using SmartHopper.ProviderSdk.Utilities;

namespace SmartHopper.Components.JSON
{
    /// <summary>
    /// Grasshopper component that sets a nested value in a JSON object using dot-notation or bracket notation paths.
    /// Supports paths like "address.city", "results[0].name", or "items[5]".
    /// </summary>
    public class JsonSetValueComponent : GH_Component
    {
        /// <inheritdoc/>
        public override Guid ComponentGuid => new Guid("A2117C14-09F4-4993-8ADD-2451F7F74FCD");

        /// <inheritdoc/>
        protected override Bitmap Icon => Resources.jsonitem;

        /// <inheritdoc/>
        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonSetValueComponent"/> class.
        /// </summary>
        public JsonSetValueComponent()
            : base(
                "JSON Set Value",
                "JsonSetValue",
                "Set a nested value in a JSON object using a dot-notation path.\nExample: \"address.city\" sets the city in the address object.",
                "SmartHopper",
                "JSON")
        {
        }

        /// <inheritdoc/>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("JSON", "J", "JSON string (object or array)", GH_ParamAccess.item);
            pManager.AddTextParameter("Path", "P", "Dot-notation path to the value (e.g. \"address.city\")", GH_ParamAccess.item);
            pManager.AddTextParameter("Value", "V", "Value to set. JSON literals (123, true, [1,2], {\"a\":1}) are parsed; otherwise treated as a string.", GH_ParamAccess.item);
        }

        /// <inheritdoc/>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("JSON", "J", "Updated JSON string", GH_ParamAccess.item);
        }

        /// <inheritdoc/>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string json = string.Empty;
            string path = string.Empty;
            string valueText = string.Empty;

            DA.GetData("JSON", ref json);
            DA.GetData("Path", ref path);
            DA.GetData("Value", ref valueText);

            if (string.IsNullOrWhiteSpace(json))
            {
                DA.SetData("JSON", "{}");
                return;
            }

            try
            {
                var root = JToken.Parse(json.Trim());
                var value = ParseValue(valueText);

                if (string.IsNullOrWhiteSpace(path))
                {
                    DA.SetData("JSON", JsonFormatHelper.JsonToString(value));
                    return;
                }

                if (TrySetValue(root, path.Trim(), value, out var errorMessage))
                {
                    DA.SetData("JSON", JsonFormatHelper.JsonToString(root));
                }
                else
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, JsonPathHelper.FormatJsonPathError(path, errorMessage));
                }
            }
            catch (Exception ex)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, JsonPathHelper.FormatJsonPathError(path, ex.Message));
            }
        }

        /// <summary>
        /// Parses the value input as JSON when possible; otherwise treats it as a plain string.
        /// </summary>
        private static JToken ParseValue(string valueText)
        {
            if (string.IsNullOrWhiteSpace(valueText))
            {
                return string.Empty;
            }

            var trimmed = valueText.Trim();
            try
            {
                return JToken.Parse(trimmed);
            }
            catch
            {
                return new JValue(valueText);
            }
        }

        /// <summary>
        /// Sets a value at the specified path, creating missing intermediate object properties.
        /// </summary>
        private static bool TrySetValue(JToken root, string path, JToken value, out string errorMessage)
        {
            errorMessage = string.Empty;
            var segments = ParsePathSegments(path);

            if (segments.Count == 0)
            {
                errorMessage = "Path is empty.";
                return false;
            }

            var current = root;
            for (int i = 0; i < segments.Count - 1; i++)
            {
                var segment = segments[i];

                if (segment.IsArrayIndex)
                {
                    if (!(current is JArray arr))
                    {
                        errorMessage = $"Expected an array at segment '{segment}' but found '{current.Type}'.";
                        return false;
                    }

                    if (segment.Index < 0 || segment.Index >= arr.Count)
                    {
                        errorMessage = $"Array index {segment.Index} is out of range.";
                        return false;
                    }

                    current = arr[segment.Index];
                }
                else
                {
                    if (!(current is JObject obj))
                    {
                        errorMessage = $"Expected an object at segment '{segment}' but found '{current.Type}'.";
                        return false;
                    }

                    if (obj[segment.Name] is JToken child)
                    {
                        current = child;
                    }
                    else
                    {
                        var newObj = new JObject();
                        obj[segment.Name] = newObj;
                        current = newObj;
                    }
                }
            }

            var last = segments[segments.Count - 1];
            if (last.IsArrayIndex)
            {
                if (!(current is JArray arr))
                {
                    errorMessage = $"Expected an array at final segment '{last}' but found '{current.Type}'.";
                    return false;
                }

                if (last.Index < 0 || last.Index > arr.Count)
                {
                    errorMessage = $"Array index {last.Index} is out of range.";
                    return false;
                }

                if (last.Index == arr.Count)
                {
                    arr.Add(value);
                }
                else
                {
                    arr[last.Index] = value;
                }
            }
            else
            {
                if (!(current is JObject finalObj))
                {
                    errorMessage = $"Expected an object at final segment '{last}' but found '{current.Type}'.";
                    return false;
                }

                finalObj[last.Name] = value;
            }

            return true;
        }

        /// <summary>
        /// Parses a path string into segments, handling both dot notation and bracket notation.
        /// </summary>
        private static List<PathSegment> ParsePathSegments(string path)
        {
            var segments = new List<PathSegment>();
            var current = new StringBuilder();

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
            /// Gets or sets the array index (when <see cref="IsArrayIndex"/> is true).
            /// </summary>
            public int Index { get; set; }

            /// <inheritdoc/>
            public override string ToString()
            {
                return this.IsArrayIndex ? $"[{this.Index}]" : this.Name;
            }
        }
    }
}
