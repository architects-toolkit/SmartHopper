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

using Newtonsoft.Json;

namespace SmartHopper.Core.Models.Components
{
    /// <summary>
    /// Represents additional settings for parameters such as flags and modifiers.
    /// </summary>
    public class AdditionalParameterSettings
    {

        /// <summary>
        /// Gets or sets a value indicating whether the parameter data should be reversed.
        /// </summary>
        [JsonProperty("reverse", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Reverse { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the parameter data tree should be simplified.
        /// </summary>
        [JsonProperty("simplify", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Simplify { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the parameter is principal for matching.
        /// </summary>
        [JsonProperty("isPrincipal", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsPrincipal { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the parameter is locked.
        /// </summary>
        [JsonProperty("locked", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Locked { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the parameter inverts boolean values (Param_Boolean only).
        /// </summary>
        [JsonProperty("invert", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Invert { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether vectors should be unitized (Param_Vector only).
        /// </summary>
        [JsonProperty("unitize", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Unitize { get; set; }
    }
}
