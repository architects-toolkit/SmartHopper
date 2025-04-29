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

            // Build a dependency graph
            //Dictionary<string, List<string>> dependencyGraph = new Dictionary<string, List<string>>();
            //Dictionary<string, ComponentProperties> componentDict = new Dictionary<string, ComponentProperties>();

            //foreach (var component in components)
            //{
            //    componentDict[component.ID.ToString()] = component;
            //    dependencyGraph[component.ID.ToString()] = new List<string>();

            //    Debug.WriteLine("Component: " + component.Name);
            //}

            //foreach (var component in components)
            //{
            //    foreach (var input in component.Inputs)
            //    {
            //        foreach (var source in input.Sources)
            //        {
            //            if (dependencyGraph.ContainsKey(source.ToString()))
            //            {
            //                dependencyGraph[source.ToString()].Add(component.ID.ToString());

            //                Debug.WriteLine("Input: " + source.ToString() + " -> " + component.ID.ToString());
            //            }
            //        }
            //    }
            //}

            // Topologically sort the components to determine the execution order
            var order = TopologicalSort(dependencyGraph);
            var grid  = new Dictionary<string, PointF>();
            int n     = order.Count;
            int cols  = (int)Math.Ceiling(Math.Sqrt(n));
            float spacing = 100f;

            for (int i = 0; i < n; i++)
            {
                var key = order[i];                            // GUID string
                var comp = components
                            .First(c => c.InstanceGuid.ToString() == key);
                var pivot = comp.Pivot;                       // direct PointF
                int row = i / cols, col = i % cols;
                grid[key] = new PointF(
                    pivot.X + col * spacing,
                    pivot.Y + row * spacing
                );
            }
            return grid;
            // // Create the grid
            // Dictionary<string, PointF> grid = new Dictionary<string, PointF>();
            // Dictionary<string, PointF> positions = new Dictionary<string, PointF>();
            // HashSet<(int, int)> usedPositions = new HashSet<(int, int)>();

            // foreach (var componentGuid in executionOrder)
            // {
            //     int col = 0;
            //     List<int> inputRows = new List<int>();

            //     foreach (var input in componentDict[componentGuid].Inputs)
            //     {
            //         foreach (var source in input.Sources)
            //         {
            //             if (positions.ContainsKey(source.ToString()))
            //             {
            //                 col = Math.Max(col, (int)positions[source.ToString()].X + 1);
            //                 inputRows.Add((int)positions[source.ToString()].Y);
            //             }
            //         }
            //     }

            //     int row;
            //     if (inputRows.Count > 0)
            //     {
            //         row = (int)Math.Round(inputRows.Average());
            //     }
            //     else
            //     {
            //         row = 0;
            //         while (usedPositions.Contains((col, row)))
            //         {
            //             row++;
            //         }
            //     }

            //     while (usedPositions.Contains((col, row)))
            //     {
            //         row++;
            //     }

            //     PointF position = new PointF(col, row);
            //     positions[componentGuid] = position;
            //     grid[componentGuid] = position;
            //     usedPositions.Add((col, row));
            // }

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
