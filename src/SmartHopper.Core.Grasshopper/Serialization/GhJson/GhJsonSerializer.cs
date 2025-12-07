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
using System.Globalization;
using System.Linq;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Special;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using RhinoCodePlatform.GH;
using SmartHopper.Core.Grasshopper.Serialization.GhJson.ScriptComponents;
using SmartHopper.Core.Grasshopper.Serialization.GhJson.Shared;
using SmartHopper.Core.Grasshopper.Utils.Serialization;
using SmartHopper.Core.Models.Components;
using SmartHopper.Core.Models.Connections;
using SmartHopper.Core.Models.Document;
using SmartHopper.Core.Models.Serialization;
using SmartHopper.Core.Serialization.DataTypes;

namespace SmartHopper.Core.Grasshopper.Serialization.GhJson
{
    /// <summary>
    /// Serializes Grasshopper canvas objects to GhJSON format.
    /// Extracts components, connections, and groups into a structured document.
    /// </summary>
    public static class GhJsonSerializer
    {
        /// <summary>
        /// The current schema version of GhJSON.
        /// </summary>
        public const string SchemaVersion = "1.0";

        /// <summary>
        /// Serializes Grasshopper objects to a GhJSON document.
        /// </summary>
        /// <param name="objects">Objects to serialize</param>
        /// <param name="options">Serialization options (null uses Standard)</param>
        /// <returns>GhJSON document representation</returns>
        public static GrasshopperDocument Serialize(
            IEnumerable<IGH_ActiveObject> objects,
            SerializationOptions options = null)
        {
            Debug.WriteLine("[GhJsonSerializer] Serialize called");

            if (objects == null)
            {
                Debug.WriteLine("[GhJsonSerializer] ERROR: objects parameter is null");
                throw new ArgumentNullException(nameof(objects));
            }

            var objectsList = objects.ToList();
            Debug.WriteLine($"[GhJsonSerializer] Processing {objectsList.Count} objects");

            for (int i = 0; i < objectsList.Count; i++)
            {
                var obj = objectsList[i];
                if (obj == null)
                {
                    Debug.WriteLine($"[GhJsonSerializer] ERROR: objects[{i}] is null");
                    throw new ArgumentNullException($"objects[{i}]");
                }

                Debug.WriteLine($"[GhJsonSerializer] Object {i}: {obj?.Name} ({obj?.InstanceGuid})");
            }

            options ??= SerializationOptions.Standard;
            Debug.WriteLine($"[GhJsonSerializer] Using options: IncludeConnections={options.IncludeConnections}, IncludeMetadata={options.IncludeMetadata}, IncludeGroups={options.IncludeGroups}");

            // Create property manager if not provided
            var propertyManager = options.PropertyManager ?? new PropertyManagerV2(options.Context);
            Debug.WriteLine("[GhJsonSerializer] Property manager created");

            return SerializeWithManager(objectsList, propertyManager, options);
        }

        /// <summary>
        /// Serializes with a specific property manager.
        /// </summary>
        private static GrasshopperDocument SerializeWithManager(
            IEnumerable<IGH_ActiveObject> objects,
            PropertyManagerV2 propertyManager,
            SerializationOptions options)
        {
            Debug.WriteLine("[GhJsonSerializer] SerializeWithManager called");

            var objectsList = objects.ToList();
            Debug.WriteLine($"[GhJsonSerializer] objectsList count: {objectsList.Count}");

            var document = new GrasshopperDocument
            {
                SchemaVersion = SchemaVersion,
            };

            Debug.WriteLine("[GhJsonSerializer] Created GrasshopperDocument");

            try
            {
                Debug.WriteLine("[GhJsonSerializer] Starting component extraction...");
                document.Components = ExtractComponents(objectsList, propertyManager, options);
                Debug.WriteLine($"[GhJsonSerializer] Extracted {document.Components?.Count ?? 0} components");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GhJsonSerializer] Exception in ExtractComponents: {ex.GetType().Name}: {ex.Message}");
                Debug.WriteLine($"[GhJsonSerializer] Stack trace: {ex.StackTrace}");
                throw;
            }

            // Assign sequential IDs if using compact representation
            if (options.UseCompactIds)
            {
                Debug.WriteLine("[GhJsonSerializer] Assigning component IDs...");
                AssignComponentIds(document);
                Debug.WriteLine("[GhJsonSerializer] Component IDs assigned");
            }

            // Extract connections
            if (options.IncludeConnections)
            {
                Debug.WriteLine("[GhJsonSerializer] Extracting connections...");
                try
                {
                    document.Connections = ExtractConnections(objectsList, document);
                    Debug.WriteLine($"[GhJsonSerializer] Extracted {document.Connections?.Count ?? 0} connections");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[GhJsonSerializer] Exception in ExtractConnections: {ex.GetType().Name}: {ex.Message}");
                    Debug.WriteLine($"[GhJsonSerializer] Stack trace: {ex.StackTrace}");
                    throw;
                }
            }

