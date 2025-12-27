/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System.Collections.Generic;
using Grasshopper.Kernel;
using SmartHopper.Core.Grasshopper.Serialization.GhJson;

namespace SmartHopper.Core.Grasshopper.Serialization.Canvas
{
    /// <summary>
    /// Shared utility methods for canvas operations.
    /// Provides common functionality used by ComponentPlacer, ConnectionManager, and GroupManager.
    /// </summary>
    public static class CanvasUtilities
    {
        /// <summary>
        /// Builds mapping from integer IDs to component instances.
        /// Used by ConnectionManager and GroupManager to resolve component references.
        /// </summary>
        /// <param name="result">Deserialization result containing components and document</param>
        /// <returns>Dictionary mapping integer IDs to component instances</returns>
        public static Dictionary<int, IGH_DocumentObject> BuildIdMapping(DeserializationResult result)
        {
            var idToComponent = new Dictionary<int, IGH_DocumentObject>();

            if (result?.Document?.Components != null && result.GuidMapping != null)
            {
                foreach (var compProps in result.Document.Components)
                {
                    if (compProps.Id.HasValue && result.GuidMapping.TryGetValue(compProps.InstanceGuid, out var instance))
                    {
                        idToComponent[compProps.Id.Value] = instance;
                    }
                }
            }

            return idToComponent;
        }
    }
}
