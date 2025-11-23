/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Grasshopper.Kernel;
using SmartHopper.Core.Grasshopper.Serialization.GhJson;
using SmartHopper.Core.Models.Connections;

namespace SmartHopper.Core.Grasshopper.Serialization.Canvas
{
    /// <summary>
    /// Handles creation of wire connections between components.
    /// Maps connection definitions from GhJSON to actual parameter wiring.
    /// </summary>
    public static class ConnectionManager
    {
        /// <summary>
        /// Creates all connections from deserialization result.
        /// </summary>
        /// <param name="result">Deserialization result with components and document</param>
        /// <returns>Number of connections created</returns>
        public static int CreateConnections(DeserializationResult result)
        {
            if (result?.Document?.Connections == null || result.Document.Connections.Count == 0)
            {
                return 0;
            }

            var connectionsCreated = 0;
            var idToComponent = CanvasUtilities.BuildIdMapping(result);

            foreach (var connection in result.Document.Connections)
            {
                try
                {
                    if (CreateConnection(connection, idToComponent, result.GuidMapping))
                    {
                        connectionsCreated++;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ConnectionManager] Error creating connection: {ex.Message}");
                }
            }

            Debug.WriteLine($"[ConnectionManager] Created {connectionsCreated} connections");
            return connectionsCreated;
        }

        /// <summary>
        /// Creates a single connection between components.
        /// </summary>
        private static bool CreateConnection(
            ConnectionPairing connection,
            Dictionary<int, IGH_DocumentObject> idToComponent,
            Dictionary<Guid, IGH_DocumentObject> guidMapping)
        {
            // Get source and target components
            if (!idToComponent.TryGetValue(connection.From.Id, out var sourceObj) ||
                !idToComponent.TryGetValue(connection.To.Id, out var targetObj))
            {
                Debug.WriteLine($"[ConnectionManager] Component not found for connection");
                return false;
            }

            // Handle connections from stand-alone parameters
            IGH_Param sourceParam = null;
            if (sourceObj is IGH_Param standAloneParam)
            {
                sourceParam = standAloneParam;
            }
            else if (sourceObj is IGH_Component sourceComp)
            {
                // Find source output parameter by index first, then fallback to name
                if (connection.From.ParamIndex.HasValue &&
                    connection.From.ParamIndex.Value >= 0 &&
                    connection.From.ParamIndex.Value < sourceComp.Params.Output.Count)
                {
                    sourceParam = sourceComp.Params.Output[connection.From.ParamIndex.Value];
                }
                else
                {
                    sourceParam = sourceComp.Params.Output.FirstOrDefault(p => p.Name == connection.From.ParamName);
                }

                if (sourceParam == null)
                {
                    Debug.WriteLine($"[ConnectionManager] Source parameter '{connection.From.ParamName}' (index: {connection.From.ParamIndex}) not found");
                    return false;
                }
            }
            else
            {
                return false;
            }

            // Find target input parameter
            IGH_Param targetParam = null;

            if (targetObj is IGH_Param standAloneTargetParam)
            {
                // Target is a stand-alone parameter - it receives data directly
                targetParam = standAloneTargetParam;
            }
            else if (targetObj is IGH_Component targetComp)
            {
                // Find target input parameter by index first, then fallback to name
                if (connection.To.ParamIndex.HasValue &&
                    connection.To.ParamIndex.Value >= 0 &&
                    connection.To.ParamIndex.Value < targetComp.Params.Input.Count)
                {
                    targetParam = targetComp.Params.Input[connection.To.ParamIndex.Value];
                }
                else
                {
                    targetParam = targetComp.Params.Input.FirstOrDefault(p => p.Name == connection.To.ParamName);
                }

                if (targetParam == null)
                {
                    Debug.WriteLine($"[ConnectionManager] Target parameter '{connection.To.ParamName}' (index: {connection.To.ParamIndex}) not found");
                    return false;
                }
            }
            else
            {
                return false;
            }

            // Create the connection
            targetParam.AddSource(sourceParam);

            string sourceName = sourceObj is IGH_Component sc ? sc.Name : sourceParam.Name;
            string targetName = targetObj is IGH_Component tc ? tc.Name : targetParam.Name;
            Debug.WriteLine($"[ConnectionManager] Connected {sourceName}.{sourceParam.Name} → {targetName}.{targetParam.Name}");

            return true;
        }
    }
}
