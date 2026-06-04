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
using SmartHopper.Components.Properties;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Core.Models;
using SmartHopper.Core.Types;

namespace SmartHopper.Components.Input
{
    /// <summary>
    /// Wraps text input into an AIInputPayload for AI processing.
    /// </summary>
    public class Text2AIComponent : AIInputAdapterBase
    {
        public override Guid ComponentGuid => new Guid("1B98B6D8-1F08-4981-BDE8-9FFB6B7F2646");

        protected override Bitmap Icon => Resources.toaitext;

        public Text2AIComponent()
            : base(
                  "Text to AI",
                  "Text2AI",
                  "Wraps text input into an AIInputPayload for AI processing.",
                  GH_Exposure.secondary)
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Text", "T", "Text content to wrap into an AIInputPayload.", GH_ParamAccess.item);
        }

        protected override string PayloadOutputDescription => "AIInputPayload wrapping the text.";

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string text = null;
            if (!DA.GetData(0, ref text))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Text cannot be empty.");
                return;
            }

            try
            {
                var payload = this.CreateTextPayload(text);
                DA.SetData(0, this.WrapPayload(payload));
            }
            catch (Exception ex)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Error creating payload: {ex.Message}");
            }
        }
    }
}
