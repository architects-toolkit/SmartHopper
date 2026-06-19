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
using Newtonsoft.Json;
using SmartHopper.Components.Properties;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Core.Types;

namespace SmartHopper.Components.Input
{
    /// <summary>
    /// Wraps a list of booleans into a single AIInputPayload as a JSON array.
    /// </summary>
    public class BooleanList2AIComponent : AIInputAdapterBase
    {
        public override Guid ComponentGuid => new Guid("8314FFFC-201C-4C63-94D6-DEC66A0F791E");

        protected override Bitmap Icon => Resources.toaibool;

        public BooleanList2AIComponent()
            : base(
                "Boolean List to AI",
                "BoolList2AI",
                "Wraps a list of booleans into a single AIInputPayload as a JSON array (one array per branch).",
                GH_Exposure.secondary)
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Boolean List", "B", "List of booleans to collapse into a single JSON array payload.", GH_ParamAccess.list);
        }

        protected override string PayloadOutputDescription => "AIInputPayload wrapping the JSON-serialized list.";

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var items = new List<bool>();
            if (!DA.GetDataList(0, items) || items.Count == 0)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Boolean List cannot be empty.");
                return;
            }

            try
            {
                var json = JsonConvert.SerializeObject(items);
                var payload = this.CreateTextPayload(json);
                DA.SetData(0, this.WrapPayload(payload));
            }
            catch (Exception ex)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Error creating payload: {ex.Message}");
            }
        }
    }
}
