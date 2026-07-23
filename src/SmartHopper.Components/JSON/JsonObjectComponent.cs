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
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Components.Properties;
using SmartHopper.ProviderSdk.Utilities;

namespace SmartHopper.Components.JSON
{
    /// <summary>
    /// Grasshopper component that creates a JSON object from key-value pairs.
    /// </summary>
    public class JsonObjectComponent : GH_Component
    {
        /// <inheritdoc/>
        public override Guid ComponentGuid => new Guid("9299C148-3301-4FBC-8CE4-EBD495F99B29");

        /// <inheritdoc/>
        protected override Bitmap Icon => Resources.jsonobj;

        /// <inheritdoc/>
        public override GH_Exposure Exposure => GH_Exposure.secondary;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonObjectComponent"/> class.
        /// </summary>
        public JsonObjectComponent()
            : base(
                "JSON Object",
                "JsonObject",
                "Create a JSON object from key-value pairs.\nNumbers, booleans and valid JSON arrays/objects are auto-coerced from their string representation.",
                "SmartHopper",
                "JSON")
        {
        }

        /// <inheritdoc/>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Key", "K", "List of property keys", GH_ParamAccess.list);
            pManager.AddGenericParameter("Value", "V", "List of values (number, boolean, string, or nested JSON auto-coerced)", GH_ParamAccess.list);
        }

        /// <inheritdoc/>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("JSON", "J", "JSON object string", GH_ParamAccess.item);
        }

        /// <inheritdoc/>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var keys = new List<string>();
            var values = new List<IGH_Goo>();

            DA.GetDataList("Key", keys);
            DA.GetDataList("Value", values);

            if (keys.Count == 0)
            {
                DA.SetData("JSON", "{}");
                return;
            }

            if (keys.Count != values.Count)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Key count ({keys.Count}) must match Value count ({values.Count}).");
                return;
            }

            try
            {
                var obj = new JObject();
                for (int i = 0; i < keys.Count; i++)
                {
                    string key = keys[i];
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Empty key at index {i} skipped.");
                        continue;
                    }

                    obj[key] = CoerceToJToken(values[i]);
                }

                DA.SetData("JSON", JsonFormatHelper.JsonToString(obj));
            }
            catch (Exception ex)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Error building JSON object: {ex.Message}");
            }
        }

        /// <summary>
        /// Coerces a Grasshopper goo value to a JToken.
        /// Numbers, booleans and valid JSON arrays/objects are detected automatically.
        /// </summary>
        private static JToken CoerceToJToken(IGH_Goo goo)
        {
            if (goo == null)
            {
                return JValue.CreateNull();
            }

            string str = goo.ToString();

            // Attempt to parse as a JSON token (object, array, number, boolean, null)
            if (!string.IsNullOrWhiteSpace(str))
            {
                var trimmed = str.Trim();

                // Boolean
                if (trimmed.Equals("true", StringComparison.OrdinalIgnoreCase))
                {
                    return new JValue(true);
                }

                if (trimmed.Equals("false", StringComparison.OrdinalIgnoreCase))
                {
                    return new JValue(false);
                }

                // Null
                if (trimmed.Equals("null", StringComparison.OrdinalIgnoreCase))
                {
                    return JValue.CreateNull();
                }

                // Number
                if (double.TryParse(trimmed, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double numVal))
                {
                    return new JValue(numVal);
                }

                // JSON object or array
                if ((trimmed.StartsWith("{") && trimmed.EndsWith("}")) ||
                    (trimmed.StartsWith("[") && trimmed.EndsWith("]")))
                {
                    try
                    {
                        return JToken.Parse(trimmed);
                    }
                    catch
                    {
                        // Fall through to string
                    }
                }
            }

            return new JValue(str);
        }
    }
}
