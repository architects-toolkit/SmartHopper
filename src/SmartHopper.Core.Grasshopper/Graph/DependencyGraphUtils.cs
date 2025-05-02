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
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using SmartHopper.Core.Grasshopper.Utils;
using SmartHopper.Core.Models.Components;
using SmartHopper.Core.Models.Connections;
using SmartHopper.Core.Models.Document;

namespace SmartHopper.Core.Grasshopper.Graph
{
    public static class DependencyGraphUtils
    {
        /// <summary>
        /// Layout components to minimize wire crossings using the Sugiyama framework.
        /// </summary>
        /// <param name="doc">The Grasshopper document containing components and connections.</param>
        /// <param name="force">If true, forces a full layout recalculation even when pivots exist.</param>
        /// <param name="spacingX">The horizontal spacing between component columns.</param>
        /// <param name="spacingY">The vertical spacing between grid rows.</param>
        /// <param name="islandSpacingY">The vertical spacing between islands.</param>
        /// <returns>A dictionary mapping each component's GUID to its calculated pivot PointF.</returns>
        public static Dictionary<Guid, PointF> CreateComponentGrid(GrasshopperDocument doc, bool force = false, float spacingX = 50f, float spacingY = 80f, float islandSpacingY = 0f)
        {
            Debug.WriteLine("[CreateComponentGrid] Starting layout...");
            Debug.WriteLine($"[CreateComponentGrid] Components: {doc.Components.Count}, Connections: {doc.Connections.Count}");

            // 1. Grab data
            var components = doc.Components;
            var connections = doc.Connections;

            // 2. Try original pivots
            var orig = TryUseOriginalPivots(components, force);
            if (orig != null) return orig;

            // 3. Build port order & graph
            var inputOrder = BuildInputOrder(components);
            var (graph, phantomMap) = ExpandGraph(components, connections, inputOrder);

            // 4. Compute layers & ordering
            var layerNodes = ComputeLayerNodes(graph, connections, inputOrder, phantomMap);

            // 5. Position components in a grid
            var grid = CalculateGridPositions(layerNodes, components, connections, spacingX, spacingY);

            // 6. Stack disconnected “islands”
            grid = StackIslands(graph, grid, spacingY, islandSpacingY);

            // 7. Convert string-keys → Guid
            return ConvertStringKeysToGuids(grid);

            #region old CreateComponentGrid

            /*var components = doc.Components;
            var connections = doc.Connections;

            // Pivot check: find first default pivot
            bool IsEmptyPivot(PointF pt) => pt.X == 0 && pt.Y == 0;
            var missingComp = components.FirstOrDefault(c => IsEmptyPivot(c.Pivot));
            
            // If any component has a default pivot, recalculate all positions
            if (missingComp != null)
            {
                Debug.WriteLine($"[CreateComponentGrid] Component {missingComp.Name} (ID: {missingComp.InstanceGuid}) has default pivot, recalculating all positions");
            }

            // If force is enabled, recalculate all positions
            else if (force)
            {
                Debug.WriteLine("[CreateComponentGrid] Force layout, recalculating all positions");
            }
            
            // Return original pivots relative to the most top-left position
            else
            {
                Debug.WriteLine("[CreateComponentGrid] All components have valid pivots");
                Debug.WriteLine("[CreateComponentGrid] Using adjusted original pivots");
                float minX = components.Min(c => c.Pivot.X);
                float minY = components.Min(c => c.Pivot.Y);
                Debug.WriteLine($"[CreateComponentGrid] Normalizing pivots by ({minX}, {minY})");
                return components.ToDictionary(
                    c => c.InstanceGuid,
                    c => UnifyCenterPivot(c.InstanceGuid, new PointF(c.Pivot.X - minX, c.Pivot.Y - minY))
                );
            }

            // Spacing settings
            float spacingX = 50f;
            float spacingY = 80f;

            // 1. Build expanded graph with phantom nodes per input port in original order
            var graph = components.ToDictionary(c => c.InstanceGuid.ToString(), c => new List<string>());

            // map each component to input port sequence
            var inputOrder = new Dictionary<string, List<string>>();
            foreach (var compProps in components)
            {
                var proxy = GHObjectFactory.FindProxy(compProps.ComponentGuid, compProps.Name);
                var ghComp = GHObjectFactory.CreateInstance(proxy) as IGH_Component;
                var inputs = ghComp != null
                    ? GHParameterUtils.GetAllInputs(ghComp).Select(p => p.Name).ToList()
                    : new List<string>();
                inputOrder[compProps.InstanceGuid.ToString()] = inputs;
            }

            // group connections by destination component
            var phantomMap = new Dictionary<(string comp, string port), string>();
            foreach (var grp in connections.GroupBy(c => c.To.ComponentId.ToString()))
            {
                var dst = grp.Key;
                var ports = inputOrder.ContainsKey(dst)
                    ? inputOrder[dst]
                    : grp.Select(c => c.To.ParamName).Distinct().ToList();
                foreach (var port in ports)
                {
                    var pid = $"{dst}:{port}";
                    phantomMap[(dst, port)] = pid;
                    // edge: phantom node -> component
                    graph[pid] = new List<string> { dst };
                    // edges: each source -> phantom node
                    foreach (var c in grp.Where(c => c.To.ParamName == port))
                    {
                        var src = c.From.ComponentId.ToString();
                        graph[src].Add(pid);
                    }
                }
            }


            // Prepare grid early
            var grid = new Dictionary<string, PointF>();
            try
            {
                // 2. Topological sort
                Debug.WriteLine("[TopologicalSort] Starting...");
                var topo = TopologicalSort(graph);
                Debug.WriteLine($"[CreateComponentGrid] Topo order: {string.Join(" -> ", topo)}");

                // 3. Compute layers (reverse sink-based to source-based)
                var sinkLayers = ComputeLayers(graph);
                var maxSinkLayer = sinkLayers.Values.DefaultIfEmpty(0).Max();

                // flip so sources (inputs) are on left
                var layers = sinkLayers.ToDictionary(kv => kv.Key, kv => maxSinkLayer - kv.Value);

                // Adjust true sink components to the next layer after their parents
                var sinkIds = components.Select(c => c.InstanceGuid.ToString()).ToHashSet();
                var fromIds = connections.Select(c => c.From.ComponentId.ToString()).ToHashSet();
                var trueSinkIds = sinkIds.Except(fromIds);
                foreach (var sinkId in trueSinkIds)
                {
                    var parentLayers = connections
                        .Where(c => c.To.ComponentId.ToString() == sinkId)
                        .Select(c => layers[c.From.ComponentId.ToString()]);
                    var baseLayer = parentLayers.DefaultIfEmpty(0).Max();
                    layers[sinkId] = baseLayer + 1;
                }

                // group nodes by new layer and init ordering by barycenter heuristic
                var parentsMap = graph.Invert();
                var layerNodes = new Dictionary<int, List<string>>();
                var layerKeys = layers.Values.Distinct().OrderBy(i => i);
                foreach (var li in layerKeys)
                {
                    var nodesInLayer = layers.Where(kv => kv.Value == li).Select(kv => kv.Key).ToList();
                    List<string> sorted;
                    if (li == 0)
                        sorted = nodesInLayer.OrderBy(n => topo.IndexOf(n)).ToList();
                    else
                    {
                        var prev = layerNodes[li - 1];
                        sorted = nodesInLayer
                            .Select(n => new { key = n, bary = parentsMap[n]
                                .Where(p => layers[p] == li - 1)
                                .Select(p => prev.IndexOf(p))
                                .DefaultIfEmpty(topo.IndexOf(n))
                                .Average() })
                            .OrderBy(x => x.bary)
                            .Select(x => x.key)
                            .ToList();
                    }
                    // Must-stay-together: group phantom nodes of same component
                    var grouped = new List<string>();
                    var addedComps = new HashSet<string>();
                    foreach (var k in sorted)
                    {
                        if (k.Contains(':'))
                        {
                            var compId = k.Split(':')[0];
                            if (!addedComps.Contains(compId))
                            {
                                var group = sorted.Where(x => x.StartsWith(compId + ":")).ToList();
                                grouped.AddRange(group);
                                addedComps.Add(compId);
                            }
                        }
                        else grouped.Add(k);
                    }
                    layerNodes[li] = grouped;
                }

                // refine crossing order
                RefineLayerOrders(layerNodes, graph, parentsMap, topo);

                // DEBUG: show all layers before collapse
                foreach (var kv in layerNodes)
                    Debug.WriteLine($"[CreateComponentGrid] Layer {kv.Key} nodes: {string.Join(", ", kv.Value)}; realCount={kv.Value.Count(n => !phantomMap.Values.Contains(n))}");

                // collapse empty layers keeping only layers with real components
                var realLayers = layerNodes
                    .Where(kv => kv.Value.Any(k => !phantomMap.Values.Contains(k)))
                    .OrderBy(kv => kv.Key)
                    .Select(kv => kv.Key)
                    .ToList();
                Debug.WriteLine($"[CreateComponentGrid] RealLayers after collapse: {string.Join(", ", realLayers)}");

                // Calculate dynamic column offsets based on component widths
                var columnOffsets = new Dictionary<int, float>();
                float cumulativeX = 0f;
                for (int idx = 0; idx < realLayers.Count; idx++)
                {
                    var layerKey = realLayers[idx];
                    var compIds = layerNodes[layerKey].Where(k => !phantomMap.Values.Contains(k)).ToList();
                    float maxWidth = compIds.Select(k =>
                    {
                        if (Guid.TryParse(k, out var id))
                        {
                            var bounds = GHComponentUtils.GetComponentBounds(id);
                            return bounds.Width;
                        }
                        return 0f;
                    }).DefaultIfEmpty(0f).Max();
                    columnOffsets[idx] = cumulativeX;
                    cumulativeX += maxWidth + spacingX;
                }

                // 4. Assign positions for real components with cascade-based fractional rows
                var nextFree = new Dictionary<int, float>();
                var rowIndices = new Dictionary<string, float>();
                var originalOrder = components.Select((c, i) => new { key = c.InstanceGuid.ToString(), index = i })
                                              .ToDictionary(x => x.key, x => x.index);
                for (int li = 0; li < realLayers.Count; li++)
                {
                    var oldLayer = realLayers[li];
                    var compLayer = layerNodes[oldLayer].Where(k => !phantomMap.Values.Contains(k)).ToList();
                    foreach (var key in compLayer)
                    {
                        var comp = components.First(c => c.InstanceGuid.ToString() == key);
                        try
                        {
                            float rowVal;
                            if (li > 0 && connections.Any(c => c.To.ComponentId.ToString() == key))
                            {
                                var parentRows = connections
                                    .Where(c => c.To.ComponentId.ToString() == key)
                                    .Select(conn => conn.From.ComponentId.ToString())
                                    .Where(id => rowIndices.ContainsKey(id))
                                    .Select(id => rowIndices[id])
                                    .Distinct()
                                    .ToList();
                                rowVal = parentRows.Any() ? parentRows.Average() : nextFree.GetValueOrDefault(li, 0f);
                            }
                            else
                            {
                                rowVal = nextFree.GetValueOrDefault(li, 0f);
                            }
                            var floor = nextFree.GetValueOrDefault(li, 0f);
                            if (rowVal < floor) rowVal = floor;
                            rowIndices[key] = rowVal;
                            nextFree[li] = rowVal + 1f;
                            grid[key] = UnifyCenterPivot(Guid.Parse(key), new PointF(columnOffsets[li], rowVal * spacingY));
                            Debug.WriteLine($"[CreateComponentGrid] {comp.Name} ({key}) at layer={li}, row={rowVal}");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[CreateComponentGrid] Layout failed for {comp.Name} ({key}): {ex.Message}, applying fallback for component");
                            int fallbackIndex = originalOrder[key];
                            grid[key] = UnifyCenterPivot(comp.InstanceGuid, new PointF(0f, fallbackIndex * spacingY));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CreateComponentGrid] Layout failed: {ex.Message}, applying fallback");

                // Fallback: linear layout by original components order
                int r = 0;
                foreach (var comp in components)
                {
                    var key = comp.InstanceGuid.ToString();
                    grid[key] = UnifyCenterPivot(comp.InstanceGuid, new PointF(0f, r * spacingY));
                    Debug.WriteLine($"[CreateComponentGrid] Fallback {comp.Name} ({key}) at row={r}");
                    r++;
                }
            }

            // stack islands vertically
            var undirected = graph.ToDictionary(
                kv => kv.Key,
                kv => kv.Value.Concat(graph.Where(x => x.Value.Contains(kv.Key)).Select(x => x.Key)).ToList()
            );
            var visited = new HashSet<string>();
            var islands = new List<List<string>>();
            foreach (var node in graph.Keys)
            {
                if (visited.Contains(node)) continue;
                var queue = new Queue<string>();
                queue.Enqueue(node);
                visited.Add(node);
                var comp = new List<string>();
                while (queue.Count > 0)
                {
                    var n = queue.Dequeue();
                    comp.Add(n);
                    foreach (var neigh in undirected[n])
                        if (!visited.Contains(neigh))
                        {
                            visited.Add(neigh);
                            queue.Enqueue(neigh);
                        }
                }
                islands.Add(comp);
            }
            // compute vertical stacking
            float islandOffsetY = islandSpacingY;
            float marginY = spacingY;
            var stackedGrid = new Dictionary<string, PointF>();
            foreach (var island in islands)
            {
                var ys = island.Where(k => grid.ContainsKey(k)).Select(k => grid[k].Y);
                float minY = ys.DefaultIfEmpty(0f).Min();
                float shift = islandOffsetY - minY;
                foreach (var key in island)
                    if (grid.TryGetValue(key, out var pt))
                        stackedGrid[key] = new PointF(pt.X, pt.Y + shift);
                float maxY = stackedGrid.Where(kv => island.Contains(kv.Key)).Select(kv => kv.Value.Y).DefaultIfEmpty(0f).Max();
                islandOffsetY = maxY + marginY;
            }
            grid = stackedGrid;

            // convert string keys to Guid and return
            var result = new Dictionary<Guid, PointF>();
            foreach (var kv in grid)
            {
                if (Guid.TryParse(kv.Key, out var id))
                {
                    result[id] = kv.Value;
                }
            }

            return result;*/

            #endregion

        }

