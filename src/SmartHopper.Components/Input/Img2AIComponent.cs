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
using SmartHopper.Core.Parameters;
using SmartHopper.Core.Types;

namespace SmartHopper.Components.Input
{
    /// <summary>
    /// Wraps image input (Bitmap, file path, URL, base64, or data-URI) into an AIInputPayload for AI vision processing.
    /// Auto-detects the image source kind and converts to appropriate format.
    /// </summary>
    public class Img2AIComponent : AIInputAdapterBase
    {
        public override Guid ComponentGuid => new Guid("538086D1-E762-4F13-8305-42109B045722");

        protected override Bitmap Icon => Resources.toaiimg;

        public Img2AIComponent()
            : base(
                  "Image to AI",
                  "Img2AI",
                  "Wraps image input into an AIInputPayload for AI processing.",
                  GH_Exposure.secondary)
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddParameter(new VersatileImageParameter(), "Image", "I", "Image source (Bitmap, file path, URL, base64, or data-URI).", GH_ParamAccess.item);
        }

        protected override string PayloadOutputDescription => "AIInputPayload wrapping the image for AI vision processing.";

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var imageGoo = new GH_VersatileImage();
            if (!DA.GetData(0, ref imageGoo) || imageGoo.Value == null)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Image input is required.");
                return;
            }

            try
            {
                var imageSource = imageGoo.Value;
                var payload = this.CreateImagePayload(imageSource);
                DA.SetData(0, this.WrapPayload(payload));
            }
            catch (Exception ex)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Error creating image payload: {ex.Message}");
            }
        }
    }
}
