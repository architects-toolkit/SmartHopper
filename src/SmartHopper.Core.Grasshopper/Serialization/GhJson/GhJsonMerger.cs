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
using System.Diagnostics;
using System.Linq;
using SmartHopper.Core.Models.Components;
using SmartHopper.Core.Models.Connections;
using SmartHopper.Core.Models.Document;

namespace SmartHopper.Core.Grasshopper.Serialization.GhJson
{
    /// <summary>
    /// Result of a GhJSON merge operation.
    /// </summary>
    public class MergeResult
    {
        /// <summary>Gets or sets the merged document.</summary>
        public GrasshopperDocument Document { get; set; }

        /// <summary>Gets or sets the number of components added from the source.</summary>
        public int ComponentsAdded { get; set; }

        /// <summary>Gets or sets the number of components that were duplicates (same GUID).</summary>
        public int ComponentsDuplicated { get; set; }

        /// <summary>Gets or sets the number of connections added from the source.</summary>
        public int ConnectionsAdded { get; set; }

        /// <summary>Gets or sets the number of connections that were duplicates.</summary>
        public int ConnectionsDuplicated { get; set; }

        /// <summary>Gets or sets the number of groups added from the source.</summary>
        public int GroupsAdded { get; set; }

        /// <summary>Gets or sets the set of external component GUIDs (components from source not in target).</summary>
        public HashSet<Guid> ExternalComponentGuids { get; set; } = new HashSet<Guid>();

        /// <summary>Gets or sets the ID remapping from source IDs to merged IDs.</summary>
        public Dictionary<int, int> IdRemapping { get; set; } = new Dictionary<int, int>();
    }

