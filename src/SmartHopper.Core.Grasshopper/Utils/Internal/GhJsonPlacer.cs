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
#if DEBUG
        /// <summary>
        /// Extracts the RunScript signature from script code (C# or Python) for debugging purposes.
        /// </summary>
        private static string ExtractRunScriptSignature(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;

            try
            {
                // Try C# style: "private void RunScript("...
                var marker = "RunScript(";
                var idx = code.IndexOf(marker, StringComparison.Ordinal);
                if (idx >= 0)
                {
                    // get line start
                    var lineStart = code.LastIndexOf('\n', idx);
                    lineStart = lineStart < 0 ? 0 : lineStart + 1;
                    // get line end
                    var lineEnd = code.IndexOf(')', idx);
                    if (lineEnd > idx)
                    {
                        var snippet = code.Substring(lineStart, Math.Min(code.Length - lineStart, (lineEnd - lineStart) + 1));
                        return snippet.Trim();
                    }
                }

                // Try Python style: def RunScript(
                var pyIdx = code.IndexOf("def RunScript(", StringComparison.Ordinal);
                if (pyIdx >= 0)
                {
                    var lineEnd = code.IndexOf('\n', pyIdx);
                    if (lineEnd < 0) lineEnd = code.Length;
                    var snippet = code.Substring(pyIdx, lineEnd - pyIdx);
                    return snippet.Trim();
                }
            }
            catch { }

            return null;
        }
