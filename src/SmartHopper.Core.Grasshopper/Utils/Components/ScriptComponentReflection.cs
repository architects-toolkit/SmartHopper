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
        /// Known component GUIDs for Rhino 8 script components.
        /// </summary>
        private static readonly HashSet<Guid> ScriptComponentGuids = new HashSet<Guid>
        {
            new Guid("719467e6-7cf5-4848-99b0-c5dd57e5442c"), // Python
            new Guid("97aa26ef-88ae-4ba6-98a6-ed6ddeca11d1"), // IronPython
            new Guid("b6ba1144-02d6-4a2d-b53c-ec62e290eeb7"), // C#
            new Guid("079bd9bd-54a0-41d4-98af-db999015f63d"), // VB
        };

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
            if (obj is IGH_Component comp && ScriptComponentGuids.Contains(comp.ComponentGuid))
            {
                return true;
            }

            // Fallback: check for a writable Text property (all script components expose code this way)
            var textProp = obj.GetType().GetProperty("Text");
            return textProp != null && textProp.PropertyType == typeof(string);
        }

        /// <summary>
        /// Gets the script code text from a script component via reflection.
        /// </summary>
        public static string GetScriptText(object scriptComp)
        {
            if (scriptComp == null)
            {
                return string.Empty;
            }

            try
            {
                var textProp = scriptComp.GetType().GetProperty("Text");
                if (textProp != null)
                {
                    return textProp.GetValue(scriptComp)?.ToString() ?? string.Empty;
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
        /// </summary>
        public static void SetScriptText(object scriptComp, string code)
        {
            if (scriptComp == null || code == null)
            {
                return;
            }

            try
            {
                var textProp = scriptComp.GetType().GetProperty("Text");
                if (textProp != null && textProp.CanWrite)
                {
                    textProp.SetValue(scriptComp, code);
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