            // Extract metadata
            if (options.IncludeMetadata)
            {
                Debug.WriteLine("[GhJsonSerializer] Creating metadata...");
                try
                {
                    document.Metadata = CreateDocumentMetadata(objectsList);
                    Debug.WriteLine("[GhJsonSerializer] Metadata created");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[GhJsonSerializer] Exception in CreateDocumentMetadata: {ex.GetType().Name}: {ex.Message}");
                    Debug.WriteLine($"[GhJsonSerializer] Stack trace: {ex.StackTrace}");
                    throw;
                }
            }

            // Extract groups
            if (options.IncludeGroups)
            {
                Debug.WriteLine("[GhJsonSerializer] Extracting group information...");
                try
                {
                    ExtractGroupInformation(document);
                    Debug.WriteLine("[GhJsonSerializer] Group information extracted");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[GhJsonSerializer] Exception in ExtractGroupInformation: {ex.GetType().Name}: {ex.Message}");
                    Debug.WriteLine($"[GhJsonSerializer] Stack trace: {ex.StackTrace}");
                    throw;
                }
            }

            Debug.WriteLine("[GhJsonSerializer] SerializeWithManager completed successfully");
            return document;
        }

        #region Component Extraction

        /// <summary>
        /// Extracts component properties from Grasshopper objects.
        /// Handles both IGH_Component (components) and IGH_Param (stand-alone parameters).
        /// </summary>
        private static List<ComponentProperties> ExtractComponents(
            List<IGH_ActiveObject> objects,
            PropertyManagerV2 propertyManager,
            SerializationOptions options)
        {
            Debug.WriteLine("[GhJsonSerializer] ExtractComponents called");
            var components = new List<ComponentProperties>();

            Debug.WriteLine($"[GhJsonSerializer] Processing {objects.Count} objects for component extraction");

            int componentIndex = 0;

            // Process IGH_Component objects (regular components)
            foreach (var obj in objects.OfType<IGH_Component>())
            {
                Debug.WriteLine($"[GhJsonSerializer] Processing component {componentIndex}: {obj?.Name} ({obj?.InstanceGuid})");
                componentIndex++;

                try
                {
                    var component = CreateComponentProperties(obj, propertyManager, options);
                    if (component != null)
                    {
                        components.Add(component);
                        Debug.WriteLine($"[GhJsonSerializer] Successfully added component: {component.Name}");
                    }
                    else
                    {
                        Debug.WriteLine($"[GhJsonSerializer] CreateComponentProperties returned null for {obj.Name}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[GhJsonSerializer] Error extracting component {obj.Name}: {ex.Message}");
                    Debug.WriteLine($"[GhJsonSerializer] Exception type: {ex.GetType().Name}");
                    Debug.WriteLine($"[GhJsonSerializer] Stack trace: {ex.StackTrace}");
                }
            }

            // Process IGH_Param objects (stand-alone parameters) that are NOT part of components
            int paramIndex = 0;
            foreach (var obj in objects.OfType<IGH_Param>())
            {
                // Skip parameters that are already part of a component
                // (they will be serialized as part of their parent component)
                if (obj.Attributes?.Parent != null)
                {
                    Debug.WriteLine($"[GhJsonSerializer] Skipping parameter {obj.Name} - part of component");
                    continue;
                }

                Debug.WriteLine($"[GhJsonSerializer] Processing stand-alone parameter {paramIndex}: {obj?.Name} ({obj?.InstanceGuid})");
                paramIndex++;

                try
                {
                    var component = CreateComponentProperties(obj, propertyManager, options);
                    if (component != null)
                    {
                        components.Add(component);
                        Debug.WriteLine($"[GhJsonSerializer] Successfully added stand-alone parameter: {component.Name}");
                    }
                    else
                    {
                        Debug.WriteLine($"[GhJsonSerializer] CreateComponentProperties returned null for {obj.Name}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[GhJsonSerializer] Error extracting stand-alone parameter {obj.Name}: {ex.Message}");
                    Debug.WriteLine($"[GhJsonSerializer] Exception type: {ex.GetType().Name}");
                    Debug.WriteLine($"[GhJsonSerializer] Stack trace: {ex.StackTrace}");
                }
            }

            Debug.WriteLine($"[GhJsonSerializer] ExtractComponents completed with {components.Count} total objects ({componentIndex} components, {paramIndex} stand-alone parameters)");
            return components;
        }

        /// <summary>
        /// Creates ComponentProperties from a Grasshopper component.
        /// </summary>
        private static ComponentProperties CreateComponentProperties(
            IGH_ActiveObject obj,
            PropertyManagerV2 propertyManager,
            SerializationOptions options)
        {
            Debug.WriteLine($"[GhJsonSerializer] CreateComponentProperties called for {obj?.Name}");

            if (obj == null)
            {
                Debug.WriteLine("[GhJsonSerializer] ERROR: obj is null in CreateComponentProperties");
                throw new ArgumentNullException(nameof(obj));
            }

            if (obj.InstanceGuid == Guid.Empty)
            {
                Debug.WriteLine($"[GhJsonSerializer] WARNING: {obj.Name} has empty InstanceGuid");
            }

            if (obj.ComponentGuid == Guid.Empty)
            {
                Debug.WriteLine($"[GhJsonSerializer] WARNING: {obj.Name} has empty ComponentGuid");
            }

            try
            {
                var component = new ComponentProperties
                {
                    InstanceGuid = obj.InstanceGuid,
                    ComponentGuid = obj.ComponentGuid,
                    Name = obj.Name,
                    Pivot = new CompactPosition(obj.Attributes.Pivot.X, obj.Attributes.Pivot.Y),
                };

                // // Only include Library if present and meaningful
                // if (!string.IsNullOrWhiteSpace(obj.Category))
                // {
                //     component.Library = obj.Category;
                // }

                // // Only include Type if present and meaningful
                // if (!string.IsNullOrWhiteSpace(obj.SubCategory))
                // {
                //     component.Type = obj.SubCategory;
                // }

                // Only include NickName if different from Name
                if (!string.IsNullOrWhiteSpace(obj.NickName) &&
                    !string.Equals(obj.Name, obj.NickName, StringComparison.Ordinal))
                {
                    component.NickName = obj.NickName;
                }

                Debug.WriteLine($"[GhJsonSerializer] Created basic component properties for {component.Name}");

                // Only include Selected when true (avoid irrelevant false values)
                if (obj.Attributes.Selected)
                {
                    component.Selected = true;
                }

                Debug.WriteLine($"[GhJsonSerializer] Set basic flags for {component.Name}");

                // Extract schema properties using property manager
                try
                {
                    Debug.WriteLine($"[GhJsonSerializer] Extracting schema properties for {component.Name}");
                    ExtractSchemaProperties(component, obj, propertyManager);
                    Debug.WriteLine($"[GhJsonSerializer] Schema properties extracted for {component.Name}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[GhJsonSerializer] Exception in ExtractSchemaProperties for {obj.Name}: {ex.GetType().Name}: {ex.Message}");
                    Debug.WriteLine($"[GhJsonSerializer] Stack trace: {ex.StackTrace}");
                    throw;
                }

                // Extract parameter settings for GH_Component objects
                if (obj is GH_Component ghComponent)
                {
                    try
                    {
                        Debug.WriteLine($"[GhJsonSerializer] Extracting parameter settings for {component.Name}");
                        component.InputSettings = ExtractParameterSettings(
                            ghComponent.Params.Input, propertyManager, ghComponent, options);
                        component.OutputSettings = ExtractParameterSettings(
                            ghComponent.Params.Output, propertyManager, ghComponent, options);
                        Debug.WriteLine($"[GhJsonSerializer] Parameter settings extracted for {component.Name}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[GhJsonSerializer] Exception in ExtractParameterSettings for {obj.Name}: {ex.GetType().Name}: {ex.Message}");
                        Debug.WriteLine($"[GhJsonSerializer] Stack trace: {ex.StackTrace}");
                        throw;
                    }
                }

                // Extract component state (includes universal value)
                if (options.ExtractComponentState)
                {
                    try
                    {
                        Debug.WriteLine($"[GhJsonSerializer] Extracting component state for {component.Name}");
                        component.ComponentState = ExtractComponentState(component, obj, propertyManager);
                        Debug.WriteLine($"[GhJsonSerializer] Component state extracted for {component.Name}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[GhJsonSerializer] Exception in ExtractComponentState for {obj.Name}: {ex.GetType().Name}: {ex.Message}");
                        Debug.WriteLine($"[GhJsonSerializer] Stack trace: {ex.StackTrace}");
                        throw;
                    }
                }

                return component;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GhJsonSerializer] Exception in CreateComponentProperties for {obj.Name}: {ex.GetType().Name}: {ex.Message}");
                Debug.WriteLine($"[GhJsonSerializer] Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Extracts schema properties using the property manager.
        /// </summary>
        private static void ExtractSchemaProperties(
            ComponentProperties component,
            IGH_ActiveObject originalObject,
            PropertyManagerV2 propertyManager)
        {
            var extractedProps = propertyManager.ExtractProperties(originalObject);
            if (extractedProps != null && extractedProps.Count > 0)
            {
                // Convert to Dictionary<string, object>
                component.SchemaProperties = extractedProps.ToDictionary(
                    kvp => kvp.Key,
                    kvp => (object)kvp.Value);
            }
        }

        /// <summary>
        /// Extracts basic parameters using property manager.
        /// Uses DataTypeSerializer for complex types like Color, Point3d, etc.
        /// </summary>
        private static Dictionary<string, object> ExtractBasicParams(
            IGH_ActiveObject obj,
            PropertyManagerV2 propertyManager)
        {
            var basicParams = new Dictionary<string, object>();

            // Get all instance properties
            var properties = obj.GetType().GetProperties();

            foreach (var prop in properties)
            {
                if (propertyManager.ShouldIncludeProperty(prop.Name, obj))
                {
                    try
                    {
                        var value = prop.GetValue(obj);
                        if (value != null)
                        {
                            // Use DataTypeSerializer for complex types to ensure proper formatting
                            if (DataTypeSerializer.IsTypeSupported(value.GetType()))
                            {
                                basicParams[prop.Name] = DataTypeSerializer.Serialize(value);
                            }
                            else
                            {
                                basicParams[prop.Name] = value;
                            }
                        }
                    }
                    catch
                    {
                        // Skip properties that throw on access
                    }
                }
            }

            return basicParams.Count > 0 ? basicParams : null;
        }

        #endregion

        #region Parameter Extraction

        /// <summary>
        /// Extracts parameter settings from a collection of parameters.
        /// </summary>
        private static List<ParameterSettings> ExtractParameterSettings(
            IEnumerable<IGH_Param> parameters,
            PropertyManagerV2 propertyManager,
            IGH_Component component,
            SerializationOptions options)
        {
            var settings = new List<ParameterSettings>();
            var paramList = parameters.ToList();
            bool isScriptComponent = component is IScriptComponent || IsVBScriptComponent(component);

            for (int i = 0; i < paramList.Count; i++)
            {
                var param = paramList[i];

                // Skip the standard output "out" parameter for script components (including VB Script)
                // This is controlled by the UsingStandardOutputParam property in ComponentState
                if (isScriptComponent && param.Kind == GH_ParamKind.output &&
                    param is Param_String &&
                    (param.Name.Equals("out", StringComparison.OrdinalIgnoreCase) ||
                     param.NickName.Equals("out", StringComparison.OrdinalIgnoreCase)))
                {
                    Debug.WriteLine($"[GhJsonSerializer] Skipping standard output 'out' parameter for script component");
                    continue;
                }

                // Determine if this parameter is the principal (master) input
                int principalIndex = -1;
                if (component != null && component.IsValidMasterParameterIndex)
                {
                    var idx = component.MasterParameterIndex;
                    if (idx >= 0 && idx < paramList.Count)
                        principalIndex = idx;
                }

                bool isPrincipal = (principalIndex >= 0 && i == principalIndex);

                var paramSettings = CreateParameterSettings(param, component, isPrincipal, options);
                if (paramSettings != null)
                {
                    settings.Add(paramSettings);
                }
            }

            return settings.Count > 0 ? settings : null;
        }

        /// <summary>
        /// Creates parameter settings for a single parameter.
        /// </summary>
        private static ParameterSettings CreateParameterSettings(
            IGH_Param param,
            IGH_Component owner,
            bool isPrincipal,
            SerializationOptions options)
        {
            // Check if owner is a script component (C#, Python, IronPython)
            if (owner is IScriptComponent scriptComp)
            {
                return ScriptParameterMapper.ExtractSettings(param, scriptComp, isPrincipal);
            }

            // Check if owner is VB Script (doesn't implement IScriptComponent but uses ScriptVariableParam)
            else if (IsVBScriptComponent(owner))
            {
                // VB Script uses ScriptVariableParam but doesn't implement IScriptComponent
                return ScriptParameterMapper.ExtractVBScriptSettings(param, isPrincipal);
            }
            else
            {
                // Use generic parameter mapper for non-script components
                if (ParameterMapper.HasCustomSettings(param, isPrincipal))
                {
                    return ParameterMapper.ExtractSettings(param, isPrincipal);
                }
            }

            return null;
        }

        /// <summary>
        /// Checks if a component is a VB Script component.
        /// VB Script components don't implement IScriptComponent but use ScriptVariableParam.
        /// </summary>
        private static bool IsVBScriptComponent(IGH_Component component)
        {
            if (component == null)
                return false;

            // Check if component name or type name contains "VB"
            return component.Name.Contains("VB", StringComparison.OrdinalIgnoreCase) ||
                   component.GetType().Name.Contains("VB", StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region State Extraction

        /// <summary>
        /// Extracts component state (enabled, locked, hidden, etc.).
        /// </summary>
        private static ComponentState ExtractComponentState(
            ComponentProperties component,
            IGH_ActiveObject originalObject,
            PropertyManagerV2 propertyManager)
        {
            var state = new ComponentState();
            bool hasState = false;

            // Extract Enabled state
            if (!originalObject.Locked && propertyManager.ShouldIncludeProperty("Enabled", originalObject))
            {
                state.Enabled = true;
                hasState = true;
            }

            // Extract Locked state
            if (originalObject.Locked && propertyManager.ShouldIncludeProperty("Locked", originalObject))
            {
                state.Locked = true;
                hasState = true;
            }

            // Extract Hidden and Preview state for components
            if (originalObject is IGH_Component ghComponent)
            {
                if (ghComponent.Hidden && propertyManager.ShouldIncludeProperty("Hidden", originalObject))
                {
                    state.Hidden = true;
                    hasState = true;
                }

                if (!ghComponent.Hidden && propertyManager.ShouldIncludeProperty("Preview", originalObject))
                {
                    state.Preview = !ghComponent.Hidden;
                    hasState = true;
                }
            }

            // Extract universal value for special components
            var universalValue = ExtractUniversalValue(originalObject, propertyManager);
            if (universalValue != null)
            {
                // VB Script returns VBScriptCode object - store in VBCode property
                if (universalValue is VBScriptCode vbCode)
                {
                    state.VBCode = vbCode;
                    hasState = true;
                    Debug.WriteLine($"[GhJsonSerializer] Stored VB Script 3 sections in ComponentState.VBCode");
                }
                else
                {
                    // Other components store value as-is
                    state.Value = universalValue;
                    hasState = true;
                }
            }

            // Extract standard output visibility for script components
            if (originalObject is IScriptComponent)
            {
                try
                {
                    var compType = originalObject.GetType();
                    var usingStdOutputProp = compType.GetProperty("UsingStandardOutputParam");
                    if (usingStdOutputProp != null && usingStdOutputProp.CanRead)
                    {
                        var value = (bool)usingStdOutputProp.GetValue(originalObject);

                        // Only serialize if true (non-default behavior)
                        if (value)
                        {
                            state.ShowStandardOutput = true;
                            hasState = true;
                            Debug.WriteLine($"[GhJsonSerializer] Extracted UsingStandardOutputParam = {value}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[GhJsonSerializer] Error extracting UsingStandardOutputParam: {ex.Message}");
                }
            }

            return hasState ? state : null;
        }

        #endregion

        #region Universal Value Extraction

        /// <summary>
        /// Extracts universal value for special components (sliders, panels, etc.).
        /// Note: This is only called when ExtractComponentState is true, so no additional filtering needed.
        /// </summary>
        private static object ExtractUniversalValue(
            IGH_ActiveObject originalObject,
            PropertyManagerV2 propertyManager)
        {
            try
            {
                // Number slider
                if (originalObject is GH_NumberSlider slider)
                {
                    return FormatSliderValue(slider);
                }

                // Panel
                if (originalObject is GH_Panel panel)
                {
                    return panel.UserText;
                }

                // Value list
                if (originalObject is GH_ValueList valueList)
                {
                    return valueList.FirstSelectedItem?.Name;
                }

                // Boolean toggle
                if (originalObject is GH_BooleanToggle toggle)
                {
                    return toggle.Value;
                }

                // Colour swatch
                if (originalObject is GH_ColourSwatch swatch)
                {
                    return DataTypeSerializer.Serialize(swatch.SwatchColour);
                }

                // Button object
                if (originalObject is GH_ButtonObject btn)
                {
                    var expNormal = btn.ExpressionNormal;
                    var expPressed = btn.ExpressionPressed;

                    // Only serialize if not default values
                    if (expNormal != "False" || expPressed != "True")
                    {
                        return new Dictionary<string, string>
                        {
                            { "normal", expNormal ?? "False" },
                            { "pressed", expPressed ?? "True" }
                        };
                    }
                }

                // Script components (Python, C#, VB, IronPython)
                if (originalObject is IScriptComponent scriptComp)
                {
                    var scriptCode = scriptComp.Text;
                    Debug.WriteLine($"[GhJsonSerializer] Script component '{(originalObject as IGH_DocumentObject)?.Name}' - Code length: {scriptCode?.Length ?? 0}");
                    if (!string.IsNullOrEmpty(scriptCode))
                    {
                        Debug.WriteLine($"[GhJsonSerializer] Returning script code for '{(originalObject as IGH_DocumentObject)?.Name}'");
                        return scriptCode;
                    }
                    else
                    {
                        Debug.WriteLine($"[GhJsonSerializer] Script code is null or empty for '{(originalObject as IGH_DocumentObject)?.Name}'");
                    }
                }

                // VB Script components don't implement IScriptComponent - use reflection
                else if (originalObject is IGH_Component component &&
                         (component.Name.Contains("VB", StringComparison.OrdinalIgnoreCase) ||
                          component.GetType().Name.Contains("VB", StringComparison.OrdinalIgnoreCase)))
                {
                    return ExtractVBScriptCode(component);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GhJsonSerializer] Error extracting universal value: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Extracts VB Script code as a VBScriptCode object with 3 separate sections.
        /// </summary>
        private static object ExtractVBScriptCode(IGH_Component component)
        {
            try
            {
                var componentType = component.GetType();

                // Access ScriptSource object via reflection
                var scriptSourceProp = componentType.GetProperty("ScriptSource");
                if (scriptSourceProp != null && scriptSourceProp.CanRead)
                {
                    var scriptSourceObj = scriptSourceProp.GetValue(component);
                    if (scriptSourceObj != null)
                    {
                        var scriptSourceType = scriptSourceObj.GetType();

                        // Extract the 3 code sections
                        var usingCodeProp = scriptSourceType.GetProperty("UsingCode");
                        var scriptCodeProp = scriptSourceType.GetProperty("ScriptCode");
                        var additionalCodeProp = scriptSourceType.GetProperty("AdditionalCode");

                        var usingCode = usingCodeProp?.GetValue(scriptSourceObj) as string;
                        var scriptCode = scriptCodeProp?.GetValue(scriptSourceObj) as string;
                        var additionalCode = additionalCodeProp?.GetValue(scriptSourceObj) as string;

                        Debug.WriteLine($"[GhJsonSerializer] VB Script 3 sections extracted: " +
                                      $"Imports={usingCode?.Length ?? 0}, Script={scriptCode?.Length ?? 0}, Additional={additionalCode?.Length ?? 0}");

                        // Return VBScriptCode object with 3 sections
                        var vbCode = new VBScriptCode
                        {
                            Imports = usingCode,
                            Script = scriptCode,
                            Additional = additionalCode
                        };

                        return vbCode;
                    }
                    else
                    {
                        Debug.WriteLine($"[GhJsonSerializer] VB Script ScriptSource is null");
                    }
                }
                else
                {
                    Debug.WriteLine($"[GhJsonSerializer] VB Script ScriptSource property not found");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GhJsonSerializer] Error extracting VB script 3 sections: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Formats a slider value with appropriate precision.
        /// </summary>
        private static string FormatSliderValue(GH_NumberSlider slider)
        {
            var decimals = slider.Slider.DecimalPlaces;

            if (decimals == 0)
                return slider.CurrentValue.ToString("F0", CultureInfo.InvariantCulture);

            return slider.CurrentValue.ToString($"F{decimals}", CultureInfo.InvariantCulture);
        }

        #endregion

        #region Connection Extraction

        /// <summary>
        /// Extracts wire connections between components and stand-alone parameters.
        /// </summary>
        private static List<ConnectionPairing> ExtractConnections(
            List<IGH_ActiveObject> objects,
            GrasshopperDocument document)
        {
            var connections = new List<ConnectionPairing>();

            // Build GUID to ID mapping if using compact IDs
            var guidToId = new Dictionary<Guid, int>();
            if (document.Components != null)
            {
                foreach (var comp in document.Components)
                {
                    if (comp.Id.HasValue)
                    {
                        guidToId[comp.InstanceGuid] = comp.Id.Value;
                    }
                }
            }

            // Extract connections from components
            foreach (var obj in objects.OfType<IGH_Component>())
            {
                for (int outIdx = 0; outIdx < obj.Params.Output.Count; outIdx++)
                {
                    var outputParam = obj.Params.Output[outIdx];
                    foreach (var recipient in outputParam.Recipients)
                    {
                        var targetDocObj = recipient.Attributes?.GetTopLevel?.DocObject;
                        if (targetDocObj != null && guidToId.ContainsKey(targetDocObj.InstanceGuid))
                        {
                            // Find recipient parameter index
                            int? recipientIndex = null;
                            if (targetDocObj is IGH_Component targetComp)
                            {
                                // Target is a component - find input parameter index
                                int idx = targetComp.Params.Input.IndexOf(recipient);
                                recipientIndex = idx >= 0 ? idx : null;
                            }
                            else if (targetDocObj is IGH_Param)
                            {
                                // Target is a standalone parameter - single input at index 0
                                recipientIndex = 0;
                            }

                            var connection = new ConnectionPairing
                            {
                                From = new Connection
                                {
                                    Id = guidToId.ContainsKey(obj.InstanceGuid)
                                        ? guidToId[obj.InstanceGuid]
                                        : -1,
                                    ParamName = outputParam.Name,
                                    ParamIndex = outIdx
                                },
                                To = new Connection
                                {
                                    Id = guidToId[targetDocObj.InstanceGuid],
                                    ParamName = recipient.Name,
                                    ParamIndex = recipientIndex
                                }
                            };

                            connections.Add(connection);
                        }
                    }
                }
            }

            // Extract connections from stand-alone parameters
            foreach (var param in objects.OfType<IGH_Param>())
            {
                // Skip parameters that are part of components (already handled above)
                if (param.Attributes?.Parent != null)
                    continue;

                // Stand-alone parameters can have recipients
                foreach (var recipient in param.Recipients)
                {
                    var targetDocObj = recipient.Attributes?.GetTopLevel?.DocObject;
                    if (targetDocObj != null && guidToId.ContainsKey(targetDocObj.InstanceGuid))
                    {
                        // Find recipient parameter index
                        int? recipientIndex = null;
                        if (targetDocObj is IGH_Component targetComp)
                        {
                            // Target is a component - find input parameter index
                            int idx = targetComp.Params.Input.IndexOf(recipient);
                            recipientIndex = idx >= 0 ? idx : null;
                        }
                        else if (targetDocObj is IGH_Param)
                        {
                            // Target is a stand-alone parameter - single input at index 0
                            recipientIndex = 0;
                        }

                        var connection = new ConnectionPairing
                        {
                            From = new Connection
                            {
                                Id = guidToId.ContainsKey(param.InstanceGuid)
                                    ? guidToId[param.InstanceGuid]
                                    : -1,
                                ParamName = param.Name,
                                ParamIndex = 0  // Stand-alone parameters have single output
                            },
                            To = new Connection
                            {
                                Id = guidToId[targetDocObj.InstanceGuid],
                                ParamName = recipient.Name,
                                ParamIndex = recipientIndex
                            }
                        };

                        connections.Add(connection);
                    }
                }
            }

            return connections.Count > 0 ? connections : null;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Assigns sequential IDs to components for compact representation.
        /// </summary>
        private static void AssignComponentIds(GrasshopperDocument document)
        {
            if (document.Components != null)
            {
                for (int i = 0; i < document.Components.Count; i++)
                {
                    document.Components[i].Id = i;
                }
            }
        }

        /// <summary>
        /// Creates document metadata.
        /// </summary>
        private static DocumentMetadata CreateDocumentMetadata(List<IGH_ActiveObject> objects)
        {
            // Get Grasshopper version from the assembly
            var ghVersion = typeof(Instances).Assembly.GetName().Version?.ToString() ?? "Unknown";

            // Get Rhino version
            var rhinoVersion = Rhino.RhinoApp.Version.ToString();

            // Count components and parameters separately
            var componentCount = objects.OfType<IGH_Component>().Count();
            var parameterCount = objects.OfType<IGH_Param>().Count();

            // Collect unique plugin dependencies from components
            var dependencies = objects
                .Select(obj => obj.GetType().Assembly.GetName().Name)
                .Where(name => !name.StartsWith("Grasshopper") &&
                              !name.StartsWith("RhinoCommon") &&
                              !name.StartsWith("System") &&
                              !name.StartsWith("mscorlib"))
                .Distinct()
                .OrderBy(name => name)
                .ToList();

            return new DocumentMetadata
            {
                Version = "1",
                Created = DateTime.UtcNow.ToString("o"),
                RhinoVersion = rhinoVersion,
                GrasshopperVersion = ghVersion,
                ComponentCount = componentCount,
                ParameterCount = parameterCount,
                Dependencies = dependencies.Count > 0 ? dependencies : null,
                PluginVersion = typeof(GhJsonSerializer).Assembly.GetName().Version?.ToString()
            };
        }

        /// <summary>
        /// Extracts group information from the canvas.
        /// </summary>
        private static void ExtractGroupInformation(GrasshopperDocument document)
        {
            try
            {
                var canvas = Instances.ActiveCanvas;
                if (canvas?.Document == null)
                    return;

                var groups = canvas.Document.Objects.OfType<GH_Group>().ToList();
                if (groups.Count == 0)
                    return;

                // Build GUID to ID mapping
                var guidToId = new Dictionary<Guid, int>();
                if (document.Components != null)
                {
                    foreach (var comp in document.Components)
                    {
                        if (comp.Id.HasValue)
                        {
                            guidToId[comp.InstanceGuid] = comp.Id.Value;
                        }
                    }
                }

                document.Groups = new List<GroupInfo>();

                foreach (var group in groups)
                {
                    var groupInfo = new GroupInfo
                    {
                        InstanceGuid = group.InstanceGuid,
                        Name = group.NickName,
                        Color = DataTypeSerializer.Serialize(group.Colour)
                    };

                    // Map member GUIDs to IDs
                    var memberIds = new List<int>();
                    foreach (var member in group.Objects())
                    {
                        // Current enumeration yields IGH_DocumentObject; get InstanceGuid
                        var objDo = member as IGH_DocumentObject;
                        if (objDo == null)
                            continue;

                        var guid = objDo.InstanceGuid;
                        if (guidToId.TryGetValue(guid, out var id))
                        {
                            memberIds.Add(id);
                        }
                    }

                    groupInfo.Members = memberIds;

                    // Only add groups that have members
                    if (memberIds.Count > 0)
                    {
                        document.Groups.Add(groupInfo);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GhJsonSerializer] Error extracting groups: {ex.Message}");
            }
        }

        #endregion

        #region Runtime Data Extraction

        /// <summary>
        /// Extracts runtime/volatile data from component outputs.
        /// Returns a JObject keyed by component instanceGuid containing output parameter data.
        /// </summary>
        /// <param name="objects">List of Grasshopper objects to extract data from.</param>
        /// <returns>JObject containing runtime data organized by component GUID.</returns>
        public static JObject ExtractRuntimeData(IEnumerable<IGH_ActiveObject> objects)
        {
            var result = new JObject();

            foreach (var obj in objects)
            {
                var componentData = new JObject();

                if (obj is IGH_Component comp)
                {
                    // Extract output parameter data for components
                    var outputsData = new JObject();
                    foreach (var output in comp.Params.Output)
                    {
                        var paramData = ExtractParameterVolatileData(output);
                        if (paramData != null)
                        {
                            outputsData[output.NickName] = paramData;
                        }
                    }

                    if (outputsData.Count > 0)
                    {
                        componentData["outputs"] = outputsData;
                    }

                    // Also extract input data if it has values
                    var inputsData = new JObject();
                    foreach (var input in comp.Params.Input)
                    {
                        var paramData = ExtractParameterVolatileData(input);
                        if (paramData != null)
                        {
                            inputsData[input.NickName] = paramData;
                        }
                    }

                    if (inputsData.Count > 0)
                    {
                        componentData["inputs"] = inputsData;
                    }
                }
                else if (obj is IGH_Param param)
                {
                    // Extract data for standalone parameters
                    var paramData = ExtractParameterVolatileData(param);
                    if (paramData != null)
                    {
                        componentData["data"] = paramData;
                    }
                }

                if (componentData.Count > 0)
                {
                    componentData["name"] = obj.Name;
                    result[obj.InstanceGuid.ToString()] = componentData;
                }
            }

            return result;
        }

        /// <summary>
        /// Extracts volatile data from a single parameter.
        /// Returns count, paths, and sample values in a compact format.
        /// </summary>
        /// <param name="param">The parameter to extract volatile data from.</param>
        /// <returns>JObject with totalCount, branchCount, and branches array; null if empty.</returns>
        public static JObject ExtractParameterVolatileData(IGH_Param param)
        {
            var volatileData = param.VolatileData;
            if (volatileData == null || volatileData.IsEmpty)
            {
                return null;
            }

            var paramData = new JObject
            {
                ["totalCount"] = volatileData.DataCount,
                ["branchCount"] = volatileData.PathCount,
            };

            // Extract path structure with item counts
            var branches = new JArray();
            foreach (var path in volatileData.Paths)
            {
                var branch = volatileData.get_Branch(path);
                var branchInfo = new JObject
                {
                    ["path"] = path.ToString(),
                    ["count"] = branch?.Count ?? 0,
                };

                // Sample first few values for context (limit to avoid huge responses)
                if (branch != null && branch.Count > 0)
                {
                    var samples = new JArray();
                    int sampleCount = Math.Min(branch.Count, 3); // Limit to 3 samples per branch
                    for (int i = 0; i < sampleCount; i++)
                    {
                        var item = branch[i];
                        if (item is IGH_Goo goo)
                        {
                            samples.Add(goo.ToString());
                        }
                        else if (item != null)
                        {
                            samples.Add(item.ToString());
                        }
                    }

                    if (samples.Count > 0)
                    {
                        branchInfo["samples"] = samples;
                    }

                    if (branch.Count > sampleCount)
                    {
                        branchInfo["hasMore"] = true;
                    }
                }

                branches.Add(branchInfo);
            }

            paramData["branches"] = branches;

            return paramData;
        }

        #endregion

        #region Factory Methods

        /// <summary>
        /// Factory methods for common serialization scenarios.
        /// </summary>
        public static class Factory
        {
            /// <summary>
            /// Serializes with Standard format (default).
            /// Balanced, clean structure for AI processing with all essential data.
            /// </summary>
            public static GrasshopperDocument Standard(IEnumerable<IGH_ActiveObject> objects)
            {
                return Serialize(objects, SerializationOptions.Standard);
            }

            /// <summary>
            /// Serializes with Lite format.
            /// Compressed variant optimized for minimal token usage.
            /// </summary>
            public static GrasshopperDocument Lite(IEnumerable<IGH_ActiveObject> objects)
            {
                return Serialize(objects, SerializationOptions.Lite);
            }
        }

        #endregion
    }
}
