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

namespace SmartHopper.Components.JSON
{
    /// <summary>
    /// Grasshopper component that creates a JSON array from a list of items.
    /// </summary>
    public class JsonArrayComponent : GH_Component
    {
        /// <inheritdoc/>
        public override Guid ComponentGuid => new Guid("68048C57-E60E-4A6C-A59A-B2C78EB4B1D9");

        /// <inheritdoc/>
        protected override Bitmap Icon => Resources.jsonarray;

        /// <inheritdoc/>
        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonArrayComponent"/> class.
        /// </summary>
        public JsonArrayComponent()
            : base(
                "JSON Array",
                "JsonArray",
                "Create a JSON array from a list of items.\nNumbers, booleans and valid JSON arrays/objects are auto-coerced from their string representation.",
                "SmartHopper",
                "JSON")
        {
        }

        /// <inheritdoc/>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Items", "I", "List of items to include in the array (number, boolean, string, or nested JSON auto-coerced)", GH_ParamAccess.list);
        }

        /// <inheritdoc/>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("JSON", "J", "JSON array string", GH_ParamAccess.item);
        }

        /// <inheritdoc/>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var items = new List<IGH_Goo>();
            DA.GetDataList("Items", items);

            if (items == null || items.Count == 0)
            {
                DA.SetData("JSON", "[]");
                return;
            }

            try
            {
                var array = new JArray();
                foreach (var item in items)
                {
                    array.Add(CoerceToJToken(item));
                }

                DA.SetData("JSON", array.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Error building JSON array: {ex.Message}");
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

            if (!string.IsNullOrWhiteSpace(str))
            {
                var trimmed = str.Trim();

                if (trimmed.Equals("true", StringComparison.OrdinalIgnoreCase))
                {
                    return new JValue(true);
                }

                if (trimmed.Equals("false", StringComparison.OrdinalIgnoreCase))
                {
                    return new JValue(false);
                }

                if (trimmed.Equals("null", StringComparison.OrdinalIgnoreCase))
                {
                    return JValue.CreateNull();
                }

                if (double.TryParse(trimmed, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double numVal))
                {
                    return new JValue(numVal);
                }

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
