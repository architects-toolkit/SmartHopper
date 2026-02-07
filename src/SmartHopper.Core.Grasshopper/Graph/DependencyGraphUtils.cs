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
using System.Drawing;
using System.Linq;
using GhJSON.Core.SchemaModels;
using Grasshopper.Kernel;
using SmartHopper.Core.Grasshopper.Utils.Canvas;

namespace SmartHopper.Core.Grasshopper.Graph
{
    /// <summary>
    /// Utilities to build and lay out a dependency graph of Grasshopper components and parameters.
    /// Provides algorithms to compute layers, minimize crossings, align parameters, and generate
    /// a consistent grid of nodes for canvas placement.
    /// </summary>
    public static class DependencyGraphUtils
    {
        /// <summary>
        /// Layout components and produce a unified grid of NodeGridComponent.
        /// </summary>
        /// <param name="doc">The Grasshopper document containing components and connections.</param>
        /// <param name="force">If true, forces a full layout recalculation even when pivots exist.</param>
        /// <param name="spacingX">Horizontal spacing between component columns.</param>
        /// <param name="spacingY">Vertical spacing between grid rows.</param>
        /// <param name="islandSpacingY">Vertical spacing between disconnected islands.</param>
        /// <returns>List of NodeGridComponent entries for each component.</returns>
        public static List<NodeGridComponent> CreateComponentGrid(GhJsonDocument doc, bool force = false, float spacingX = 50f, float spacingY = 80f, float islandSpacingY = 80f)
        {
            Debug.WriteLine("[CreateComponentGrid] Initializing unified grid...");

            // Initialize grid
            var grid = InitializeGrid(doc);

            // Return if pivots are already provided and force is not set
            if (!force)
            {
                if (grid.All(n => n.Pivot != PointF.Empty))
                {
                    var minX = grid.Min(n => n.Pivot.X);
                    var minY = grid.Min(n => n.Pivot.Y);
                    foreach (var n in grid)
                        n.Pivot = UnifyCenterPivot(n.ComponentId, new PointF(n.Pivot.X - minX, n.Pivot.Y - minY));
                }

                return grid;
            }

            // Island detection: split into connected components by parent/child links
            var idToNode = grid.ToDictionary(n => n.ComponentId, n => n);
            var visited = new HashSet<Guid>();
            var islands = new List<List<NodeGridComponent>>();
            foreach (var node in grid)
            {
                if (visited.Contains(node.ComponentId)) continue;
                var stack = new Stack<Guid>();
                stack.Push(node.ComponentId);
                visited.Add(node.ComponentId);
                var island = new List<NodeGridComponent>();
                while (stack.Count > 0)
                {
                    var id = stack.Pop();
                    var n = idToNode[id];
                    island.Add(n);
                    foreach (var neighbor in n.Children.Keys.Concat(n.Parents.Keys))
                    {
                        if (!visited.Contains(neighbor) && idToNode.ContainsKey(neighbor))
                        {
                            visited.Add(neighbor);
                            stack.Push(neighbor);
                        }
                    }
                }

                islands.Add(island);
            }

            Debug.WriteLine($"[CreateComponentGrid] Found {islands.Count} islands");

            // Layout each island and stack vertically
            var result = new List<NodeGridComponent>();
            float currentYOffset = 0f;
            foreach (var island in islands)
            {
                // independent layout
                var sub = SugiyamaAlgorithm(new List<NodeGridComponent>(island));
                sub = ApplySpacing(sub, spacingX, spacingY);

                // offset Y by accumulated island offset
                foreach (var n in sub)
                    n.Pivot = new PointF(n.Pivot.X, n.Pivot.Y + currentYOffset);
                result.AddRange(sub);

                // minimize layer connections
                result = MinimizeLayerConnections(result);

                // one-to-one connections
                result = OneToOneConnections(result, spacingY);

                // align params to inputs
                result = AlignParamsToInputs(result, spacingY);

                // align parents and children
                // result = AlignParentsAndChildren(result, spacingY);

                // avoid collisions
                result = AvoidCollisions(result);

                // update offset for next island
                var maxY = sub.Max(n => n.Pivot.Y);
                currentYOffset = maxY + islandSpacingY;
            }

            DebugDumpGrid("Result", result);

            return result;
        }

