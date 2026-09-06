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
    /// Captures the visible Grasshopper canvas as a persistent VersatileImage.
    /// </summary>
    public sealed class CanvasCaptureComponent : ScreenshotCaptureComponentBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CanvasCaptureComponent"/> class.
        /// </summary>
        public CanvasCaptureComponent()
            : base(
                "Canvas To Image",
                "Canvas2Img",
                "Capture the visible Grasshopper canvas as a persistent VersatileImage when Run is triggered.",
                "canvas_screenshot")
        {
        }

        /// <inheritdoc/>
        public override Guid ComponentGuid => new Guid("22786AAD-D6E9-40E7-A57B-F9B75167DA50");

        /// <inheritdoc/>
        public override IEnumerable<string> Keywords => new[]
        {
            "Canvas Capture",
            "Canvas Screenshot",
            "Canvas To Image",
            "canvas_screenshot",
            "Grasshopper Screenshot",
            "VersatileImage",
        };

        /// <inheritdoc/>
        protected override Bitmap Icon => Resources.aitoimg;

        /// <inheritdoc/>
        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
            ArgumentNullException.ThrowIfNull(pManager);
            pManager.AddIntegerParameter("Max Width", "W", "Maximum output width in pixels.", GH_ParamAccess.item, 1920);
            pManager.AddIntegerParameter("Max Height", "H", "Maximum output height in pixels.", GH_ParamAccess.item, 1080);
        }

        /// <inheritdoc/>
        protected override JObject BuildCaptureArguments(IGH_DataAccess dataAccess)
        {
            ArgumentNullException.ThrowIfNull(dataAccess);
            int maxWidth = 1920;
            int maxHeight = 1080;
            dataAccess.GetData("Max Width", ref maxWidth);
            dataAccess.GetData("Max Height", ref maxHeight);
            return new JObject
            {
                ["maxWidth"] = maxWidth,
                ["maxHeight"] = maxHeight,
            };
        }
    }
}
