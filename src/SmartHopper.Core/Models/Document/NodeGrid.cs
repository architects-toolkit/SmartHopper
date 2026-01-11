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
 * along with this library; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using System;
using System.Collections.Generic;
using System.Drawing;

namespace SmartHopper.Core.Models.Document
{
    /// <summary>
    /// Represents a component in the grid with pivot, parent and child relationships.
    /// </summary>
    public class NodeGridComponent
    {
        /// <summary>Gets or sets component's GUID.</summary>
        public Guid ComponentId { get; set; }

        /// <summary>Gets or sets calculated pivot position.</summary>
        public PointF Pivot { get; set; }

        /// <summary>Gets or sets mapping of parent component IDs to the input parameter index (incoming edges) on this component.</summary>
        public Dictionary<Guid, int> Parents { get; set; } = new Dictionary<Guid, int>();

        /// <summary>Gets or sets mapping of child component IDs to the output parameter index (outgoing edges) from this component.</summary>
        public Dictionary<Guid, int> Children { get; set; } = new Dictionary<Guid, int>();
    }
}