        /// <summary>
        /// Initializes grid nodes.
        /// </summary>
        /// <param name="doc">The Grasshopper document containing components and connections.</param>
        private static List<NodeGridComponent> InitializeGrid(GhJsonDocument doc)
        {
            var grid = doc.Components.Select(c => new NodeGridComponent
            {
                ComponentId = c.InstanceGuid.GetValueOrDefault(),
                Pivot = c.Pivot?.ToPointF() ?? PointF.Empty,
                Parents = new Dictionary<Guid, int>(),
                Children = new Dictionary<Guid, int>(),
            }).ToList();

            var idToGuidMap = doc.GetIdToGuidMapping();
            if (doc.Connections == null)
            {
                return grid;
            }

            foreach (var conn in doc.Connections)
            {
                // Resolve integer IDs to GUIDs
                if (idToGuidMap.TryGetValue(conn.From.Id, out var fromGuid) &&
                    idToGuidMap.TryGetValue(conn.To.Id, out var toGuid) &&
                    grid.Any(n => n.ComponentId == toGuid) &&
                    grid.Any(n => n.ComponentId == fromGuid))
                {
                    var toNode = grid.First(n => n.ComponentId == toGuid);
                    var fromNode = grid.First(n => n.ComponentId == fromGuid);

                    // compute input parameter index on child
                    int inputIndex = -1;
                    if (CanvasAccess.FindInstance(toNode.ComponentId) is IGH_Component childComp)
                    {
                        var inputs = ParameterAccess.GetAllInputs(childComp);
                        inputIndex = inputs.FindIndex(p => p.Name == conn.To.ParamName);
                    }

                    toNode.Parents[fromGuid] = inputIndex;

                    // compute output parameter index on parent
                    int outputIndex = -1;
                    if (CanvasAccess.FindInstance(fromNode.ComponentId) is IGH_Component parentComp)
                    {
                        var outputs = ParameterAccess.GetAllOutputs(parentComp);
                        outputIndex = outputs.FindIndex(p => p.Name == conn.From.ParamName);
                    }

                    fromNode.Children[toGuid] = outputIndex;
                }
            }

            return grid;
        }

        private static List<NodeGridComponent> SugiyamaAlgorithm(List<NodeGridComponent> grid)
        {
            Debug.WriteLine($"[SugiyamaAlgorithm] Step 1: Compute layers");
            grid = Sugiyama01_ComputeLayers(grid);
            DebugDumpGrid("After ComputeLayers", grid);

            Debug.WriteLine($"[SugiyamaAlgorithm] Step 2: Edge concentration");
            grid = Sugiyama02_EdgeConcentration(grid);
            DebugDumpGrid("After EdgeConcentration", grid);

            Debug.WriteLine($"[SugiyamaAlgorithm] Step 3: Compute rows");
            grid = Sugiyama03_ComputeRows(grid);
            DebugDumpGrid("After ComputeRows", grid);

            Debug.WriteLine($"[SugiyamaAlgorithm] Step 4: Minimize edge crossings");
            grid = Sugiyama04_MinimizeEdgeCrossings(grid);
            DebugDumpGrid("After MinimizeEdgeCrossings", grid);

            Debug.WriteLine($"[SugiyamaAlgorithm] Step 5: Multi-layer sweep");
            grid = Sugiyama05_MultiLayerSweep(grid);
            DebugDumpGrid("After MultiLayerSweep", grid);

            return grid;
        }

        private static List<NodeGridComponent> Sugiyama01_ComputeLayers(List<NodeGridComponent> grid)
        {
            var graph = grid.ToDictionary(n => n.ComponentId, n => n.Children);
            var layers = new Dictionary<Guid, int>();
            int Dfs(Guid n)
            {
                if (layers.TryGetValue(n, out var v)) return v;
                var children = graph[n];
                var layer = children.Count == 0 ? 0 : children.Select(child => Dfs(child.Key)).Max() + 1;
                layers[n] = layer;
                return layer;
            }

            foreach (var n in graph.Keys) Dfs(n);
            var maxLayer = layers.Values.DefaultIfEmpty(0).Max();
            foreach (var n in grid)
            {
                if (layers.TryGetValue(n.ComponentId, out var layer))
                {
                    n.Pivot = new PointF(maxLayer - layer, n.Pivot.Y);
                }
            }

            return grid;
        }

