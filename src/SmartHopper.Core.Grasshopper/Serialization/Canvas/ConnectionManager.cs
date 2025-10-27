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

            if (sourceObj is not IGH_Component sourceComp || targetObj is not IGH_Component targetComp)
            {
                return false;
            }

            // Find source output parameter
            var sourceParam = sourceComp.Params.Output.FirstOrDefault(p => p.Name == connection.From.ParamName);
            if (sourceParam == null)
            {
                Debug.WriteLine($"[ConnectionManager] Source parameter '{connection.From.ParamName}' not found");
                return false;
            }

            // Find target input parameter
            var targetParam = targetComp.Params.Input.FirstOrDefault(p => p.Name == connection.To.ParamName);
            if (targetParam == null)
            {
                Debug.WriteLine($"[ConnectionManager] Target parameter '{connection.To.ParamName}' not found");
                return false;
            }

            // Create the connection
            targetParam.AddSource(sourceParam);
            Debug.WriteLine($"[ConnectionManager] Connected {sourceComp.Name}.{sourceParam.Name} → {targetComp.Name}.{targetParam.Name}");

            return true;
        }
    }
}