        // Topological sort with debug
        private static List<string> TopologicalSort(Dictionary<string, List<string>> graph)
        {
            var sorted = new List<string>();
            var visited = new HashSet<string>();
            var temp = new HashSet<string>();
            foreach (var node in graph.Keys)
            {
                if (!visited.Contains(node))
                {
                    Visit(node, graph, visited, temp, sorted);
                }
            }

            sorted.Reverse();
            Debug.WriteLine($"[TopologicalSort] Sorted: {string.Join(" -> ", sorted)}");
            return sorted;
        }

        // DFS visit
        private static void Visit(string node, Dictionary<string, List<string>> graph, HashSet<string> visited, HashSet<string> temp, List<string> sorted)
        {
            Debug.WriteLine($"[Visit] Visiting node {node}");
            if (temp.Contains(node))
            {
                Debug.WriteLine("[Visit] Cyclic dependency detected");
                throw new Exception("Cyclic dependency detected");
            }
            if (!visited.Contains(node))
            {
                temp.Add(node);
                foreach (var child in graph[node]) Visit(child, graph, visited, temp, sorted);
                temp.Remove(node);
                visited.Add(node);
                sorted.Add(node);
            }
        }

        // Compute layers: sink nodes = 0
        private static Dictionary<string, int> ComputeLayers(Dictionary<string, List<string>> graph)
        {
            var layers = new Dictionary<string, int>();
            int Dfs(string n)
            {
                if (layers.TryGetValue(n, out var v)) return v;
                var children = graph[n];
                var layer = children.Count == 0 ? 0 : children.Select(Dfs).Max() + 1;
                layers[n] = layer;
                return layer;
            }
            foreach (var n in graph.Keys) Dfs(n);
            return layers;
        }

