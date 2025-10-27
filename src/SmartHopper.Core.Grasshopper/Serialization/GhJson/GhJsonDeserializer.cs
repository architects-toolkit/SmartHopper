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

            var result = new DeserializationResult();
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

            // Set instance GUID
            if (instance is IGH_DocumentObject docObj)
            {
                docObj.NewInstanceGuid(props.InstanceGuid);
            }

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
        /// Applies schema properties to a component.
        /// </summary>
        private static void ApplySchemaProperties(
            IGH_DocumentObject instance,
            Dictionary<string, object> properties)
        {
            foreach (var kvp in properties)
            {
                try
                {
                    var prop = instance.GetType().GetProperty(kvp.Key);
                    if (prop != null && prop.CanWrite)
                    {
                        var value = kvp.Value;

                        // Try to deserialize if it's a string with type prefix
                        if (value is string strValue && DataTypeSerializer.TryDeserializeFromPrefix(strValue, out var deserializedValue))
                        {
                            value = deserializedValue;
                        }

                        prop.SetValue(instance, value);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[GhJsonDeserializer] Error setting property '{kvp.Key}': {ex.Message}");
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
        /// Gets whether deserialization was successful.
        /// </summary>
        public bool IsSuccess => Components.Count > 0 && Errors.Count == 0;
    }
}
