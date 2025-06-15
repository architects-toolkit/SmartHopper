/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
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
        /// <summary>Component's GUID.</summary>
        public Guid ComponentId { get; set; }

        /// <summary>Calculated pivot position.</summary>
        public PointF Pivot { get; set; }

        /// <summary>Mapping of parent component IDs to the input parameter index (incoming edges) on this component.</summary>
        public Dictionary<Guid, int> Parents { get; set; } = new Dictionary<Guid, int>();

        /// <summary>Mapping of child component IDs to the output parameter index (outgoing edges) from this component.</summary>
        public Dictionary<Guid, int> Children { get; set; } = new Dictionary<Guid, int>();
    }
}
