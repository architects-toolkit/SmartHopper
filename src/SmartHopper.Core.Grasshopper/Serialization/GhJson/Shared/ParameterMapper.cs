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
using SmartHopper.Core.Models.Components;

namespace SmartHopper.Core.Grasshopper.Serialization.GhJson.Shared
{
    /// <summary>
    /// Handles bidirectional mapping between IGH_Param and ParameterSettings.
    /// Provides consistent parameter conversion for non-script components.
    /// </summary>
    public static class ParameterMapper
    {
        /// <summary>
        /// Extracts parameter settings from a Grasshopper parameter.
        /// </summary>
        /// <param name="param">The parameter to extract from</param>
        /// <param name="isPrincipal">Whether this parameter is the principal/master input parameter (only valid for inputs)</param>
        /// <returns>Parameter settings or null if no custom settings exist</returns>
        public static ParameterSettings ExtractSettings(IGH_Param param, bool isPrincipal = false)
        {
            if (param == null)
                return null;

            var settings = new ParameterSettings
            {
                ParameterName = param.Name
            };

            bool hasSettings = false;

            // Extract NickName if different from Name
            if (!string.IsNullOrEmpty(param.NickName) &&
                !string.Equals(param.Name, param.NickName, StringComparison.Ordinal))
            {
                settings.NickName = param.NickName;
                hasSettings = true;
            }

            // Description is implicit from component definition - not serialized

            // Mark as principal if applicable (only valid for inputs)
            if (isPrincipal && param.Kind == GH_ParamKind.input)
            {
                settings.IsPrincipal = true;
                hasSettings = true;
            }

            // Extract DataMapping (None, Flatten, Graft)
            if (param.DataMapping != GH_DataMapping.None)
            {
                settings.DataMapping = param.DataMapping.ToString();
                hasSettings = true;
            }

            // Extract additional settings (modifiers)
            var additionalSettings = ExtractAdditionalSettings(param);
            if (additionalSettings != null)
            {
                settings.AdditionalSettings = additionalSettings;
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
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ParameterMapper] Error extracting Optional property from '{param.Name}': {ex.Message}");
                }
            }

            // Extract expression generically if parameter exposes an 'Expression' property
            try
            {
                var expressionProp = param.GetType().GetProperty("Expression");
                if (expressionProp != null && expressionProp.CanRead)
                {
                    var expressionObj = expressionProp.GetValue(param);
                    var expressionStr = expressionObj?.ToString();
                    if (!string.IsNullOrWhiteSpace(expressionStr))
                    {
                        settings.Expression = expressionStr;
                        hasSettings = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ParameterMapper] Error extracting expression from '{param.Name}': {ex.Message}");
            }

            return hasSettings ? settings : null;
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

            // Extract Unitize flag for Param_Vector using reflection
            if (param is global::Grasshopper.Kernel.Parameters.Param_Vector)
            {
                var unitizeProp = param.GetType().GetProperty("Unitize");
                if (unitizeProp != null)
                {
                    var unitizeValue = (bool)unitizeProp.GetValue(param);
                    if (unitizeValue)
                    {
                        additionalSettings.Unitize = true;
                        hasAdditionalSettings = true;
                    }
                }
            }

            return hasAdditionalSettings ? additionalSettings : null;
        }

        /// <summary>
        /// Applies parameter settings to a Grasshopper parameter.
        /// </summary>
        /// <param name="param">Parameter to apply settings to</param>
        /// <param name="settings">Settings to apply</param>
        public static void ApplySettings(IGH_Param param, ParameterSettings settings)
        {
            if (param == null || settings == null)
                return;

            try
            {
                // Apply NickName if provided
                if (!string.IsNullOrEmpty(settings.NickName))
                {
                    param.NickName = settings.NickName;
                }

                // Description is implicit - not applied from settings

                // Apply DataMapping if provided
                if (!string.IsNullOrEmpty(settings.DataMapping))
                {
                    if (Enum.TryParse<GH_DataMapping>(settings.DataMapping, true, out var dataMapping))
                    {
                        param.DataMapping = dataMapping;
                    }
                }

                // Apply additional settings (modifiers)
                if (settings.AdditionalSettings != null)
                {
                    ApplyAdditionalSettings(param, settings.AdditionalSettings);
                }

                // Access mode is implicit from component type - not applied from settings

                // Apply expression if it's a Grasshopper expression parameter
                if (!string.IsNullOrEmpty(settings.Expression))
                {
                    ApplyExpression(param, settings.Expression);
                }

                // Apply Required/Optional property (only for input parameters)
                if (param.Kind == GH_ParamKind.input)
                {
                    try
                    {
                        var optionalProp = param.GetType().GetProperty("Optional");
                        if (optionalProp != null && optionalProp.CanWrite)
                        {
                            bool isOptional = settings.Required.HasValue ? !settings.Required.Value : true;
                            optionalProp.SetValue(param, isOptional);
                            Debug.WriteLine($"[ParameterMapper] Set Optional={isOptional} (Required={settings.Required?.ToString() ?? "null"}) for '{param.Name}'");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[ParameterMapper] Error applying Optional property to '{param.Name}': {ex.Message}");
                    }
                }

                Debug.WriteLine($"[ParameterMapper] Applied settings to parameter '{param.Name}'");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ParameterMapper] Error applying settings to '{param.Name}': {ex.Message}");
            }
        }