        private static List<NodeGridComponent> Sugiyama02_EdgeConcentration(List<NodeGridComponent> grid)
        {
            var newGrid = new List<NodeGridComponent>(grid);

            // group nodes by layer key (float Pivot.X)
            var byLayer = grid.GroupBy(n => n.Pivot.X).OrderBy(g => g.Key).ToList();
            for (int li = 0; li < byLayer.Count - 1; li++)
            {
                var left = byLayer[li].ToList();
                var rightIds = new HashSet<Guid>(byLayer[li + 1].Select(n => n.ComponentId));

                // group left nodes by identical set of targets in next layer
                var groups = left.Select(n => new
                {
                    Node = n,
                    Targets = n.Children.Keys.Where(id => rightIds.Contains(id)).OrderBy(id => id).ToList(),
                })
                                .GroupBy(x => string.Join(",", x.Targets))
                                .Where(g => g.Count() > 1 && g.First().Targets.Count > 1);
                foreach (var grp in groups)
                {
                    var S = grp.Select(x => x.Node).ToList();
                    var T = grp.First().Targets;

                    // create edge-concentration node
                    var ec = new NodeGridComponent
                    {
                        ComponentId = Guid.NewGuid(),
                        Pivot = new PointF(left[0].Pivot.X + 0.5f, 0),
                        Parents = new Dictionary<Guid, int>(),
                        Children = new Dictionary<Guid, int>(),
                    };

                    // remove original edges S->T
                    foreach (var s in S)
                    {
                        foreach (var t in T)
                        {
                            s.Children.Remove(t);
                        }
                    }

                    foreach (var t in newGrid.Where(n => T.Contains(n.ComponentId)))
                    {
                        foreach (var s in S)
                        {
                            t.Parents.Remove(s.ComponentId);
                        }
                    }

                    // add stars: S->ec and ec->T
                    foreach (var s in S)
                    {
                        s.Children[ec.ComponentId] = -1;
                        ec.Parents[s.ComponentId] = -1;
                    }

                    foreach (var t in newGrid.Where(n => T.Contains(n.ComponentId)))
                    {
                        t.Parents[ec.ComponentId] = -1;
                        ec.Children[t.ComponentId] = -1;
                    }

                    newGrid.Add(ec);
                }
            }

            return newGrid;
        }

        private static List<NodeGridComponent> Sugiyama03_ComputeRows(List<NodeGridComponent> grid)
        {
            var byLayer = grid.GroupBy(n => (int)n.Pivot.X)
                               .OrderBy(g => g.Key)
                               .Select(g => g.ToList())
                               .ToList();

            // Top-down pass: initial ordering by output indices and barycenter
            for (int layerIndex = 0; layerIndex < byLayer.Count; layerIndex++)
            {
                var currentLayer = byLayer[layerIndex].ToList();
                if (layerIndex == 0)
                {
                    // Sort sources by average output parameter index
                    currentLayer.Sort((a, b) =>
                    {
                        float aOut = a.Children.Any() ? (float)a.Children.Values.Average() : float.MaxValue;
                        float bOut = b.Children.Any() ? (float)b.Children.Values.Average() : float.MaxValue;
                        return aOut.CompareTo(bOut);
                    });
                }
                else
                {
                    SortLayerByBarycenter(currentLayer, byLayer[layerIndex - 1].ToList(), useParents: false);
                }

                // Assign row positions
                for (int i = 0; i < currentLayer.Count; i++)
                    currentLayer[i].Pivot = new PointF(currentLayer[i].Pivot.X, i);
            }

            // Bottom-up pass: refine ordering by parent barycenter
            for (int layerIndex = byLayer.Count - 2; layerIndex >= 0; layerIndex--)
            {
                var currentLayer = byLayer[layerIndex].ToList();
                SortLayerByBarycenter(currentLayer, byLayer[layerIndex + 1].ToList(), useParents: true);
                for (int i = 0; i < currentLayer.Count; i++)
                    currentLayer[i].Pivot = new PointF(currentLayer[i].Pivot.X, i);
            }

            return grid;
        }

        // Sorts a layer based on barycenter relative to nodes in adjacent layer
        private static void SortLayerByBarycenter(List<NodeGridComponent> currentLayer,
            List<NodeGridComponent> adjacentLayer, bool useParents)
        {
            currentLayer.Sort((a, b) =>
            {
                float aKey = CalculateBarycenter(a, adjacentLayer, useParents);
                float bKey = CalculateBarycenter(b, adjacentLayer, useParents);
                return aKey.CompareTo(bKey);
            });
        }

