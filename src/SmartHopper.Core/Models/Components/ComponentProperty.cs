/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using Newtonsoft.Json;

namespace SmartHopper.Core.Models.Components
{
    /// <summary>
    /// Represents a property of a Grasshopper component.
    /// </summary>
    public class ComponentProperty
    {
        /// <summary>
        /// Gets or sets the actual value of the property.
        /// </summary>
        [JsonProperty("value")]
        public object Value { get; set; }
    }
}
