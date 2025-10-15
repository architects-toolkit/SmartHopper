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
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RhinoCodePlatform.GH;
using RhinoCodePluginGH.Parameters;
using SmartHopper.Core.Grasshopper.Converters;
using SmartHopper.Core.Grasshopper.Graph;
using SmartHopper.Core.Grasshopper.Utils.Canvas;
using SmartHopper.Core.Grasshopper.Utils.Serialization;
using SmartHopper.Core.Models.Document;

namespace SmartHopper.Core.Grasshopper.Utils.Internal
{
    /// <summary>
    /// Utility to place deserialized Grasshopper objects onto the canvas.
    /// </summary>
    internal static class GhJsonPlacer
    {
        public static List<string> PutObjectsOnCanvas(GrasshopperDocument document, int span = 100)
        {
            var startPoint = CanvasAccess.StartPoint(span);
            return PutObjectsOnCanvas(document, startPoint);
        }

        /// <summary>
        /// Core placement logic; returns mapping from template InstanceGuid to placed component InstanceGuid.
        /// </summary>
        private static Dictionary<Guid, Guid> InternalPutObjects(GrasshopperDocument document, PointF startPoint)
        {
            // Replace integer IDs with proper GUIDs before processing
            document = ReplaceIntegerIdsInGhJson(document);

            var guidMapping = new Dictionary<Guid, Guid>();

            // Compute positions with fallback mechanism
            try
            {
                var nodes = DependencyGraphUtils.CreateComponentGrid(document);
                var posMap = nodes.ToDictionary(n => n.ComponentId, n => n.Pivot);
                var positionedCount = 0;

                foreach (var component in document.Components)
                {
                    if (posMap.TryGetValue(component.InstanceGuid, out var pivot))
                    {
                        component.Pivot = pivot;
                        positionedCount++;
                    }
                }

                // Fallback: If not all components got positions, use force layout like gh_tidy_up.cs
                if (positionedCount < document.Components.Count)
                {
                    Debug.WriteLine($"[GhJsonPlacer] Initial positioning incomplete ({positionedCount}/{document.Components.Count}). Using fallback with force layout.");

                    try
                    {
                        var forceNodes = DependencyGraphUtils.CreateComponentGrid(document, force: true);
                        var forcePosMap = forceNodes.ToDictionary(n => n.ComponentId, n => n.Pivot);

                        // Apply positions from force layout to components that don't have positions
                        foreach (var component in document.Components)
                        {
                            if (component.Pivot.IsEmpty && forcePosMap.TryGetValue(component.InstanceGuid, out var forcePivot))
                            {
                                component.Pivot = forcePivot;
                                Debug.WriteLine($"[GhJsonPlacer] Applied fallback position to component {component.InstanceGuid}: ({forcePivot.X}, {forcePivot.Y})");
                            }
                        }
                    }
                    catch (Exception fallbackEx)
                    {
                        Debug.WriteLine($"[GhJsonPlacer] Fallback position calculation also failed: {fallbackEx.Message}");
                    }
                }
                else
                {
                    Debug.WriteLine($"[GhJsonPlacer] Successfully positioned all {positionedCount} components.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GhJsonPlacer] Error in initial position calculation: {ex.Message}. Attempting fallback.");

                // Complete fallback: Use force layout for all components
                try
                {
                    var fallbackNodes = DependencyGraphUtils.CreateComponentGrid(document, force: true);
                    var fallbackPosMap = fallbackNodes.ToDictionary(n => n.ComponentId, n => n.Pivot);

                    foreach (var component in document.Components)
                    {
                        if (fallbackPosMap.TryGetValue(component.InstanceGuid, out var fallbackPivot))
                        {
                            component.Pivot = fallbackPivot;
                            Debug.WriteLine($"[GhJsonPlacer] Applied complete fallback position to component {component.InstanceGuid}: ({fallbackPivot.X}, {fallbackPivot.Y})");
                        }
                    }
                }
                catch (Exception fallbackEx)
                {
                    Debug.WriteLine($"[GhJsonPlacer] Complete fallback position calculation failed: {fallbackEx.Message}");
                }
            }

            // Instantiate components and set up
            foreach (var component in document.Components)
            {
                var proxy = ObjectFactory.FindProxy(component.ComponentGuid, component.Name);
                var instance = ObjectFactory.CreateInstance(proxy);

                if (instance is GH_NumberSlider slider && component.Properties != null)
                {
                    try
                    {
                        var prop = component.Properties.GetValueOrDefault("CurrentValue");
                        if (prop?.Value != null)
                        {
                            var initCode = prop.Value.ToString();
                            slider.SetInitCode(initCode);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error setting slider value: {ex.Message}");
                    }
                }
                else if (instance is IScriptComponent scriptComp && component.Properties != null)
                {
                    // clear default script inputs and outputs
                    var ghComp = (IGH_Component)scriptComp;

                    // remove all existing inputs
                    foreach (var p in ghComp.Params.Input.ToArray())
                        ghComp.Params.UnregisterInputParameter(p);

                    // remove all existing outputs
                    foreach (var p in ghComp.Params.Output.ToArray())
                        ghComp.Params.UnregisterOutputParameter(p);

                    // Script code
                    if (component.Properties.TryGetValue("Script", out var scriptProperty) && scriptProperty?.Value != null)
                    {
                        var scriptCode = scriptProperty.Value.ToString();
                        scriptComp.Text = scriptCode;
                        Debug.WriteLine($"Set script code for component {instance.InstanceGuid}, length: {scriptCode.Length}");
                    }

                    // Inputs
                    if (component.Properties.TryGetValue("ScriptInputs", out var inputsProperty) && inputsProperty?.Value is JArray inputArray)
                    {
                        foreach (JObject o in inputArray)
                        {
                            var variableName = o["variableName"]?.ToString() ?? string.Empty;
                            var prettyName = o["name"]?.ToString() ?? variableName;
                            var description = o["description"]?.ToString() ?? string.Empty;
                            var access = Enum.TryParse<GH_ParamAccess>(o["access"]?.ToString(), true, out var pa) ? pa : GH_ParamAccess.item;

                            if (ParameterAccess.GetInputByName((IGH_Component)scriptComp, variableName) == null)
                            {
                                var param = new ScriptVariableParam(variableName)
                                {
                                    PrettyName = prettyName,
                                    Description = description,
                                    Access = access,
                                };
                                param.CreateAttributes();
                                if (o["simplify"] != null)
                                    param.Simplify = o["simplify"].Value<bool>();
                                if (o["reverse"] != null)
                                    param.Reverse = o["reverse"].Value<bool>();
                                if (o["dataMapping"] != null)
                                    param.DataMapping = StringConverter.StringToGHDataMapping(o["dataMapping"].ToString());
                                ((IGH_Component)scriptComp).Params.RegisterInputParam(param);
                            }
                            else
                            {
                                var existing = ParameterAccess.GetInputByName((IGH_Component)scriptComp, variableName);
                                if (existing != null)
                                {
                                    if (o["simplify"] != null)
                                        existing.Simplify = o["simplify"].Value<bool>();
                                    if (o["reverse"] != null)
                                        existing.Reverse = o["reverse"].Value<bool>();
                                    if (o["dataMapping"] != null)
                                        existing.DataMapping = StringConverter.StringToGHDataMapping(o["dataMapping"].ToString());
                                }
                            }
                        }
                    }

                    // Outputs
                    if (component.Properties.TryGetValue("ScriptOutputs", out var outputsProperty) && outputsProperty?.Value is JArray outputArray)
                    {
                        foreach (JObject o in outputArray)
                        {
                            var variableName = o["variableName"]?.ToString() ?? string.Empty;
                            var prettyName = o["name"]?.ToString() ?? variableName;
                            var description = o["description"]?.ToString() ?? string.Empty;
                            var access = Enum.TryParse<GH_ParamAccess>(o["access"]?.ToString(), true, out var pa2) ? pa2 : GH_ParamAccess.item;

                            if (ParameterAccess.GetOutputByName((IGH_Component)scriptComp, variableName) == null)
                            {
                                var param = new ScriptVariableParam(variableName)
                                {
                                    PrettyName = prettyName,
                                    Description = description,
                                    Access = access,
                                };
                                param.CreateAttributes();
                                if (o["simplify"] != null)
                                    param.Simplify = o["simplify"].Value<bool>();
                                if (o["reverse"] != null)
                                    param.Reverse = o["reverse"].Value<bool>();
                                if (o["dataMapping"] != null)
                                    param.DataMapping = StringConverter.StringToGHDataMapping(o["dataMapping"].ToString());
                                ((IGH_Component)scriptComp).Params.RegisterOutputParam(param);
                            }
                            else
                            {
                                var existing = ParameterAccess.GetOutputByName((IGH_Component)scriptComp, variableName);
                                if (existing != null)
                                {
                                    if (o["simplify"] != null)
                                        existing.Simplify = o["simplify"].Value<bool>();
                                    if (o["reverse"] != null)
                                        existing.Reverse = o["reverse"].Value<bool>();
                                    if (o["dataMapping"] != null)
                                        existing.DataMapping = StringConverter.StringToGHDataMapping(o["dataMapping"].ToString());
                                }
                            }
                        }
                    }

                    // Rebuild variable parameter UI
                    ((dynamic)scriptComp).VariableParameterMaintenance();
                }
                else if (component.Properties != null)
                {
                    var filtered = component.Properties
                        .Where(kvp => !PropertyManager.IsPropertyOmitted(kvp.Key))
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                    PropertyManager.SetProperties(instance, filtered);
                }

                // Position and add
                var position = component.Pivot.IsEmpty
                    ? startPoint
                    : new PointF(component.Pivot.X + startPoint.X, component.Pivot.Y + startPoint.Y);
                CanvasAccess.AddObjectToCanvas(instance, position, true);
                guidMapping[component.InstanceGuid] = instance.InstanceGuid;
            }

            // Connections
            foreach (var connection in document.Connections)
            {
                try
                {
                    if (!connection.IsValid())
                        continue;

                    if (!guidMapping.TryGetValue(connection.From.InstanceId, out var src) ||
                        !guidMapping.TryGetValue(connection.To.InstanceId, out var tgt))
                        continue;

                    var srcObj = CanvasAccess.FindInstance(src);
                    var tgtObj = CanvasAccess.FindInstance(tgt);
                    if (srcObj == null || tgtObj == null)
                        continue;

                    IGH_Param srcParam = null;
                    if (srcObj is IGH_Component sc)
                        srcParam = ParameterAccess.GetOutputByName(sc, connection.From.ParamName);
                    else if (srcObj is IGH_Param sp)
                        srcParam = sp;

                    if (tgtObj is IGH_Component tc)
                    {
                        var tp = ParameterAccess.GetInputByName(tc, connection.To.ParamName);
                        if (tp != null && srcParam != null)
                            ParameterAccess.SetSource(tp, srcParam);
                    }
                    else if (tgtObj is IGH_Param tp2 && srcParam != null)
                    {
                        ParameterAccess.SetSource(tp2, srcParam);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error creating connection: {ex.Message}");
                }
            }

            return guidMapping;
        }

        /// <summary>
        /// Processes a GhJSON string to replace integer-based IDs with proper GUIDs.
        /// Uses flexible JSON parsing to avoid coupling to specific object structures.
        /// </summary>
        /// <param name="ghjsonString">The GhJSON string to process</param>
        /// <returns>A processed GhJSON string with GUIDs replacing integer IDs</returns>
        public static string ReplaceIntegerIdsInGhJson(string ghjsonString)
        {
            if (string.IsNullOrWhiteSpace(ghjsonString))
                return ghjsonString;

            try
            {
                var idToGuidMap = new Dictionary<string, string>();

                // Helper to get or create GUID string for an ID
                string GetOrCreateGuidString(string id)
                {
                    if (string.IsNullOrWhiteSpace(id))
                        return id;

                    // If it's already a valid GUID, return it unchanged
                    if (Guid.TryParse(id, out _))
                        return id;

                    // Otherwise, map integer IDs to consistent GUIDs
                    if (!idToGuidMap.TryGetValue(id, out var mappedGuid))
                    {
                        mappedGuid = Guid.NewGuid().ToString();
                        idToGuidMap[id] = mappedGuid;
                        Debug.WriteLine($"[GhJsonPlacer] Mapped ID '{id}' to GUID '{mappedGuid}'");
                    }

                    return mappedGuid;
                }

                // Parse JSON to work with the structure flexibly
                var jsonObject = JObject.Parse(ghjsonString);

                // Process all components array
                var components = jsonObject["components"] as JArray;
                if (components != null)
                {
                    foreach (var component in components)
                    {
                        // Replace instanceGuid if it exists
                        if (component["instanceGuid"] != null)
                        {
                            var currentId = component["instanceGuid"].ToString();
                            component["instanceGuid"] = GetOrCreateGuidString(currentId);
                        }
                    }
                }

                // Process all connections array
                var connections = jsonObject["connections"] as JArray;
                if (connections != null)
                {
                    foreach (var connection in connections)
                    {
                        // Replace from.instanceId if it exists
                        var fromObj = connection["from"];
                        if (fromObj?["instanceId"] != null)
                        {
                            var currentId = fromObj["instanceId"].ToString();
                            fromObj["instanceId"] = GetOrCreateGuidString(currentId);
                        }

                        // Replace to.instanceId if it exists
                        var toObj = connection["to"];
                        if (toObj?["instanceId"] != null)
                        {
                            var currentId = toObj["instanceId"].ToString();
                            toObj["instanceId"] = GetOrCreateGuidString(currentId);
                        }
                    }
                }

                Debug.WriteLine($"[GhJsonPlacer] ID replacement completed. Mapped {idToGuidMap.Count} integer IDs to GUIDs.");
                return jsonObject.ToString();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GhJsonPlacer] Error in JSON ID replacement: {ex.Message}");
                return ghjsonString; // Return original JSON if replacement fails
            }
        }

        /// <summary>
        /// Processes a GrasshopperDocument to replace integer-based IDs with proper GUIDs.
        /// Overload that works directly with GrasshopperDocument objects.
        /// </summary>
        /// <param name="document">The GrasshopperDocument to process</param>
        /// <returns>A processed GrasshopperDocument with GUIDs replacing integer IDs</returns>
        public static GrasshopperDocument ReplaceIntegerIdsInGhJson(GrasshopperDocument document)
        {
            if (document == null)
                return document;

            try
            {
                // Serialize to JSON, process, then deserialize back
                var jsonString = JsonConvert.SerializeObject(document, Formatting.None);
                var processedJsonString = ReplaceIntegerIdsInGhJson(jsonString);
                var processedDocument = JsonConvert.DeserializeObject<GrasshopperDocument>(processedJsonString);

                return processedDocument ?? document;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GhJsonPlacer] Error in GrasshopperDocument ID replacement: {ex.Message}");
                return document; // Return original document if replacement fails
            }
        }

        /// <summary>
        /// Places components and returns their names.
        /// </summary>
        public static List<string> PutObjectsOnCanvas(GrasshopperDocument document, PointF startPoint)
        {
            var mapping = InternalPutObjects(document, startPoint);

            // Recreate groups if present in the document
            if (document.Groups != null && document.Groups.Count > 0)
            {
                Debug.WriteLine($"[GhJsonPlacer] About to recreate {document.Groups.Count} groups");
                RecreateGroups(document, mapping);
                Debug.WriteLine("[GhJsonPlacer] Groups recreated");
            }

            Debug.WriteLine("[GhJsonPlacer] PutObjectsOnCanvas returning");
            return document.Components.Select(c => c.Name).Distinct().ToList();
        }

        /// <summary>
        /// Places components and returns mapping; overload for span.
        /// </summary>
        public static Dictionary<Guid, Guid> PutObjectsOnCanvasWithMapping(GrasshopperDocument document, int span = 100)
        {
            var startPoint = CanvasAccess.StartPoint(span);
            return InternalPutObjects(document, startPoint);
        }

        /// <summary>
        /// Places components and returns mapping; overload for point.
        /// </summary>
        public static Dictionary<Guid, Guid> PutObjectsOnCanvasWithMapping(GrasshopperDocument document, PointF startPoint)
        {
            return InternalPutObjects(document, startPoint);
        }

        /// <summary>
        /// Recreates groups from the document using the GUID mapping from placed components.
        /// </summary>
        /// <param name="document">The document containing group information.</param>
        /// <param name="guidMapping">Mapping from template GUIDs to placed component GUIDs.</param>
        private static void RecreateGroups(GrasshopperDocument document, Dictionary<Guid, Guid> guidMapping)
        {
            try
            {
                foreach (var groupInfo in document.Groups)
                {
                    // Map member GUIDs from template to placed components
                    var placedMemberGuids = new List<Guid>();
                    foreach (var templateMemberGuid in groupInfo.Members)
                    {
                        if (guidMapping.TryGetValue(templateMemberGuid, out var placedGuid))
                        {
                            placedMemberGuids.Add(placedGuid);
                        }
                        else
                        {
                            Debug.WriteLine($"[GhJsonPlacer] Warning: Group member {templateMemberGuid} not found in placed components");
                        }
                    }

                    if (placedMemberGuids.Count == 0)
                    {
                        Debug.WriteLine($"[GhJsonPlacer] Skipping group {groupInfo.Name} - no valid members found");
                        continue;
                    }

                    // Parse color if provided
                    Color? groupColor = null;
                    if (!string.IsNullOrEmpty(groupInfo.Color))
                    {
                        try
                        {
                            groupColor = StringConverter.StringToColor(groupInfo.Color);
                            Debug.WriteLine($"[GhJsonPlacer] Parsed group color from '{groupInfo.Color}' to ARGB({groupColor.Value.A},{groupColor.Value.R},{groupColor.Value.G},{groupColor.Value.B})");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[GhJsonPlacer] Error parsing group color '{groupInfo.Color}': {ex.Message}");
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"[GhJsonPlacer] No color provided for group '{groupInfo.Name}'");
                    }

                    // Create the group using DocumentIntrospection.GroupObjects
                    var createdGroup = DocumentIntrospection.GroupObjects(
                        placedMemberGuids,
                        groupInfo.Name,
                        groupColor);

                    if (createdGroup != null)
                    {
                        Debug.WriteLine($"[GhJsonPlacer] Successfully recreated group '{groupInfo.Name}' with {placedMemberGuids.Count} members");
                    }
                    else
                    {
                        Debug.WriteLine($"[GhJsonPlacer] Failed to create group '{groupInfo.Name}'");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GhJsonPlacer] Error recreating groups: {ex.Message}");
            }
        }
    }
}
