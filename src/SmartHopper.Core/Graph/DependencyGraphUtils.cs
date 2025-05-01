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
using SmartHopper.Core.Models.Document;

namespace SmartHopper.Core.Graph
{
    public class DependencyGraphUtils
    {
        public static Dictionary<string, PointF> CreateComponentGrid(GrasshopperDocument doc)
        {
            Debug.WriteLine("Creating component grid...");
            Debug.WriteLine("Number of components to order: " + doc.Components.Count);

            var components = doc.Components;
            var connections = doc.Connections;

            var dependencyGraph = components
                .ToDictionary(c => c.InstanceGuid.ToString(),
                                c => new List<string>());

            // Map each connection: source → target
            foreach (var conn in connections)
            {
            var src = conn.From.ComponentId.ToString();
            var dst = conn.To.ComponentId.ToString();
            if (dependencyGraph.ContainsKey(src))
                dependencyGraph[src].Add(dst);
            }

            // If every component already specifies a pivot, use those positions directly
            if (components.All(c => !c.Pivot.IsEmpty))
                return components.ToDictionary(c => c.InstanceGuid.ToString(), c => c.Pivot);

            // Topologically sort the components to determine the execution order
            var order = TopologicalSort(dependencyGraph);

            // Dependency-aware placement with median alignment and optimized collision avoidance
            var grid = new Dictionary<string, PointF>();
            var positions = new Dictionary<string, (int col, int row)>();
            var columnNextFreeRow = new Dictionary<int, int>();
            // Build incoming dependency map
            var incoming = components.ToDictionary(c => c.InstanceGuid.ToString(), c => new List<string>());
            foreach (var conn in connections)
            {
                var src = conn.From.ComponentId.ToString();
                var dst = conn.To.ComponentId.ToString();
                if (incoming.ContainsKey(dst))
                    incoming[dst].Add(src);
            }
            float spacingX = 200f, spacingY = 100f; // X and Y spacing
            foreach (var key in order)
            {
                int col = 0;
                var inputRows = new List<int>();
                foreach (var parent in incoming[key])
                {
                    if (positions.TryGetValue(parent, out var pr))
                    {
                        col = Math.Max(col, pr.col + 1);
                        inputRows.Add(pr.row);
                    }
                }
                int row = 0;
                if (inputRows.Count > 0)
                {
                    inputRows.Sort();
                    row = inputRows[inputRows.Count / 2]; // median
                }
                if (columnNextFreeRow.TryGetValue(col, out var nextFree))
                    row = Math.Max(row, nextFree);
                columnNextFreeRow[col] = row + 1;
                positions[key] = (col, row);
                var comp = components.First(c => c.InstanceGuid.ToString() == key);
                var pivot = comp.Pivot;
                grid[key] = new PointF(pivot.X + col * spacingX, pivot.Y + row * spacingY);
                Debug.WriteLine($"[CreateComponentGrid] {comp.Name} ({key}) at col={col}, row={row} -> pos=({grid[key].X:F1},{grid[key].Y:F1})");
            }
            return grid;
        }

        private static List<string> TopologicalSort(Dictionary<string, List<string>> graph)
        {
            List<string> sorted = new List<string>();
            HashSet<string> visited = new HashSet<string>();
            HashSet<string> temp = new HashSet<string>();

            foreach (var node in graph.Keys)
            {
                if (!visited.Contains(node))
                {
                    Visit(node, graph, visited, temp, sorted);
                }
            }

            sorted.Reverse();
            return sorted;
        }

        private static void Visit(string node, Dictionary<string, List<string>> graph, HashSet<string> visited, HashSet<string> temp, List<string> sorted)
        {
            Debug.WriteLine("Checking if node " + node + " is cyclic");

            if (temp.Contains(node))
            {
                Debug.WriteLine("Cyclic dependency detected");
                throw new Exception("Cyclic dependency detected");
            }

            if (!visited.Contains(node))
            {
                temp.Add(node);

                foreach (var neighbor in graph[node])
                {
                    Visit(neighbor, graph, visited, temp, sorted);
                }

                temp.Remove(node);
                visited.Add(node);
                sorted.Add(node);
            }
        }
    }
}
