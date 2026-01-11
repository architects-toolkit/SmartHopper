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

namespace SmartHopper.Core.Grasshopper.Graph
{
    /// <summary>
    /// Provides methods to traverse and expand component connections.
    /// </summary>
    public static class ConnectionGraphUtils
    {
        /// <summary>
        /// Expands a set of component IDs by following connections up to the given depth.
        /// </summary>
        /// <param name="edges">Pairs of connected component IDs (undirected).</param>
        /// <param name="initialIds">Initial component IDs to include.</param>
        /// <param name="depth">Number of connection hops to include beyond initial IDs.</param>
        /// <returns>All component IDs reachable within the specified depth, including initial ones.</returns>
        public static HashSet<Guid> ExpandByDepth(IEnumerable<(Guid From, Guid To)> edges, IEnumerable<Guid> initialIds, int depth)
        {
            // Build undirected adjacency map
            var adjacency = edges
                .Concat(edges.Select(e => (From: e.To, To: e.From)))
                .GroupBy(e => e.From)
                .ToDictionary(g => g.Key, g => new HashSet<Guid>(g.Select(e => e.To)));

            var visited = new HashSet<Guid>(initialIds);
            var frontier = new HashSet<Guid>(initialIds);

            for (int level = 0; level < depth; level++)
            {
                var next = new HashSet<Guid>();
                foreach (var id in frontier)
                {
                    if (adjacency.TryGetValue(id, out var neighbors))
                    {
                        foreach (var n in neighbors)
                        {
                            if (!visited.Contains(n))
                            {
                                next.Add(n);
                            }
                        }
                    }
                }

                if (next.Count == 0)
                {
                    break;
                }

                visited.UnionWith(next);
                frontier = next;
            }

            return visited;
        }
    }
}
