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
using Newtonsoft.Json.Linq;
using RhinoCodePlatform.GH;
using SmartHopper.Core.Models.Components;

namespace SmartHopper.Core.Grasshopper.Serialization.GhJson.ScriptComponents
{
    /// <summary>
    /// Centralized factory for creating script component definitions.
    /// Provides script component GUIDs, display names, and ComponentProperties builders.
    /// Eliminates the need for temporary component instantiation.
    /// </summary>
    public static class ScriptComponentFactory
    {
        #region Script Component GUIDs

        /// <summary>
        /// Python 3 script component GUID.
        /// </summary>
        public static readonly Guid Python3Guid = new Guid("719467e6-7cf5-4848-99b0-c5dd57e5442c");

        /// <summary>
        /// IronPython 2 script component GUID.
        /// </summary>
        public static readonly Guid IronPython2Guid = new Guid("97aa26ef-88ae-4ba6-98a6-ed6ddeca11d1");

        /// <summary>
        /// C# script component GUID.
        /// </summary>
        public static readonly Guid CSharpGuid = new Guid("b6ba1144-02d6-4a2d-b53c-ec62e290eeb7");

        /// <summary>
        /// VB.NET script component GUID.
        /// </summary>
        public static readonly Guid VBNetGuid = new Guid("079bd9bd-54a0-41d4-98af-db999015f63d");

        #endregion

        #region Language Mapping

        /// <summary>
        /// Maps language key strings to component information.
        /// </summary>
        private static readonly Dictionary<string, ScriptComponentInfo> LanguageMap = new Dictionary<string, ScriptComponentInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["python"] = new ScriptComponentInfo(Python3Guid, "Python 3 Script", "python"),
            ["python3"] = new ScriptComponentInfo(Python3Guid, "Python 3 Script", "python"),
            ["ironpython"] = new ScriptComponentInfo(IronPython2Guid, "IronPython 2 Script", "ironpython"),
            ["ironpython2"] = new ScriptComponentInfo(IronPython2Guid, "IronPython 2 Script", "ironpython"),
            ["c#"] = new ScriptComponentInfo(CSharpGuid, "C# Script", "csharp"),
            ["csharp"] = new ScriptComponentInfo(CSharpGuid, "C# Script", "csharp"),
            ["vb"] = new ScriptComponentInfo(VBNetGuid, "VB Script", "vb"),
            ["vb.net"] = new ScriptComponentInfo(VBNetGuid, "VB Script", "vb"),
            ["vbnet"] = new ScriptComponentInfo(VBNetGuid, "VB Script", "vb"),
        };

        #endregion

        #region Public API

        /// <summary>
        /// Gets script component information by language key.
        /// </summary>
        /// <param name="languageKey">Language identifier (e.g., "python", "c#", "vb")</param>
        /// <returns>Script component info, or null if not found</returns>
        public static ScriptComponentInfo GetComponentInfo(string languageKey)
        {
            if (string.IsNullOrEmpty(languageKey))
                return null;

            return LanguageMap.TryGetValue(languageKey.Trim(), out var info) ? info : null;
        }

