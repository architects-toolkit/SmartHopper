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
    /// Wraps a list of integers into a single AIInputPayload as a JSON array.
    /// </summary>
    public class IntegerList2AIComponent : AIInputAdapterBase
    {
        public override Guid ComponentGuid => new Guid("D309A738-7B77-401A-8BAA-D1D6EF2EBBA2");

        protected override Bitmap Icon => Resources.IntegerList2AI;

        public IntegerList2AIComponent()
            : base(
                "Integer List to AI",
                "IntList2AI",
                "Package a list of integers as a single AI input (one list per branch) to wire into any AI output component.",
                GH_Exposure.secondary | GH_Exposure.obscure)
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddIntegerParameter("Integer List", "I", "List of integers to send together in a single AI request.", GH_ParamAccess.list);
        }

        protected override string PayloadOutputDescription => "Packaged integer list ready to wire into any AI output component's 'Input >' parameter.";

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var items = new List<int>();
            if (!DA.GetDataList(0, items) || items.Count == 0)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Integer List cannot be empty.");
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
