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
        /// Resolves the connection endpoints from integer IDs to GUIDs using the provided mapping.
        /// </summary>
        /// <param name="idToGuidMapping">Mapping from integer ID to GUID.</param>
        /// <param name="fromGuid">Output: The resolved source component GUID.</param>
        /// <param name="toGuid">Output: The resolved target component GUID.</param>
        /// <returns>True if both endpoints were successfully resolved.</returns>
        public bool TryResolveGuids(Dictionary<int, Guid> idToGuidMapping, out Guid fromGuid, out Guid toGuid)
        {
            fromGuid = Guid.Empty;
            toGuid = Guid.Empty;

            return idToGuidMapping.TryGetValue(this.From.Id, out fromGuid) &&
                   idToGuidMapping.TryGetValue(this.To.Id, out toGuid);
        }

    }
}
