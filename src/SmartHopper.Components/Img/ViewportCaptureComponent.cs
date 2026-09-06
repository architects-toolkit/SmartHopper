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
using Newtonsoft.Json.Linq;
using SmartHopper.Components.Properties;

namespace SmartHopper.Components.Img
{
    /// <summary>
    /// Captures the active or named Rhino viewport as a persistent VersatileImage.
    /// </summary>
    public sealed class ViewportCaptureComponent : ScreenshotCaptureComponentBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ViewportCaptureComponent"/> class.
        /// </summary>
        public ViewportCaptureComponent()
            : base(
                "Viewport To Image",
                "Viewport2Img",
                "Capture the active or named Rhino viewport as a persistent VersatileImage when Run is triggered.",
                "viewport_screenshot")
        {
        }

        /// <inheritdoc/>
        public override Guid ComponentGuid => new Guid("1DC325F6-D9C6-465E-B5AD-4DADFD8C1852");

        /// <inheritdoc/>
        public override IEnumerable<string> Keywords => new[]
        {
            "Viewport Capture",
            "Viewport Screenshot",
            "Viewport To Image",
            "viewport_screenshot",
            "Rhino Screenshot",
            "VersatileImage",
        };

        /// <inheritdoc/>
        protected override Bitmap Icon => Resources.aitoimg;

        /// <inheritdoc/>
        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
            ArgumentNullException.ThrowIfNull(pManager);
            pManager.AddTextParameter("View Name", "V", "Optional Rhino viewport name. Leave empty to capture the active view.", GH_ParamAccess.item, string.Empty);
            pManager.AddIntegerParameter("Width", "W", "Maximum output width in pixels.", GH_ParamAccess.item, 1024);
            pManager.AddIntegerParameter("Height", "H", "Maximum output height in pixels.", GH_ParamAccess.item, 1024);
        }

        /// <inheritdoc/>
        protected override void RegisterCaptureOutputParams(GH_OutputParamManager pManager)
        {
            ArgumentNullException.ThrowIfNull(pManager);
            pManager.AddTextParameter("View Name", "V", "Name of the captured Rhino viewport.", GH_ParamAccess.item);
        }

        /// <inheritdoc/>
        protected override JObject BuildCaptureArguments(IGH_DataAccess dataAccess)
        {
            ArgumentNullException.ThrowIfNull(dataAccess);
            string viewName = string.Empty;
            int width = 1024;
            int height = 1024;
            dataAccess.GetData("View Name", ref viewName);
            dataAccess.GetData("Width", ref width);
            dataAccess.GetData("Height", ref height);
            return new JObject
            {
                ["viewName"] = viewName,
                ["width"] = width,
                ["height"] = height,
            };
        }

        /// <inheritdoc/>
        protected override void SetCaptureOutputs(JObject result, IGH_DataAccess dataAccess)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(dataAccess);
            this.SetPersistentOutput("View Name", result["viewName"]?.ToString() ?? string.Empty, dataAccess);
        }
    }
}