    /// <summary>
    /// Merges two GhJSON documents into one.
    /// Handles component, connection, and group merging with proper ID remapping and deduplication.
    /// </summary>
    public static class GhJsonMerger
    {
        /// <summary>
        /// Merges a source document into a target document.
        /// Target always wins on conflicts - duplicate components (by GUID) from source are skipped.
        /// Connections and groups are remapped to use the merged component IDs.
        /// </summary>
        /// <param name="target">The target document to merge into. This document is modified in place.</param>
        /// <param name="source">The source document to merge from.</param>
        /// <returns>Result containing merge statistics and the merged document.</returns>
        public static MergeResult Merge(GrasshopperDocument target, GrasshopperDocument source)
        {
            var result = new MergeResult { Document = target };

            if (target == null || source == null)
            {
                return result;
            }

            // Initialize target collections if needed
            target.Components ??= new List<ComponentProperties>();
            target.Connections ??= new List<ConnectionPairing>();
            target.Groups ??= new List<GroupInfo>();

            // Build GUID→ID mapping for target components
            var targetGuidToId = new Dictionary<Guid, int>();
            int maxId = 0;

            foreach (var comp in target.Components.Where(c => c.Id.HasValue))
            {
                targetGuidToId[comp.InstanceGuid] = comp.Id.Value;
                maxId = Math.Max(maxId, comp.Id.Value);
            }

            // Merge components from source
            if (source.Components != null)
            {
                foreach (var sourceComp in source.Components)
                {
                    if (targetGuidToId.TryGetValue(sourceComp.InstanceGuid, out var existingId))
                    {
                        // Component already exists in target - target wins, just remap source ID to target ID
                        if (sourceComp.Id.HasValue)
                        {
                            result.IdRemapping[sourceComp.Id.Value] = existingId;
                        }

                        result.ComponentsDuplicated++;
                        Debug.WriteLine($"[GhJsonMerger] Target wins - skipping source component: {sourceComp.Name} ({sourceComp.InstanceGuid})");
                    }
                    else
                    {
                        // New component - assign new ID and add
                        var newId = ++maxId;
                        if (sourceComp.Id.HasValue)
                        {
                            result.IdRemapping[sourceComp.Id.Value] = newId;
                        }

                        // Clone the component with new ID
                        var mergedComp = CloneComponentProperties(sourceComp);
                        mergedComp.Id = newId;

                        target.Components.Add(mergedComp);
                        targetGuidToId[mergedComp.InstanceGuid] = newId;
                        result.ExternalComponentGuids.Add(mergedComp.InstanceGuid);
                        result.ComponentsAdded++;

                        Debug.WriteLine($"[GhJsonMerger] Added component: {mergedComp.Name} ({mergedComp.InstanceGuid}) → ID {newId}");
                    }
                }
            }

            // Build existing connections key set for deduplication
            var existingConnectionKeys = target.Connections
                .Select(conn => GetConnectionKey(conn))
                .ToHashSet();

            // Merge connections from source (with ID remapping)
            if (source.Connections != null)
            {
                foreach (var sourceConn in source.Connections)
                {
                    // Remap IDs
                    var fromId = RemapId(sourceConn.From.Id, result.IdRemapping);
                    var toId = RemapId(sourceConn.To.Id, result.IdRemapping);

                    // Create remapped connection
                    var mergedConn = new ConnectionPairing
                    {
                        From = new Connection
                        {
                            Id = fromId,
                            ParamName = sourceConn.From.ParamName,
                            ParamIndex = sourceConn.From.ParamIndex
                        },
                        To = new Connection
                        {
                            Id = toId,
                            ParamName = sourceConn.To.ParamName,
                            ParamIndex = sourceConn.To.ParamIndex
                        }
                    };

                    var key = GetConnectionKey(mergedConn);
                    if (existingConnectionKeys.Contains(key))
                    {
                        result.ConnectionsDuplicated++;
                        Debug.WriteLine($"[GhJsonMerger] Skipping duplicate connection: {key}");
                    }
                    else
                    {
                        target.Connections.Add(mergedConn);
                        existingConnectionKeys.Add(key);
                        result.ConnectionsAdded++;
                        Debug.WriteLine($"[GhJsonMerger] Added connection: {key}");
                    }
                }
            }

            // Merge groups from source (with ID remapping)
            if (source.Groups != null)
            {
                foreach (var sourceGroup in source.Groups)
                {
                    // Remap component IDs in group (Members = component IDs in compact form)
                    var remappedMemberIds = sourceGroup.Members?
                        .Select(id => RemapId(id, result.IdRemapping))
                        .ToList() ?? new List<int>();

                    var mergedGroup = new GroupInfo
                    {
                        InstanceGuid = sourceGroup.InstanceGuid,
                        Name = sourceGroup.Name,
                        Color = sourceGroup.Color,
                        Members = remappedMemberIds,
                    };

                    target.Groups.Add(mergedGroup);
                    result.GroupsAdded++;
                    Debug.WriteLine($"[GhJsonMerger] Added group: {mergedGroup.Name}");
                }
            }

            Debug.WriteLine($"[GhJsonMerger] Merge complete: +{result.ComponentsAdded} components ({result.ComponentsDuplicated} dupes), +{result.ConnectionsAdded} connections ({result.ConnectionsDuplicated} dupes), +{result.GroupsAdded} groups");

            return result;
        }

        /// <summary>
        /// Creates a unique key for a connection (for deduplication).
        /// </summary>
        private static string GetConnectionKey(ConnectionPairing conn)
        {
            return $"{conn.From.Id}:{conn.From.ParamName}->{conn.To.Id}:{conn.To.ParamName}";
        }

        /// <summary>
        /// Remaps an ID using the remapping dictionary, or returns the original if not found.
        /// </summary>
        private static int RemapId(int id, Dictionary<int, int> remapping)
        {
            return remapping.TryGetValue(id, out var newId) ? newId : id;
        }

        /// <summary>
        /// Creates a shallow clone of ComponentProperties.
        /// </summary>
        private static ComponentProperties CloneComponentProperties(ComponentProperties source)
        {
            return new ComponentProperties
            {
                Id = source.Id,
                InstanceGuid = source.InstanceGuid,
                ComponentGuid = source.ComponentGuid,
                Name = source.Name,
                NickName = source.NickName,
                Pivot = source.Pivot,
                Params = source.Params,
                InputSettings = source.InputSettings,
                OutputSettings = source.OutputSettings,
                ComponentState = source.ComponentState
            };
        }
    }
}
