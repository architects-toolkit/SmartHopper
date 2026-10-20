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
 * along with this library; if not, see <https://www.gnu.org/licenses/lgpl-3.0.html>.
 */

using System;
using System.Collections.Generic;
using GhJSON.Core.SchemaModels;

namespace SmartHopper.Core.Grasshopper.Utils.Canvas
{
    /// <summary>
    /// Rewrites the sequential component ids of GhJSON snapshots into ids that stay stable per instance GUID
    /// across snapshots of the same document. GhPatch matches connections by endpoint ids, so without this
    /// adding or removing a component would shift later ids and report spurious connection changes.
    /// </summary>
    public sealed class GhJsonStableIdMap
    {
        private readonly Dictionary<Guid, int> idsByGuid = new Dictionary<Guid, int>();
        private int nextId = 1;

        /// <summary>
        /// Rewrites component ids, connection endpoints and group members of <paramref name="document"/> in place.
        /// </summary>
        /// <param name="document">The snapshot to normalize.</param>
        public void Apply(GhJsonDocument document)
        {
            if (document == null)
            {
                return;
            }

            var remap = new Dictionary<int, int>();
            foreach (var component in document.Components)
            {
                int stableId;
                if (component.InstanceGuid.HasValue && component.InstanceGuid.Value != Guid.Empty)
                {
                    if (!this.idsByGuid.TryGetValue(component.InstanceGuid.Value, out stableId))
                    {
                        stableId = this.nextId++;
                        this.idsByGuid[component.InstanceGuid.Value] = stableId;
                    }
                }
                else
                {
                    stableId = this.nextId++;
                }

                if (component.Id.HasValue && !remap.ContainsKey(component.Id.Value))
                {
                    remap[component.Id.Value] = stableId;
                }

                component.Id = stableId;
            }

            foreach (var connection in document.Connections ?? Array.Empty<GhJsonConnection>())
            {
                if (remap.TryGetValue(connection.From.Id, out var fromId))
                {
                    connection.From.Id = fromId;
                }

                if (remap.TryGetValue(connection.To.Id, out var toId))
                {
                    connection.To.Id = toId;
                }
            }

            foreach (var group in document.Groups ?? Array.Empty<GhJsonGroup>())
            {
                if (group.Members == null)
                {
                    continue;
                }

                for (var i = 0; i < group.Members.Count; i++)
                {
                    if (remap.TryGetValue(group.Members[i], out var memberId))
                    {
                        group.Members[i] = memberId;
                    }
                }
            }
        }
    }
}
