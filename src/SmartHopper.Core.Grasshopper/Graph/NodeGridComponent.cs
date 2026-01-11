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

namespace SmartHopper.Core.Grasshopper.Graph
{
    /// <summary>
    /// Represents a node in the dependency graph grid with position and connection information.
    /// </summary>
    public class NodeGridComponent
    {
        /// <summary>
        /// Component instance GUID.
        /// </summary>
        public Guid ComponentId { get; set; }

        /// <summary>
        /// Position on the canvas/grid.
        /// </summary>
        public PointF Pivot { get; set; }

        /// <summary>
        /// Parent connections (upstream dependencies).
        /// Key: Parent component GUID, Value: Connection count.
        /// </summary>
        public Dictionary<Guid, int> Parents { get; set; } = new Dictionary<Guid, int>();

        /// <summary>
        /// Child connections (downstream dependencies).
        /// Key: Child component GUID, Value: Connection count.
        /// </summary>
        public Dictionary<Guid, int> Children { get; set; } = new Dictionary<Guid, int>();

        /// <summary>
        /// Target GUIDs for edge concentration nodes.
        /// </summary>
        public List<Guid> Targets { get; set; } = new List<Guid>();
    }
}
