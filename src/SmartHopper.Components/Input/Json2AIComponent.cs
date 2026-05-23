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
using SmartHopper.Core.ComponentBase;
using SmartHopper.Core.Models;
using SmartHopper.Core.Types;

namespace SmartHopper.Components.Input
{
    /// <summary>
    /// Validates and wraps JSON text into an AIInputPayload for AI processing.
    /// </summary>
    public class Json2AIComponent : AIInputAdapterBase
    {
        public override Guid ComponentGuid => new Guid("F5EBC521-05A0-41D6-8052-A284608EE86B");

        protected override Bitmap Icon => Resources.json2ai;

        public Json2AIComponent()
            : base(
                  "JSON to AI",
                  "Json2AI",
                  "Wraps JSON input into an AIInputPayload for AI processing.",
                  GH_Exposure.secondary)
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("JSON", "J", "JSON text to validate and wrap into an AIInputPayload.", GH_ParamAccess.item);
        }

        protected override string PayloadOutputDescription => "AIInputPayload wrapping the validated JSON.";

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string json = null;
            if (!DA.GetData(0, ref json))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "JSON cannot be empty.");
                return;
            }

            try
            {
                // Validate JSON
                JToken.Parse(json);

                var payload = this.CreateTextPayload(json);
                DA.SetData(0, this.WrapPayload(payload));
            }
            catch (Exception ex)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Invalid JSON: {ex.Message}");
            }
        }
    }
}
