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
using Newtonsoft.Json;
using SmartHopper.Core.Serialization;

namespace SmartHopper.Core.Models.Document
{
    /// <summary>
    /// Represents a Grasshopper group with its members and properties.
    /// </summary>
    public class GroupInfo
    {
        /// <summary>
        /// Gets or sets the unique identifier for this group instance.
        /// </summary>
        [JsonProperty("instanceGuid")]
        public Guid InstanceGuid { get; set; }

        /// <summary>
        /// Gets or sets the name/nickname of the group.
        /// </summary>
        [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(EmptyStringIgnoreConverter))]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the group color in ARGB format (e.g., "255,0,200,0").
        /// </summary>
        [JsonProperty("color", NullValueHandling = NullValueHandling.Ignore)]
        public string Color { get; set; }

        /// <summary>
        /// Gets or sets the list of component IDs that belong to this group.
        /// Uses integer IDs instead of GUIDs for compact representation.
        /// </summary>
        [JsonProperty("members")]
        public List<int> Members { get; set; } = new List<int>();
    }
}
