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
using System.Linq;
using Newtonsoft.Json;
using SmartHopper.Core.Models.Components;
using SmartHopper.Core.Models.Connections;

namespace SmartHopper.Core.Models.Document
{
    /// <summary>
    /// Represents a complete Grasshopper document with components and their connections.
    /// </summary>
    public class GrasshopperDocument
    {
        /// <summary>
        /// List of all components in the document.
        /// </summary>
        [JsonProperty("components")]
        public List<ComponentProperties> Components { get; set; } = new List<ComponentProperties>();

        /// <summary>
        /// List of all connections between components in the document.
        /// </summary>
        [JsonProperty("connections")]
        public List<ConnectionPairing> Connections { get; set; } = new List<ConnectionPairing>();

        /// <summary>
        /// Gets all components with validation issues (errors or warnings).
        /// </summary>
        /// <returns>A list of components that have either errors or warnings</returns>
        public List<ComponentProperties> GetComponentsWithIssues()
        {
            return Components.Where(c => c.HasIssues).ToList();
        }

        /// <summary>
        /// Gets all connections for a specific component.
        /// </summary>
        /// <param name="componentId">The ID of the component to get connections for</param>
        /// <returns>A list of all connections involving the specified component</returns>
        public List<ConnectionPairing> GetComponentConnections(Guid componentId)
        {
            return Connections.Where(c =>
                c.From.ComponentId == componentId ||
                c.To.ComponentId == componentId
            ).ToList();
        }

        /// <summary>
        /// Gets all input connections for a specific component.
        /// </summary>
        /// <param name="componentId">The ID of the component to get input connections for</param>
        /// <returns>A list of connections where the specified component is the target</returns>
        public List<ConnectionPairing> GetComponentInputs(Guid componentId)
        {
            return Connections.Where(c => c.To.ComponentId == componentId).ToList();
        }

        /// <summary>
        /// Gets all output connections for a specific component.
        /// </summary>
        /// <param name="componentId">The ID of the component to get output connections for</param>
        /// <returns>A list of connections where the specified component is the source</returns>
        public List<ConnectionPairing> GetComponentOutputs(Guid componentId)
        {
            return Connections.Where(c => c.From.ComponentId == componentId).ToList();
        }
    }
}