        // Invert graph: child -> [parents]
        private static Dictionary<string, List<string>> Invert(this Dictionary<string, List<string>> graph)
        {
            var inv = graph.Keys.ToDictionary(k => k, k => new List<string>());
            foreach (var kv in graph)
                foreach (var child in kv.Value)
                    inv[child].Add(kv.Key);
            return inv;
        }

        // Refine orders to reduce crossings
        private static void RefineLayerOrders(
            Dictionary<int, List<string>> layerNodes,
            Dictionary<string, List<string>> childrenMap,
            Dictionary<string, List<string>> parentsMap,
            List<string> topo)
        {
            int maxL = layerNodes.Keys.Max();

            // forward sweep
            for (int l = 1; l <= maxL; l++)
            {
                var prev = layerNodes[l - 1];
                layerNodes[l] = layerNodes[l]
                    .OrderBy(k =>
                    {
                        var idxs = parentsMap[k]
                            .Where(prev.Contains)
                            .Select(p => prev.IndexOf(p))
                            .OrderBy(i => i)
                            .ToList();
                        if (!idxs.Any()) return double.MaxValue;
                        var m = idxs.Count % 2 == 1 ? idxs[idxs.Count / 2] : (idxs[idxs.Count/2 - 1] + idxs[idxs.Count/2]) / 2.0;
                        Debug.WriteLine($"[Refine forward] layer {l} {k} median {m}");
                        return m;
                    }).ToList();
            }
            // reverse sweep
            for (int l = maxL - 1; l >= 0; l--)
            {
                var next = layerNodes[l + 1];
                layerNodes[l] = layerNodes[l]
                    .OrderBy(k =>
                    {
                        var idxs = childrenMap[k]
                            .Where(next.Contains)
                            .Select(c => next.IndexOf(c))
                            .OrderBy(i => i)
                            .ToList();
                        if (!idxs.Any()) return double.MaxValue;
                        var m = idxs.Count % 2 == 1 ? idxs[idxs.Count/2] : (idxs[idxs.Count/2 - 1] + idxs[idxs.Count/2]) / 2.0;
                        Debug.WriteLine($"[Refine reverse] layer {l} {k} median {m}");
                        return m;
                    }).ToList();
            }
        }

