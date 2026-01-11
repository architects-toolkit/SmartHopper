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
using RhinoCodePlatform.GH;

namespace SmartHopper.Core.Grasshopper.Utils.Internal
{
    /// <summary>
    /// Centralized utility for script component detection and operations.
    /// Provides consistent behavior across GhJSON extraction and placement operations.
    /// </summary>
    public static class ScriptComponentHelper
    {
        /// <summary>
        /// Detects whether an IScriptComponent is a C# script component.
        /// </summary>
        /// <param name="scriptComp">The script component to check</param>
        /// <returns>True if the component is a C# script component</returns>
        public static bool IsCSharpScriptComponent(IScriptComponent scriptComp)
        {
            if (scriptComp == null)
                return false;

            try
            {
                var typeName = scriptComp.GetType().Name;
                if (typeName.Contains("CSharp", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // Check for C# language property if available
                var langProp = scriptComp.GetType().GetProperty("Language");
                if (langProp != null)
                {
                    var langValue = langProp.GetValue(scriptComp);
                    if (langValue != null && langValue.ToString().Contains("C#", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch { }

            return false;
        }

        /// <summary>
        /// Detects whether an IScriptComponent is a Python script component.
        /// </summary>
        /// <param name="scriptComp">The script component to check</param>
        /// <returns>True if the component is a Python script component</returns>
        public static bool IsPythonScriptComponent(IScriptComponent scriptComp)
        {
            if (scriptComp == null)
                return false;

            try
            {
                var typeName = scriptComp.GetType().Name;
                if (typeName.Contains("Python", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // Check for Python language property if available
                var langProp = scriptComp.GetType().GetProperty("Language");
                if (langProp != null)
                {
                    var langValue = langProp.GetValue(scriptComp);
                    if (langValue != null)
                    {
                        var langStr = langValue.ToString();
                        if (langStr.Contains("Python", StringComparison.OrdinalIgnoreCase) ||
                            langStr.Contains("IronPython", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }
            catch { }

            return false;
        }

        /// <summary>
        /// Detects whether an IScriptComponent is a VB.NET script component.
        /// </summary>
        /// <param name="scriptComp">The script component to check</param>
        /// <returns>True if the component is a VB.NET script component</returns>
        public static bool IsVBScriptComponent(IScriptComponent scriptComp)
        {
            if (scriptComp == null)
                return false;

            try
            {
                var typeName = scriptComp.GetType().Name;
                if (typeName.Contains("VB", StringComparison.OrdinalIgnoreCase) ||
                    typeName.Contains("VisualBasic", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // Check for VB language property if available
                var langProp = scriptComp.GetType().GetProperty("Language");
                if (langProp != null)
                {
                    var langValue = langProp.GetValue(scriptComp);
                    if (langValue != null)
                    {
                        var langStr = langValue.ToString();
                        if (langStr.Contains("VB", StringComparison.OrdinalIgnoreCase) ||
                            langStr.Contains("VisualBasic", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }
            catch { }

            return false;
        }

        /// <summary>
        /// Gets the script language name for a script component.
        /// </summary>
        /// <param name="scriptComp">The script component to check</param>
        /// <returns>Language name (e.g., "C#", "Python", "IronPython", "VB") or "Unknown"</returns>
        public static string GetScriptLanguage(IScriptComponent scriptComp)
        {
            if (scriptComp == null)
                return "Unknown";

            try
            {
                // Try to get language from property first
                var langProp = scriptComp.GetType().GetProperty("Language");
                if (langProp != null)
                {
                    var langValue = langProp.GetValue(scriptComp);
                    if (langValue != null)
                    {
                        return langValue.ToString();
                    }
                }

                // Fallback to type name detection
                var typeName = scriptComp.GetType().Name;
                if (typeName.Contains("CSharp", StringComparison.OrdinalIgnoreCase))
                    return "C#";
                if (typeName.Contains("IronPython", StringComparison.OrdinalIgnoreCase))
                    return "IronPython";
                if (typeName.Contains("Python", StringComparison.OrdinalIgnoreCase))
                    return "Python";
                if (typeName.Contains("VB", StringComparison.OrdinalIgnoreCase) ||
                    typeName.Contains("VisualBasic", StringComparison.OrdinalIgnoreCase))
                    return "VB";
            }
            catch { }

            return "Unknown";
        }

        /// <summary>
        /// Gets the script language type enum for a script component.
        /// </summary>
        /// <param name="scriptComp">The script component to check</param>
        /// <returns>ScriptLanguage enum value</returns>
        public static ScriptLanguage GetScriptLanguageType(IScriptComponent scriptComp)
        {
            if (scriptComp == null)
                return ScriptLanguage.Unknown;

            if (IsCSharpScriptComponent(scriptComp))
                return ScriptLanguage.CSharp;
            if (IsPythonScriptComponent(scriptComp))
            {
                var langName = GetScriptLanguage(scriptComp);
                if (!string.IsNullOrEmpty(langName) && langName.Contains("IronPython", StringComparison.OrdinalIgnoreCase))
                    return ScriptLanguage.IronPython;
                return ScriptLanguage.Python;
            }

            if (IsVBScriptComponent(scriptComp))
                return ScriptLanguage.VB;

            return ScriptLanguage.Unknown;
        }
    }

    /// <summary>
    /// Enum representing the script language types supported by Grasshopper.
    /// </summary>
    public enum ScriptLanguage
    {
        /// <summary>Unknown or unsupported script language</summary>
        Unknown,

        /// <summary>C# script component</summary>
        CSharp,

        /// <summary>Python script component (GhPython or newer Python 3)</summary>
        Python,

        /// <summary>IronPython script component (legacy GhPython)</summary>
        IronPython,

        /// <summary>VB.NET script component</summary>
        VB
    }
}
