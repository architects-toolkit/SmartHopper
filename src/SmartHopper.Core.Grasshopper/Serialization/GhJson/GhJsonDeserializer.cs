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
using Grasshopper.Kernel.Special;
using RhinoCodePlatform.GH;
using RhinoCodePluginGH.Parameters;
using SmartHopper.Core.Grasshopper.Converters;
using SmartHopper.Core.Grasshopper.Serialization.GhJson.ScriptComponents;
using SmartHopper.Core.Grasshopper.Serialization.GhJson.Shared;
using SmartHopper.Core.Grasshopper.Utils.Canvas;
using SmartHopper.Core.Grasshopper.Utils.Internal;
using SmartHopper.Core.Grasshopper.Utils.Serialization;
using SmartHopper.Core.Models.Components;
using SmartHopper.Core.Models.Document;
using SmartHopper.Core.Models.Serialization;
using SmartHopper.Core.Serialization.DataTypes;

namespace SmartHopper.Core.Grasshopper.Serialization.GhJson
{
    /// <summary>
    /// Deserializes GhJSON documents to Grasshopper component instances.
    /// Creates and configures components without placing them on canvas.
    /// </summary>
    public static class GhJsonDeserializer
    {
        /// <summary>
        /// Deserializes a GhJSON document to component instances.
        /// Components are created and configured but NOT placed on canvas.
        /// </summary>
        /// <param name="document">GhJSON document to deserialize</param>
        /// <param name="options">Deserialization options (null uses Standard)</param>
        /// <returns>List of created component instances with GUID mapping</returns>
        public static DeserializationResult Deserialize(
            GrasshopperDocument document,
            DeserializationOptions options = null)
        {
            options ??= DeserializationOptions.Standard;

            // Replace integer IDs with GUIDs if needed
            if (options.ReplaceIntegerIds)
            {
                document = ReplaceIntegerIdsWithGuids(document);
            }

            var result = new DeserializationResult
            {
                Options = options
            };

            var guidMapping = new Dictionary<Guid, IGH_DocumentObject>();

            if (document.Components == null || document.Components.Count == 0)
            {
                return result;
            }

            // Create all components
            foreach (var compProps in document.Components)
            {
                try
                {
                    var instance = CreateComponent(compProps, options);
                    if (instance != null)
                    {
                        result.Components.Add(instance);
                        guidMapping[compProps.InstanceGuid] = instance;
                    }
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Failed to create component '{compProps.Name}': {ex.Message}");
                    Debug.WriteLine($"[GhJsonDeserializer] Error creating component: {ex.Message}");
                }
            }

            // Store GUID mapping for connection/group creation
            result.GuidMapping = guidMapping;
            result.Document = document;

            return result;
        }

        #region Component Creation

        /// <summary>
        /// Creates a single component from properties.
        /// </summary>
        private static IGH_DocumentObject CreateComponent(
            ComponentProperties props,
            DeserializationOptions options)
        {
            // Find component in library
            var instance = InstantiateFromLibrary(props);
            if (instance == null)
            {
                Debug.WriteLine($"[GhJsonDeserializer] Component not found: {props.Library}/{props.Type}/{props.Name}");
                return null;
            }

            // Note: Do NOT set InstanceGuid from JSON - let Grasshopper generate new GUIDs
            // This prevents "An item with the same key has already been added" errors
            // when placing components that already exist in the document

            // Apply configuration based on options
            if (options.ApplyProperties)
            {
                ApplyComponentProperties(instance, props, options);
            }

            return instance;
        }