        // Helper to unify pivot origin: params use top-left by default, center-align for grid
        private static PointF UnifyCenterPivot(Guid id, PointF pivot)
        {
            var obj = GHCanvasUtils.FindInstance(id);
            if (obj is IGH_Param)
            {
                var bounds = GHComponentUtils.GetComponentBounds(id);
                if (!bounds.IsEmpty)
                {
                    pivot = new PointF(pivot.X - bounds.Width / 2f, pivot.Y - bounds.Height / 2f);
                }
            }
            return pivot;
        }

        #region CreateComponentGrid Helper Methods

        /// <summary>
        /// If all components have valid non-zero pivots (and force==false),
        /// normalize them to the top-left origin and return that map.
        /// Otherwise returns null to signal a full layout pass.
        /// </summary>
        private static Dictionary<Guid, PointF> TryUseOriginalPivots(
            IEnumerable<ComponentProperties> components,
            bool force)
        {
            bool IsEmpty(PointF pt) => pt.X == 0 && pt.Y == 0;
            if (force || components.Any(c => IsEmpty(c.Pivot)))
                return null;

            float minX = components.Min(c => c.Pivot.X);
            float minY = components.Min(c => c.Pivot.Y);
            return components.ToDictionary(
                c => c.InstanceGuid,
                c => UnifyCenterPivot(c.InstanceGuid, new PointF(c.Pivot.X - minX, c.Pivot.Y - minY))
            );
        }

