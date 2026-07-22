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
using Newtonsoft.Json;
using SmartHopper.Components.Properties;
using SmartHopper.Core.ComponentBase;

namespace SmartHopper.Components.Input
{
    /// <summary>
    /// Wraps a number into an AIInputPayload as a JSON scalar.
    /// </summary>
    public class Number2AIComponent : AIInputAdapterBase
    {
        public override Guid ComponentGuid => new Guid("5AF79992-7CBF-4554-87C7-80350A0C2F20");

        protected override Bitmap Icon => Resources.toainumeric;

        public Number2AIComponent()
            : base(
                "Number to AI",
                "Num2AI",
                "Wraps a number into an AIInputPayload as a JSON scalar.",
                GH_Exposure.primary)
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Number", "N", "Number to wrap into an AIInputPayload.", GH_ParamAccess.item);
        }

        protected override string PayloadOutputDescription => "AIInputPayload wrapping the JSON-serialized number.";

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            double value = 0;
            if (!DA.GetData(0, ref value))
            {
                return;
            }

            try
            {
                var json = JsonConvert.SerializeObject(value);
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
