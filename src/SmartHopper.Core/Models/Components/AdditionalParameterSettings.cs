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
        /// Gets or sets a value indicating whether the parameter inverts boolean/numeric values.
        /// </summary>
        [JsonProperty("invert", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Invert { get; set; }
    }
}
