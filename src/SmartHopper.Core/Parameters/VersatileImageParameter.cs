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
using Grasshopper.Kernel;
using SmartHopper.Core.Types;

namespace SmartHopper.Core.Parameters
{
    /// <summary>
    /// Grasshopper parameter for GH_VersatileImage.
    /// </summary>
    public class VersatileImageParameter : GH_Param<GH_VersatileImage>
    {
        /// <summary>
        /// Gets the unique component GUID.
        /// </summary>
        public override Guid ComponentGuid => new Guid("F6391051-88D6-4532-8E28-1743712BA187");

        /// <summary>
        /// Initializes a new instance of the <see cref="VersatileImageParameter"/> class.
        /// </summary>
        public VersatileImageParameter()
            : base(
                "VersatileImage",
                "VImg",
                "A VersatileImage type accepting Bitmap, file paths, URLs, base64, data-URIs, and extracted document images with metadata.",
                "SmartHopper",
                "Parameters",
                GH_ParamAccess.item)
        {
        }

        /// <inheritdoc/>
        public override GH_Exposure Exposure => GH_Exposure.hidden;

        /// <inheritdoc/>
        protected override GH_VersatileImage PreferredCast(object data)
        {
            if (data is GH_VersatileImage versatile)
            {
                return versatile;
            }

            if (data is VersatileImage source)
            {
                return new GH_VersatileImage(source);
            }

            var goo = new GH_VersatileImage();
            if (goo.CastFrom(data))
            {
                return goo;
            }

            return null;
        }
    }
}
