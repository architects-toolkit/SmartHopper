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
using SmartHopper.Core.Models.Serialization;

namespace SmartHopper.Core.Grasshopper.Serialization.GhJson
{
    /// <summary>
    /// Helper utilities for GhJSON operations.
    /// </summary>
    public static class GhJsonHelpers
    {
        /// <summary>
        /// Applies pivot positions to the GhJSON document in a deserialization result by their original instanceGuid.
        /// This modifies the Document.Components pivots so that ComponentPlacer will use the correct positions.
        /// </summary>
        /// <param name="result">The deserialization result containing the document.</param>
        /// <param name="positions">Dictionary mapping instanceGuids to their desired pivot positions.</param>
        /// <returns>Number of components that had their position updated.</returns>
        public static int ApplyPivotsByInstanceGuid(DeserializationResult result, Dictionary<Guid, PointF> positions)
        {
            if (result == null || positions == null || positions.Count == 0)
            {
                return 0;
            }

            return positions.Count(kvp => ApplyPivotByInstanceGuid(result, kvp.Key, kvp.Value));
        }

        /// <summary>
        /// Applies a single pivot position to a component in the GhJSON document by its original instanceGuid.
        /// This modifies the Document.Components pivot so that ComponentPlacer will use the correct position.
        /// </summary>
        /// <param name="result">The deserialization result containing the document.</param>
        /// <param name="instanceGuid">The original instanceGuid of the component.</param>
        /// <param name="position">The desired pivot position.</param>
        /// <returns>True if the component was found and updated; otherwise false.</returns>
        public static bool ApplyPivotByInstanceGuid(DeserializationResult result, Guid instanceGuid, PointF position)
        {
            if (result?.Document?.Components == null || instanceGuid == Guid.Empty)
            {
                return false;
            }

            var compProps = result.Document.Components.FirstOrDefault(c => c.InstanceGuid == instanceGuid);
            if (compProps == null)
            {
                return false;
            }

            compProps.Pivot = new CompactPosition(position.X, position.Y);
            Debug.WriteLine($"[GhJsonHelpers] Applied pivot ({position.X}, {position.Y}) to document component '{compProps.Name}' (GUID: {instanceGuid})");
            return true;
        }

        /// <summary>
        /// Applies pivot positions directly to component instances (not the document).
        /// Use this after ComponentPlacer has already run, to override calculated positions.
        /// </summary>
        /// <param name="result">The deserialization result containing components.</param>
        /// <param name="positions">Dictionary mapping instanceGuids to their desired pivot positions.</param>
        /// <returns>Number of components that had their position updated.</returns>
        public static int ApplyPivotsToComponentInstances(DeserializationResult result, Dictionary<Guid, PointF> positions)
        {
            if (result == null || positions == null || positions.Count == 0)
            {
                return 0;
            }

            return positions
                .Where(kvp => result.GuidMapping.TryGetValue(kvp.Key, out _))
                .Select(kvp =>
                {
                    var component = result.GuidMapping[kvp.Key];
                    component.Attributes.Pivot = kvp.Value;
                    component.Attributes.ExpireLayout();
                    Debug.WriteLine($"[GhJsonHelpers] Applied pivot ({kvp.Value.X}, {kvp.Value.Y}) directly to component instance '{component.Name}'");
                    return 1;
                })
                .Sum();
        }

        /// <summary>
        /// Sets the InstanceGuid on components to match the original GUIDs from GhJSON.
        /// This should only be used for replacement scenarios where the original component has been removed.
        /// </summary>
        /// <param name="result">The deserialization result containing components.</param>
        /// <param name="guidsToRestore">List of original GUIDs to restore on matching components.</param>
        /// <returns>Number of components that had their GUID restored.</returns>
        public static int RestoreInstanceGuids(DeserializationResult result, IEnumerable<Guid> guidsToRestore)
        {
            if (result?.GuidMapping == null || guidsToRestore == null)
            {
                return 0;
            }

            return guidsToRestore
                .Where(originalGuid => result.GuidMapping.TryGetValue(originalGuid, out _))
                .Select(originalGuid =>
                {
                    var component = result.GuidMapping[originalGuid];

                    // The GuidMapping uses the original GUID as key, but the component has a new GUID
                    // We need to set the component's InstanceGuid to the original value
                    try
                    {
                        component.NewInstanceGuid(originalGuid);
                        Debug.WriteLine($"[GhJsonHelpers] Restored InstanceGuid {originalGuid} to component '{component.Name}'");
                        return 1;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[GhJsonHelpers] Failed to restore InstanceGuid {originalGuid}: {ex.Message}");
                        return 0;
                    }
                })
                .Sum();
        }
    }
}
