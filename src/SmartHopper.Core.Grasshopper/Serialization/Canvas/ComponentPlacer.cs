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
using SmartHopper.Core.Grasshopper.Graph;
using SmartHopper.Core.Grasshopper.Serialization.GhJson;
using SmartHopper.Core.Grasshopper.Utils.Canvas;
using SmartHopper.Core.Models.Document;

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
        /// <param name="useExactPositions">When true, uses exact pivot positions from GhJSON without offset calculation. Use for component replacement scenarios.</param>
        /// <returns>List of component names that were placed</returns>
        public static List<string> PlaceComponents(
            DeserializationResult result,
            PointF? startPosition = null,
            int spacing = 100,
            bool useExactPositions = false)
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

            var componentProps = result.Document?.Components;
            bool hasPivots = componentProps != null && componentProps.Any(p => !p.Pivot.IsEmpty);

            // Calculate positions based on whether pivots exist in GhJSON
            if (hasPivots)
            {
                if (useExactPositions)
                {
                    // Use exact positions from GhJSON without any offset (for replacement scenarios)
                    ApplyOffsettedPositions(result.Components, componentProps, result.GuidMapping, PointF.Empty);
                    Debug.WriteLine("[ComponentPlacer] Applied exact pivot positions (no offset)");
                }
                else
                {
                    // Pivots exist: offset them to prevent overlap with existing components
                    var offset = CalculatePivotOffset(componentProps, startPosition);
                    ApplyOffsettedPositions(result.Components, componentProps, result.GuidMapping, offset);
                    Debug.WriteLine($"[ComponentPlacer] Applied pivot offset: ({offset.X}, {offset.Y})");
                }
            }
            else
            {
                // No pivots: use tidy-up algorithm to calculate new positions
                var layoutNodes = DependencyGraphUtils.CreateComponentGrid(result.Document, force: true);
                var gridStartPosition = startPosition ?? CanvasAccess.StartPoint(spacing);
                ApplyGridLayout(result.Components, layoutNodes, result.GuidMapping, gridStartPosition);
                Debug.WriteLine($"[ComponentPlacer] Applied grid layout starting at ({gridStartPosition.X}, {gridStartPosition.Y})");
            }

            // Add components to canvas (positions already set above)
            foreach (var component in result.Components)
            {
                try
                {
                    // Get position (already set by ApplyOffsettedPositions or ApplyGridLayout)
                    var compPosition = GetComponentPosition(component);

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
                // Only refresh the canvas display, don't trigger a new solution
                // NewSolution() can cause infinite loops by re-triggering AI components
                Instances.ActiveCanvas?.Refresh();
            }

            return placedNames;
        }

        /// <summary>
        /// Calculates the offset to apply to pivots from GhJSON to prevent overlap.
        /// Returns (0, lowestY) where lowestY is the lowest Y position on the current canvas.
        /// </summary>
        private static PointF CalculatePivotOffset(
            List<SmartHopper.Core.Models.Components.ComponentProperties> componentProps,
            PointF? startPosition)
        {
            if (startPosition.HasValue)
            {
                // Use provided start position
                var maxY = componentProps.Where(p => !p.Pivot.IsEmpty).Max(p => ((PointF)p.Pivot).Y);
                return new PointF(startPosition.Value.X, startPosition.Value.Y + maxY);
            }

            // Find the lowest Y position on the current canvas
            var currentObjects = CanvasAccess.GetCurrentObjects();
            float lowestY = 0f;

            if (currentObjects.Any())
            {
                lowestY = currentObjects.Max(o => o.Attributes.Pivot.Y + o.Attributes.Bounds.Height);
                lowestY += 100f; // Add spacing buffer
            }

            // Calculate offset to move the top of the new components to lowestY
            var minComponentY = componentProps.Where(p => !p.Pivot.IsEmpty).Min(p => ((PointF)p.Pivot).Y);
            return new PointF(0, lowestY - minComponentY);
        }

        /// <summary>
        /// Applies offsetted positions from GhJSON to component instances.
        /// </summary>
        private static void ApplyOffsettedPositions(
            List<IGH_DocumentObject> components,
            List<SmartHopper.Core.Models.Components.ComponentProperties> componentProps,
            Dictionary<Guid, IGH_DocumentObject> guidMapping,
            PointF offset)
        {
            foreach (var props in componentProps)
            {
                if (!props.Pivot.IsEmpty && guidMapping.TryGetValue(props.InstanceGuid, out var instance))
                {
                    var originalPivot = (PointF)props.Pivot;
                    var newPivot = new PointF(originalPivot.X + offset.X, originalPivot.Y + offset.Y);
                    instance.Attributes.Pivot = newPivot;
                    Debug.WriteLine($"[ComponentPlacer] Set position for '{instance.Name}' to ({newPivot.X}, {newPivot.Y})");
                }
            }
        }

        /// <summary>
        /// Applies grid layout calculated by DependencyGraphUtils to component instances.
        /// </summary>
        private static void ApplyGridLayout(
            List<IGH_DocumentObject> components,
            List<NodeGridComponent> layoutNodes,
            Dictionary<Guid, IGH_DocumentObject> guidMapping,
            PointF startPosition)
        {
            foreach (var node in layoutNodes)
            {
                if (guidMapping.TryGetValue(node.ComponentId, out var instance))
                {
                    var newPivot = new PointF(startPosition.X + node.Pivot.X, startPosition.Y + node.Pivot.Y);
                    instance.Attributes.Pivot = newPivot;
                    Debug.WriteLine($"[ComponentPlacer] Set grid position for '{instance.Name}' to ({newPivot.X}, {newPivot.Y})");
                }
            }
        }

        /// <summary>
        /// Gets the position for a component.
        /// This is called after positions have been set by ApplyOffsettedPositions or ApplyGridLayout.
        /// </summary>
        private static PointF GetComponentPosition(IGH_DocumentObject component)
        {
            // Position should already be set by ApplyOffsettedPositions or ApplyGridLayout
            return component.Attributes.Pivot;
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