        /// <summary>
        /// Instantiates a component from library/type/name.
        /// </summary>
        private static IGH_DocumentObject InstantiateFromLibrary(ComponentProperties props)
        {
            try
            {
                // Try to find component by GUID first
                if (props.ComponentGuid != Guid.Empty)
                {
                    var proxyByGuid = ObjectFactory.FindProxy(props.ComponentGuid);
                    if (proxyByGuid != null)
                    {
                        return ObjectFactory.CreateInstance(proxyByGuid);
                    }
                }

                // Fallback to search by name
                var proxy = ObjectFactory.FindProxy(props.Name);
                if (proxy != null)
                {
                    return ObjectFactory.CreateInstance(proxy);
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GhJsonDeserializer] Error instantiating component: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Applies all properties to a component instance.
        /// </summary>
        private static void ApplyComponentProperties(
            IGH_DocumentObject instance,
            ComponentProperties props,
            DeserializationOptions options)
        {
            // Handle script components specially
            if (instance is IScriptComponent scriptComp)
            {
                ApplyScriptComponentProperties(scriptComp, props, options);
                return;
            }

            // Apply nickname
            if (!string.IsNullOrEmpty(props.NickName) && props.NickName != props.Name)
            {
                instance.NickName = props.NickName;
            }

            // Apply schema properties
            if (props.SchemaProperties != null)
            {
                ApplySchemaProperties(instance, props.SchemaProperties);
            }

            // Apply basic params
            if (props.Params != null)
            {
                ApplyBasicParams(instance, props.Params);
            }

            // Apply parameter settings
            if (options.ApplyParameterSettings && instance is IGH_Component component)
            {
                ApplyParameterSettings(component, props.InputSettings, props.OutputSettings);
            }

            // Apply component state (includes universal value)
            if (options.ApplyComponentState && props.ComponentState != null)
            {
                ApplyComponentState(instance, props.ComponentState);
                
                // Apply universal value if present in state
                if (props.ComponentState.Value != null)
                {
                    ApplyUniversalValue(instance, props.ComponentState.Value);
                }
            }
        }

        #endregion

        #region Script Component Handling

        /// <summary>
        /// Applies properties specific to script components.
        /// </summary>
        private static void ApplyScriptComponentProperties(
            IScriptComponent scriptComp,
            ComponentProperties props,
            DeserializationOptions options)
        {
            var docObj = scriptComp as IGH_DocumentObject;

            // Apply nickname
            if (!string.IsNullOrEmpty(props.NickName) && docObj != null)
            {
                docObj.NickName = props.NickName;
            }

            // Clear default parameters before registering custom ones
            // Script components come with default parameters (x, y, out, a) which need to be removed
            if (scriptComp is IGH_Component ghComp)
            {
                if (props.InputSettings != null && props.InputSettings.Any())
                {
                    Debug.WriteLine($"[GhJsonDeserializer] Clearing {ghComp.Params.Input.Count} default input parameters");
                    ghComp.Params.Input.Clear();
                }
                if (props.OutputSettings != null && props.OutputSettings.Any())
                {
                    Debug.WriteLine($"[GhJsonDeserializer] Clearing {ghComp.Params.Output.Count} default output parameters");
                    ghComp.Params.Output.Clear();
                }
            }

            // Register input parameters
            if (props.InputSettings != null)
            {
                foreach (var settings in props.InputSettings)
                {
                    var param = ScriptParameterMapper.CreateParameter(settings, "input", scriptComp);
                    if (param != null)
                    {
                        try
                        {
                            var mi = scriptComp.GetType().GetMethod("RegisterInputParameter");
                            if (mi != null)
                            {
                                mi.Invoke(scriptComp, new object[] { param });
                            }
                            else if (scriptComp is IGH_Component ghc)
                            {
                                ghc.Params.Input.Add(param);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[GhJsonDeserializer] Failed to add input parameter: {ex.Message}");
                        }
                    }
                }
            }

            // Register output parameters
            if (props.OutputSettings != null)
            {
                foreach (var settings in props.OutputSettings)
                {
                    var param = ScriptParameterMapper.CreateParameter(settings, "output", scriptComp);
                    if (param != null)
                    {
                        try
                        {
                            var mi = scriptComp.GetType().GetMethod("RegisterOutputParameter");
                            if (mi != null)
                            {
                                mi.Invoke(scriptComp, new object[] { param });
                            }
                            else if (scriptComp is IGH_Component ghc)
                            {
                                ghc.Params.Output.Add(param);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[GhJsonDeserializer] Failed to add output parameter: {ex.Message}");
                        }
                    }
                }
            }

            // Apply script code with type hints if available
            if (props.SchemaProperties != null && props.SchemaProperties.TryGetValue("ScriptCode", out var codeObj))
            {
                var code = codeObj?.ToString();
                if (!string.IsNullOrEmpty(code))
                {
                    // Inject type hints if option enabled
                    if (options.InjectScriptTypeHints)
                    {
                        code = ScriptSignatureParser.InjectTypeHintsIntoScript(
                            code,
                            props.InputSettings,
                            props.OutputSettings,
                            scriptComp);
                    }

                    scriptComp.Text = code;
                }
            }

            // Apply component state
            if (options.ApplyComponentState && props.ComponentState != null && docObj != null)
            {
                ApplyComponentState(docObj, props.ComponentState);
            }
        }

        #endregion

        #region Property Application

        /// <summary>
        /// Applies schema properties to a component using PropertyManagerV2.
        /// </summary>
        private static void ApplySchemaProperties(
            IGH_DocumentObject instance,
            Dictionary<string, object> properties)
        {
            // Use PropertyManagerV2 for proper property handling (especially PersistentData)
            var propertyManager = PropertyManagerFactory.CreateStandard();

            foreach (var kvp in properties)
            {
                Debug.WriteLine($"[ApplySchemaProperties] Processing property '{kvp.Key}' for {instance.GetType().Name}, value type: {kvp.Value?.GetType().Name}");
                
                try
                {
                    // Convert dictionary value to ComponentProperty
                    // The value should be a JObject with a "value" property
                    ComponentProperty componentProp = null;

                    if (kvp.Value is Newtonsoft.Json.Linq.JObject jobj)
                    {
                        // Deserialize JObject to ComponentProperty
                        componentProp = jobj.ToObject<ComponentProperty>();
                    }
                    else if (kvp.Value is Dictionary<string, object> dict && dict.ContainsKey("value"))
                    {
                        // Handle dictionary format
                        componentProp = new ComponentProperty { Value = dict["value"] };
                    }
                    else
                    {
                        // Fallback: wrap raw value
                        componentProp = new ComponentProperty { Value = kvp.Value };
                    }

                    // Use PropertyManagerV2 to apply the property
                    if (componentProp != null)
                    {
                        propertyManager.ApplyProperty(instance, kvp.Key, componentProp);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[GhJsonDeserializer] Error setting property '{kvp.Key}': {ex.Message}");
                    Debug.WriteLine($"[GhJsonDeserializer] Stack trace: {ex.StackTrace}");
                }
            }
        }

        /// <summary>
        /// Applies basic parameters to a component.
        /// </summary>
        private static void ApplyBasicParams(
            IGH_DocumentObject instance,
            Dictionary<string, object> basicParams)
        {
            foreach (var kvp in basicParams)
            {
                try
                {
                    var prop = instance.GetType().GetProperty(kvp.Key);
                    if (prop != null && prop.CanWrite)
                    {
                        var value = kvp.Value;

                        // Deserialize complex types
                        if (value is string strValue && DataTypeSerializer.TryDeserializeFromPrefix(strValue, out var deserializedValue))
                        {
                            value = deserializedValue;
                        }

                        prop.SetValue(instance, value);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[GhJsonDeserializer] Error setting basic param '{kvp.Key}': {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Applies parameter settings to component inputs/outputs.
        /// </summary>
        private static void ApplyParameterSettings(
            IGH_Component component,
            List<ParameterSettings> inputSettings,
            List<ParameterSettings> outputSettings)
        {
            // Apply input settings
            if (inputSettings != null)
            {
                // Set principal (master) input if specified
                int principalIndex = -1;
                for (int i = 0; i < Math.Min(inputSettings.Count, component.Params.Input.Count); i++)
                {
                    if (inputSettings[i]?.IsPrincipal == true)
                    {
                        principalIndex = i;
                        break;
                    }
                }

                if (principalIndex >= 0)
                {
                    component.MasterParameterIndex = principalIndex;
                }

                for (int i = 0; i < Math.Min(inputSettings.Count, component.Params.Input.Count); i++)
                {
                    ParameterMapper.ApplySettings(component.Params.Input[i], inputSettings[i]);
                }
            }

            // Apply output settings
            if (outputSettings != null)
            {
                for (int i = 0; i < Math.Min(outputSettings.Count, component.Params.Output.Count); i++)
                {
                    ParameterMapper.ApplySettings(component.Params.Output[i], outputSettings[i]);
                }
            }
        }

        /// <summary>
        /// Applies component state (locked, hidden, etc.).
        /// </summary>
        private static void ApplyComponentState(IGH_DocumentObject instance, ComponentState state)
        {
            if (state.Locked.HasValue && instance is IGH_ActiveObject activeObj)
            {
                activeObj.Locked = state.Locked.Value;
            }

            if (state.Hidden.HasValue && instance is IGH_Component component)
            {
                component.Hidden = state.Hidden.Value;
            }
        }

        /// <summary>
        /// Applies universal value to special components (sliders, panels, etc.).
        /// </summary>
        private static void ApplyUniversalValue(
            IGH_DocumentObject instance,
            object universalValue)
        {
            try
            {
                // Number slider
                if (instance is GH_NumberSlider slider && universalValue != null)
                {
                    if (double.TryParse(universalValue.ToString(), out var value))
                    {
                        slider.SetSliderValue((decimal)value);
                    }
                }

                // Panel
                if (instance is GH_Panel panel && universalValue != null)
                {
                    panel.UserText = universalValue.ToString();
                }

                // Boolean toggle
                if (instance is GH_BooleanToggle toggle && universalValue != null)
                {
                    if (bool.TryParse(universalValue.ToString(), out var value))
                    {
                        toggle.Value = value;
                    }
                }

                // Colour swatch
                if (instance is GH_ColourSwatch swatch && universalValue != null)
                {
                    var colorStr = universalValue.ToString();
                    if (DataTypeSerializer.TryDeserializeFromPrefix(colorStr, out var obj) && obj is System.Drawing.Color color)
                    {
                        swatch.SwatchColour = color;
                    }
                }

                // Button object
                if (instance is GH_ButtonObject btn && universalValue != null)
                {
                    try
                    {
                        // Handle both object (dictionary) and legacy string formats
                        string expNormal = "False";
                        string expPressed = "True";

                        if (universalValue is Newtonsoft.Json.Linq.JObject jobj)
                        {
                            expNormal = jobj["normal"]?.ToString() ?? "False";
                            expPressed = jobj["pressed"]?.ToString() ?? "True";
                        }
                        else if (universalValue is System.Collections.Generic.IDictionary<string, object> dict)
                        {
                            if (dict.TryGetValue("normal", out var n)) expNormal = n?.ToString() ?? "False";
                            if (dict.TryGetValue("pressed", out var p)) expPressed = p?.ToString() ?? "True";
                        }

                        btn.ExpressionNormal = expNormal;
                        btn.ExpressionPressed = expPressed;
                        btn.EvaluateExpressions();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[GhJsonDeserializer] Error applying button expressions: {ex.Message}");
                    }
                }

                // Value list
                if (instance is GH_ValueList valueList && universalValue != null)
                {
                    var targetValue = universalValue.ToString();
                    for (int i = 0; i < valueList.ListItems.Count; i++)
                    {
                        if (valueList.ListItems[i].Name == targetValue)
                        {
                            valueList.SelectItem(i);
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GhJsonDeserializer] Error applying universal value: {ex.Message}");
            }
        }

        #endregion

        #region ID Replacement

        /// <summary>
        /// Replaces integer IDs with proper GUIDs throughout the document.
        /// </summary>
        private static GrasshopperDocument ReplaceIntegerIdsWithGuids(GrasshopperDocument document)
        {
            if (document.Components == null)
                return document;

            var idToGuid = new Dictionary<int, Guid>();

            // Replace instance GUIDs that are integers
            foreach (var comp in document.Components)
            {
                if (comp.Id.HasValue)
                {
                    idToGuid[comp.Id.Value] = comp.InstanceGuid;
                }

                // Check if GUID looks like an integer
                if (int.TryParse(comp.InstanceGuid.ToString("N"), out var intId))
                {
                    var newGuid = Guid.NewGuid();
                    idToGuid[intId] = newGuid;
                    comp.InstanceGuid = newGuid;
                }
            }

            return document;
        }

        #endregion

        #region Factory Methods

        /// <summary>
        /// Factory methods for common deserialization scenarios.
        /// </summary>
        public static class Factory
        {
            /// <summary>
            /// Deserializes with Standard options (full configuration).
            /// </summary>
            public static DeserializationResult Standard(GrasshopperDocument document)
            {
                return Deserialize(document, DeserializationOptions.Standard);
            }

            /// <summary>
            /// Deserializes components only (no placement, connections, or groups).
            /// </summary>
            public static DeserializationResult ComponentsOnly(GrasshopperDocument document)
            {
                return Deserialize(document, DeserializationOptions.ComponentsOnly);
            }

            /// <summary>
            /// Deserializes with minimal configuration (structure only).
            /// </summary>
            public static DeserializationResult Minimal(GrasshopperDocument document)
            {
                return Deserialize(document, DeserializationOptions.Minimal);
            }
        }

        #endregion
    }

    /// <summary>
    /// Result of deserialization operation.
    /// </summary>
    public class DeserializationResult
    {
        /// <summary>
        /// Successfully created component instances.
        /// </summary>
        public List<IGH_DocumentObject> Components { get; set; } = new List<IGH_DocumentObject>();

        /// <summary>
        /// Mapping from original GUIDs to created instances.
        /// </summary>
        public Dictionary<Guid, IGH_DocumentObject> GuidMapping { get; set; } = new Dictionary<Guid, IGH_DocumentObject>();

        /// <summary>
        /// Original document (may be modified if IDs were replaced).
        /// </summary>
        public GrasshopperDocument Document { get; set; }

        /// <summary>
        /// Errors encountered during deserialization.
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();

        /// <summary>
        /// Deserialization options used for this operation.
        /// </summary>
        public DeserializationOptions Options { get; set; }

        /// <summary>
        /// Gets whether deserialization was successful.
        /// </summary>
        public bool IsSuccess => Components.Count > 0 && Errors.Count == 0;
    }
}
