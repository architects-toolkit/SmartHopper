/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartHopper.Components.Properties;
using SmartHopper.Core.Grasshopper;
using SmartHopper.Core.Grasshopper.Utils;
using SmartHopper.Core.JSON;
using SmartHopper.Core.Graph;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;

namespace SmartHopper.Components.Grasshopper
{
    public class GhPutComponent : GH_Component
    {
        private List<string> lastComponentNames = new List<string>();

        public GhPutComponent()
            : base("Place Components", "GhPut", "Convert GhJSON to a Grasshopper definition in this file", "SmartHopper", "Grasshopper")
        {
        }

        public override Guid ComponentGuid => new Guid("25E07FD9-382C-48C0-8A97-8BFFAEAD8592");

        protected override System.Drawing.Bitmap Icon => Resources.ghput;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("JSON", "J", "JSON", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Run?", "R", "Run this component?", GH_ParamAccess.item, false);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Components", "C", "List of components", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Get run parameter
            bool run = false;

            if (!DA.GetData(1, ref run)) return;

            if (!run)
            {
                if (lastComponentNames.Count > 0)
                {
                    DA.SetDataList(0, lastComponentNames);
                }
                else
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Set Run to True to place components");
                }
                return;
            }

            // Clear previous results when starting a new run
            lastComponentNames.Clear();

            try
            {
                // Get the JSON
                string json = null;
                if (!DA.GetData(0, ref json)) return;

                // Parse and validate JSON
                Core.JSON.GrasshopperDocument document;
                try
                {
                    document = JsonConvert.DeserializeObject<Core.JSON.GrasshopperDocument>(json);
                    if (document?.Components == null || !document.Components.Any())
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "JSON must contain a non-empty components array");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid JSON format: " + ex.Message);
                    return;
                }

                // Dictionary to store original to new GUIDs mapping
                var guidMapping = new Dictionary<Guid, Guid>();

                // Get the starting position for the components
                var startPoint = GHCanvasUtils.StartPoint(100);

                // Check if any components are missing pivot positions
                bool needsPositioning = document.Components.Any(c => c.Pivot.IsEmpty);
                Dictionary<string, PointF> generatedPositions = null;

                if (needsPositioning)
                {
                    try
                    {
                        // Convert to JsonStructure format for DependencyGraphUtils
                        var jsonStructures = document.Components.Select(c => new JsonStructure
                        {
                            ID = c.InstanceGuid,
                            Name = c.Name,
                            Inputs = document.GetComponentConnections(c.InstanceGuid)
                                .Where(conn => conn.To.ComponentId == c.InstanceGuid)
                                .Select(conn => new JsonInput
                                {
                                    Sources = new List<Guid> { conn.From.ComponentId }
                                }).ToList()
                        }).ToList();

                        // Generate positions for components using DependencyGraphUtils
                        var positions = DependencyGraphUtils.Program.CreateComponentGrid(jsonStructures);
                        
                        // Update component positions in the document
                        foreach (var component in document.Components)
                        {
                            if (positions.TryGetValue(component.InstanceGuid.ToString(), out var position))
                            {
                                component.Pivot = new System.Drawing.Point((int)position.X * 150, (int)position.Y * 150);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error generating component positions: {ex.Message}");
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Could not generate optimal component positions");
                    }
                }

                // Create components
                foreach (var component in document.Components)
                {
                    try
                    {
                        // Find and create component
                        IGH_ObjectProxy proxy = GHObjectFactory.FindProxy(component.ComponentGuid, component.Name);
                        IGH_DocumentObject instance = GHObjectFactory.CreateInstance(proxy);

                        Debug.WriteLine($"Creating component: {component.Name} of type {component.Type}");

                        // Handle number sliders specially
                        if (instance is GH_NumberSlider slider && component.Properties != null)
                        {
                            try
                            {
                                var currentValueProp = component.Properties["CurrentValue"];
                                if (currentValueProp != null && currentValueProp.Value != null)
                                {
                                    var currentValue = ((JObject)currentValueProp.Value)["Value"].ToString();
                                    Debug.WriteLine($"Setting slider value to: {currentValue}");
                                    slider.SetInitCode(currentValue);
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Error setting number slider value: {ex.Message}");
                                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Error setting number slider value: {ex.Message}");
                            }
                        }
                        // Set properties for non-slider components
                        else if (component.Properties != null)
                        {
                            var filteredProperties = component.Properties
                                .Where(kvp => !GHPropertyManager.IsPropertyOmitted(kvp.Key))
                                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                            GHPropertyManager.SetProperties(instance, filteredProperties);
                        }

                        // Set position - use generated position if pivot is missing
                        PointF position;
                        if (component.Pivot.IsEmpty)
                        {
                            position = generatedPositions != null && generatedPositions.TryGetValue(component.InstanceGuid.ToString(), out PointF genPos)
                                ? new PointF(genPos.X + startPoint.X, genPos.Y + startPoint.Y)
                                : startPoint; // Fallback to startPoint if no generated position
                        }
                        else
                        {
                            position = new PointF(
                                component.Pivot.X + startPoint.X,
                                component.Pivot.Y + startPoint.Y
                            );
                        }

                        // Add to canvas
                        GHCanvasUtils.AddObjectToCanvas(instance, position, true);

                        // Store GUID mapping
                        guidMapping[component.InstanceGuid] = instance.InstanceGuid;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error creating component {component.Name}: {ex.Message}");
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Error creating component {component.Name}");
                    }
                }

                // Create connections
                foreach (var connection in document.Connections)
                {
                    try
                    {
                        if (!connection.IsValid()) continue;

                        // Get source and target objects
                        if (!guidMapping.TryGetValue(connection.From.ComponentId, out Guid sourceGuid) ||
                            !guidMapping.TryGetValue(connection.To.ComponentId, out Guid targetGuid))
                        {
                            continue;
                        }

                        IGH_DocumentObject sourceObj = GHCanvasUtils.FindInstance(sourceGuid);
                        IGH_DocumentObject targetObj = GHCanvasUtils.FindInstance(targetGuid);

                        if (sourceObj == null || targetObj == null) continue;

                        // Get source parameter
                        IGH_Param sourceParam = null;
                        if (sourceObj is IGH_Component sourceComp)
                        {
                            sourceParam = GHParameterUtils.GetOutputByName(sourceComp, connection.From.ParamName);
                        }
                        else if (sourceObj is IGH_Param)
                        {
                            sourceParam = sourceObj as IGH_Param;
                        }

                        // Get target parameter and set source
                        if (targetObj is IGH_Component targetComp)
                        {
                            var targetParam = GHParameterUtils.GetInputByName(targetComp, connection.To.ParamName);
                            if (targetParam != null && sourceParam != null)
                            {
                                GHParameterUtils.SetSource(targetParam, sourceParam);
                            }
                        }
                        else if (targetObj is IGH_Param targetParam)
                        {
                            if (sourceParam != null)
                            {
                                GHParameterUtils.SetSource(targetParam, sourceParam);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error creating connection: {ex.Message}");
                    }
                }

                // Output component names
                var componentNames = document.Components.Select(c => c.Name).Distinct().ToList();
                lastComponentNames = componentNames;
                DA.SetDataList(0, componentNames);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error: {ex.Message}");
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Error: {ex.Message}");
            }
        }
    }
}
