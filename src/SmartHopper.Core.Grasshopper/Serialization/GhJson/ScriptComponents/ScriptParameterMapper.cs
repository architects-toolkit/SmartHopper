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
                Access = AccessModeMapper.ToString(param.Access)
            };

            bool hasSettings = false;

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

                settings.VariableName = variableName;
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

                if (!string.IsNullOrEmpty(typeHint))
                {
                    settings.TypeHint = typeHint;
                    hasSettings = true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScriptParameterMapper] Error extracting type hint: {ex.Message}");
            }

            if (isPrincipal)
            {
                settings.IsPrincipal = true;
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
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScriptParameterMapper] Error setting VariableName: {ex.Message}");
            }

            // Apply type hint if provided
            if (!string.IsNullOrEmpty(settings.TypeHint))
            {
                ApplyTypeHint(param, settings.TypeHint, variableName);
            }

            return param;
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
                var typeHintProperty = param.GetType().GetProperty("TypeHint");
                var propType = typeHintProperty?.PropertyType?.FullName;

                if (typeHintProperty != null && typeHintProperty.CanWrite)
                {
                    if (propType == "System.String")
                    {
                        typeHintProperty.SetValue(param, typeHint);
                        Debug.WriteLine($"[ScriptParameterMapper] Applied type hint '{typeHint}' to '{parameterName}'");
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
    }
}
