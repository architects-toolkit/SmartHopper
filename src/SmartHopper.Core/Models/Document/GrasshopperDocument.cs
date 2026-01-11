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
using System.Linq;
using Newtonsoft.Json;
using SmartHopper.Core.Models.Components;
using SmartHopper.Core.Models.Connections;
using SmartHopper.Core.Serialization;

namespace SmartHopper.Core.Models.Document
{
    /// <summary>
    /// Represents a complete Grasshopper document with components and their connections.
    /// </summary>
    public class GrasshopperDocument
    {
        /// <summary>
        /// Gets or sets the GhJSON schema version.
        /// </summary>
        [JsonProperty("schemaVersion", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(EmptyStringIgnoreConverter))]
        public string SchemaVersion { get; set; }

        /// <summary>
        /// Gets or sets the document metadata (optional).
        /// </summary>
        [JsonProperty("metadata", NullValueHandling = NullValueHandling.Ignore)]
        public DocumentMetadata Metadata { get; set; }

        /// <summary>
        /// Gets or sets list of all components in the document.
        /// </summary>
        [JsonProperty("components")]
        public List<ComponentProperties> Components { get; set; } = new List<ComponentProperties>();

        /// <summary>
        /// Gets or sets list of all connections between components in the document.
        /// </summary>
        [JsonProperty("connections")]
        public List<ConnectionPairing> Connections { get; set; } = new List<ConnectionPairing>();

        /// <summary>
        /// Gets or sets list of all groups in the document (optional).
        /// </summary>
        [JsonProperty("groups", NullValueHandling = NullValueHandling.Ignore)]
        public List<GroupInfo> Groups { get; set; }

        /// <summary>
        /// Gets all components with validation issues (errors or warnings).
        /// </summary>
        /// <returns>A list of components that have either errors or warnings.</returns>
        public List<ComponentProperties> GetComponentsWithIssues()
        {
            return this.Components.Where(c => c.HasIssues).ToList();
        }

        /// <summary>
        /// Gets all connections for a specific component.
        /// </summary>
        /// <param name="componentId">The integer ID of the component to get connections for.</param>
        /// <returns>A list of all connections involving the specified component.</returns>
        public List<ConnectionPairing> GetComponentConnections(int componentId)
        {
            return this.Connections.Where(c =>
                c.From.Id == componentId ||
                c.To.Id == componentId)
            .ToList();
        }

        /// <summary>
        /// Gets all input connections for a specific component.
        /// </summary>
        /// <param name="componentId">The integer ID of the component to get input connections for.</param>
        /// <returns>A list of connections where the specified component is the target.</returns>
        public List<ConnectionPairing> GetComponentInputs(int componentId)
        {
            return this.Connections.Where(c => c.To.Id == componentId).ToList();
        }

        /// <summary>
        /// Gets all output connections for a specific component.
        /// </summary>
        /// <param name="componentId">The integer ID of the component to get output connections for.</param>
        /// <returns>A list of connections where the specified component is the source.</returns>
        public List<ConnectionPairing> GetComponentOutputs(int componentId)
        {
            return this.Connections.Where(c => c.From.Id == componentId).ToList();
        }

        /// <summary>
        /// Creates a mapping from component integer IDs to their instance GUIDs.
        /// This is useful for translating connections (which use integer IDs) to component GUIDs.
        /// </summary>
        /// <returns>Dictionary mapping integer ID to GUID.</returns>
        public Dictionary<int, Guid> GetIdToGuidMapping()
        {
            return this.Components
                .Where(c => c.Id.HasValue)
                .ToDictionary(c => c.Id.Value, c => c.InstanceGuid);
        }

        /// <summary>
        /// Creates a mapping from component instance GUIDs to their integer IDs.
        /// This is useful for creating connections from component GUIDs.
        /// </summary>
        /// <returns>Dictionary mapping GUID to integer ID.</returns>
        public Dictionary<Guid, int> GetGuidToIdMapping()
        {
            return this.Components
                .Where(c => c.Id.HasValue)
                .ToDictionary(c => c.InstanceGuid, c => c.Id.Value);
        }
    }
}
