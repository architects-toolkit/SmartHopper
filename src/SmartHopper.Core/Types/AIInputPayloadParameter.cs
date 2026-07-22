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

using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using SmartHopper.Core.Models;

namespace SmartHopper.Core.Types
{
    /// <summary>
    /// Grasshopper parameter type for AIInputPayload.
    /// Allows wiring AIInputPayload between components.
    /// </summary>
    public class AIInputPayloadParameter : GH_Param<GH_AIInputPayload>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AIInputPayloadParameter"/> class.
        /// </summary>
        public AIInputPayloadParameter()
            : base("AIInputPayload", "Payload", "An AI interaction payload for wiring between components", "SmartHopper", "AI", GH_ParamAccess.item)
        {
        }

        /// <summary>
        /// Gets the unique ID for this parameter type.
        /// </summary>
        public override System.Guid ComponentGuid => new System.Guid("5206698B-29FB-48B2-824F-A0B6AF81A359");

        /// <summary>
        /// Gets the icon for this parameter.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => null;
    }
}
