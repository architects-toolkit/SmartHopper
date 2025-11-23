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
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Special;
using RhinoCodePlatform.GH;
using SmartHopper.Core.Grasshopper.Serialization.GhJson.ScriptComponents;
using SmartHopper.Core.Grasshopper.Serialization.GhJson.Shared;
using SmartHopper.Core.Grasshopper.Utils.Canvas;
using SmartHopper.Core.Grasshopper.Utils.Serialization;
using SmartHopper.Core.Models.Components;
using SmartHopper.Core.Models.Document;
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
                Options = options,
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

            // Handle VB Script components (don't implement IScriptComponent)
            if (instance is IGH_Component ghComp &&
                (instance.Name.Contains("VB", StringComparison.OrdinalIgnoreCase) ||
                 instance.GetType().Name.Contains("VB", StringComparison.OrdinalIgnoreCase)))
            {
                ApplyVBScriptComponentProperties(ghComp, props, options);
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
        /// Applies properties specific to script components (Python, C#, IronPython).
        /// </summary>
        private static void ApplyScriptComponentProperties(
            IScriptComponent scriptComp,
            ComponentProperties props,
            DeserializationOptions options)
        {
            var ghComp = scriptComp as IGH_Component;
            var docObj = scriptComp as IGH_DocumentObject;

            // Apply nickname
            if (!string.IsNullOrEmpty(props.NickName) && docObj != null)
            {
                docObj.NickName = props.NickName;
            }

            // STEP 1: Set script code FIRST (from componentState["value"])
            // This is important because setting script code may trigger parameter regeneration
            string scriptCode = null;
            if (props.ComponentState?.Value != null)
            {
                scriptCode = props.ComponentState.Value.ToString();
                if (!string.IsNullOrEmpty(scriptCode))
                {
                    // Inject type hints into C# signatures if option enabled
                    if (options.InjectScriptTypeHints)
                    {
                        scriptCode = ScriptSignatureParser.InjectTypeHintsIntoScript(
                            scriptCode,
                            props.InputSettings,
                            props.OutputSettings,
                            scriptComp);
                    }

                    scriptComp.Text = scriptCode;
                    Debug.WriteLine($"[GhJsonDeserializer] Set script code for '{docObj?.Name}' ({scriptCode?.Length ?? 0} chars)");
                }
            }

            // STEP 2: Configure parameters (inputs and outputs)
            // For C# scripts, parameters are generated from the RunScript signature
            // For Python/IronPython, we need to set type hints after script code is set
            if (options.ApplyParameterSettings)
            {
                if (props.InputSettings != null && props.InputSettings.Any() && ghComp != null)
                {
                    Debug.WriteLine($"[GhJsonDeserializer] Clearing {ghComp.Params.Input.Count} default input parameters");
                    ghComp.Params.Input.Clear();

                    for (int i = 0; i < props.InputSettings.Count; i++)
                    {
                        var settings = props.InputSettings[i];
                        var param = ScriptParameterMapper.CreateParameter(settings, "input", scriptComp);
                        if (param != null)
                        {
                            ghComp.Params.RegisterInputParam(param);

                            // Get the actual parameter from the collection (might be different from what we passed)
                            var registeredParam = ghComp.Params.Input[i];

                            // Re-apply type hint to registered parameter (Python/IronPython need this)
                            // ScriptVariableParam uses IScriptParameter.Converter property, not TypeHint
                            if (!string.IsNullOrEmpty(settings.TypeHint))
                            {
                                Debug.WriteLine($"[GhJsonDeserializer] Attempting to re-apply TypeHint '{settings.TypeHint}' to input '{registeredParam.Name}'");
                                ScriptParameterMapper.ApplyTypeHintToParameter(registeredParam, settings.TypeHint, scriptComp);
                            }
                            else
                            {
                                Debug.WriteLine($"[GhJsonDeserializer] No TypeHint to apply for input '{registeredParam.Name}'");
                            }

                            // Apply DataMapping (Flatten, Graft)
                            if (!string.IsNullOrEmpty(settings.DataMapping))
                            {
                                if (Enum.TryParse<GH_DataMapping>(settings.DataMapping, true, out var dataMapping))
                                {
                                    registeredParam.DataMapping = dataMapping;
                                    Debug.WriteLine($"[GhJsonDeserializer] Applied DataMapping={dataMapping} to input parameter '{registeredParam.Name}'");
                                }
                            }

                            // Apply additional settings (reverse, simplify) to the registered parameter
                            if (settings.AdditionalSettings != null)
                            {
                                if (settings.AdditionalSettings.Reverse == true)
                                {
                                    registeredParam.Reverse = true;
                                    Debug.WriteLine($"[GhJsonDeserializer] Applied Reverse to input parameter '{registeredParam.Name}'");
                                }
                                if (settings.AdditionalSettings.Simplify == true)
                                {
                                    registeredParam.Simplify = true;
                                    Debug.WriteLine($"[GhJsonDeserializer] Applied Simplify to input parameter '{registeredParam.Name}'");
                                }
                            }

                            Debug.WriteLine($"[GhJsonDeserializer] Registered input parameter '{registeredParam.Name}'");
                        }
                    }

                    // Set principal (master) input if specified
                    for (int i = 0; i < Math.Min(props.InputSettings.Count, ghComp.Params.Input.Count); i++)
                    {
                        if (props.InputSettings[i]?.IsPrincipal == true)
                        {
                            ghComp.MasterParameterIndex = i;
                            Debug.WriteLine($"[GhJsonDeserializer] Set principal input parameter at index {i}");
                            break;
                        }
                    }
                }

                if (props.OutputSettings != null && props.OutputSettings.Any() && ghComp != null)
                {
                    Debug.WriteLine($"[GhJsonDeserializer] Clearing {ghComp.Params.Output.Count} default output parameters");
                    ghComp.Params.Output.Clear();

                    for (int i = 0; i < props.OutputSettings.Count; i++)
                    {
                        var settings = props.OutputSettings[i];
                        var param = ScriptParameterMapper.CreateParameter(settings, "output", scriptComp);
                        if (param != null)
                        {
                            ghComp.Params.RegisterOutputParam(param);

                            // Get the actual parameter from the collection (might be different from what we passed)
                            var registeredParam = ghComp.Params.Output[i];

                            // Re-apply type hint to registered parameter (Python/IronPython need this)
                            // ScriptVariableParam uses IScriptParameter.Converter property, not TypeHint
                            if (!string.IsNullOrEmpty(settings.TypeHint))
                            {
                                Debug.WriteLine($"[GhJsonDeserializer] Attempting to re-apply TypeHint '{settings.TypeHint}' to output '{registeredParam.Name}'");
                                ScriptParameterMapper.ApplyTypeHintToParameter(registeredParam, settings.TypeHint, scriptComp);
                            }
                            else
                            {
                                Debug.WriteLine($"[GhJsonDeserializer] No TypeHint to apply for output '{registeredParam.Name}'");
                            }

                            // Apply DataMapping (Flatten, Graft)
                            if (!string.IsNullOrEmpty(settings.DataMapping))
                            {
                                if (Enum.TryParse<GH_DataMapping>(settings.DataMapping, true, out var dataMapping))
                                {
                                    registeredParam.DataMapping = dataMapping;
                                    Debug.WriteLine($"[GhJsonDeserializer] Applied DataMapping={dataMapping} to output parameter '{registeredParam.Name}'");
                                }
                            }

                            // Apply additional settings (reverse, simplify) to the registered parameter
                            if (settings.AdditionalSettings != null)
                            {
                                if (settings.AdditionalSettings.Reverse == true)
                                {
                                    registeredParam.Reverse = true;
                                    Debug.WriteLine($"[GhJsonDeserializer] Applied Reverse to output parameter '{registeredParam.Name}'");
                                }

                                if (settings.AdditionalSettings.Simplify == true)
                                {
                                    registeredParam.Simplify = true;
                                    Debug.WriteLine($"[GhJsonDeserializer] Applied Simplify to output parameter '{registeredParam.Name}'");
                                }
                            }

                            Debug.WriteLine($"[GhJsonDeserializer] Registered output parameter '{registeredParam.Name}'");
                        }
                    }
                }
            }

            // STEP 2.5: Apply ShowStandardOutput state AFTER parameters are configured
            // This controls the visibility of the "out" parameter in script components
            if (options.ApplyComponentState && props.ComponentState?.ShowStandardOutput.HasValue == true)
            {
                try
                {
                    var compType = docObj?.GetType();
                    var usingStdOutputProp = compType?.GetProperty("UsingStandardOutputParam");
                    if (usingStdOutputProp != null && usingStdOutputProp.CanWrite)
                    {
                        bool desiredValue = props.ComponentState.ShowStandardOutput.Value;
                        bool currentValue = (bool)usingStdOutputProp.GetValue(docObj);

                        // Force application by toggling if values match
                        if (currentValue == desiredValue)
                        {
                            usingStdOutputProp.SetValue(docObj, !desiredValue);
                            Debug.WriteLine($"[GhJsonDeserializer] Forced toggle UsingStandardOutputParam to {!desiredValue}");

                            if (ghComp is IGH_VariableParameterComponent varParamComp2)
                            {
                                varParamComp2.VariableParameterMaintenance();
                            }
                        }

                        // Set to desired value
                        usingStdOutputProp.SetValue(docObj, desiredValue);
                        Debug.WriteLine($"[GhJsonDeserializer] Set UsingStandardOutputParam = {desiredValue}");

                        // Trigger parameter maintenance to add/remove the "out" parameter
                        if (ghComp is IGH_VariableParameterComponent varParamComp3)
                        {
                            varParamComp3.VariableParameterMaintenance();
                            Debug.WriteLine($"[GhJsonDeserializer] Triggered VariableParameterMaintenance for 'out' parameter");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[GhJsonDeserializer] Error applying UsingStandardOutputParam: {ex.Message}");
                }
            }

            // STEP 3: Apply properties (MarshInputs, MarshOutputs, etc.)
            // NOTE: Properties are stored in SchemaProperties (JSON: "properties"), not Params (JSON: "params")
            Debug.WriteLine($"[GhJsonDeserializer] STEP 3: ApplyProperties={options.ApplyProperties}, SchemaProperties={(props.SchemaProperties != null ? $"{props.SchemaProperties.Count} properties" : "null")}, Params={(props.Params != null ? $"{props.Params.Count} params" : "null")}");
            if (options.ApplyProperties)
            {
                // SchemaProperties (modern format with "properties" key)
                if (props.SchemaProperties != null && props.SchemaProperties.Count > 0)
                {
                    Debug.WriteLine($"[GhJsonDeserializer] Applying {props.SchemaProperties.Count} schema properties");
                    ApplyBasicParams(docObj, props.SchemaProperties);
                }
                else
                {
                    Debug.WriteLine($"[GhJsonDeserializer] No properties to apply");
                }
            }

            // STEP 4: Apply component state (locked, hidden, etc.)
            if (options.ApplyComponentState && props.ComponentState != null && docObj != null)
            {
                ApplyComponentState(docObj, props.ComponentState);
            }

            // STEP 5: Recreate Attributes after parameter manipulation
            // Parameter registration invalidates Attributes, so recreate them
            if (docObj != null)
            {
                docObj.CreateAttributes();
                Debug.WriteLine($"[GhJsonDeserializer] Recreated Attributes for script component '{docObj.Name}'");
            }
        }

        /// <summary>
        /// Applies properties specific to VB Script components.
        /// VB Script doesn't implement IScriptComponent, so needs separate handling.
        /// </summary>
        private static void ApplyVBScriptComponentProperties(
            IGH_Component vbComp,
            ComponentProperties props,
            DeserializationOptions options)
        {
            // Apply nickname
            if (!string.IsNullOrEmpty(props.NickName))
            {
                vbComp.NickName = props.NickName;
            }

            // STEP 1: Set VB script code (3 sections) FIRST
            // Code must be set before parameters so script signature is available
            if (props.ComponentState?.VBCode != null)
            {
                ApplyVBScriptCode(vbComp, props.ComponentState.VBCode);
            }

            // STEP 2: Configure parameters using ScriptVariableParam
            if (options.ApplyParameterSettings)
            {
                ApplyVBScriptParameters(vbComp, props.InputSettings, props.OutputSettings);
            }

            // Apply component state
            if (options.ApplyComponentState && props.ComponentState != null)
            {
                ApplyComponentState(vbComp, props.ComponentState);
            }

            // Recreate Attributes
            vbComp.CreateAttributes();
        }

        /// <summary>
        /// Applies parameter settings to VB Script component by creating ScriptVariableParam parameters.
        /// VB Script uses ScriptVariableParam like other script components.
        /// </summary>
        private static void ApplyVBScriptParameters(
            IGH_Component vbComp,
            List<ParameterSettings> inputSettings,
            List<ParameterSettings> outputSettings)
        {
            // Clear and recreate input parameters
            if (inputSettings != null && inputSettings.Any())
            {
                Debug.WriteLine($"[GhJsonDeserializer] Clearing {vbComp.Params.Input.Count} default VB input parameters");
                vbComp.Params.Input.Clear();

                for (int i = 0; i < inputSettings.Count; i++)
                {
                    var settings = inputSettings[i];
                    var param = CreateVBScriptParameter(settings, "input");
                    if (param != null)
                    {
                        vbComp.Params.RegisterInputParam(param);
                        var registeredParam = vbComp.Params.Input[i];

                        // Apply DataMapping (Flatten, Graft)
                        if (!string.IsNullOrEmpty(settings.DataMapping))
                        {
                            if (Enum.TryParse<GH_DataMapping>(settings.DataMapping, true, out var dataMapping))
                            {
                                registeredParam.DataMapping = dataMapping;
                                Debug.WriteLine($"[GhJsonDeserializer] Applied DataMapping={dataMapping} to VB input '{registeredParam.Name}'");
                            }
                        }

                        // Apply modifiers
                        if (settings.AdditionalSettings != null)
                        {
                            if (settings.AdditionalSettings.Reverse == true)
                            {
                                registeredParam.Reverse = true;
                                Debug.WriteLine($"[GhJsonDeserializer] Applied Reverse to VB input '{registeredParam.Name}'");
                            }
                            if (settings.AdditionalSettings.Simplify == true)
                            {
                                registeredParam.Simplify = true;
                                Debug.WriteLine($"[GhJsonDeserializer] Applied Simplify to VB input '{registeredParam.Name}'");
                            }
                        }

                        Debug.WriteLine($"[GhJsonDeserializer] Registered VB input parameter '{registeredParam.Name}'");
                    }
                }

                // Set principal (master) input if specified
                for (int i = 0; i < Math.Min(inputSettings.Count, vbComp.Params.Input.Count); i++)
                {
                    if (inputSettings[i]?.IsPrincipal == true)
                    {
                        vbComp.MasterParameterIndex = i;
                        Debug.WriteLine($"[GhJsonDeserializer] Set VB principal input parameter at index {i}");
                        break;
                    }
                }
            }

            // Clear and recreate output parameters
            if (outputSettings != null && outputSettings.Any())
            {
                Debug.WriteLine($"[GhJsonDeserializer] Clearing {vbComp.Params.Output.Count} default VB output parameters");
                vbComp.Params.Output.Clear();

                for (int i = 0; i < outputSettings.Count; i++)
                {
                    var settings = outputSettings[i];
                    var param = CreateVBScriptParameter(settings, "output");
                    if (param != null)
                    {
                        vbComp.Params.RegisterOutputParam(param);
                        var registeredParam = vbComp.Params.Output[i];

                        // Apply DataMapping (Flatten, Graft)
                        if (!string.IsNullOrEmpty(settings.DataMapping))
                        {
                            if (Enum.TryParse<GH_DataMapping>(settings.DataMapping, true, out var dataMapping))
                            {
                                registeredParam.DataMapping = dataMapping;
                                Debug.WriteLine($"[GhJsonDeserializer] Applied DataMapping={dataMapping} to VB output '{registeredParam.Name}'");
                            }
                        }

                        // Apply modifiers
                        if (settings.AdditionalSettings != null)
                        {
                            if (settings.AdditionalSettings.Reverse == true)
                            {
                                registeredParam.Reverse = true;
                                Debug.WriteLine($"[GhJsonDeserializer] Applied Reverse to VB output '{registeredParam.Name}'");
                            }
                            if (settings.AdditionalSettings.Simplify == true)
                            {
                                registeredParam.Simplify = true;
                                Debug.WriteLine($"[GhJsonDeserializer] Applied Simplify to VB output '{registeredParam.Name}'");
                            }
                        }

                        Debug.WriteLine($"[GhJsonDeserializer] Registered VB output parameter '{registeredParam.Name}'");
                    }
                }
            }
        }

        /// <summary>
        /// Creates a Param_ScriptVariable for VB Script from settings.
        /// </summary>
        private static IGH_Param CreateVBScriptParameter(ParameterSettings settings, string defaultName)
        {
            if (settings == null)
                return null;

            var variableName = settings.VariableName ?? settings.ParameterName ?? defaultName;
            var accessMode = AccessModeMapper.FromString(settings.Access);

            // VB Script uses Param_ScriptVariable, not ScriptVariableParam
            var param = new Param_ScriptVariable
            {
                Name = variableName,
                NickName = variableName,
                Description = string.Empty,
                Access = accessMode,
            };

            // Apply Required/Optional property
            try
            {
                var optionalProp = param.GetType().GetProperty("Optional");
                if (optionalProp != null && optionalProp.CanWrite)
                {
                    bool isOptional = settings.Required.HasValue ? !settings.Required.Value : true;
                    optionalProp.SetValue(param, isOptional);
                    Debug.WriteLine($"[GhJsonDeserializer] Set Optional={isOptional} for VB parameter '{variableName}'");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GhJsonDeserializer] Error applying Optional to VB '{variableName}': {ex.Message}");
            }

            return param;
        }

        /// <summary>
        /// Applies VB Script code from 3 separate sections (imports, script, additional).
        /// IMPORTANT: Code modification must run on UI thread.
        /// </summary>
        private static void ApplyVBScriptCode(IGH_Component vbComp, VBScriptCode vbCode)
        {
            try
            {
                var compType = vbComp.GetType();
                var scriptSourceProp = compType.GetProperty("ScriptSource");

                if (scriptSourceProp != null && scriptSourceProp.CanRead)
                {
                    var scriptSourceObj = scriptSourceProp.GetValue(vbComp);
                    if (scriptSourceObj != null)
                    {
                        var scriptSourceType = scriptSourceObj.GetType();

                        // Set the 3 code sections
                        var usingCodeProp = scriptSourceType.GetProperty("UsingCode");
                        var scriptCodeProp = scriptSourceType.GetProperty("ScriptCode");
                        var additionalCodeProp = scriptSourceType.GetProperty("AdditionalCode");

                        if (usingCodeProp != null && usingCodeProp.CanWrite && vbCode.Imports != null)
                        {
                            usingCodeProp.SetValue(scriptSourceObj, vbCode.Imports);
                            Debug.WriteLine($"[GhJsonDeserializer] Set VB imports section ({vbCode.Imports.Length} chars)");
                        }

                        if (scriptCodeProp != null && scriptCodeProp.CanWrite && vbCode.Script != null)
                        {
                            scriptCodeProp.SetValue(scriptSourceObj, vbCode.Script);
                            Debug.WriteLine($"[GhJsonDeserializer] Set VB script section ({vbCode.Script.Length} chars)");
                        }

                        if (additionalCodeProp != null && additionalCodeProp.CanWrite && vbCode.Additional != null)
                        {
                            additionalCodeProp.SetValue(scriptSourceObj, vbCode.Additional);
                            Debug.WriteLine($"[GhJsonDeserializer] Set VB additional code section ({vbCode.Additional.Length} chars)");
                        }

                        Debug.WriteLine("[GhJsonDeserializer] Successfully applied VB Script 3 sections");
                    }
                    else
                    {
                        Debug.WriteLine("[GhJsonDeserializer] VB Script ScriptSource is null");
                    }
                }
                else
                {
                    Debug.WriteLine("[GhJsonDeserializer] VB Script ScriptSource property not found");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GhJsonDeserializer] Error in ApplyVBScriptCode: {ex.Message}");
                throw;
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
            Debug.WriteLine($"[GhJsonDeserializer] ApplyBasicParams called for '{instance.Name}' with {basicParams.Count} properties");
            foreach (var kvp in basicParams)
            {
                Debug.WriteLine($"[GhJsonDeserializer]   Attempting to set '{kvp.Key}' = '{kvp.Value}'");
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
                        Debug.WriteLine($"[GhJsonDeserializer]   ✓ Set '{kvp.Key}' = '{value}'");
                    }
                    else
                    {
                        Debug.WriteLine($"[GhJsonDeserializer]   ✗ Property '{kvp.Key}' not found or not writable");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[GhJsonDeserializer]   ✗ Error setting basic param '{kvp.Key}': {ex.Message}");
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