        // Calculates average Y position of connections to adjacent layer
        private static float CalculateBarycenter(NodeGridComponent node,
            List<NodeGridComponent> adjacentLayer, bool useParents)
        {
            var connected = useParents ? node.Parents.Keys : node.Children.Keys;
            var positions = new List<float>();
            foreach (var id in connected)
            {
                var found = adjacentLayer.FirstOrDefault(n => n.ComponentId == id);
                if (found != null) positions.Add(found.Pivot.Y);
            }

            return positions.Any() ? (float)positions.Average() : float.MaxValue;
        }

        /// <summary>
        /// Reduces edge crossings by applying a median heuristic to reorder nodes within each layer.
        /// </summary>
        /// <param name="grid">Grid of node components with layer assignments and initial ordering.</param>
        /// <returns>Grid with updated node ordering to minimize edge crossings.</returns>
        private static List<NodeGridComponent> Sugiyama04_MinimizeEdgeCrossings(List<NodeGridComponent> grid)
        {
            var byLayer = grid.GroupBy(n => (int)n.Pivot.X)
                               .OrderBy(g => g.Key)
                               .Select(g => g.ToList())
                               .ToList();

            // Top-down pass: reorder by median parent positions
            for (int layerIndex = 1; layerIndex < byLayer.Count; layerIndex++)
            {
                var prevLayer = byLayer[layerIndex - 1];
                var currLayer = byLayer[layerIndex];
                currLayer.Sort((a, b) => CalculateMedian(a, prevLayer, useParents: true)
                                      .CompareTo(CalculateMedian(b, prevLayer, useParents: true)));
                for (int i = 0; i < currLayer.Count; i++)
                    currLayer[i].Pivot = new PointF(currLayer[i].Pivot.X, i);
            }

            // Bottom-up pass: reorder by median child positions
            for (int layerIndex = byLayer.Count - 2; layerIndex >= 0; layerIndex--)
            {
                var nextLayer = byLayer[layerIndex + 1];
                var currLayer = byLayer[layerIndex];
                currLayer.Sort((a, b) => CalculateMedian(a, nextLayer, useParents: false)
                                      .CompareTo(CalculateMedian(b, nextLayer, useParents: false)));
                for (int i = 0; i < currLayer.Count; i++)
                    currLayer[i].Pivot = new PointF(currLayer[i].Pivot.X, i);
            }

            return grid;
        }

        /// <summary>
        /// Calculates the median position of adjacent nodes in a given layer.
        /// </summary>
        /// <param name="node">The node to calculate the median for.</param>
        /// <param name="adjacentLayer">Nodes in the adjacent layer.</param>
        /// <param name="useParents">If true, use parent connections; otherwise, use child connections.</param>
        /// <returns>Median Y position of connected nodes, or float.MaxValue if no connections.</returns>
        private static float CalculateMedian(NodeGridComponent node, List<NodeGridComponent> adjacentLayer, bool useParents)
        {
            var connected = useParents ? node.Parents.Keys : node.Children.Keys;
            var positions = new List<float>();
            foreach (var id in connected)
            {
                var found = adjacentLayer.FirstOrDefault(n => n.ComponentId == id);
                if (found != null)
                    positions.Add(found.Pivot.Y);
            }

            if (!positions.Any())
                return float.MaxValue;
            positions.Sort();
            int mid = positions.Count / 2;
            if (positions.Count % 2 == 1)
                return positions[mid];
            return (positions[mid - 1] + positions[mid]) / 2f;
        }

        /// <summary>
        /// Repeatedly applies one-pass crossing minimization until node ordering stabilizes across layers.
        /// </summary>
        /// <param name="grid">Grid of node components ordered by layer and initial row positions.</param>
        /// <returns>Grid with stable ordering after iterative crossing minimization.</returns>
        private static List<NodeGridComponent> Sugiyama05_MultiLayerSweep(List<NodeGridComponent> grid)
        {
            bool changed;
            do
            {
                // Snapshot current row positions
                var oldY = grid.ToDictionary(n => n.ComponentId, n => n.Pivot.Y);

                // One-pass median crossing minimization
                grid = Sugiyama04_MinimizeEdgeCrossings(grid);

                // Check if any ordering changed
                changed = grid.Any(n => n.Pivot.Y != oldY[n.ComponentId]);

                Debug.WriteLine($"[Sugiyama05_MultiLayerSweep] Changing pivot for {grid.Count(n => n.Pivot.Y != oldY[n.ComponentId])} nodes");
            }

            while (changed);

            return grid;
        }

