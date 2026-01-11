/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024-2026 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 * 
 * This library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * Lesser General Public License for more details.
 * 
 * You should have received a copy of the GNU Lesser General Public License
 * along with this library; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using RhinoCodePlatform.GH;
using SmartHopper.Core.Grasshopper.Utils.Internal;
using SmartHopper.Core.Models.Components;

namespace SmartHopper.Core.Grasshopper.Serialization.GhJson.ScriptComponents
{
    /// <summary>
    /// Handles script signature parsing and injection for all script languages.
    /// Extracts type hints from RunScript signatures and injects them back during deserialization.
    /// </summary>
    public static class ScriptSignatureParser
    {
        /// <summary>
        /// Extracts type hint for a parameter from the RunScript method signature.
        /// </summary>
        /// <param name="scriptCode">Script code containing RunScript method</param>
        /// <param name="variableName">Parameter variable name to find</param>
        /// <param name="isInput">True for input parameters, false for outputs</param>
        /// <param name="scriptComp">Script component instance</param>
        /// <returns>Type hint string or null if not found</returns>
        public static string ExtractTypeHintFromSignature(
            string scriptCode,
            string variableName,
            bool isInput,
            IScriptComponent scriptComp)
        {
            if (string.IsNullOrEmpty(scriptCode) || string.IsNullOrEmpty(variableName))
                return null;

            try
            {
                var language = ScriptComponentHelper.GetScriptLanguageType(scriptComp);

                return language switch
                {
                    ScriptLanguage.CSharp => ExtractFromCSharpSignature(scriptCode, variableName, isInput),
                    ScriptLanguage.Python => ExtractFromPythonSignature(scriptCode, variableName, isInput),
                    ScriptLanguage.IronPython => ExtractFromPythonSignature(scriptCode, variableName, isInput),
                    ScriptLanguage.VB => ExtractFromVBSignature(scriptCode, variableName, isInput),
                    _ => null
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScriptSignatureParser] Error extracting type hint: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Injects type hints into script code based on parameter settings.
        /// </summary>
        /// <param name="scriptCode">Original script code</param>
        /// <param name="inputSettings">Input parameter settings with type hints</param>
        /// <param name="outputSettings">Output parameter settings with type hints</param>
        /// <param name="scriptComp">Script component instance</param>
        /// <returns>Script code with injected type hints</returns>
        public static string InjectTypeHintsIntoScript(
            string scriptCode,
            List<ParameterSettings> inputSettings,
            List<ParameterSettings> outputSettings,
            IScriptComponent scriptComp)
        {
            if (string.IsNullOrEmpty(scriptCode))
                return scriptCode;

            try
            {
                var language = ScriptComponentHelper.GetScriptLanguageType(scriptComp);

                if (language != ScriptLanguage.CSharp)
                {
                    // Python/VB scripts don't use type hints in Grasshopper
                    return scriptCode;
                }

                return InjectTypeHintsIntoCSharp(scriptCode, inputSettings, outputSettings);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScriptSignatureParser] Error injecting type hints: {ex.Message}");
                return scriptCode;
            }
        }

        #region C# Signature Parsing

        private static string ExtractFromCSharpSignature(string scriptCode, string variableName, bool isInput)
        {
            var match = Regex.Match(scriptCode,
                @"private\s+void\s+RunScript\s*\((.*?)\)",
                RegexOptions.Singleline);

            if (!match.Success)
                return null;

            var parametersStr = match.Groups[1].Value;
            var parameters = SplitParametersByComma(parametersStr);

            foreach (var param in parameters)
            {
                var trimmed = param.Trim();

                // C# format: "Type varName" or "ref Type varName" (outputs use ref)
                var isRef = trimmed.StartsWith("ref ", StringComparison.Ordinal);
                if (isRef != !isInput) // ref params are outputs, non-ref are inputs
                    continue;

                var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    var paramName = parts[parts.Length - 1].Trim();
                    var cleanParamName = paramName.TrimStart('@');
                    var cleanVariableName = variableName.TrimStart('@');

                    if (cleanParamName.Equals(cleanVariableName, StringComparison.Ordinal))
                    {
                        // Type is everything except the last part (variable name) and "ref" if present
                        var typeStartIdx = isRef ? 1 : 0;
                        var typeParts = new string[parts.Length - 1 - typeStartIdx];
                        Array.Copy(parts, typeStartIdx, typeParts, 0, typeParts.Length);
                        return string.Join(" ", typeParts).Trim();
                    }
                }
            }

            return null;
        }

