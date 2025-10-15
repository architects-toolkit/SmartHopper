/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

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
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the group color in ARGB format (e.g., "255,0,200,0").
        /// </summary>
        [JsonProperty("color", NullValueHandling = NullValueHandling.Ignore)]
        public string Color { get; set; }

        /// <summary>
        /// Gets or sets the list of component instance GUIDs that belong to this group.
        /// </summary>
        [JsonProperty("members")]
        public List<Guid> Members { get; set; } = new List<Guid>();
    }
}
