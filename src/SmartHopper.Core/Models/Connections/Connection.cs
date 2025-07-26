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
using Grasshopper.Kernel;
using Newtonsoft.Json;

namespace SmartHopper.Core.Models.Connections
{
    /// <summary>
    /// Represents a connection endpoint in a Grasshopper document.
    /// </summary>
    public class Connection
    {
        /// <summary>
        /// Gets or sets the ID of the component that this connection endpoint belongs to.
        /// </summary>
        [JsonProperty("instanceId")]
        [JsonRequired]
        public Guid InstanceId { get; set; }

        /// <summary>
        /// Gets or sets the name of the parameter on the component.
        /// </summary>
        [JsonProperty("paramName")]
        [JsonRequired]
        public required string ParamName { get; set; }

        /// <summary>
        /// Checks if the connection has valid component ID and parameter name.
        /// </summary>
        /// <returns>True if the connection has a non-empty GUID and parameter name.</returns>
        public bool IsValid()
        {
            return this.InstanceId != Guid.Empty && !string.IsNullOrEmpty(this.ParamName);
        }

        /// <summary>
        /// Creates a new Connection from a Grasshopper parameter.
        /// </summary>
        /// <param name="param">The Grasshopper parameter to create the connection from.</param>
        /// <returns>A new Connection object representing the parameter.</returns>
        public static Connection? FromParameter(IGH_Param param)
        {
            if (param == null)
            {
                return null;
            }

            return new Connection
            {
                InstanceId = param.InstanceGuid,
                ParamName = param.Name,
            };
        }
    }
}