        /// <summary>
        /// Scales pivots by spacing and offsets positions based on component size.
        /// </summary>
        /// <param name="grid">Grid of node components.</param>
        /// <param name="spacingX">Horizontal spacing.</param>
        /// <param name="spacingY">Vertical spacing.</param>
        /// <returns>List of positioned node grid components.</returns>
        private static List<NodeGridComponent> ApplySpacing(List<NodeGridComponent> grid, float spacingX, float spacingY)
        {
            // Scale pivots by spacing
            foreach (var n in grid)
                n.Pivot = new PointF(n.Pivot.X * spacingX, n.Pivot.Y * spacingY);

            // Compute column offsets based on component widths
            var columns = grid.GroupBy(n => (int)(n.Pivot.X / spacingX)).OrderBy(g => g.Key);
            var colOffsets = new Dictionary<int, float>();
            float xOffset = 0;
            foreach (var group in columns)
            {
                float maxWidth = group.Max(n => ComponentManipulation.GetComponentBounds(n.ComponentId).Width);
                colOffsets[group.Key] = xOffset;
                xOffset += maxWidth + spacingX;
            }

            // Compute row offsets based on component heights
            var rows = grid.GroupBy(n => (int)(n.Pivot.Y / spacingY)).OrderBy(g => g.Key);
            var rowOffsets = new Dictionary<int, float>();
            float yOffset = 0;
            foreach (var group in rows)
            {
                float maxHeight = group.Max(n => ComponentManipulation.GetComponentBounds(n.ComponentId).Height);
                rowOffsets[group.Key] = yOffset;
                yOffset += maxHeight + spacingY;
            }

            // Apply final positions
            foreach (var n in grid)
            {
                int col = (int)(n.Pivot.X / spacingX);
                int row = (int)(n.Pivot.Y / spacingY);
                n.Pivot = new PointF(colOffsets[col], rowOffsets[row]);
            }

            return grid;
        }

        /// <summary>
        /// Aligns params to inputs of the same single child.
        /// </summary>
        /// <param name="grid">The positioned grid of node components.</param>
        /// <param name="spacingY">Vertical spacing between original grid rows.</param>
        private static List<NodeGridComponent> AlignParamsToInputs(List<NodeGridComponent> grid, float spacingY)
        {
            spacingY = spacingY / 2;

            Debug.WriteLine($"[AlignParamsToInputs] Starting alignment with spacingY={spacingY}");

            // Group nodes by actual X position (columns)
            var byColumn = grid.GroupBy(n => n.Pivot.X)
                                .OrderBy(g => g.Key)
                                .Select(g => g.ToList())
                                .ToList();

            // For each layer beyond the first, align parents above each child
            for (int i = 1; i < byColumn.Count; i++)
            {
                Debug.WriteLine($"[AlignParentsAndChildren] Processing column {i}, X={byColumn[i].First().Pivot.X}");
                var prevCol = byColumn[i - 1];
                var currCol = byColumn[i];
                foreach (var child in currCol)
                {
                    Debug.WriteLine($"[AlignParentsAndChildren] Child {child.ComponentId} at Y={child.Pivot.Y}");

                    // all parents connecting to this child
                    var parents = prevCol.Where(p => p.Children.ContainsKey(child.ComponentId)).ToList();

                    // param-case: one slider per input -> reorder to input order and align to port Ys
                    if (parents.Count > 1
                        && CanvasAccess.FindInstance(child.ComponentId) is IGH_Component childComp
                        && childComp.Params.Input.Count == parents.Count
                        && parents.All(p => CanvasAccess.FindInstance(p.ComponentId) is IGH_Param))
                    {
                        var inputs = ParameterAccess.GetAllInputs(childComp);
                        foreach (var p in parents.OrderBy(p => child.Parents[p.ComponentId]))
                        {
                            int inputIdx = child.Parents[p.ComponentId];
                            if (inputIdx >= 0 && inputIdx < inputs.Count)
                            {
                                var rect = inputs[inputIdx].Attributes.Bounds;

                                // calculate relative grid Y based on canvas offset
                                float inputPivotY = rect.Y + rect.Height / 2f;
                                var canvasChildBounds = ComponentManipulation.GetComponentBounds(child.ComponentId);
                                float canvasChildCenterY = canvasChildBounds.Y + canvasChildBounds.Height / 2f;
                                float deltaCanvasY = inputPivotY - canvasChildCenterY;

                                // target grid Y
                                float targetY = child.Pivot.Y + deltaCanvasY;
                                Debug.WriteLine($"[AlignParamsToInputs] Param-case: aligning parent {p.ComponentId} relativeGridY={targetY}");

                                // pivot relative to child pivot group
                                p.Pivot = new PointF(p.Pivot.X, targetY);
                            }
                        }
                    }
                }
            }

            return grid;
        }