        /// <summary>
        /// Creates ComponentProperties for a script component from AI-generated data.
        /// </summary>
        /// <param name="languageKey">Language identifier</param>
        /// <param name="scriptCode">Script source code</param>
        /// <param name="inputs">Input parameter definitions (JArray)</param>
        /// <param name="outputs">Output parameter definitions (JArray)</param>
        /// <param name="nickname">Optional component nickname</param>
        /// <returns>Configured ComponentProperties ready for deserialization</returns>
        public static ComponentProperties CreateScriptComponent(
            string languageKey,
            string scriptCode,
            JArray inputs,
            JArray outputs,
            string nickname = null)
        {
            var info = GetComponentInfo(languageKey);
            if (info == null)
            {
                throw new ArgumentException($"Unsupported script language: {languageKey}. Supported: python, ironpython, c#, vb.");
            }

            // Build input settings from AI response
            var inputSettings = new List<ParameterSettings>();
            if (inputs != null)
            {
                foreach (var input in inputs)
                {
                    var paramSettings = new ParameterSettings
                    {
                        ParameterName = input["name"]?.ToString() ?? "input",
                        VariableName = input["name"]?.ToString() ?? "input",
                        Access = input["access"]?.ToString()?.ToLowerInvariant() ?? "item",
                        TypeHint = input["type"]?.ToString(),
                        Description = input["description"]?.ToString(),
                        DataMapping = input["dataMapping"]?.ToString(),
                        Expression = input["expression"]?.ToString(),
                        IsPrincipal = input["isPrincipal"]?.Value<bool>(),
                        Required = input["required"]?.Value<bool>(),
                    };

                    // Map additional settings (reverse, simplify, invert)
                    var hasAdditionalSettings = false;
                    var additionalSettings = new AdditionalParameterSettings();

                    if (input["reverse"] != null && input["reverse"].Value<bool>())
                    {
                        additionalSettings.Reverse = true;
                        hasAdditionalSettings = true;
                    }

                    if (input["simplify"] != null && input["simplify"].Value<bool>())
                    {
                        additionalSettings.Simplify = true;
                        hasAdditionalSettings = true;
                    }

                    if (input["invert"] != null && input["invert"].Value<bool>())
                    {
                        additionalSettings.Invert = true;
                        hasAdditionalSettings = true;
                    }

                    if (hasAdditionalSettings)
                    {
                        paramSettings.AdditionalSettings = additionalSettings;
                    }

                    inputSettings.Add(paramSettings);
                }
            }

            // Build output settings from AI response
            var outputSettings = new List<ParameterSettings>();
            if (outputs != null)
            {
                foreach (var output in outputs)
                {
                    var paramSettings = new ParameterSettings
                    {
                        ParameterName = output["name"]?.ToString() ?? "output",
                        VariableName = output["name"]?.ToString() ?? "output",
                        TypeHint = output["type"]?.ToString(),
                        Description = output["description"]?.ToString(),
                        DataMapping = output["dataMapping"]?.ToString(),
                    };

                    // Map additional settings (reverse, simplify, invert) for outputs
                    var hasAdditionalSettings = false;
                    var additionalSettings = new AdditionalParameterSettings();

                    if (output["reverse"] != null && output["reverse"].Value<bool>())
                    {
                        additionalSettings.Reverse = true;
                        hasAdditionalSettings = true;
                    }

                    if (output["simplify"] != null && output["simplify"].Value<bool>())
                    {
                        additionalSettings.Simplify = true;
                        hasAdditionalSettings = true;
                    }

                    if (output["invert"] != null && output["invert"].Value<bool>())
                    {
                        additionalSettings.Invert = true;
                        hasAdditionalSettings = true;
                    }

                    if (hasAdditionalSettings)
                    {
                        paramSettings.AdditionalSettings = additionalSettings;
                    }

                    outputSettings.Add(paramSettings);
                }
            }

            // Create component properties following GhJSON schema
            return new ComponentProperties
            {
                Name = info.DisplayName,
                ComponentGuid = info.Guid,
                InstanceGuid = Guid.NewGuid(),
                NickName = nickname,
                ComponentState = new ComponentState
                {
                    Value = scriptCode, // Script code in componentState.value
                },
                InputSettings = inputSettings,
                OutputSettings = outputSettings,
            };
        }

        /// <summary>
        /// Checks if a language key is supported.
        /// </summary>
        public static bool IsLanguageSupported(string languageKey)
        {
            return !string.IsNullOrEmpty(languageKey) &&
                   LanguageMap.ContainsKey(languageKey.Trim());
        }

