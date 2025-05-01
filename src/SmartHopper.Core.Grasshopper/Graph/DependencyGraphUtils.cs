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
        /// Layout components to minimize wire crossings using Sugiyama framework.
        /// </summary>
        public static Dictionary<Guid, PointF> CreateComponentGrid(GrasshopperDocument doc)
        {
            Debug.WriteLine("[CreateComponentGrid] Starting layout...");
            Debug.WriteLine($"[CreateComponentGrid] Components: {doc.Components.Count}, Connections: {doc.Connections.Count}");

            var components = doc.Components;
            var connections = doc.Connections;

            // Pivot check: find first default pivot
            bool IsEmptyPivot(PointF pt) => pt.X == 0 && pt.Y == 0;
            var missingComp = components.FirstOrDefault(c => IsEmptyPivot(c.Pivot));
            
            // If any component has a default pivot, recalculate all positions
            if (missingComp != null)
            {
                Debug.WriteLine($"[CreateComponentGrid] Component {missingComp.Name} (ID: {missingComp.InstanceGuid}) has default pivot, recalculating all positions");
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
            const float horizontalMargin = 50f;
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

                // group nodes by new layer and init ordering by topo
                var layerNodes = layers.GroupBy(kv => kv.Value)
                    .ToDictionary(g => g.Key, g => g.OrderBy(kv => topo.IndexOf(kv.Key)).Select(kv => kv.Key).ToList());

                // refine crossing order
                var parentsMap = graph.Invert();
                RefineLayerOrders(layerNodes, graph, parentsMap, topo);

                // collapse empty layers keeping only layers with real components
                var realLayers = layerNodes
                    .Where(kv => kv.Value.Any(k => !phantomMap.Values.Contains(k)))
                    .OrderBy(kv => kv.Key)
                    .Select(kv => kv.Key)
                    .ToList();

                // enforce input-port order among sources for each target component
                var compInputs = connections.GroupBy(c => c.To.ComponentId.ToString()).ToDictionary(g => g.Key, g => g.ToList());
                foreach (var target in compInputs.Keys)
                {
                    if (!layers.TryGetValue(target, out var tLayer)) continue;
                    
                    // walk backwards until we find a layer containing a real component
                    var srcLayer = tLayer - 1;
                    while (srcLayer >= 0 && !layerNodes[srcLayer].Any(k => !phantomMap.Values.Contains(k)))
                    {
                        srcLayer--;
                    }
                    if (srcLayer < 0) continue;

                    if (!layerNodes.ContainsKey(srcLayer)) continue;
                    
                    var layerList = layerNodes[srcLayer];
                    var portOrder = inputOrder[target];
                    var mapping = compInputs[target]
                        .Select(c => new { src = c.From.ComponentId.ToString(), idx = portOrder.IndexOf(c.To.ParamName) })
                        .Distinct()
                        .Where(m => layerList.Contains(m.src) && !phantomMap.Values.Contains(m.src))
                        .ToList();
                    var srcs = mapping.Select(m => m.src).ToList();
                    if (srcs.Count <= 1) continue;
                    var indices = srcs.Select(s => layerList.IndexOf(s)).OrderBy(i => i).ToList();
                    if (indices.Last() - indices.First() + 1 != indices.Count) continue;
                    var orderedSrcs = mapping.OrderBy(m => m.idx).Select(m => m.src).ToList();
                    var before = layerList.Take(indices.First()).ToList();
                    var after = layerList.Skip(indices.Last() + 1).ToList();
                    layerNodes[srcLayer] = before.Concat(orderedSrcs).Concat(after).ToList();
                }

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
                    cumulativeX += maxWidth + horizontalMargin;
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
                            if (li > 0 && compInputs.TryGetValue(key, out var conns) && conns.Any())
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
                            var pivot = comp.Pivot;
                            grid[key] = UnifyCenterPivot(Guid.Parse(key), new PointF(pivot.X + columnOffsets[li], pivot.Y + rowVal * spacingY));
                            Debug.WriteLine($"[CreateComponentGrid] {comp.Name} ({key}) at layer={li}, row={rowVal}");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[CreateComponentGrid] Layout failed for {comp.Name} ({key}): {ex.Message}, applying fallback for component");
                            int fallbackIndex = originalOrder[key];
                            var pivot = comp.Pivot;
                            grid[key] = UnifyCenterPivot(comp.InstanceGuid, new PointF(pivot.X, pivot.Y + fallbackIndex * spacingY));
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
                    var pivot = comp.Pivot;
                    grid[key] = UnifyCenterPivot(comp.InstanceGuid, new PointF(pivot.X, pivot.Y + r * spacingY));
                    Debug.WriteLine($"[CreateComponentGrid] Fallback {comp.Name} ({key}) at row={r}");
                    r++;
                }
            }

            // convert string keys to Guid and return
            var result = new Dictionary<Guid, PointF>();
            foreach (var kv in grid)
            {
                if (Guid.TryParse(kv.Key, out var id))
                {
                    result[id] = kv.Value;
                }
            }

            return result;
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
    }
}
