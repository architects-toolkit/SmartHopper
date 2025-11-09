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
using System.Diagnostics;
using Grasshopper.Kernel;
using RhinoCodePlatform.GH;
using RhinoCodePluginGH.Parameters;
using SmartHopper.Core.Grasshopper.Serialization.GhJson.Shared;
using SmartHopper.Core.Grasshopper.Utils.Internal;
using SmartHopper.Core.Models.Components;

namespace SmartHopper.Core.Grasshopper.Serialization.GhJson.ScriptComponents
{
    /// <summary>
    /// Handles bidirectional mapping for script component parameters.
    /// Manages variable names, type hints, and C# identifier sanitization.
    /// </summary>
    public static class ScriptParameterMapper
    {
        /// <summary>
        /// Extracts parameter settings from a VB Script component parameter.
        /// VB Script doesn't implement IScriptComponent, so this method doesn't extract type hints.
        /// </summary>
        public static ParameterSettings ExtractVBScriptSettings(IGH_Param param, bool isPrincipal = false)
        {
            if (param == null)
                return null;

            var settings = new ParameterSettings
            {
                ParameterName = param.Name,
                Access = param.Kind == GH_ParamKind.input
                    ? AccessModeMapper.ToString(param.Access)
                    : null,
            };

            bool hasSettings = true;

            // Extract variable name from NickName
            var variableName = param.NickName;
            if (!string.IsNullOrEmpty(variableName) &&
                !string.Equals(settings.ParameterName, variableName, StringComparison.Ordinal))
            {
                settings.VariableName = variableName;
            }

            // Mark as principal if applicable (only valid for inputs)
            if (isPrincipal && param.Kind == GH_ParamKind.input)
            {
                settings.IsPrincipal = true;
            }

            // Extract Required/Optional property (only for inputs)
            if (param.Kind == GH_ParamKind.input)
            {
                try
                {
                    var optionalProp = param.GetType().GetProperty("Optional");
                    if (optionalProp != null && optionalProp.CanRead)
                    {
                        var isOptional = optionalProp.GetValue(param) as bool?;
                        if (isOptional.HasValue && !isOptional.Value)
                        {
                            settings.Required = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ScriptParameterMapper] Error extracting Optional from VB '{param.Name}': {ex.Message}");
                }
            }

            // Extract DataMapping (Flatten, Graft)
            if (param.DataMapping != GH_DataMapping.None)
            {
                settings.DataMapping = param.DataMapping.ToString();
            }

            // Extract additional settings (Reverse, Simplify, etc.)
            var additionalSettings = ExtractAdditionalSettings(param);
            if (additionalSettings != null)
            {
                settings.AdditionalSettings = additionalSettings;
            }

            return hasSettings ? settings : null;
        }

        /// <summary>
        /// Extracts parameter settings from a script component parameter.
        /// </summary>
        /// <param name="param">Script parameter to extract from</param>
        /// <param name="scriptComp">Parent script component</param>
        /// <param name="isPrincipal">Whether this is a principal parameter</param>
        /// <returns>ParameterSettings with script-specific data</returns>
        public static ParameterSettings ExtractSettings(IGH_Param param, IScriptComponent scriptComp, bool isPrincipal = false)
        {
            if (param == null || scriptComp == null)
                return null;

            var settings = new ParameterSettings
            {
                ParameterName = param.Name,

                // Only include Access for inputs; outputs' access is implicit and not serialized
                Access = param.Kind == GH_ParamKind.input
                    ? AccessModeMapper.ToString(param.Access)
                    : null,
            };

            // Always serialize parameters (at minimum with parameterName and access)
            bool hasSettings = true;

            // Extract variable name from NickName (script parameters use NickName as variable name)
            var variableName = param.NickName;
            if (!string.IsNullOrEmpty(variableName))
            {
                // Unsanitize C# identifiers to store original names in JSON
                if (ScriptComponentHelper.IsCSharpScriptComponent(scriptComp))
                {
                    variableName = CSharpIdentifierHelper.UnsanitizeIdentifier(variableName);
                    if (!string.Equals(param.NickName, variableName, StringComparison.Ordinal))
                    {
                        Debug.WriteLine($"[ScriptParameterMapper] Unsanitized '{param.NickName}' -> '{variableName}'");
                    }
                }

                // Only serialize variableName when it differs from parameterName to avoid duplication
                if (!string.Equals(settings.ParameterName, variableName, StringComparison.Ordinal))
                {
                    settings.VariableName = variableName;
                }
                hasSettings = true;
            }

            // Try to extract type hint from script signature or parameter type
            try
            {
                var isInput = param.Kind == GH_ParamKind.input;
                var scriptCode = scriptComp.Text;

                var typeHint = ScriptSignatureParser.ExtractTypeHintFromSignature(
                    scriptCode, variableName, isInput, scriptComp);

                // If not found in signature, infer from parameter's runtime type
                if (string.IsNullOrEmpty(typeHint))
                {
                    typeHint = InferTypeHintFromParameter(param, scriptComp);
                }

                // Only serialize type hint if it's not "object" (case-insensitive)
                // "object" is the default/generic type hint and doesn't need to be serialized
                if (!string.IsNullOrEmpty(typeHint) && 
                    !string.Equals(typeHint, "object", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(typeHint, "Object", StringComparison.OrdinalIgnoreCase))
                {
                    settings.TypeHint = typeHint;
                    hasSettings = true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScriptParameterMapper] Error extracting type hint: {ex.Message}");
            }

            // Mark as principal if applicable (only valid for inputs)
            if (isPrincipal && param.Kind == GH_ParamKind.input)
            {
                settings.IsPrincipal = true;
                hasSettings = true;
            }

            // Extract Required/Optional property (only for inputs)
            if (param.Kind == GH_ParamKind.input)
            {
                try
                {
                    var optionalProp = param.GetType().GetProperty("Optional");
                    if (optionalProp != null && optionalProp.CanRead)
                    {
                        var isOptional = optionalProp.GetValue(param) as bool?;
                        if (isOptional.HasValue && !isOptional.Value)
                        {
                            // Only serialize if parameter is explicitly required (Optional=false)
                            settings.Required = true;
                            hasSettings = true;
                            Debug.WriteLine($"[ScriptParameterMapper] Extracted Required=true for input '{param.Name}' (Optional={isOptional.Value})");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ScriptParameterMapper] Error extracting Optional property from '{param.Name}': {ex.Message}");
                }
            }

            // Extract DataMapping (Flatten, Graft)
            if (param.DataMapping != GH_DataMapping.None)
            {
                settings.DataMapping = param.DataMapping.ToString();
                hasSettings = true;
                Debug.WriteLine($"[ScriptParameterMapper] Extracted DataMapping={param.DataMapping} for '{param.Name}'");
            }

            // Extract additional parameter settings (modifiers: Reverse, Simplify, Locked, etc.)
            var additionalSettings = ExtractAdditionalSettings(param);
            if (additionalSettings != null)
            {
                settings.AdditionalSettings = additionalSettings;
                hasSettings = true;
            }

            return hasSettings ? settings : null;
        }

        /// <summary>
        /// Creates a script parameter from settings and applies it to a script component.
        /// </summary>
        /// <param name="settings">Parameter settings</param>
        /// <param name="defaultName">Default name if not specified</param>
        /// <param name="scriptComp">Parent script component</param>
        /// <returns>Configured ScriptVariableParam</returns>
        public static ScriptVariableParam CreateParameter(
            ParameterSettings settings,
            string defaultName,
            IScriptComponent scriptComp)
        {
            if (settings == null || scriptComp == null)
                return null;

            var variableNameRaw = settings.VariableName ?? settings.ParameterName ?? defaultName;
            var compName = (scriptComp as IGH_DocumentObject)?.Name ?? scriptComp.GetType().Name;

            // Sanitize for C# if needed
            var variableName = variableNameRaw;
            if (ScriptComponentHelper.IsCSharpScriptComponent(scriptComp))
            {
                variableName = CSharpIdentifierHelper.SanitizeIdentifier(variableNameRaw);
                if (!string.Equals(variableNameRaw, variableName, StringComparison.Ordinal))
                {
                    Debug.WriteLine($"[ScriptParameterMapper] Sanitized '{variableNameRaw}' -> '{variableName}' for {compName}");
                }
            }

            var accessMode = AccessModeMapper.FromString(settings.Access);

            // Create parameter with sanitized variable name for code execution
            // but use original name for display (NickName)
            var param = new ScriptVariableParam(variableName)
            {
                Name = variableNameRaw,        // Display name (original, unsanitized)
                NickName = variableNameRaw,    // Display name (original, unsanitized)
                Description = string.Empty,
                Access = accessMode,
            };

            // Try to set VariableName property if it exists
            try
            {
                var vnProp = param.GetType().GetProperty("VariableName");
                if (vnProp != null && vnProp.CanWrite)
                {
                    vnProp.SetValue(param, variableName);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScriptParameterMapper] Error setting VariableName: {ex.Message}");
            }

            // Apply Required/Optional property
            // If Required is not specified in JSON, default to Optional=true (not required)
            try
            {
                var optionalProp = param.GetType().GetProperty("Optional");
                if (optionalProp != null && optionalProp.CanWrite)
                {
                    bool isOptional = settings.Required.HasValue ? !settings.Required.Value : true;
                    optionalProp.SetValue(param, isOptional);
                    Debug.WriteLine($"[ScriptParameterMapper] Set Optional={isOptional} (Required={settings.Required?.ToString() ?? "null"}) for '{variableNameRaw}'");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScriptParameterMapper] Error applying Optional property to '{variableNameRaw}': {ex.Message}");
            }

            // Always enable AllowTreeAccess for script parameters to show Item/List/Tree access menu
            try
            {
                var allowTreeAccessProp = param.GetType().GetProperty("AllowTreeAccess");
                if (allowTreeAccessProp != null && allowTreeAccessProp.CanWrite)
                {
                    allowTreeAccessProp.SetValue(param, true);
                    Debug.WriteLine($"[ScriptParameterMapper] Set AllowTreeAccess=true for '{variableNameRaw}'");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScriptParameterMapper] Error setting AllowTreeAccess: {ex.Message}");
            }

            // Apply type hint if provided - MUST be done before registering parameter
            if (!string.IsNullOrEmpty(settings.TypeHint))
            {
                ApplyTypeHint(param, settings.TypeHint, variableName);
            }

#if DEBUG
            // Debug: Verify type hint was applied
            try
            {
                var typeHintProp = param.GetType().GetProperty("TypeHint");
                if (typeHintProp != null && typeHintProp.CanRead)
                {
                    var appliedHint = typeHintProp.GetValue(param) as string;
                    Debug.WriteLine($"[ScriptParameterMapper] Final TypeHint on '{variableName}': '{appliedHint ?? "null"}' (expected: '{settings.TypeHint ?? "null"}')" );
                }
            }
            catch { }
#endif    

            return param;
        }

        /// <summary>
        /// Applies a type hint to a registered parameter using IScriptParameter.Converter.
        /// PUBLIC method for post-registration type hint application.
        /// </summary>
        public static void ApplyTypeHintToParameter(IGH_Param param, string typeHint, IScriptComponent scriptComp)
        {
            if (param == null || string.IsNullOrEmpty(typeHint))
                return;

            // Skip "object" type hints (case-insensitive) - it's the default and doesn't need to be applied
            if (string.Equals(typeHint, "object", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(typeHint, "Object", StringComparison.OrdinalIgnoreCase))
            {
                Debug.WriteLine($"[ScriptParameterMapper] Skipping default 'object' type hint for '{param.Name}'");
                return;
            }

            // Skip generic type hints (e.g., DataTree<XX>, List<XX>) - extract base type instead
            if (typeHint.Contains("<") && typeHint.Contains(">"))
            {
                string baseType = ExtractBaseType(typeHint);
                if (!string.IsNullOrEmpty(baseType) && !string.Equals(baseType, typeHint, StringComparison.Ordinal))
                {
                    Debug.WriteLine($"[ScriptParameterMapper] Generic type hint '{typeHint}' detected, applying base type '{baseType}' instead");
                    typeHint = baseType;
                }
                else
                {
                    Debug.WriteLine($"[ScriptParameterMapper] Skipping generic type hint '{typeHint}' - no base type to extract");
                    return;
                }
            }

            // Skip "object" type hints (case-insensitive) - it's the default and doesn't need to be applied
            if (string.Equals(typeHint, "object", StringComparison.OrdinalIgnoreCase) || 
                string.Equals(typeHint, "Object", StringComparison.OrdinalIgnoreCase))
            {
                Debug.WriteLine($"[ScriptParameterMapper] Skipping default 'object' type hint for '{param.Name}'");
                return;
            }

            try
            {
                // Try to cast to IScriptParameter (ScriptVariableParam implements this)
                if (param is IScriptParameter scriptParam)
                {
                    // Try to get the converter via TypeHints property
                    var typeHintsProp = param.GetType().GetProperty("TypeHints");
                    if (typeHintsProp != null)
                    {
                        var typeHints = typeHintsProp.GetValue(param);
                        if (typeHints != null)
                        {
                            // Call Select(string name) method to set type hint
                            var selectMethod = typeHints.GetType().GetMethod("Select", new[] { typeof(string) });
                            if (selectMethod != null)
                            {
                                try
                                {
                                    selectMethod.Invoke(typeHints, new object[] { typeHint });
                                    Debug.WriteLine($"[ScriptParameterMapper] ✓ Applied type hint '{typeHint}' to '{param.Name}' via TypeHints.Select()");
                                    return;
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"[ScriptParameterMapper] TypeHints.Select('{typeHint}') failed: {ex.InnerException?.Message ?? ex.Message}");
                                }
                            }
                        }
                    }
                }

                Debug.WriteLine($"[ScriptParameterMapper] Could not apply type hint '{typeHint}' to '{param.Name}' - IScriptParameter not available");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScriptParameterMapper] Error applying type hint to registered parameter: {ex.Message}");
            }
        }

        /// <summary>
        /// Extracts the base type from access wrappers like "List<Curve>" → "Curve" or "DataTree<Object>" → "Object"
        /// </summary>
        private static string ExtractBaseType(string typeHint)
        {
            if (string.IsNullOrEmpty(typeHint))
                return typeHint;

            // Handle generic types like "List<Curve>" or "DataTree<Object>"
            int startBracket = typeHint.IndexOf('<');
            int endBracket = typeHint.LastIndexOf('>');
            
            if (startBracket > 0 && endBracket > startBracket)
            {
                // Extract content between < and >
                string innerType = typeHint.Substring(startBracket + 1, endBracket - startBracket - 1).Trim();
                Debug.WriteLine($"[ScriptParameterMapper] Extracted base type '{innerType}' from '{typeHint}'");
                return innerType;
            }

            return typeHint;
        }

        /// <summary>
        /// Applies a type hint to a script parameter.
        /// </summary>
        private static void ApplyTypeHint(IGH_Param param, string typeHint, string parameterName)
        {
            if (string.IsNullOrEmpty(typeHint))
                return;

            try
            {
                // Try to set TypeHint property
                var typeHintProperty = param.GetType().GetProperty("TypeHint");
                if (typeHintProperty != null && typeHintProperty.CanWrite && typeHintProperty.PropertyType == typeof(string))
                {
                    typeHintProperty.SetValue(param, typeHint);
                    Debug.WriteLine($"[ScriptParameterMapper] Applied type hint '{typeHint}' to '{parameterName}'");
                }
                else
                {
                    // Try alternative: directly set the converter based on type hint
                    var converterProperty = param.GetType().GetProperty("Converter");
                    if (converterProperty != null && converterProperty.CanWrite)
                    {
                        Debug.WriteLine($"[ScriptParameterMapper] Attempting to set converter for type hint '{typeHint}' on '{parameterName}'");

                        // Let TypeHint property handle converter creation if it exists
                        if (typeHintProperty != null && typeHintProperty.CanWrite)
                        {
                            typeHintProperty.SetValue(param, typeHint);
                            Debug.WriteLine($"[ScriptParameterMapper] Set TypeHint property to '{typeHint}' (indirect converter update)");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScriptParameterMapper] Error applying type hint: {ex.Message}");
            }
        }

        /// <summary>
        /// Infers type hint from a parameter's runtime type information.
        /// </summary>
        private static string InferTypeHintFromParameter(IGH_Param param, IScriptComponent scriptComp)
        {
            try
            {
                // Try to access _converter field via reflection (ScriptVariableParam implementation detail)
                var converterField = param.GetType().GetField("_converter", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (converterField != null)
                {
                    var converter = converterField.GetValue(param);
                    if (converter != null)
                    {
                        var targetTypeProperty = converter.GetType().GetProperty("TargetType");
                        if (targetTypeProperty != null)
                        {
                            var targetType = targetTypeProperty.GetValue(converter);
                            if (targetType != null)
                            {
                                var typeProperty = targetType.GetType().GetProperty("Type");
                                if (typeProperty != null)
                                {
                                    var type = typeProperty.GetValue(targetType) as Type;
                                    if (type != null)
                                    {
                                        return TypeHintMapper.FormatTypeHint(type.Name, param.Access);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScriptParameterMapper] Error inferring type hint: {ex.Message}");
            }

            // Fallback to generic types based on access mode
            return TypeHintMapper.FormatTypeHint("object", param.Access);
        }

        /// <summary>
        /// Extracts additional parameter settings (modifiers) from a Grasshopper parameter.
        /// </summary>
        private static AdditionalParameterSettings ExtractAdditionalSettings(IGH_Param param)
        {
            var additionalSettings = new AdditionalParameterSettings();
            bool hasAdditionalSettings = false;

            // Extract Reverse flag (reverses list order)
            if (param.Reverse)
            {
                additionalSettings.Reverse = true;
                hasAdditionalSettings = true;
            }

            // Extract Simplify flag (simplifies data tree paths)
            if (param.Simplify)
            {
                additionalSettings.Simplify = true;
                hasAdditionalSettings = true;
            }

            // Extract Locked flag
            // NOTE: Don't extract locked state if it's inherited from the parent component
            // When a component is locked, Grasshopper automatically locks all its parameters
            // We only want to serialize explicit parameter-level locks, not inherited ones
            if (param.Locked)
            {
                // Check if parent component is locked
                bool isInheritedLock = false;
                if (param.Attributes?.Parent != null)
                {
                    var parentDocObj = param.Attributes.Parent.DocObject;
                    if (parentDocObj is IGH_ActiveObject parentActiveObj && parentActiveObj.Locked)
                    {
                        // This is an inherited lock from the parent component, don't serialize it
                        isInheritedLock = true;
                    }
                }

                if (!isInheritedLock)
                {
                    additionalSettings.Locked = true;
                    hasAdditionalSettings = true;
                }
            }

            // Extract Invert flag for Param_Boolean using reflection
            if (param is global::Grasshopper.Kernel.Parameters.Param_Boolean)
            {
                var invertProp = param.GetType().GetProperty("Invert");
                if (invertProp != null)
                {
                    var invertValue = (bool)invertProp.GetValue(param);
                    if (invertValue)
                    {
                        additionalSettings.Invert = true;
                        hasAdditionalSettings = true;
                    }
                }
            }

            return hasAdditionalSettings ? additionalSettings : null;
        }
    }
}