        /// <summary>
        /// Gets all supported language keys.
        /// </summary>
        public static IEnumerable<string> GetSupportedLanguages()
        {
            return new[] { "python", "ironpython", "c#", "vb" };
        }

        /// <summary>
        /// Detects language from a live script component instance.
        /// Uses reflection to access LanguageSpec property, avoiding compile-time dependency.
        /// </summary>
        /// <param name="scriptComp">Script component to detect language from</param>
        /// <returns>Normalized language key (python, ironpython, c#, vb) or "unknown"</returns>
        public static string DetectLanguage(IScriptComponent scriptComp)
        {
            if (scriptComp == null)
                return "unknown";

            try
            {
                // Use reflection to get LanguageSpec property value (avoids compile-time dependency)
                var langSpecProperty = scriptComp.GetType().GetProperty("LanguageSpec");
                if (langSpecProperty != null)
                {
                    var langSpec = langSpecProperty.GetValue(scriptComp);
                    if (langSpec != null)
                    {
                        var langStr = langSpec.ToString()?.ToLowerInvariant() ?? "unknown";
                        Debug.WriteLine($"[ScriptComponentFactory] Language detected via LanguageSpec: {langStr}");

                        // Normalize to our language keys
                        if (langStr.Contains("python3") || langStr.Contains("python 3"))
                            return "python";
                        if (langStr.Contains("ironpython") || langStr.Contains("python2") || langStr.Contains("python 2"))
                            return "ironpython";
                        if (langStr.Contains("csharp") || langStr.Contains("c#"))
                            return "c#";
                        if (langStr.Contains("visualbasic") || langStr.Contains("vb") || langStr.Contains("visual basic"))
                            return "vb";

                        Debug.WriteLine($"[ScriptComponentFactory] Unknown language from LanguageSpec: {langStr}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScriptComponentFactory] Error detecting language from LanguageSpec: {ex.Message}");
            }

            // Fallback to type name detection
            return DetectLanguageFromTypeName(scriptComp);
        }

        /// <summary>
        /// Gets component info from a live component instance.
        /// Combines language detection with component metadata lookup.
        /// </summary>
        /// <param name="scriptComp">Script component instance</param>
        /// <returns>Component info or null if language not supported</returns>
        public static ScriptComponentInfo GetComponentInfoFromInstance(IScriptComponent scriptComp)
        {
            var language = DetectLanguage(scriptComp);
            return GetComponentInfo(language);
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Detects language from component type name as fallback.
        /// </summary>
        private static string DetectLanguageFromTypeName(IScriptComponent scriptComp)
        {
            if (scriptComp == null)
                return "unknown";

            try
            {
                var typeName = scriptComp.GetType().Name.ToLowerInvariant();
                Debug.WriteLine($"[ScriptComponentFactory] Detecting language from type name: {typeName}");

                if (typeName.Contains("python3"))
                    return "python";
                if (typeName.Contains("ironpython") || typeName.Contains("python2"))
                    return "ironpython";
                if (typeName.Contains("csharp"))
                    return "c#";
                if (typeName.Contains("vb"))
                    return "vb";

                Debug.WriteLine($"[ScriptComponentFactory] Unknown type name: {typeName}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScriptComponentFactory] Error detecting language from type name: {ex.Message}");
            }

            return "unknown";
        }

        #endregion

        #region Helper Classes

        /// <summary>
        /// Information about a script component type.
        /// </summary>
        public class ScriptComponentInfo
        {
            /// <summary>
            /// Component GUID for instantiation.
            /// </summary>
            public Guid Guid { get; }

            /// <summary>
            /// Display name for the component.
            /// </summary>
            public string DisplayName { get; }

            /// <summary>
            /// Normalized language key.
            /// </summary>
            public string LanguageKey { get; }

            public ScriptComponentInfo(Guid guid, string displayName, string languageKey)
            {
                Guid = guid;
                DisplayName = displayName;
                LanguageKey = languageKey;
            }
        }

        #endregion
    }
}