        private static string InjectTypeHintsIntoCSharp(
            string scriptCode,
            List<ParameterSettings> inputSettings,
            List<ParameterSettings> outputSettings)
        {
            // Find the RunScript signature
            var match = Regex.Match(scriptCode,
                @"(private\s+void\s+RunScript\s*\()(.*?)(\))",
                RegexOptions.Singleline);

            if (!match.Success)
                return scriptCode;

            var before = match.Groups[1].Value;
            var parametersStr = match.Groups[2].Value;
            var after = match.Groups[3].Value;

            // Build new parameter list with type hints
            var newParams = BuildCSharpParameterListWithTypeHints(parametersStr, inputSettings, outputSettings);

            // Replace signature
            var newSignature = before + newParams + after;
            return scriptCode.Replace(match.Value, newSignature);
        }

        private static string BuildCSharpParameterListWithTypeHints(
            string originalParams,
            List<ParameterSettings> inputSettings,
            List<ParameterSettings> outputSettings)
        {
            var result = new StringBuilder();
            var originalParamList = SplitParametersByComma(originalParams);
            int inputIndex = 0;
            int outputIndex = 0;

            foreach (var param in originalParamList)
            {
                var trimmed = param.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;

                var isRef = trimmed.StartsWith("ref ", StringComparison.Ordinal);
                var settings = isRef
                    ? (outputIndex < outputSettings?.Count ? outputSettings[outputIndex++] : null)
                    : (inputIndex < inputSettings?.Count ? inputSettings[inputIndex++] : null);

                // Extract variable name
                var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var varName = parts.Length > 0 ? parts[parts.Length - 1] : "param";

                // Use type hint if available, otherwise keep original type
                string typeHint;
                if (!string.IsNullOrEmpty(settings?.TypeHint))
                {
                    typeHint = settings.TypeHint;
                }
                else
                {
                    // Extract original type
                    var typeStartIdx = isRef ? 1 : 0;
                    var typeParts = new string[parts.Length - 1 - typeStartIdx];
                    Array.Copy(parts, typeStartIdx, typeParts, 0, typeParts.Length);
                    typeHint = string.Join(" ", typeParts).Trim();
                }

                if (result.Length > 0)
                    result.Append(", ");

                if (isRef)
                    result.Append("ref ");

                result.Append(typeHint);
                result.Append(" ");
                result.Append(varName);
            }

            return result.ToString();
        }

        #endregion

        #region Python Signature Parsing

        private static string ExtractFromPythonSignature(string scriptCode, string variableName, bool isInput)
        {
            // Python doesn't have type hints in Grasshopper RunScript signatures
            // All parameters are inputs (outputs are return values)
            if (!isInput)
                return null;

            var match = Regex.Match(scriptCode,
                @"def\s+RunScript\s*\(\s*self\s*,\s*(.*?)\s*\)\s*:",
                RegexOptions.Singleline);

            if (!match.Success)
                return null;

            // Python parameters don't have type information in signature
            return null;
        }

        #endregion

        #region VB Signature Parsing

        private static string ExtractFromVBSignature(string scriptCode, string variableName, bool isInput)
        {
            // VB.NET signature parsing (similar to C#)
            var match = Regex.Match(scriptCode,
                @"Sub\s+RunScript\s*\((.*?)\)",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);

            if (!match.Success)
                return null;

            // Similar logic to C# but with VB syntax
            // TODO: Implement VB-specific parsing if needed
            return null;
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Splits parameter string by commas, respecting nested generics like List&lt;Curve&gt;.
        /// </summary>
        private static List<string> SplitParametersByComma(string parametersStr)
        {
            var result = new List<string>();
            var current = new StringBuilder();
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

        #endregion
    }
}
