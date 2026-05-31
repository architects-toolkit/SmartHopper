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
 * along with this library; if not, see <https://www.gnu.org/licenses/lgpl-3.0.html>.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using GhJSON.Core.NameResolution;
using Grasshopper.Kernel;

namespace SmartHopper.Core.Grasshopper.Utils.Components
{
    /// <summary>
    /// Reflection-based helpers for interacting with Grasshopper script components
    /// (Python, C#, IronPython, VB) without compile-time dependencies on RhinoCode assemblies.
    /// </summary>
    public static class ScriptComponentReflection
    {
        /// <summary>
        /// Determines whether the specified document object is a script component.
        /// Checks known GUIDs first, then falls back to detecting a <c>Text</c> property.
        /// </summary>
        public static bool IsScriptComponent(IGH_DocumentObject obj)
        {
            if (obj == null)
            {
                return false;
            }

            // Fast path: known script component GUIDs
            if (obj is IGH_Component comp && ScriptComponentRegistry.IsScriptComponent(comp.ComponentGuid))
            {
                return true;
            }

            // Fallback: check for a writable Text property (all script components expose code this way)
            var textProp = obj.GetType().GetProperty("Text");
            return textProp != null && textProp.PropertyType == typeof(string);
        }

        /// <summary>
        /// Gets the script code text from a script component via reflection.
        /// Tries IScriptComponent interface first, then common property names,
        /// then nested ScriptSource objects.
        /// </summary>
        public static string GetScriptText(object scriptComp)
        {
            if (scriptComp == null)
            {
                return string.Empty;
            }

            try
            {
                var type = scriptComp.GetType();

                // Strategy 1: IScriptComponent interface (Rhino 8 Python 3, C#, IronPython)
                var scriptInterface = type.GetInterfaces().FirstOrDefault(i => i.Name == "IScriptComponent");
                if (scriptInterface != null)
                {
                    var textProp = scriptInterface.GetProperty("Text");
                    if (textProp != null && textProp.CanRead)
                    {
                        var value = textProp.GetValue(scriptComp)?.ToString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            return value;
                        }
                    }
                }

                // Strategy 2: Common property names on concrete type
                string[] candidates = { "Text", "Script", "Code", "ScriptCode", "Source", "SourceCode" };
                foreach (var name in candidates)
                {
                    var prop = type.GetProperty(name);
                    if (prop != null && prop.CanRead)
                    {
                        var value = prop.GetValue(scriptComp)?.ToString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            return value;
                        }
                    }
                }

                // Strategy 3: Nested ScriptSource object
                var scriptSourceProp = type.GetProperty("ScriptSource");
                if (scriptSourceProp != null && scriptSourceProp.CanRead)
                {
                    var scriptSourceObj = scriptSourceProp.GetValue(scriptComp);
                    if (scriptSourceObj != null)
                    {
                        var scriptSourceType = scriptSourceObj.GetType();
                        string[] sourceCandidates = { "ScriptCode", "Code", "Text", "Source", "SourceCode" };
                        foreach (var name in sourceCandidates)
                        {
                            var prop = scriptSourceType.GetProperty(name);
                            if (prop != null && prop.CanRead)
                            {
                                var value = prop.GetValue(scriptSourceObj)?.ToString();
                                if (!string.IsNullOrWhiteSpace(value))
                                {
                                    return value;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScriptComponentReflection] Failed to get script text: {ex.Message}");
            }

            return string.Empty;
        }

        /// <summary>
        /// Sets the script code text on a script component via reflection.
        /// Tries IScriptComponent interface first, then common property names,
        /// then nested ScriptSource objects.
        /// </summary>
        public static void SetScriptText(object scriptComp, string code)
        {
            if (scriptComp == null || code == null)
            {
                return;
            }

            try
            {
                var type = scriptComp.GetType();

                // Strategy 1: IScriptComponent interface
                var scriptInterface = type.GetInterfaces().FirstOrDefault(i => i.Name == "IScriptComponent");
                if (scriptInterface != null)
                {
                    var textProp = scriptInterface.GetProperty("Text");
                    if (textProp != null && textProp.CanWrite)
                    {
                        textProp.SetValue(scriptComp, code);
                        return;
                    }
                }

                // Strategy 2: Common property names
                string[] candidates = { "Text", "Script", "Code", "ScriptCode", "Source", "SourceCode" };
                foreach (var name in candidates)
                {
                    var prop = type.GetProperty(name);
                    if (prop != null && prop.CanWrite)
                    {
                        prop.SetValue(scriptComp, code);
                        return;
                    }
                }

                // Strategy 3: Nested ScriptSource object
                var scriptSourceProp = type.GetProperty("ScriptSource");
                if (scriptSourceProp != null && scriptSourceProp.CanRead)
                {
                    var scriptSourceObj = scriptSourceProp.GetValue(scriptComp);
                    if (scriptSourceObj != null)
                    {
                        var scriptSourceType = scriptSourceObj.GetType();
                        string[] sourceCandidates = { "ScriptCode", "Code", "Text", "Source", "SourceCode" };
                        foreach (var name in sourceCandidates)
                        {
                            var prop = scriptSourceType.GetProperty(name);
                            if (prop != null && prop.CanWrite)
                            {
                                prop.SetValue(scriptSourceObj, code);
                                return;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScriptComponentReflection] Failed to set script text: {ex.Message}");
            }
        }

        /// <summary>
        /// Refreshes a script component after structural modifications.
        /// Triggers <see cref="IGH_VariableParameterComponent.VariableParameterMaintenance()"/>
        /// if supported, then recreates attributes and expires the solution.
        /// </summary>
        public static void RefreshScriptComponent(object scriptComp)
        {
            var ghComp = scriptComp as IGH_Component;
            if (ghComp == null)
            {
                return;
            }

            if (ghComp is IGH_VariableParameterComponent varParamComp)
            {
                varParamComp.VariableParameterMaintenance();
            }

            ghComp.CreateAttributes();
            ghComp.ExpireSolution(true);

            Debug.WriteLine($"[ScriptComponentReflection] Refreshed script component '{ghComp.Name}'");
        }

        /// <summary>
        /// Sets the <see cref="GH_ParamAccess"/> on a parameter via reflection.
        /// Handles script-specific parameters (e.g. <c>ScriptVariableParam</c>) that define
        /// an <c>Access</c> property not exposed by the <see cref="IGH_Param"/> interface.
        /// </summary>
        public static void SetParameterAccess(IGH_Param param, GH_ParamAccess access)
        {
            if (param == null)
            {
                return;
            }

            try
            {
                var accessProp = param.GetType().GetProperty("Access");
                if (accessProp != null && accessProp.CanWrite && accessProp.PropertyType == typeof(GH_ParamAccess))
                {
                    accessProp.SetValue(param, access);
                    Debug.WriteLine($"[ScriptComponentReflection] Set access '{access}' on parameter '{param.Name}'");
                }
                else
                {
                    Debug.WriteLine($"[ScriptComponentReflection] Parameter '{param.Name}' does not support Access property");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScriptComponentReflection] Failed to set access on '{param.Name}': {ex.Message}");
            }
        }
    }
}
