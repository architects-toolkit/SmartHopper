/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using Grasshopper.Kernel;
using Newtonsoft.Json;

namespace SmartHopper.Core.Models.Connections
{
    /// <summary>
    /// Represents a connection between two components in a Grasshopper document.
    /// </summary>
    public class ConnectionPairing
    {
        /// <summary>
        /// Gets or sets the source endpoint of the connection.
        /// </summary>
        [JsonProperty("from")]
        [JsonRequired]
        public required Connection From { get; set; }

        /// <summary>
        /// Gets or sets the target endpoint of the connection.
        /// </summary>
        [JsonProperty("to")]
        [JsonRequired]
        public required Connection To { get; set; }

        /// <summary>
        /// Checks if both endpoints of the connection are valid.
        /// </summary>
        /// <returns>True if both the source and target endpoints are valid.</returns>
        public bool IsValid()
        {
            return this.To.IsValid() && this.From.IsValid();
        }

        /// <summary>
        /// Creates a new ConnectionPairing from source and target parameters.
        /// </summary>
        /// <param name="source">The source parameter.</param>
        /// <param name="target">The target parameter.</param>
        /// <returns>A new ConnectionPairing connecting the specified parameters.</returns>
        public static ConnectionPairing Create(IGH_Param source, IGH_Param target)
        {
            return new ConnectionPairing
            {
                From = Connection.FromParameter(source),
                To = Connection.FromParameter(target),
            };
        }
    }
}