        /// <summary>
        /// Builds a map: component-ID → list of its input-port names in GH parameter order.
        /// </summary>
        private static Dictionary<string, List<string>> BuildInputOrder(
            IEnumerable<ComponentProperties> components)
        {
            var inputOrder = new Dictionary<string, List<string>>();
            foreach (var cp in components)
            {
                var proxy = GHObjectFactory.FindProxy(cp.ComponentGuid, cp.Name);
                var ghComp = GHObjectFactory.CreateInstance(proxy) as IGH_Component;
                var names = ghComp != null
                    ? GHParameterUtils.GetAllInputs(ghComp).Select(p => p.Name).ToList()
                    : new List<string>();
                inputOrder[cp.InstanceGuid.ToString()] = names;
            }
            return inputOrder;
        }

        /// <summary>
        /// Expands the graph by creating a phantom node for each component:port,
        /// wiring source→phantom and phantom→component edges.
        /// </summary>
        private static (Dictionary<string,List<string>> graph,
                    Dictionary<(string comp,string port),string> phantomMap)
        ExpandGraph(
            IEnumerable<ComponentProperties> components,
            IEnumerable<ConnectionPairing> connections,
            Dictionary<string,List<string>> inputOrder)
        {
            var graph = components.ToDictionary(
                c => c.InstanceGuid.ToString(),
                c => new List<string>());

            var phantomMap = new Dictionary<(string, string), string>();
            foreach (var grp in connections.GroupBy(c => c.To.ComponentId.ToString()))
            {
                var dst = grp.Key;
                var ports = inputOrder.ContainsKey(dst)
                    ? inputOrder[dst]
                    : grp.Select(c => c.To.ParamName).Distinct().ToList();
                foreach (var port in ports)
                {
                    var pid = $"{dst}:{port}";
                    phantomMap[(dst, port)] = pid;
                    // edge: phantom node -> component
                    graph[pid] = new List<string> { dst };
                    // edges: each source -> phantom node
                    foreach (var c in grp.Where(c => c.To.ParamName == port))
                    {
                        var src = c.From.ComponentId.ToString();
                        graph[src].Add(pid);
                    }
                }
            }
            return (graph, phantomMap);
        }

