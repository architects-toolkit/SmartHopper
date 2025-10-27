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
using System.Drawing;
using System.Linq;
using Grasshopper;
using Grasshopper.Kernel;
using SmartHopper.Core.Grasshopper.Serialization.GhJson;
using SmartHopper.Core.Grasshopper.Utils.Canvas;
using SmartHopper.Core.Models.Serialization;

namespace SmartHopper.Core.Grasshopper.Serialization.Canvas
{
    /// <summary>
    /// Handles placement of deserialized components onto the Grasshopper canvas.
    /// Manages positioning, adding to document, and canvas updates.
    /// </summary>
    public static class ComponentPlacer
    {
        /// <summary>
        /// Places components on the canvas using deserialization result.
        /// </summary>
        /// <param name="result">Deserialization result with components and document</param>
        /// <param name="startPosition">Starting position for placement (null for auto)</param>
        /// <param name="spacing">Spacing between components</param>
        /// <returns>List of component names that were placed</returns>
        public static List<string> PlaceComponents(
            DeserializationResult result,
            PointF? startPosition = null,
            int spacing = 100)
        {
            if (result == null || result.Components == null || result.Components.Count == 0)
            {
                Debug.WriteLine("[ComponentPlacer] No components to place");
                return new List<string>();
            }

            var placedNames = new List<string>();
            var document = Instances.ActiveCanvas?.Document;

            if (document == null)
            {
                Debug.WriteLine("[ComponentPlacer] No active Grasshopper document");
                return placedNames;
            }

            // Get starting position
            var position = startPosition ?? CanvasAccess.StartPoint(spacing);

            // Apply positions from GhJSON if available
            var componentProps = result.Document?.Components;
            if (componentProps != null)
            {
                ApplyPositionsFromDocument(result.Components, componentProps, result.GuidMapping);
            }

            // Add components to canvas
            foreach (var component in result.Components)
            {
                try
                {
                    // Get position for this component
                    var compPosition = GetComponentPosition(component, componentProps, result.GuidMapping, position);

                    // Add to canvas
                    CanvasAccess.AddObjectToCanvas(component, compPosition, redraw: false);
                    placedNames.Add(component.Name);

                    Debug.WriteLine($"[ComponentPlacer] Placed component '{component.Name}' at ({compPosition.X}, {compPosition.Y})");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ComponentPlacer] Error placing component '{component.Name}': {ex.Message}");
                }
            }

            // Redraw canvas once at the end
            if (placedNames.Count > 0)
            {
                document.NewSolution(false);
                Instances.ActiveCanvas?.Refresh();
            }

            return placedNames;
        }

        /// <summary>
        /// Applies positions from GhJSON document to component instances.
        /// </summary>
        private static void ApplyPositionsFromDocument(
            List<IGH_DocumentObject> components,
            List<SmartHopper.Core.Models.Components.ComponentProperties> componentProps,
            Dictionary<Guid, IGH_DocumentObject> guidMapping)
        {
            foreach (var props in componentProps)
            {
                if (!props.Pivot.IsEmpty && guidMapping.TryGetValue(props.InstanceGuid, out var instance))
                {
                    var pivot = (PointF)props.Pivot;
                    instance.Attributes.Pivot = pivot;
                    Debug.WriteLine($"[ComponentPlacer] Set position for '{instance.Name}' to ({pivot.X}, {pivot.Y})");
                }
            }
        }

        /// <summary>
        /// Gets the position for a component.
        /// </summary>
        private static PointF GetComponentPosition(
            IGH_DocumentObject component,
            List<SmartHopper.Core.Models.Components.ComponentProperties> componentProps,
            Dictionary<Guid, IGH_DocumentObject> guidMapping,
            PointF defaultPosition)
        {
            // Try to find position from props
            if (componentProps != null)
            {
                var props = componentProps.FirstOrDefault(p => guidMapping.ContainsKey(p.InstanceGuid) && guidMapping[p.InstanceGuid] == component);
                if (props != null && !props.Pivot.IsEmpty)
                {
                    return (PointF)props.Pivot;
                }
            }

            // Check if component already has a position set
            if (component.Attributes.Pivot != PointF.Empty)
            {
                return component.Attributes.Pivot;
            }

            // Use default position
            return defaultPosition;
        }

        /// <summary>
        /// Places components with automatic layout.
        /// </summary>
        /// <param name="components">Components to place</param>
        /// <param name="startPosition">Starting position</param>
        /// <param name="spacing">Spacing between components</param>
        /// <returns>List of placed component names</returns>
        public static List<string> PlaceWithAutoLayout(
            List<IGH_DocumentObject> components,
            PointF? startPosition = null,
            int spacing = 100)
        {
            var result = new DeserializationResult
            {
                Components = components,
                GuidMapping = components.ToDictionary(c => c.InstanceGuid, c => c)
            };

            return PlaceComponents(result, startPosition, spacing);
        }
    }
}
