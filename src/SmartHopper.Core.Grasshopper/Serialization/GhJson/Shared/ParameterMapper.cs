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
using Grasshopper.Kernel.Special;
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
        /// <param name="param">Parameter to extract settings from</param>
        /// <param name="isPrincipal">Whether this is the principal parameter</param>
        /// <returns>ParameterSettings or null if no relevant settings</returns>
        public static ParameterSettings ExtractSettings(IGH_Param param, bool isPrincipal = false)
        {
            if (param == null)
                return null;

            var settings = new ParameterSettings
            {
                ParameterName = param.Name,
                Access = AccessModeMapper.ToString(param.Access)
            };

            bool hasSettings = false;

            // Extract NickName if different from Name
            if (!string.IsNullOrEmpty(param.NickName) && 
                !string.Equals(param.Name, param.NickName, StringComparison.Ordinal))
            {
                settings.NickName = param.NickName;
                hasSettings = true;
            }

            // Extract Description if present
            if (!string.IsNullOrEmpty(param.Description))
            {
                settings.Description = param.Description;
                hasSettings = true;
            }

            // Mark as principal if applicable
            if (isPrincipal)
            {
                settings.IsPrincipal = true;
                hasSettings = true;
            }

            // Extract optional flag
            if (param.Optional)
            {
                settings.Optional = true;
                hasSettings = true;
            }

            // Extract expression if it's a Grasshopper expression parameter
            if (param is global::Grasshopper.Kernel.Parameters.Param_ScriptVariable scriptVar)
            {
                var expressionProp = scriptVar.GetType().GetProperty("Expression");
                if (expressionProp != null)
                {
                    var expressionObj = expressionProp.GetValue(scriptVar);
                    if (expressionObj != null)
                    {
                        var expressionStr = expressionObj.ToString();
                        if (!string.IsNullOrEmpty(expressionStr))
                        {
                            settings.Expression = expressionStr;
                            hasSettings = true;
                        }
                    }
                }
            }

            return hasSettings ? settings : null;
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

                // Apply Description if provided
                if (!string.IsNullOrEmpty(settings.Description))
                {
                    param.Description = settings.Description;
                }

                // Apply optional flag
                if (settings.Optional.HasValue)
                {
                    param.Optional = settings.Optional.Value;
                }

                // Apply access mode if provided
                if (!string.IsNullOrEmpty(settings.Access))
                {
                    param.Access = AccessModeMapper.FromString(settings.Access);
                }

                // Apply expression if it's a Grasshopper expression parameter
                if (!string.IsNullOrEmpty(settings.Expression))
                {
                    ApplyExpression(param, settings.Expression);
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
                    // Get the type of expression object needed
                    var exprType = expressionProp.PropertyType;
                    
                    // Try to create expression from string
                    var parseMethod = exprType.GetMethod("Parse", new[] { typeof(string) });
                    if (parseMethod != null)
                    {
                        var exprObj = parseMethod.Invoke(null, new object[] { expression });
                        expressionProp.SetValue(param, exprObj);
                        Debug.WriteLine($"[ParameterMapper] Applied expression '{expression}' to '{param.Name}'");
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

            // Check for description
            if (!string.IsNullOrEmpty(param.Description))
                return true;

            // Principal parameters should be marked
            if (isPrincipal)
                return true;

            // Check for optional flag
            if (param.Optional)
                return true;

            // Check for expression
            if (param is global::Grasshopper.Kernel.Parameters.Param_ScriptVariable)
                return true;

            return false;
        }
    }
}