        /// <summary>
        /// Runs TopologicalSort, ComputeLayers, applies barycenter ordering + phantom grouping,
        /// calls RefineLayerOrders, then collapses phantom-only layers.
        /// </summary>
        private static Dictionary<int,List<string>> ComputeLayerNodes(
            Dictionary<string,List<string>> graph,
            IEnumerable<ConnectionPairing> connections,
            Dictionary<string,List<string>> inputOrder,
            Dictionary<(string comp,string port),string> phantomMap)
        {
            Debug.WriteLine("[ComputeLayerNodes] Starting topo sort");
            var topo = TopologicalSort(graph);
            Debug.WriteLine($"[ComputeLayerNodes] Topo order: {string.Join(" -> ", topo)}");

            // compute sink-based layers
            var sinkLayers = ComputeLayers(graph);
            var maxSinkLayer = sinkLayers.Values.DefaultIfEmpty(0).Max();

            // flip so sources on left
            var layers = sinkLayers.ToDictionary(kv => kv.Key, kv => maxSinkLayer - kv.Value);

            // adjust true sinks
            var sinkIds = graph.Keys.Except(phantomMap.Values).ToHashSet();
            var fromIds = connections.Select(c => c.From.ComponentId.ToString()).ToHashSet();
            var trueSinkIds = sinkIds.Except(fromIds);
            foreach (var sinkId in trueSinkIds)
            {
                var parentLayers = connections
                    .Where(c => c.To.ComponentId.ToString() == sinkId)
                    .Select(c => layers[c.From.ComponentId.ToString()]);
                var baseLayer = parentLayers.DefaultIfEmpty(0).Max();
                layers[sinkId] = baseLayer + 1;
            }

            // initial ordering with barycenter + phantom grouping
            var parentsMap = graph.Invert();
            var layerNodes = new Dictionary<int,List<string>>();
            var layerKeys = layers.Values.Distinct().OrderBy(i => i);
            foreach (var li in layerKeys)
            {
                var nodesInLayer = layers.Where(kv => kv.Value == li).Select(kv => kv.Key).ToList();
                List<string> sorted;
                if (li == 0)
                    sorted = nodesInLayer;
                else
                {
                    var prev = layerNodes[li - 1];
                    sorted = nodesInLayer
                        .Select(n => new
                        {
                            key = n,
                            bary = parentsMap[n]
                                .Where(p => layers[p] == li - 1)
                                .Select(p => prev.IndexOf(p))
                                .DefaultIfEmpty(topo.IndexOf(n))
                                .Average(),
                        })
                        .OrderBy(x => x.bary)
                        .Select(x => x.key)
                        .ToList();
                }

                // must-stay-together grouping
                var grouped = new List<string>();
                var addedComps = new HashSet<string>();
                foreach (var k in sorted)
                {
                    if (k.Contains(':'))
                    {
                        var compId = k.Split(':')[0];
                        if (!addedComps.Contains(compId))
                        {
                            var groupNodes = sorted.Where(x => x.StartsWith(compId + ":")).ToList();
                            grouped.AddRange(groupNodes);
                            addedComps.Add(compId);
                        }
                    }
                    else grouped.Add(k);
                }
                layerNodes[li] = grouped;
            }

            // crossing reduction
            RefineLayerOrders(layerNodes, graph, parentsMap, topo);

            // collapse phantom-only layers
            var realLayers = layerNodes
                .Where(kv => kv.Value.Any(n => !phantomMap.Values.Contains(n)))
                .OrderBy(kv => kv.Key)
                .Select(kv => kv.Key)
                .ToList();
            var collapsed = new Dictionary<int, List<string>>();
            for (int i = 0; i < realLayers.Count; i++)
                collapsed[i] = layerNodes[realLayers[i]];

            return collapsed;
        }