        /// <summary>
        /// Aligns parent components belonging to the same single child into a contiguous block above the child.
        /// </summary>
        /// <param name="grid">The positioned grid of node components.</param>
        /// <param name="spacingY">Vertical spacing between original grid rows.</param>
        private static List<NodeGridComponent> AlignParentsAndChildren(List<NodeGridComponent> grid, float spacingY)
        {
            spacingY = spacingY / 2;

            Debug.WriteLine($"[AlignParentsAndChildren] Starting alignment with spacingY={spacingY}");

            // Group nodes by actual X position (columns)
            var byColumn = grid.GroupBy(n => n.Pivot.X)
                                .OrderBy(g => g.Key)
                                .Select(g => g.ToList())
                                .ToList();

            // For each layer beyond the first, align parents above each child
            for (int i = 1; i < byColumn.Count; i++)
            {
                Debug.WriteLine($"[AlignParentsAndChildren] Processing column {i}, X={byColumn[i].First().Pivot.X}");
                var prevCol = byColumn[i - 1];
                var currCol = byColumn[i];
                foreach (var child in currCol)
                {
                    Debug.WriteLine($"[AlignParentsAndChildren] Child {child.ComponentId} at Y={child.Pivot.Y}");

                    // all parents connecting to this child
                    var parents = prevCol.Where(p => p.Children.ContainsKey(child.ComponentId)).ToList();

                    Debug.WriteLine($"[AlignParentsAndChildren] Found {parents.Count} parents for child {child.ComponentId}: {string.Join(",", parents.Select(p => p.ComponentId))}");

                    // sort parents top-to-bottom and center group over child
                    var orderedParents = parents.OrderBy(p => p.Pivot.Y).ToList();

                    // compute total group height including margins between parents
                    var heights = orderedParents.Select(p => ComponentManipulation.GetComponentBounds(p.ComponentId).Height).ToList();
                    float totalHeight = heights.Sum() + spacingY * (orderedParents.Count - 1);

                    // compute child center Y
                    var childBounds = ComponentManipulation.GetComponentBounds(child.ComponentId);
                    float childCenterY = child.Pivot.Y + childBounds.Height / 2f;

                    // position group such that its center aligns with child center
                    float groupTop = childCenterY - totalHeight / 2f;
                    Debug.WriteLine($"[AlignParentsAndChildren] Parent group height={totalHeight}, childCenterY={childCenterY}, groupTop={groupTop}");
                    float yCursor = groupTop;
                    foreach (var p in orderedParents)
                    {
                        var oldY = p.Pivot.Y;
                        var bounds = ComponentManipulation.GetComponentBounds(p.ComponentId);
                        p.Pivot = new PointF(p.Pivot.X, yCursor);
                        Debug.WriteLine($"[AlignParentsAndChildren] Moving parent {p.ComponentId} from Y={oldY} to Y={yCursor}");
                        yCursor += bounds.Height + spacingY;
                    }
                }
            }

            return grid;
        }

        /// <summary>
        /// Minimizes vertical connection lengths layer by layer using mean-squared optimal shift.
        /// </summary>
        private static List<NodeGridComponent> MinimizeLayerConnections(List<NodeGridComponent> grid)
        {
            // Group nodes by column (X) and sort layers
            var byLayer = grid.GroupBy(n => n.Pivot.X).OrderBy(g => g.Key).ToList();
            var idToNode = grid.ToDictionary(n => n.ComponentId, n => n);

            // Iterate each adjacent pair: shift the next layer to minimize connection length
            for (int i = 0; i < byLayer.Count - 1; i++)
            {
                var currLayer = byLayer[i].ToList();
                var nextLayer = byLayer[i + 1].ToList();
                var deltas = new List<float>();

                // Collect vertical deltas for edges between these two layers
                foreach (var u in currLayer)
                {
                    foreach (var childId in u.Children.Keys)
                    {
                        if (idToNode.TryGetValue(childId, out var v) &&
                            Math.Abs(v.Pivot.X - nextLayer[0].Pivot.X) < 0.001f)
                        {
                            deltas.Add(u.Pivot.Y - v.Pivot.Y);
                        }
                    }
                }

                if (deltas.Count == 0) continue;

                // Compute average delta (minimizes sum of squared differences)
                var avgDelta = deltas.Sum() / deltas.Count;

                // Shift all nodes in the next layer by the average delta
                foreach (var v in nextLayer)
                    v.Pivot = new PointF(v.Pivot.X, v.Pivot.Y + avgDelta);
            }

            return grid;
        }

