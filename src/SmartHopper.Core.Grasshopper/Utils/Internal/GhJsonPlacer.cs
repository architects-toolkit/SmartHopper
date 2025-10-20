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
using SmartHopper.Core.Models.Components;
using SmartHopper.Core.Models.Document;
using SmartHopper.Core.Models.Serialization;

namespace SmartHopper.Core.Grasshopper.Utils.Internal
{
    /// <summary>
    /// Places GhJSON document components onto the Grasshopper canvas.
    /// Handles component instantiation, property application, positioning, and connections.
    /// </summary>
    internal static class GhJsonPlacer
    {
        public static List<string> PutObjectsOnCanvas(GrasshopperDocument document, int span = 100)
        {
            var startPoint = CanvasAccess.StartPoint(span);
            return PutObjectsOnCanvas(document, startPoint);
        }

        /// <summary>
        /// Places all components from a GhJSON document onto the canvas.
        /// Returns mapping from template GUIDs to placed component GUIDs.
        /// </summary>
        private static Dictionary<Guid, Guid> PlaceDocumentComponents(GrasshopperDocument document, PointF startPoint)
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
                            if (component.Pivot.IsEmpty && forcePosMap.TryGetValue(component.InstanceGuid, out var forcePivotPointF))
                            {
                                component.Pivot = new CompactPosition(forcePivotPointF.X, forcePivotPointF.Y);
                                Debug.WriteLine($"[GhJsonPlacer] Applied fallback position to component {component.InstanceGuid}: ({forcePivotPointF.X}, {forcePivotPointF.Y})");
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
                    // Apply legacy properties for backward compatibility using PropertyManagerV2
                    // Note: component.Properties is already Dictionary<string, ComponentProperty> after deserialization
                    var propertyManager = PropertyManagerFactory.CreateForAI();
                    propertyManager.ApplyProperties(instance, component.Properties);
                }

                // Apply schema properties
                ApplySchemaProperties(instance, component);

                // Position and add component
                var position = component.Pivot.IsEmpty
                    ? startPoint
                    : new PointF(component.Pivot.X + startPoint.X, component.Pivot.Y + startPoint.Y);

                CanvasAccess.AddObjectToCanvas(instance, position, true);
                guidMapping[component.InstanceGuid] = instance.InstanceGuid;
            }