        /// <summary>
        /// Given ordered layerNodes, compute each component’s grid X/Y via
        /// columnOffsets (component widths + margin) and cascade-row approach.
        /// </summary>
        private static Dictionary<string, PointF> CalculateGridPositions(
            Dictionary<int,List<string>> layerNodes,
            IEnumerable<ComponentProperties> components,
            IEnumerable<ConnectionPairing> connections,
            float spacingX = 50f,
            float spacingY = 80f)
        {
            // compute column offsets
            var columnOffsets = new Dictionary<int, float>();
            float cumulativeX = 0f;
            for (int idx = 0; idx < layerNodes.Keys.Count; idx++)
            {
                var layerKey = layerNodes.Keys.ElementAt(idx);
                var compIds = layerNodes[layerKey].Where(k => !k.Contains(":")).ToList();
                float maxWidth = compIds.Select(k =>
                {
                    if (Guid.TryParse(k, out var id))
                    {
                        var bounds = GHComponentUtils.GetComponentBounds(id);
                        return bounds.Width;
                    }
                    return 0f;
                }).DefaultIfEmpty(0f).Max();
                columnOffsets[idx] = cumulativeX;
                cumulativeX += maxWidth + spacingX;
            }

            // assign positions
            var grid = new Dictionary<string, PointF>();
            var nextFree = new Dictionary<int, float>();
            var rowIndices = new Dictionary<string, float>();
            var originalOrder = components
                .Select((c, i) => new { key = c.InstanceGuid.ToString(), index = i })
                .ToDictionary(x => x.key, x => x.index);

            for (int li = 0; li < layerNodes.Keys.Count; li++)
            {
                var layerKey = layerNodes.Keys.ElementAt(li);
                var compLayer = layerNodes[layerKey].Where(k => !k.Contains(":")).ToList();
                foreach (var key in compLayer)
                {
                    var comp = components.First(c => c.InstanceGuid.ToString() == key);
                    try
                    {
                        float rowVal;
                        var conns = connections.Where(c => c.To.ComponentId.ToString() == key).ToList();
                        if (li > 0 && conns.Any())
                        {
                            var parentRows = conns
                                .Select(conn => conn.From.ComponentId.ToString())
                                .Where(id => rowIndices.ContainsKey(id))
                                .Select(id => rowIndices[id])
                                .Distinct()
                                .ToList();
                            rowVal = parentRows.Any() ? parentRows.Average() : nextFree.GetValueOrDefault(li, 0f);
                        }
                        else
                        {
                            rowVal = nextFree.GetValueOrDefault(li, 0f);
                        }
                        var floor = nextFree.GetValueOrDefault(li, 0f);
                        if (rowVal < floor) rowVal = floor;
                        rowIndices[key] = rowVal;
                        nextFree[li] = rowVal + 1f;
                        grid[key] = UnifyCenterPivot(Guid.Parse(key), new PointF(columnOffsets[li], rowVal * spacingY));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[CalculateGridPositions] Layout failed for {key}: {ex.Message}, applying fallback");
                        int fallbackIndex = originalOrder[key];
                        grid[key] = UnifyCenterPivot(Guid.Parse(key), new PointF(0f, fallbackIndex * spacingY));
                    }
                }
            }
            return grid;
        }