        /// <summary>
        /// Aligns components that have a single parent and that parent has only that child by setting them to the same Y.
        /// </summary>
        private static List<NodeGridComponent> OneToOneConnections(List<NodeGridComponent> grid, float spacingY)
        {
            var idToNode = grid.ToDictionary(n => n.ComponentId, n => n);
            Debug.WriteLine("[OneToOneConnections] Aligning single-child parents");
            foreach (var parent in grid.Where(n => n.Children.Count == 1))
            {
                var childId = parent.Children.Keys.First();
                if (!idToNode.TryGetValue(childId, out var child)) continue;
                int inputIndex = child.Parents[parent.ComponentId];
                if (inputIndex < 0) continue;

                // fetch input port bounds
                if (!(CanvasAccess.FindInstance(child.ComponentId) is IGH_Component childComp)) continue;
                var inputs = ParameterAccess.GetAllInputs(childComp);
                if (inputIndex >= inputs.Count) continue;
                var port = inputs[inputIndex];
                var rect = port.Attributes.Bounds;

                // compute offset from child's center
                float inputPivotY = rect.Y + rect.Height / 2f;
                var canvasChildBounds = ComponentManipulation.GetComponentBounds(child.ComponentId);
                float canvasChildCenterY = canvasChildBounds.Y + canvasChildBounds.Height / 2f;
                float deltaCanvasY = inputPivotY - canvasChildCenterY;

                // target grid Y
                float targetY = child.Pivot.Y + deltaCanvasY + spacingY / 2;
                Debug.WriteLine($"[OneToOneConnections] Align parent {parent.ComponentId} to Y={targetY}");
                parent.Pivot = new PointF(parent.Pivot.X, targetY);
            }

            return grid;
        }

        /// <summary>
        /// Shifts nodes in each column to avoid overlapping based on their component bounds.
        /// </summary>
        private static List<NodeGridComponent> AvoidCollisions(List<NodeGridComponent> grid)
        {
            var byColumn = grid.GroupBy(n => n.Pivot.X).OrderBy(g => g.Key);
            foreach (var col in byColumn)
            {
                var sorted = col.OrderBy(n => n.Pivot.Y).ToList();
                float lastBottom = float.MinValue;
                foreach (var node in sorted)
                {
                    var bounds = ComponentManipulation.GetComponentBounds(node.ComponentId);
                    if (node.Pivot.Y < lastBottom)
                        node.Pivot = new PointF(node.Pivot.X, lastBottom);
                    lastBottom = node.Pivot.Y + bounds.Height;
                }
            }

            return grid;
        }

        /// <summary>
        /// Dumps the grid: component IDs and pivot positions.
        /// </summary>
        /// <param name="stage">Identifier for dump stage.</param>
        /// <param name="grid">List of NodeGridComponent to dump.</param>
        private static void DebugDumpGrid(string stage, List<NodeGridComponent> grid)
        {
            Debug.WriteLine($"[DebugDumpGrid:{stage}] Dumping {grid.Count} nodes");
            foreach (var n in grid)
                Debug.WriteLine($"[DebugDumpGrid:{stage}] {n.ComponentId} => Pivot=({n.Pivot.X},{n.Pivot.Y})");
        }

        // Helper to unify pivot origin: params use top-left by default, center-align for grid
        private static PointF UnifyCenterPivot(Guid id, PointF pivot)
        {
            var obj = CanvasAccess.FindInstance(id);
            if (obj is IGH_Param)
            {
                var bounds = ComponentManipulation.GetComponentBounds(id);
                if (!bounds.IsEmpty)
                {
                    pivot = new PointF(
                        pivot.X - bounds.Width / 2f,
                        pivot.Y - bounds.Height / 2f);
                }
            }

            return pivot;
        }
    }
}