            // Connections
            var idToGuid = document.GetIdToGuidMapping();
            foreach (var connection in document.Connections)
            {
                try
                {
                    if (!connection.IsValid())
                        continue;

                    // Map from integer ID to original GUID, then to new GUID
                    if (!connection.TryResolveGuids(idToGuid, out var fromOrigGuid, out var toOrigGuid) ||
                        !guidMapping.TryGetValue(fromOrigGuid, out var src) ||
                        !guidMapping.TryGetValue(toOrigGuid, out var tgt))
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
        /// Applies schema properties to a Grasshopper component instance.
        /// </summary>
        /// <param name="instance">The component instance to configure.</param>
        /// <param name="component">The component properties from the schema.</param>
        private static void ApplySchemaProperties(IGH_DocumentObject instance, SmartHopper.Core.Models.Components.ComponentProperties component)
        {
            try
            {
                // Apply basic params
                if (component.Params != null)
                {
                    ApplyBasicParams(instance, component.Params);
                }

                // Apply input/output parameter settings
                if (component.InputSettings != null || component.OutputSettings != null)
                {
                    ApplyParameterSettings(instance, component.InputSettings, component.OutputSettings);
                }

                // Apply component state
                if (component.ComponentState != null)
                {
                    ApplyComponentState(instance, component.ComponentState);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error applying new schema properties to {instance.InstanceGuid}: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies basic parameters to a component instance.
        /// </summary>
        /// <param name="instance">The component instance.</param>
        /// <param name="basicParams">The basic parameters to apply.</param>
        private static void ApplyBasicParams(IGH_DocumentObject instance, Dictionary<string, object> basicParams)
        {
            foreach (var param in basicParams)
            {
                try
                {
                    switch (param.Key)
                    {
                        case "NickName":
                            if (param.Value is string nickName)
                            {
                                instance.NickName = nickName;
                            }
                            break;
                        case "UserText":
                            if (instance is GH_Panel panel && param.Value is string userText)
                            {
                                panel.UserText = userText;
                            }
                            break;
                        case "Text":
                            if (instance is GH_Scribble scribble && param.Value is string text)
                            {
                                scribble.Text = text;
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error applying basic param {param.Key}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Applies parameter settings to component inputs and outputs.
        /// </summary>
        /// <param name="instance">The component instance.</param>
        /// <param name="inputSettings">Input parameter settings.</param>
        /// <param name="outputSettings">Output parameter settings.</param>
        private static void ApplyParameterSettings(IGH_DocumentObject instance, 
            List<SmartHopper.Core.Models.Components.ParameterSettings> inputSettings, 
            List<SmartHopper.Core.Models.Components.ParameterSettings> outputSettings)
        {
            if (instance is IGH_Component component)
            {
                // Apply input settings and track principal parameter
                int principalParameterIndex = -1;
                if (inputSettings != null)
                {
                    for (int i = 0; i < inputSettings.Count; i++)
                    {
                        var setting = inputSettings[i];
                        var param = ParameterAccess.GetInputByName(component, setting.ParameterName);
                        if (param != null)
                        {
                            ApplyParameterSetting(param, setting);

                            // Check if this parameter is marked as principal
                            if (setting.AdditionalSettings?.IsPrincipal == true)
                            {
                                // Find the actual index of this parameter in the component's input params
                                principalParameterIndex = component.Params.Input.IndexOf(param);
                            }
                        }
                    }
                }

                // Set the principal parameter index at component level
                if (principalParameterIndex >= 0 && component is GH_Component ghComp)
                {
                    ghComp.PrincipalParameterIndex = principalParameterIndex;
                }

                // Apply output settings
                if (outputSettings != null)
                {
                    foreach (var setting in outputSettings)
                    {
                        var param = ParameterAccess.GetOutputByName(component, setting.ParameterName);
                        if (param != null)
                        {
                            ApplyParameterSetting(param, setting);
                        }
                    }
                }
            }
            else if (instance is IGH_Param param && outputSettings != null)
            {
                // For standalone parameters, apply output settings
                var setting = outputSettings.FirstOrDefault(s => s.ParameterName == param.Name);
                if (setting != null)
                {
                    ApplyParameterSetting(param, setting);
                }
            }
        }

        /// <summary>
        /// Applies a single parameter setting to a Grasshopper parameter.
        /// </summary>
        /// <param name="param">The parameter to configure.</param>
        /// <param name="setting">The parameter settings.</param>
        private static void ApplyParameterSetting(IGH_Param param, SmartHopper.Core.Models.Components.ParameterSettings setting)
        {
            try
            {
                // Data mapping
                if (!string.IsNullOrEmpty(setting.DataMapping))
                {
                    param.DataMapping = StringConverter.StringToGHDataMapping(setting.DataMapping);
                }

                // Expression - apply through reflection since IGH_Param doesn't expose it directly
                if (!string.IsNullOrEmpty(setting.Expression))
                {
                    ApplyParameterExpression(param, setting.Expression);
                }

                // Additional settings
                if (setting.AdditionalSettings != null)
                {
                    var additional = setting.AdditionalSettings;

                    if (additional.Reverse.HasValue)
                        param.Reverse = additional.Reverse.Value;

                    if (additional.Simplify.HasValue)
                        param.Simplify = additional.Simplify.Value;

                    if (additional.Locked.HasValue)
                        param.Locked = additional.Locked.Value;

                    // Note: IsPrincipal is handled at the component level via PrincipalParameterIndex
                    // It's processed in ApplyParameterSettings(), not here at the individual parameter level

                    // Note: Invert is for boolean parameters only and is not directly supported by Grasshopper parameters
                    // It should be handled at the data level, not parameter level
                    // TODO: Implement invert functionality for boolean data processing
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error applying parameter setting to {param.Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies parameter expression using reflection since it's not exposed through IGH_Param interface.
        /// </summary>
        /// <param name="param">The parameter to apply expression to.</param>
        /// <param name="expression">The expression string to apply.</param>
        private static void ApplyParameterExpression(IGH_Param param, string expression)
        {
            try
            {
                // Try to set Expression property through reflection
                var expressionProperty = param.GetType().GetProperty("Expression");
                if (expressionProperty != null && expressionProperty.CanWrite)
                {
                    expressionProperty.SetValue(param, expression);
                    Debug.WriteLine($"Applied expression '{expression}' to parameter {param.Name}");
                }
                else
                {
                    Debug.WriteLine($"Cannot set expression on parameter {param.Name} - property not found or not writable");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error applying expression '{expression}' to parameter {param.Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies component state to a component instance.
        /// </summary>
        /// <param name="instance">The component instance.</param>
        /// <param name="state">The component state to apply.</param>
        private static void ApplyComponentState(IGH_DocumentObject instance, SmartHopper.Core.Models.Components.ComponentState state)
        {
            try
            {
                // Apply component-level properties
                if (instance is GH_Component ghComponent)
                {
                    if (state.Locked.HasValue)
                    {
                        ghComponent.Locked = state.Locked.Value;
                    }
                    if (state.Hidden.HasValue)
                    {
                        ghComponent.Hidden = state.Hidden.Value;
                    }
                }

                // Handle universal value property and component-specific types
                if (instance is GH_NumberSlider slider && state.Value != null)
                {
                    // Number Slider: value format "5.0<0.0,10.0>"
                    slider.SetInitCode(state.Value.ToString());
                }
                else if (instance is GH_Panel panel && state.Value != null)
                {
                    // Panel: value is userText
                    panel.UserText = state.Value.ToString();
                }
                else if (instance is GH_Scribble scribble && state.Value != null)
                {
                    // Scribble: value is text
                    scribble.Text = state.Value.ToString();
                }
                else if (instance is IScriptComponent scriptComp && state.Value != null)
                {
                    // Script: value is script code
                    scriptComp.Text = state.Value.ToString();
                }

                // Apply script-specific properties
                if (instance is IScriptComponent scriptComp2)
                {
                    if (state.MarshInputs.HasValue)
                    {
                        scriptComp2.MarshInputs = state.MarshInputs.Value;
                    }
                    
                    if (state.MarshOutputs.HasValue)
                    {
                        scriptComp2.MarshOutputs = state.MarshOutputs.Value;
                    }
                }

                // Apply value list mode
                if (!string.IsNullOrEmpty(state.ListMode))
                {
                    // Value list mode handling would go here
                    // This requires specific implementation for value list components
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error applying component state to {instance.InstanceGuid}: {ex.Message}");
            }
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
            var mapping = PlaceDocumentComponents(document, startPoint);

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
            return PlaceDocumentComponents(document, startPoint);
        }

        /// <summary>
        /// Places components and returns mapping; overload for point.
        /// </summary>
        public static Dictionary<Guid, Guid> PutObjectsOnCanvasWithMapping(GrasshopperDocument document, PointF startPoint)
        {
            return PlaceDocumentComponents(document, startPoint);
        }

        /// <summary>
        /// Recreates groups from the document using the GUID mapping from placed components.
        /// Maps integer IDs to GUIDs using the document's ID mapping.
        /// </summary>
        /// <param name="document">The document containing group information.</param>
        /// <param name="guidMapping">Mapping from template GUIDs to placed component GUIDs.</param>
        private static void RecreateGroups(GrasshopperDocument document, Dictionary<Guid, Guid> guidMapping)
        {
            try
            {
                // Create ID -> GUID mapping (same pattern as connections)
                var idToGuid = document.GetIdToGuidMapping();

                foreach (var groupInfo in document.Groups)
                {
                    // Map member IDs to placed component GUIDs
                    var placedMemberGuids = new List<Guid>();
                    foreach (var memberId in groupInfo.Members)
                    {
                        // Map ID -> original GUID -> placed GUID
                        if (idToGuid.TryGetValue(memberId, out var originalGuid) &&
                            guidMapping.TryGetValue(originalGuid, out var placedGuid))
                        {
                            placedMemberGuids.Add(placedGuid);
                        }
                        else
                        {
                            Debug.WriteLine($"[GhJsonPlacer] Warning: Group member ID {memberId} not found in placed components");
                        }
                    }

                    if (placedMemberGuids.Count == 0)
                    {
                        Debug.WriteLine($"[GhJsonPlacer] Skipping group '{groupInfo.Name}' - no valid members found");
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

                    // Create the group using DocumentIntrospectionV2.GroupObjects
                    var createdGroup = DocumentIntrospectionV2.GroupObjects(
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