        /// <summary>
        /// Separates disconnected subgraphs (“islands”) and stacks each island
        /// below the previous so wires never cross islands.
        /// </summary>
        private static Dictionary<string, PointF> StackIslands(
            Dictionary<string,List<string>> graph,
            Dictionary<string,PointF> grid,
            float spacingY = 80f,
            float islandSpacingY = 0f)
        {
            var undirected = graph.ToDictionary(
                kv => kv.Key,
                kv => kv.Value.Concat(graph.Where(x => x.Value.Contains(kv.Key)).Select(x => x.Key)).ToList()
            );
            var visited = new HashSet<string>();
            var islands = new List<List<string>>();
            foreach (var node in graph.Keys)
            {
                if (visited.Contains(node)) continue;
                var queue = new Queue<string>();
                queue.Enqueue(node);
                visited.Add(node);
                var comp = new List<string>();
                while (queue.Count > 0)
                {
                    var n = queue.Dequeue();
                    comp.Add(n);
                    foreach (var neigh in undirected[n])
                        if (!visited.Contains(neigh))
                        {
                            visited.Add(neigh);
                            queue.Enqueue(neigh);
                        }
                }
                islands.Add(comp);
            }
            // compute vertical stacking
            float islandOffsetY = islandSpacingY;
            float marginY = spacingY;
            var stackedGrid = new Dictionary<string, PointF>();
            foreach (var island in islands)
            {
                var ys = island.Where(k => grid.ContainsKey(k)).Select(k => grid[k].Y);
                float minY = ys.DefaultIfEmpty(0f).Min();
                float shift = islandOffsetY - minY;
                foreach (var key in island)
                    if (grid.TryGetValue(key, out var pt))
                        stackedGrid[key] = new PointF(pt.X, pt.Y + shift);
                float maxY = stackedGrid.Where(kv => island.Contains(kv.Key)).Select(kv => kv.Value.Y).DefaultIfEmpty(0f).Max();
                islandOffsetY = maxY + marginY;
            }
            return stackedGrid;
        }

        /// <summary>
        /// Converts the string keys of the grid back to Guid keys.
        /// </summary>
        private static Dictionary<Guid, PointF> ConvertStringKeysToGuids(
            Dictionary<string,PointF> grid)
        {
            var result = new Dictionary<Guid,PointF>();
            foreach (var kv in grid)
                if (Guid.TryParse(kv.Key, out var id))
                    result[id] = kv.Value;
            return result;
        }

        #endregion
    }
}
