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
using System.Linq;
using GhJSON.Core.Models.Components;
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;

namespace SmartHopper.Core.Grasshopper.Utils.Serialization
{
    /// <summary>
    /// Factory for creating script component GhJSON structures.
    /// SmartHopper-specific utility for AI tool operations.
    /// </summary>
    public static class ScriptComponentFactory
    {
        /// <summary>Python 3 script component GUID.</summary>
        public static readonly Guid Python3Guid = new Guid("719467e6-7cf5-4848-99b0-c5dd57e5442c");

        /// <summary>IronPython 2 (legacy GhPython) component GUID.</summary>
        public static readonly Guid IronPython2Guid = new Guid("97aa26ef-88ae-4ba6-98a6-ed6ddeca11d1");

        /// <summary>C# script component GUID.</summary>
        public static readonly Guid CSharpGuid = new Guid("b6ba1144-02d6-4a2d-b53c-ec62e290eeb7");

        /// <summary>VB.NET script component GUID.</summary>
        public static readonly Guid VBNetGuid = new Guid("079bd9bd-54a0-41d4-98af-db999015f63d");

        private static readonly Dictionary<string, ScriptComponentInfo> _languageMap = new Dictionary<string, ScriptComponentInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["python"] = new ScriptComponentInfo { Language = "python", ComponentGuid = Python3Guid, Name = "Python 3 Script" },
            ["python3"] = new ScriptComponentInfo { Language = "python", ComponentGuid = Python3Guid, Name = "Python 3 Script" },
            ["ironpython"] = new ScriptComponentInfo { Language = "ironpython", ComponentGuid = IronPython2Guid, Name = "IronPython 2 Script" },
            ["ironpython2"] = new ScriptComponentInfo { Language = "ironpython", ComponentGuid = IronPython2Guid, Name = "IronPython 2 Script" },
            ["c#"] = new ScriptComponentInfo { Language = "c#", ComponentGuid = CSharpGuid, Name = "C# Script" },
            ["csharp"] = new ScriptComponentInfo { Language = "c#", ComponentGuid = CSharpGuid, Name = "C# Script" },
            ["vb"] = new ScriptComponentInfo { Language = "vb", ComponentGuid = VBNetGuid, Name = "VB Script" },
            ["vb.net"] = new ScriptComponentInfo { Language = "vb", ComponentGuid = VBNetGuid, Name = "VB Script" },
            ["vbnet"] = new ScriptComponentInfo { Language = "vb", ComponentGuid = VBNetGuid, Name = "VB Script" },
        };

        /// <summary>
        /// Gets component information for a language.
        /// </summary>
        public static ScriptComponentInfo GetComponentInfo(string language)
        {
            if (string.IsNullOrWhiteSpace(language))
                return null;

            return _languageMap.TryGetValue(language.Trim(), out var info) ? info : null;
        }

        /// <summary>
        /// Gets list of supported languages.
        /// </summary>
        public static IEnumerable<string> GetSupportedLanguages()
        {
            return _languageMap.Values.Select(v => v.Language).Distinct();
        }

        /// <summary>
        /// Normalizes a language key or returns default.
        /// </summary>
        public static string NormalizeLanguageKeyOrDefault(string language, string defaultLanguage = "python")
        {
            var info = GetComponentInfo(language);
            return info?.Language ?? defaultLanguage;
        }

        /// <summary>
        /// Detects language from a Grasshopper component.
        /// </summary>
        public static string DetectLanguage(IGH_ActiveObject component)
        {
            if (component == null)
                return "unknown";

            var guid = component.ComponentGuid;
            if (guid == Python3Guid) return "python";
            if (guid == IronPython2Guid) return "ironpython";
            if (guid == CSharpGuid) return "c#";
            if (guid == VBNetGuid) return "vb";

            return "unknown";
        }

        /// <summary>
        /// Creates a script component GhJSON structure.
        /// </summary>
        public static ComponentProperties CreateScriptComponent(
            string language,
            string scriptCode,
            JArray inputs,
            JArray outputs,
            string nickname = null)
        {
            var info = GetComponentInfo(language);
            if (info == null)
                throw new ArgumentException($"Unsupported language: {language}", nameof(language));

            var component = new ComponentProperties
            {
                Name = info.Name,
                ComponentGuid = info.ComponentGuid,
                InstanceGuid = Guid.NewGuid(),
                NickName = nickname ?? info.Name,
                ComponentState = new ComponentState
                {
                    Value = scriptCode
                },
                InputSettings = ConvertParameterSettings(inputs, isInput: true),
                OutputSettings = ConvertParameterSettings(outputs, isInput: false)
            };

            return component;
        }

        private static List<ParameterSettings> ConvertParameterSettings(JArray parameters, bool isInput)
        {
            if (parameters == null || parameters.Count == 0)
                return new List<ParameterSettings>();

            var result = new List<ParameterSettings>();
            foreach (var param in parameters)
            {
                if (param is JObject obj)
                {
                    var settings = new ParameterSettings
                    {
                        ParameterName = obj["name"]?.ToString() ?? "param",
                        VariableName = obj["name"]?.ToString() ?? "param",
                        Description = obj["description"]?.ToString(),
                        DataMapping = obj["dataMapping"]?.ToString(),
                    };

                    // Parse type hint
                    var typeHint = obj["type"]?.ToString();
                    if (!string.IsNullOrEmpty(typeHint))
                    {
                        settings.TypeHint = typeHint;
                    }

                    // Parse access mode
                    var access = obj["access"]?.ToString();
                    if (!string.IsNullOrEmpty(access))
                    {
                        settings.Access = access;
                    }

                    // Parse additional settings
                    if (obj["reverse"]?.ToObject<bool>() == true)
                        settings.AdditionalSettings = settings.AdditionalSettings ?? new AdditionalParameterSettings();
                    if (obj["simplify"]?.ToObject<bool>() == true)
                        settings.AdditionalSettings = settings.AdditionalSettings ?? new AdditionalParameterSettings();
                    if (obj["isPrincipal"]?.ToObject<bool>() == true)
                        settings.AdditionalSettings = settings.AdditionalSettings ?? new AdditionalParameterSettings();

                    result.Add(settings);
                }
            }

            return result;
        }
    }

    /// <summary>
    /// Script component information.
    /// </summary>
    public class ScriptComponentInfo
    {
        public string Language { get; set; }
        public Guid ComponentGuid { get; set; }
        public string Name { get; set; }
    }
}