        /// <summary>
        /// Applies an expression to a parameter that supports expressions.
        /// </summary>
        /// <param name="param">Parameter to apply expression to</param>
        /// <param name="expression">Expression string</param>
        private static void ApplyExpression(IGH_Param param, string expression)
        {
            try
            {
                // Try to find and set Expression property via reflection
                var expressionProp = param.GetType().GetProperty("Expression");
                if (expressionProp != null && expressionProp.CanWrite)
                {
                    var exprType = expressionProp.PropertyType;

                    // If property is string, assign directly
                    if (exprType == typeof(string))
                    {
                        expressionProp.SetValue(param, expression);
                        Debug.WriteLine($"[ParameterMapper] Applied string expression '{expression}' to '{param.Name}'");
                        return;
                    }

                    // Otherwise try Parse(string) factory
                    var parseMethod = exprType.GetMethod("Parse", new[] { typeof(string) });
                    if (parseMethod != null)
                    {
                        var exprObj = parseMethod.Invoke(null, new object[] { expression });
                        expressionProp.SetValue(param, exprObj);
                        Debug.WriteLine($"[ParameterMapper] Applied parsed expression '{expression}' to '{param.Name}'");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ParameterMapper] Error applying expression to '{param.Name}': {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if a parameter has any custom settings worth serializing.
        /// </summary>
        /// <param name="param">Parameter to check</param>
        /// <param name="isPrincipal">Whether this is a principal parameter</param>
        /// <returns>True if parameter has custom settings</returns>
        public static bool HasCustomSettings(IGH_Param param, bool isPrincipal = false)
        {
            if (param == null)
                return false;

            // Check for custom nickname
            if (!string.IsNullOrEmpty(param.NickName) &&
                !string.Equals(param.Name, param.NickName, StringComparison.Ordinal))
                return true;

            // Description is implicit - not checked

            // Principal parameters should be marked
            if (isPrincipal)
                return true;

            // Check for optional flag
            if (param.Optional)
                return true;

            // Check for expression property on any parameter
            try
            {
                var expressionProp = param.GetType().GetProperty("Expression");
                if (expressionProp != null && expressionProp.CanRead)
                {
                    var expressionObj = expressionProp.GetValue(param);
                    if (expressionObj != null && !string.IsNullOrWhiteSpace(expressionObj.ToString()))
                        return true;
                }
            }
            catch (Exception) { }

            return false;
        }

        /// <summary>
        /// Applies additional parameter settings (modifiers) to a Grasshopper parameter.
        /// </summary>
        private static void ApplyAdditionalSettings(IGH_Param param, AdditionalParameterSettings additionalSettings)
        {
            Debug.WriteLine($"[ParameterMapper] ApplyAdditionalSettings called for '{param.Name}' (Type: {param.GetType().Name})");

            if (additionalSettings.Reverse.HasValue)
            {
                param.Reverse = additionalSettings.Reverse.Value;
                Debug.WriteLine($"[ParameterMapper]   Set Reverse = {additionalSettings.Reverse.Value}");
            }

            if (additionalSettings.Simplify.HasValue)
            {
                param.Simplify = additionalSettings.Simplify.Value;
                Debug.WriteLine($"[ParameterMapper]   Set Simplify = {additionalSettings.Simplify.Value}");
            }

            if (additionalSettings.Locked.HasValue)
            {
                param.Locked = additionalSettings.Locked.Value;
                Debug.WriteLine($"[ParameterMapper]   Set Locked = {additionalSettings.Locked.Value}");
            }

            // Apply Invert flag for Param_Boolean using reflection
            if (additionalSettings.Invert.HasValue && param is global::Grasshopper.Kernel.Parameters.Param_Boolean)
            {
                var invertProp = param.GetType().GetProperty("Invert");
                if (invertProp != null && invertProp.CanWrite)
                {
                    invertProp.SetValue(param, additionalSettings.Invert.Value);
                    Debug.WriteLine($"[ParameterMapper]   Set Invert = {additionalSettings.Invert.Value}");
                }
            }

            // Apply Unitize flag for Param_Vector using reflection
            if (additionalSettings.Unitize.HasValue && param is global::Grasshopper.Kernel.Parameters.Param_Vector)
            {
                var unitizeProp = param.GetType().GetProperty("Unitize");
                if (unitizeProp != null && unitizeProp.CanWrite)
                {
                    unitizeProp.SetValue(param, additionalSettings.Unitize.Value);
                    Debug.WriteLine($"[ParameterMapper]   Set Unitize = {additionalSettings.Unitize.Value}");
                }
            }

            Debug.WriteLine($"[ParameterMapper]   Verification - Reverse={param.Reverse}, Simplify={param.Simplify}, Locked={param.Locked}");
        }
    }
}