#endif

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

                if (instance is GH_NumberSlider slider && component.ComponentState?.Value != null)
                {
                    try
                    {
                        var valueStr = component.ComponentState.Value.ToString();
                        slider.SetInitCode(valueStr);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error setting slider value: {ex.Message}");
                    }
                }
                else if (instance is IScriptComponent scriptComp && component.ComponentState?.Value != null)
                {
                    // clear default script inputs and outputs
                    var ghComp = (IGH_Component)scriptComp;

                    // remove all existing inputs
                    foreach (var p in ghComp.Params.Input.ToArray())
                        ghComp.Params.UnregisterInputParameter(p);

                    // remove all existing outputs
                    foreach (var p in ghComp.Params.Output.ToArray())
                        ghComp.Params.UnregisterOutputParameter(p);

                    // CRITICAL: Create parameters BEFORE setting script code
                    // VariableParameterMaintenance() regenerates script code based on registered parameters,
                    // so we must register parameters first to prevent parameter declarations from being stripped

                    // Create input parameters from inputSettings
                    if (component.InputSettings != null)
                    {
                        Debug.WriteLine($"[GhJsonPlacer] Creating {component.InputSettings.Count} input parameters");
                        foreach (var inputSetting in component.InputSettings)
                        {
                            var param = CreateScriptParameter(inputSetting, "input", scriptComp);
                            ghComp.Params.RegisterInputParam(param);
                        }
                    }

                    // Create output parameters from outputSettings
                    if (component.OutputSettings != null)
                    {
                        Debug.WriteLine($"[GhJsonPlacer] Creating {component.OutputSettings.Count} output parameters");
                        foreach (var outputSetting in component.OutputSettings)
                        {
                            var param = CreateScriptParameter(outputSetting, "output", scriptComp);
                            ghComp.Params.RegisterOutputParam(param);
                        }
                    }

                    // NOW set script code - VariableParameterMaintenance will maintain parameter declarations
                    var scriptCode = component.ComponentState.Value.ToString();
                    scriptComp.Text = scriptCode;
                    Debug.WriteLine($"[GhJsonPlacer] Set script code, length: {scriptCode.Length}");

                    // Call VariableParameterMaintenance() to sync parameters with script
                    ((dynamic)scriptComp).VariableParameterMaintenance();
                    Debug.WriteLine($"[GhJsonPlacer] Called VariableParameterMaintenance(), component now has {ghComp.Params.Input.Count} inputs and {ghComp.Params.Output.Count} outputs");

                    // Inject type hints into the RunScript signature
                    var modifiedScript = InjectTypeHintsIntoScript(scriptComp.Text, component.InputSettings, component.OutputSettings, scriptComp);
                    if (modifiedScript != scriptComp.Text)
                    {
                        scriptComp.Text = modifiedScript;
                        Debug.WriteLine($"[GhJsonPlacer] Injected type hints into script signature");
                    }

#if DEBUG
                    // Inspect the final RunScript signature for debugging
                    var signature = ExtractRunScriptSignature(scriptComp.Text);
                    if (!string.IsNullOrEmpty(signature))
                    {
                        Debug.WriteLine($"[GhJsonPlacer] Final RunScript signature: {signature}");
                    }
#endif

                    // Apply script-specific properties (marshInputs/marshOutputs) from componentState
                    if (component.ComponentState != null)
                    {
                        if (component.ComponentState.MarshInputs.HasValue)
                        {
                            scriptComp.MarshInputs = component.ComponentState.MarshInputs.Value;
                            Debug.WriteLine($"[GhJsonPlacer] Applied MarshInputs: {component.ComponentState.MarshInputs.Value}");
                        }
                        
                        if (component.ComponentState.MarshOutputs.HasValue)
                        {
                            scriptComp.MarshOutputs = component.ComponentState.MarshOutputs.Value;
                            Debug.WriteLine($"[GhJsonPlacer] Applied MarshOutputs: {component.ComponentState.MarshOutputs.Value}");
                        }
                        
                        if (component.ComponentState.Hidden.HasValue)
                        {
                            ghComp.Hidden = component.ComponentState.Hidden.Value;
                            Debug.WriteLine($"[GhJsonPlacer] Applied Hidden: {component.ComponentState.Hidden.Value}");
                        }
                    }

#if DEBUG

                    // DEBUG: List all created parameters with Name and NickName
                    Debug.WriteLine($"[GhJsonPlacer] === Created Input Parameters ===");
                    foreach (var p in ghComp.Params.Input)
                    {
                        var thProp = p.GetType().GetProperty("TypeHint");
                        var thVal = thProp != null ? thProp.GetValue(p) : null;
                        Debug.WriteLine($"[GhJsonPlacer]   Input: Name='{p.Name}', NickName='{p.NickName}', Type={p.GetType().Name}, TypeHintProp={(thProp!=null)}, TypeHint='{thVal ?? "<null>"}'");
                    }
                    Debug.WriteLine($"[GhJsonPlacer] === Created Output Parameters ===");
                    foreach (var p in ghComp.Params.Output)
                    {
                        var thProp = p.GetType().GetProperty("TypeHint");
                        var thVal = thProp != null ? thProp.GetValue(p) : null;
                        Debug.WriteLine($"[GhJsonPlacer]   Output: Name='{p.Name}', NickName='{p.NickName}', Type={p.GetType().Name}, TypeHintProp={(thProp!=null)}, TypeHint='{thVal ?? "<null>"}'");
                    }
#endif

                    // Now apply settings to the existing parameters created by VariableParameterMaintenance()
                    // Inputs from inputSettings
                    if (component.InputSettings != null)
                    {
                        Debug.WriteLine($"[GhJsonPlacer] Processing {component.InputSettings.Count} input settings for script component");
                        foreach (var inputSetting in component.InputSettings)
                        {
                            // For script parameters, VariableName is the actual variable name in the script
                            // If not set, fall back to ParameterName
                            var variableName = inputSetting.VariableName ?? inputSetting.ParameterName ?? string.Empty;
                            
                            var isCSharp = IsCSharpScriptComponent(scriptComp);
                            var searchName = isCSharp ? SanitizeIdentifierForCSharp(variableName) : variableName;
                            if (!string.Equals(variableName, searchName, StringComparison.Ordinal))
                            {
                                Debug.WriteLine($"[GhJsonPlacer] Input search variable sanitized: '{variableName}' -> '{searchName}'");
                            }
                            Debug.WriteLine($"[GhJsonPlacer] Input: ParameterName='{inputSetting.ParameterName}', VariableName='{inputSetting.VariableName}', Using variableName='{variableName}', SearchName='{searchName}'");

                            if (string.IsNullOrEmpty(variableName))
                            {
                                Debug.WriteLine($"[GhJsonPlacer] Skipping input with empty variable name");
                                continue;
                            }

                            // Find the parameter created by VariableParameterMaintenance()
                            // Try both Name and NickName since script parameters might use either
                            var param = ParameterAccess.GetInputByName((IGH_Component)scriptComp, searchName);
                            if (param == null)
                            {
                                // Try searching by NickName instead
                                param = ghComp.Params.Input.FirstOrDefault(p => p.NickName == searchName);
                                if (param != null)
                                {
                                    Debug.WriteLine($"[GhJsonPlacer] Found input '{searchName}' by NickName (Name='{param.Name}')");
                                }
                            }
                            if (param != null)
                            {
                                // Apply settings to existing parameter
                                if (inputSetting.AdditionalSettings?.Simplify == true)
                                    param.Simplify = true;
                                if (inputSetting.AdditionalSettings?.Reverse == true)
                                    param.Reverse = true;
                                if (inputSetting.DataMapping != null)
                                    param.DataMapping = StringConverter.StringToGHDataMapping(inputSetting.DataMapping);
                                
                                Debug.WriteLine($"[GhJsonPlacer] ✓ Applied settings to input '{searchName}'");
                            }
#if DEBUG
                            else
                            {
                                Debug.WriteLine($"[GhJsonPlacer] ✗ Input parameter '{searchName}' not found in script");
                            }
#endif
                        }
                    }

                    // Outputs
                    if (component.OutputSettings != null)
                    {
                        Debug.WriteLine($"[GhJsonPlacer] Processing {component.OutputSettings.Count} output settings for script component");
                        foreach (var outputSetting in component.OutputSettings)
                        {
                            // For script parameters, VariableName is the actual variable name in the script
                            // If not set, fall back to ParameterName
                            var variableName = outputSetting.VariableName ?? outputSetting.ParameterName ?? string.Empty;
                            
                            var isCSharp = IsCSharpScriptComponent(scriptComp);
                            var searchName = isCSharp ? SanitizeIdentifierForCSharp(variableName) : variableName;
                            if (!string.Equals(variableName, searchName, StringComparison.Ordinal))
                            {
                                Debug.WriteLine($"[GhJsonPlacer] Output search variable sanitized: '{variableName}' -> '{searchName}'");
                            }
                            Debug.WriteLine($"[GhJsonPlacer] Output: ParameterName='{outputSetting.ParameterName}', VariableName='{outputSetting.VariableName}', Using variableName='{variableName}', SearchName='{searchName}'");

                            if (string.IsNullOrEmpty(variableName))
                            {
                                Debug.WriteLine($"[GhJsonPlacer] Skipping output with empty variable name");
                                continue;
                            }

                            // Find the parameter created by VariableParameterMaintenance()
                            // Try both Name and NickName since script parameters might use either
                            var param = ParameterAccess.GetOutputByName((IGH_Component)scriptComp, searchName);
                            if (param == null)
                            {
                                // Try searching by NickName instead
                                param = ghComp.Params.Output.FirstOrDefault(p => p.NickName == searchName);
                                if (param != null)
                                {
                                    Debug.WriteLine($"[GhJsonPlacer] Found output '{searchName}' by NickName (Name='{param.Name}')");
                                }
                            }
                            if (param != null)
                            {
                                // Apply settings to existing parameter
                                if (outputSetting.AdditionalSettings?.Simplify == true)
                                    param.Simplify = true;
                                if (outputSetting.AdditionalSettings?.Reverse == true)
                                    param.Reverse = true;
                                if (outputSetting.DataMapping != null)
                                    param.DataMapping = StringConverter.StringToGHDataMapping(outputSetting.DataMapping);
                                
                                Debug.WriteLine($"[GhJsonPlacer] ✓ Applied settings to output '{searchName}'");
                            }
#if DEBUG
                            else
                            {
                                Debug.WriteLine($"[GhJsonPlacer] ✗ Output parameter '{searchName}' not found in script");
                            }
#endif
                        }
                    }
                }

                // Apply schema properties (params, inputSettings, outputSettings, componentState)
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
        /// Creates a script parameter with access mode and type hint from parameter settings.
        /// </summary>
        /// <param name="settings">Parameter settings containing variable name, access mode, and type hint.</param>
        /// <param name="defaultName">Default name to use if variable name is not specified.</param>
        /// <returns>Configured ScriptVariableParam ready to be registered.</returns>
        private static ScriptVariableParam CreateScriptParameter(SmartHopper.Core.Models.Components.ParameterSettings settings, string defaultName, IScriptComponent scriptComp)
        {
            var variableNameRaw = settings.VariableName ?? settings.ParameterName ?? defaultName;
            var compName = (scriptComp as IGH_DocumentObject)?.Name ?? scriptComp.GetType().Name;
            var isCSharp = IsCSharpScriptComponent(scriptComp);
            var variableName = isCSharp ? SanitizeIdentifierForCSharp(variableNameRaw) : variableNameRaw;
            if (!string.Equals(variableNameRaw, variableName, StringComparison.Ordinal))
            {
                Debug.WriteLine($"[GhJsonPlacer] Sanitized variable name for '{compName}': '{variableNameRaw}' -> '{variableName}'");
            }
            var accessMode = ParseAccessMode(settings.Access);

            var param = new ScriptVariableParam(variableName)
            {
                Name = variableName,
                NickName = variableName,
                Description = string.Empty,
                Access = accessMode
            };

            // Try to set VariableName property if it exists
            try
            {
                var vnProp = param.GetType().GetProperty("VariableName");
                if (vnProp != null && vnProp.CanWrite)
                {
                    vnProp.SetValue(param, variableName);
                    Debug.WriteLine($"[GhJsonPlacer] Set VariableName property to '{variableName}'");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GhJsonPlacer] Error setting VariableName property: {ex.Message}");
            }

            ApplyTypeHint(param, settings.TypeHint, variableName);
            param.CreateAttributes();
            
            Debug.WriteLine($"[GhJsonPlacer] Created parameter: '{variableName}' (Access: {accessMode}, TypeHint: {settings.TypeHint ?? "none"}, Script='{compName}')");

            return param;
        }

        /// <summary>
        /// Parses access mode string to GH_ParamAccess enum.
        /// </summary>
        /// <param name="accessString">Access mode string ("item", "list", "tree").</param>
        /// <returns>Parsed GH_ParamAccess value, defaults to item if parsing fails.</returns>
        private static GH_ParamAccess ParseAccessMode(string accessString)
        {
            if (!string.IsNullOrEmpty(accessString) && 
                Enum.TryParse<GH_ParamAccess>(accessString, true, out var parsedAccess))
            {
                return parsedAccess;
            }
            return GH_ParamAccess.item;
        }

        /// <summary>
        /// Applies type hint to a script parameter using reflection.
        /// </summary>
        /// <param name="param">The parameter to apply type hint to.</param>
        /// <param name="typeHint">Type hint string (e.g., "DataTree", "List<Curve>").</param>
        /// <param name="parameterName">Parameter name for logging.</param>
        private static void ApplyTypeHint(IGH_Param param, string typeHint, string parameterName)
        {
            if (string.IsNullOrEmpty(typeHint))
                return;

            try
            {
                var typeHintProperty = param.GetType().GetProperty("TypeHint");
                if (typeHintProperty != null && typeHintProperty.CanWrite)
                {
                    var propType = typeHintProperty.PropertyType;
                    if (propType == typeof(string))
                    {
                        typeHintProperty.SetValue(param, typeHint);
                        Debug.WriteLine($"[GhJsonPlacer] Applied string type hint '{typeHint}' to parameter '{parameterName}'");
                    }
                    else if (propType.IsEnum)
                    {
                        try
                        {
                            var enumVal = Enum.Parse(propType, typeHint, ignoreCase: true);
                            typeHintProperty.SetValue(param, enumVal);
                            Debug.WriteLine($"[GhJsonPlacer] Applied enum type hint '{typeHint}' to parameter '{parameterName}'");
                        }
                        catch (Exception parseEx)
                        {
                            var names = string.Join(",", Enum.GetNames(propType));
                            Debug.WriteLine($"[GhJsonPlacer] Failed to parse enum type hint '{typeHint}' for '{parameterName}'. Valid: [{names}]. Error: {parseEx.Message}");
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"[GhJsonPlacer] TypeHint property type is not string/enum: {propType.FullName}. Attempting direct set...");
                        try
                        {
                            typeHintProperty.SetValue(param, typeHint);
                            Debug.WriteLine($"[GhJsonPlacer] Direct-set type hint succeeded for '{parameterName}'");
                        }
                        catch (Exception directEx)
                        {
                            Debug.WriteLine($"[GhJsonPlacer] Direct-set type hint FAILED for '{parameterName}': {directEx.Message}");
                        }
                    }
                }
                else
                {
                    var propType = typeHintProperty?.PropertyType?.FullName ?? "<null>";
                    Debug.WriteLine($"[GhJsonPlacer] Cannot apply type hint to '{parameterName}': TypeHint property present={typeHintProperty != null}, writable={typeHintProperty?.CanWrite == true}, propertyType={propType}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GhJsonPlacer] Error applying type hint to parameter '{parameterName}': {ex.Message}");
            }
        }

        /// <summary>
        /// Detects whether an IScriptComponent is a C# script component.
        /// </summary>
        private static bool IsCSharpScriptComponent(IScriptComponent scriptComp)
        {
            try
            {
                var name = (scriptComp as IGH_DocumentObject)?.Name ?? scriptComp.GetType().Name;
                if (!string.IsNullOrEmpty(name))
                {
                    if (name.IndexOf("C#", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("CSharp", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Sanitizes a C# identifier by escaping reserved words or invalid identifiers with '@',
        /// and replacing invalid characters with underscores.
        /// Minimal set covers observed issues (e.g., 'out').
        /// </summary>
        private static string SanitizeIdentifierForCSharp(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier)) return identifier;

            // Replace spaces and invalid chars with '_'
            var sanitized = new string(identifier.Select(ch =>
                char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_').ToArray());

            // If starts with digit, prefix underscore
            if (char.IsDigit(sanitized[0]))
                sanitized = "_" + sanitized;

            // Minimal reserved words set
            var reserved = new HashSet<string>(StringComparer.Ordinal)
            {
                "out", "ref", "params", "class", "namespace", "object", "string", "int", "float", "double",
                "public", "private", "protected", "internal", "static", "void", "var", "new"
            };

            if (reserved.Contains(sanitized))
                sanitized = "@" + sanitized;

            return sanitized;
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
                // Skip script components - they're handled inline before this method is called
                if (instance is IScriptComponent)
                {
                    return;
                }

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
                    // Number Slider: value format "5<2,10.000>" (decimal places encoded in the value with most decimals)
                    var valueStr = state.Value.ToString();
                    
                    // Parse the format: "currentValue<min,max>"
                    var parts = valueStr.Split('<');
                    if (parts.Length == 2)
                    {
                        if (decimal.TryParse(parts[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var currentValue))
                        {
                            var rangeParts = parts[1].TrimEnd('>').Split(',');
                            if (rangeParts.Length == 2 &&
                                decimal.TryParse(rangeParts[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var min) &&
                                decimal.TryParse(rangeParts[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var max))
                            {
                                // Detect decimal precision from whichever value has the most decimal places
                                int decimals = 0;
                                foreach (var valueString in new[] { parts[0], rangeParts[0], rangeParts[1].TrimEnd('>') })
                                {
                                    if (valueString.Contains("."))
                                {
                                        var decimalPart = valueString.Split('.')[1];
                                        decimals = Math.Max(decimals, decimalPart.Length);
                                    }
                                }

                                // Set slider properties directly
                                slider.Slider.Minimum = min;
                                slider.Slider.Maximum = max;
                                slider.Slider.Value = currentValue;
                                slider.Slider.DecimalPlaces = decimals;

                                // Apply rounding mode if specified
                                if (!string.IsNullOrEmpty(state.Rounding))
                                {
                                    if (Enum.TryParse<global::Grasshopper.GUI.Base.GH_SliderAccuracy>(state.Rounding, out var roundingMode))
                                    {
                                        slider.Slider.Type = roundingMode;
                                    }
                                }

                                Debug.WriteLine($"Set slider value: {currentValue} (range: {min} to {max}, decimals: {decimals}, rounding: {state.Rounding})");
                            }
                        }
                    }
                }
                else if (instance is GH_Panel panel && state.Value != null)
                {
                    // Panel: value is userText
                    panel.UserText = state.Value.ToString();

                    // Apply panel-specific properties
                    if (state.Multiline.HasValue)
                    {
                        panel.Properties.Multiline = state.Multiline.Value;
                    }

                    if (state.DrawIndices.HasValue)
                    {
                        panel.Properties.DrawIndices = state.DrawIndices.Value;
                    }

                    if (state.DrawPaths.HasValue)
                    {
                        panel.Properties.DrawPaths = state.DrawPaths.Value;
                    }

                    if (state.Alignment.HasValue)
                    {
                        panel.Properties.Alignment = (GH_Panel.Alignment)state.Alignment.Value;
                    }

                    if (state.Wrap.HasValue)
                    {
                        panel.Properties.Wrap = state.Wrap.Value;
                    }

                    // Apply panel bounds (size)
                    if (state.Bounds != null && state.Bounds.ContainsKey("width") && state.Bounds.ContainsKey("height"))
                    {
                        var width = state.Bounds["width"];
                        var height = state.Bounds["height"];
                        
                        // Set the panel size by adjusting its attributes bounds
                        if (panel.Attributes != null)
                        {
                            var currentBounds = panel.Attributes.Bounds;
                            panel.Attributes.Bounds = new System.Drawing.RectangleF(
                                currentBounds.X,
                                currentBounds.Y,
                                width,
                                height
                            );
                        }
                    }
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
                else if (instance is GH_ValueList valueList && state.Value != null)
                {
                    // Value List: value is array of items
                    if (state.Value is JArray itemsArray)
                    {
                        valueList.ListItems.Clear();
                        foreach (var item in itemsArray)
                        {
                            var name = item["Name"]?.ToString() ?? string.Empty;
                            var expression = item["Expression"]?.ToString() ?? string.Empty;
                            valueList.ListItems.Add(new GH_ValueListItem(name, expression));
                        }
                    }
                    
                    // Apply list mode if specified
                    if (!string.IsNullOrEmpty(state.ListMode))
                    {
                        if (Enum.TryParse<GH_ValueListMode>(state.ListMode, out var mode))
                        {
                            valueList.ListMode = mode;
                        }
                    }

                    // Restore selected items
                    if (state.SelectedIndices != null && state.SelectedIndices.Count > 0)
                    {
                        // First, deselect all items
                        foreach (var item in valueList.ListItems)
                        {
                            item.Selected = false;
                        }

                        // Then select the specified indices
                        foreach (var index in state.SelectedIndices)
                        {
                            if (index >= 0 && index < valueList.ListItems.Count)
                            {
                                valueList.ListItems[index].Selected = true;
                            }
                        }

                        Debug.WriteLine($"Restored {state.SelectedIndices.Count} selected items in value list");
                    }
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

        /// <summary>
        /// Injects type hints from parameter settings into the RunScript method signature.
        /// </summary>
        private static string InjectTypeHintsIntoScript(
            string scriptCode,
            List<SmartHopper.Core.Models.Components.ParameterSettings> inputSettings,
            List<SmartHopper.Core.Models.Components.ParameterSettings> outputSettings,
            IScriptComponent scriptComp)
        {
            if (string.IsNullOrEmpty(scriptCode))
                return scriptCode;

            try
            {
                var isCSharp = IsCSharpScriptComponent(scriptComp);
                if (!isCSharp)
                {
                    // Python scripts don't use type hints in Grasshopper
                    return scriptCode;
                }

                // Find the RunScript signature
                var match = System.Text.RegularExpressions.Regex.Match(
                    scriptCode,
                    @"(private\s+void\s+RunScript\s*\()(.*?)(\))",
                    System.Text.RegularExpressions.RegexOptions.Singleline);

                if (!match.Success)
                    return scriptCode;

                var prefix = match.Groups[1].Value;
                var parametersStr = match.Groups[2].Value;
                var suffix = match.Groups[3].Value;

                // Build new parameter list with type hints
                var newParameters = BuildParameterListWithTypeHints(parametersStr, inputSettings, outputSettings);

                // Replace the signature
                var newSignature = prefix + newParameters + suffix;
                var result = scriptCode.Substring(0, match.Index) + newSignature + scriptCode.Substring(match.Index + match.Length);

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[InjectTypeHintsIntoScript] Error: {ex.Message}");
                return scriptCode;
            }
        }

        /// <summary>
        /// Builds parameter list with type hints from settings.
        /// </summary>
        private static string BuildParameterListWithTypeHints(
            string originalParameters,
            List<SmartHopper.Core.Models.Components.ParameterSettings> inputSettings,
            List<SmartHopper.Core.Models.Components.ParameterSettings> outputSettings)
        {
            var parameters = SplitParametersByComma(originalParameters);
            var result = new List<string>();

            foreach (var param in parameters)
            {
                var trimmed = param.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;

                // Parse: "Type varName" or "ref Type varName"
                var isRef = trimmed.StartsWith("ref ");
                var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                
                if (parts.Length < 2)
                {
                    result.Add(param);
                    continue;
                }

                var varName = parts[parts.Length - 1].Trim();
                var cleanVarName = varName.TrimStart('@');

                // Find type hint from settings
                string typeHint = null;
                if (isRef && outputSettings != null)
                {
                    Debug.WriteLine($"[BuildParameterListWithTypeHints] Looking for output parameter '{cleanVarName}' (original: '{varName}')");
                    var setting = outputSettings.FirstOrDefault(s =>
                        (s.VariableName ?? s.ParameterName)?.TrimStart('@').Equals(cleanVarName, StringComparison.Ordinal) == true);
                    typeHint = setting?.TypeHint;
                    if (setting != null)
                    {
                        Debug.WriteLine($"[BuildParameterListWithTypeHints] Found setting: VariableName='{setting.VariableName}', ParameterName='{setting.ParameterName}', TypeHint='{setting.TypeHint}'");
                    }
                    else
                    {
                        Debug.WriteLine($"[BuildParameterListWithTypeHints] No setting found for output '{cleanVarName}'");
                    }
                }
                else if (!isRef && inputSettings != null)
                {
                    var setting = inputSettings.FirstOrDefault(s =>
                        (s.VariableName ?? s.ParameterName)?.TrimStart('@').Equals(cleanVarName, StringComparison.Ordinal) == true);
                    typeHint = setting?.TypeHint;
                }

                // Build new parameter string
                if (!string.IsNullOrEmpty(typeHint))
                {
                    var newParam = isRef ? $"ref {typeHint} {varName}" : $"{typeHint} {varName}";
                    result.Add(newParam);
                    Debug.WriteLine($"[BuildParameterListWithTypeHints] Injected type '{typeHint}' for parameter '{varName}'");
                }
                else
                {
                    result.Add(param);
                }
            }

            return string.Join(",\n\t\t", result);
        }

        /// <summary>
        /// Splits parameters by comma, respecting nested generics.
        /// </summary>
        private static List<string> SplitParametersByComma(string parametersStr)
        {
            var result = new List<string>();
            var current = new System.Text.StringBuilder();
            int depth = 0;

            foreach (char c in parametersStr)
            {
                if (c == '<')
                {
                    depth++;
                    current.Append(c);
                }
                else if (c == '>')
                {
                    depth--;
                    current.Append(c);
                }
                else if (c == ',' && depth == 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            if (current.Length > 0)
                result.Add(current.ToString());

            return result;
        }
    }
}
