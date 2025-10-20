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

namespace SmartHopper.Core.Models.Connections
{
    /// <summary>
    /// Represents a connection endpoint in a Grasshopper document.
    /// </summary>
    public class Connection
    {
        /// <summary>
        /// Gets or sets the integer ID of the component.
        /// </summary>
        [JsonProperty("id", Order = 1)]
        [JsonRequired]
        public required int Id { get; set; }

        /// <summary>
        /// Gets or sets the name of the parameter on the component.
        /// </summary>
        [JsonProperty("paramName", Order = 2)]
        [JsonRequired]
        public required string ParamName { get; set; }

        /// <summary>
        /// Checks if the connection has valid component ID and parameter name.
        /// </summary>
        /// <returns>True if the connection has a valid ID and parameter name.</returns>
        public bool IsValid()
        {
            return this.Id > 0 && !string.IsNullOrEmpty(this.ParamName);
        }

    }
}
