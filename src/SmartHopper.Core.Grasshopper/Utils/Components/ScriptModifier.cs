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
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;
using RhinoCodePlatform.GH;
using RhinoCodePluginGH.Parameters;
using SmartHopper.Core.Grasshopper.Serialization.GhJson.ScriptComponents;
using SmartHopper.Core.Models.Components;

namespace SmartHopper.Core.Grasshopper.Utils.Components
{
    /// <summary>
    /// Specialized modifier for script components (Python, C#, VB, IronPython).
    /// Handles the unique complexity of script parameters, type hints, and code updates.
    /// </summary>
    public static class ScriptModifier
    {
        #region Script Component Updates

        /// <summary>
        /// Updates a script component's code and parameters.
        /// This is the main entry point for script modifications.
        /// </summary>
        /// <param name="scriptComp">Script component to modify</param>
        /// <param name="newCode">New script code (null to keep existing)</param>
        /// <param name="newInputs">New input parameters in AI format (null to keep existing)</param>
        /// <param name="newOutputs">New output parameters in AI format (null to keep existing)</param>
        public static void UpdateScript(
            IScriptComponent scriptComp,
            string newCode = null,
            JArray newInputs = null,
            JArray newOutputs = null)
        {
            if (scriptComp == null)
                throw new ArgumentNullException(nameof(scriptComp));

            var ghComp = scriptComp as IGH_Component;
            if (ghComp == null)
                throw new ArgumentException("Script component must implement IGH_Component", nameof(scriptComp));

            // Update script code if provided
            if (newCode != null)
            {
                scriptComp.Text = newCode;
                Debug.WriteLine($"[ScriptModifier] Updated script code ({newCode.Length} chars)");
            }

            // Update inputs if provided
            if (newInputs != null)
            {
                UpdateParameters(scriptComp, newInputs, isInput: true);
            }

            // Update outputs if provided
            if (newOutputs != null)
            {
                UpdateParameters(scriptComp, newOutputs, isInput: false);
            }

            // Refresh component
            RefreshScriptComponent(scriptComp);

            Debug.WriteLine($"[ScriptModifier] Script component updated successfully");
        }

        /// <summary>
        /// Updates only the script code, keeping parameters unchanged.
        /// </summary>
        public static void UpdateCode(IScriptComponent scriptComp, string newCode)
        {
            if (scriptComp == null)
                throw new ArgumentNullException(nameof(scriptComp));
            if (string.IsNullOrEmpty(newCode))
                throw new ArgumentException("Code cannot be null or empty", nameof(newCode));

            scriptComp.Text = newCode;
            RefreshScriptComponent(scriptComp);

            Debug.WriteLine($"[ScriptModifier] Updated script code ({newCode.Length} chars)");
        }

        /// <summary>
        /// Updates only the script parameters, keeping code unchanged.
        /// </summary>
        public static void UpdateParameters(
            IScriptComponent scriptComp,
            JArray newInputs = null,
            JArray newOutputs = null)
        {
            if (scriptComp == null)
                throw new ArgumentNullException(nameof(scriptComp));

            if (newInputs != null)
            {
                UpdateParameters(scriptComp, newInputs, isInput: true);
            }

            if (newOutputs != null)
            {
                UpdateParameters(scriptComp, newOutputs, isInput: false);
            }

            RefreshScriptComponent(scriptComp);
        }

        #endregion

        #region Parameter Management

        /// <summary>
        /// Updates script component parameters (inputs or outputs).
        /// Handles type hints, access types, and data mapping.
        /// </summary>
        private static void UpdateParameters(
            IScriptComponent scriptComp,
            JArray parameters,
            bool isInput)
        {
            var ghComp = scriptComp as IGH_Component;
            if (ghComp == null) return;

            var paramList = isInput ? ghComp.Params.Input : ghComp.Params.Output;
            var paramType = isInput ? "input" : "output";

            // Convert JArray to ParameterSettings
            var settingsList = ConvertToParameterSettings(parameters, isInput);

            // Clear existing parameters
            paramList.Clear();
            Debug.WriteLine($"[ScriptModifier] Cleared existing {paramType} parameters");

            // Add new parameters
            int index = 0;
            foreach (var settings in settingsList)
            {
                var param = ScriptParameterMapper.CreateParameter(settings, paramType, scriptComp);
                if (param != null)
                {
                    // Register parameter
                    if (isInput)
                    {
                        ghComp.Params.RegisterInputParam(param);
                    }
                    else
                    {
                        ghComp.Params.RegisterOutputParam(param);
                    }

                    var registered = isInput ? ghComp.Params.Input[index] : ghComp.Params.Output[index];

                    // Apply type hint if specified
                    if (!string.IsNullOrEmpty(settings.TypeHint))
                    {
                        ScriptParameterMapper.ApplyTypeHintToParameter(registered, settings.TypeHint, scriptComp);
                        Debug.WriteLine($"[ScriptModifier] Applied type hint '{settings.TypeHint}' to {paramType} '{registered.Name}'");
                    }

                    // Apply data mapping if specified
                    ApplyParameterDataMapping(registered, settings);

                    // Apply additional settings (reverse, simplify, etc.)
                    ApplyAdditionalSettings(registered, settings);

                    Debug.WriteLine($"[ScriptModifier] Registered {paramType} parameter '{registered.Name}'");
                    index++;
                }
            }

            Debug.WriteLine($"[ScriptModifier] Updated {settingsList.Count} {paramType} parameters");
        }

        /// <summary>
        /// Adds a single input parameter to a script component.
        /// </summary>
        public static void AddInputParameter(
            IScriptComponent scriptComp,
            string name,
            string typeHint = "object",
            string access = "item",
            string description = "",
            bool optional = true)
        {
            if (scriptComp == null)
                throw new ArgumentNullException(nameof(scriptComp));

            var ghComp = scriptComp as IGH_Component;
            if (ghComp == null) return;

            var settings = new ParameterSettings
            {
                ParameterName = name,
                VariableName = name,
                TypeHint = typeHint,
                Access = access,
            };

            var param = ScriptParameterMapper.CreateParameter(settings, "input", scriptComp);
            if (param != null)
            {
                param.Description = description;
                param.Optional = optional;
                ghComp.Params.RegisterInputParam(param);

                if (!string.IsNullOrEmpty(typeHint))
                {
                    ScriptParameterMapper.ApplyTypeHintToParameter(param, typeHint, scriptComp);
                }

                Debug.WriteLine($"[ScriptModifier] Added input parameter '{name}' with type '{typeHint}'");
            }

            RefreshScriptComponent(scriptComp);
        }

        /// <summary>
        /// Adds a single output parameter to a script component.
        /// </summary>
        public static void AddOutputParameter(
            IScriptComponent scriptComp,
            string name,
            string typeHint = "object",
            string description = "")
        {
            if (scriptComp == null)
                throw new ArgumentNullException(nameof(scriptComp));

            var ghComp = scriptComp as IGH_Component;
            if (ghComp == null) return;

            var settings = new ParameterSettings
            {
                ParameterName = name,
                VariableName = name,
                TypeHint = typeHint,
                Access = "item",
            };

            var param = ScriptParameterMapper.CreateParameter(settings, "output", scriptComp);
            if (param != null)
            {
                param.Description = description;
                ghComp.Params.RegisterOutputParam(param);

                if (!string.IsNullOrEmpty(typeHint))
                {
                    ScriptParameterMapper.ApplyTypeHintToParameter(param, typeHint, scriptComp);
                }

                Debug.WriteLine($"[ScriptModifier] Added output parameter '{name}' with type '{typeHint}'");
            }

            RefreshScriptComponent(scriptComp);
        }

        /// <summary>
        /// Removes an input parameter by index.
        /// </summary>
        public static void RemoveInputParameter(IScriptComponent scriptComp, int index)
        {
            if (scriptComp == null)
                throw new ArgumentNullException(nameof(scriptComp));

            var ghComp = scriptComp as IGH_Component;
            if (ghComp == null || index < 0 || index >= ghComp.Params.Input.Count)
                return;

            var param = ghComp.Params.Input[index];
            ghComp.Params.UnregisterInputParameter(param);
            RefreshScriptComponent(scriptComp);

            Debug.WriteLine($"[ScriptModifier] Removed input parameter at index {index}");
        }

        /// <summary>
        /// Removes an output parameter by index.
        /// </summary>
        public static void RemoveOutputParameter(IScriptComponent scriptComp, int index)
        {
            if (scriptComp == null)
                throw new ArgumentNullException(nameof(scriptComp));

            var ghComp = scriptComp as IGH_Component;
            if (ghComp == null || index < 0 || index >= ghComp.Params.Output.Count)
                return;

            var param = ghComp.Params.Output[index];
            ghComp.Params.UnregisterOutputParameter(param);
            RefreshScriptComponent(scriptComp);

            Debug.WriteLine($"[ScriptModifier] Removed output parameter at index {index}");
        }

        #endregion

        #region Type Hint Management

        /// <summary>
        /// Changes the type hint of an existing input parameter.
        /// </summary>
        public static void SetInputTypeHint(IScriptComponent scriptComp, int index, string typeHint)
        {
            if (scriptComp == null)
                throw new ArgumentNullException(nameof(scriptComp));

            var ghComp = scriptComp as IGH_Component;
            if (ghComp == null || index < 0 || index >= ghComp.Params.Input.Count)
                return;

            var param = ghComp.Params.Input[index];
            ScriptParameterMapper.ApplyTypeHintToParameter(param, typeHint, scriptComp);
            RefreshScriptComponent(scriptComp);

            Debug.WriteLine($"[ScriptModifier] Set type hint '{typeHint}' for input at index {index}");
        }

        /// <summary>
        /// Changes the type hint of an existing output parameter.
        /// </summary>
        public static void SetOutputTypeHint(IScriptComponent scriptComp, int index, string typeHint)
        {
            if (scriptComp == null)
                throw new ArgumentNullException(nameof(scriptComp));

            var ghComp = scriptComp as IGH_Component;
            if (ghComp == null || index < 0 || index >= ghComp.Params.Output.Count)
                return;

            var param = ghComp.Params.Output[index];
            ScriptParameterMapper.ApplyTypeHintToParameter(param, typeHint, scriptComp);
            RefreshScriptComponent(scriptComp);

            Debug.WriteLine($"[ScriptModifier] Set type hint '{typeHint}' for output at index {index}");
        }

        #endregion

        #region Access Type Management

        /// <summary>
        /// Changes the access type (item/list/tree) of an input parameter.
        /// </summary>
        public static void SetInputAccess(IScriptComponent scriptComp, int index, GH_ParamAccess access)
        {
            if (scriptComp == null)
                throw new ArgumentNullException(nameof(scriptComp));

            var ghComp = scriptComp as IGH_Component;
            if (ghComp == null || index < 0 || index >= ghComp.Params.Input.Count)
                return;

            var param = ghComp.Params.Input[index];
            if (param is ScriptVariableParam svp)
            {
                svp.Access = access;
                RefreshScriptComponent(scriptComp);
                Debug.WriteLine($"[ScriptModifier] Set access '{access}' for input at index {index}");
            }
        }

        #endregion

        #region Script Component State

        /// <summary>
        /// Sets whether the standard output parameter ("out") is visible.
        /// Applies to Python and C# script components.
        /// </summary>
        public static void SetShowStandardOutput(IScriptComponent scriptComp, bool show)
        {
            if (scriptComp == null)
                throw new ArgumentNullException(nameof(scriptComp));

            try
            {
                var compType = scriptComp.GetType();
                var usingStdOutputProp = compType.GetProperty("UsingStandardOutputParam");

                if (usingStdOutputProp != null && usingStdOutputProp.CanWrite)
                {
                    bool currentValue = (bool)usingStdOutputProp.GetValue(scriptComp);

                    // Force application by toggling if values match
                    if (currentValue == show)
                    {
                        usingStdOutputProp.SetValue(scriptComp, !show);

                        if (scriptComp is IGH_VariableParameterComponent varParamComp)
                        {
                            varParamComp.VariableParameterMaintenance();
                        }
                    }

                    // Set to desired value
                    usingStdOutputProp.SetValue(scriptComp, show);

                    RefreshScriptComponent(scriptComp);

                    Debug.WriteLine($"[ScriptModifier] Set ShowStandardOutput to '{show}'");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScriptModifier] Error setting ShowStandardOutput: {ex.Message}");
            }
        }

        /// <summary>
        /// Sets the principal (master) input parameter index.
        /// The principal parameter determines component iteration behavior.
        /// </summary>
        public static void SetPrincipalInput(IScriptComponent scriptComp, int index)
        {
            if (scriptComp == null)
                throw new ArgumentNullException(nameof(scriptComp));

            var ghComp = scriptComp as IGH_Component;
            if (ghComp == null || index < 0 || index >= ghComp.Params.Input.Count)
                return;

            ghComp.MasterParameterIndex = index;
            RefreshScriptComponent(scriptComp);

            Debug.WriteLine($"[ScriptModifier] Set principal input to index {index}");
        }

        #endregion

        #region Parameter Properties

        /// <summary>
        /// Sets whether an input parameter is optional.
        /// </summary>
        public static void SetInputOptional(IScriptComponent scriptComp, int index, bool optional)
        {
            if (scriptComp == null)
                throw new ArgumentNullException(nameof(scriptComp));

            var ghComp = scriptComp as IGH_Component;
            if (ghComp == null || index < 0 || index >= ghComp.Params.Input.Count)
                return;

            var param = ghComp.Params.Input[index];
            param.Optional = optional;

            Debug.WriteLine($"[ScriptModifier] Set input {index} optional to '{optional}'");
        }

        /// <summary>
        /// Sets the description of an input parameter.
        /// </summary>
        public static void SetInputDescription(IScriptComponent scriptComp, int index, string description)
        {
            if (scriptComp == null)
                throw new ArgumentNullException(nameof(scriptComp));

            var ghComp = scriptComp as IGH_Component;
            if (ghComp == null || index < 0 || index >= ghComp.Params.Input.Count)
                return;

            var param = ghComp.Params.Input[index];
            param.Description = description ?? string.Empty;

            Debug.WriteLine($"[ScriptModifier] Set input {index} description");
        }

        /// <summary>
        /// Sets the description of an output parameter.
        /// </summary>
        public static void SetOutputDescription(IScriptComponent scriptComp, int index, string description)
        {
            if (scriptComp == null)
                throw new ArgumentNullException(nameof(scriptComp));

            var ghComp = scriptComp as IGH_Component;
            if (ghComp == null || index < 0 || index >= ghComp.Params.Output.Count)
                return;

            var param = ghComp.Params.Output[index];
            param.Description = description ?? string.Empty;

            Debug.WriteLine($"[ScriptModifier] Set output {index} description");
        }

        /// <summary>
        /// Renames an input parameter.
        /// </summary>
        public static void RenameInput(IScriptComponent scriptComp, int index, string newName)
        {
            if (scriptComp == null)
                throw new ArgumentNullException(nameof(scriptComp));

            var ghComp = scriptComp as IGH_Component;
            if (ghComp == null || index < 0 || index >= ghComp.Params.Input.Count)
                return;

            var param = ghComp.Params.Input[index];
            param.NickName = newName ?? "input";

            if (param is ScriptVariableParam svp)
            {
                svp.Name = newName ?? "input";
            }

            RefreshScriptComponent(scriptComp);
            Debug.WriteLine($"[ScriptModifier] Renamed input {index} to '{newName}'");
        }

        /// <summary>
        /// Renames an output parameter.
        /// </summary>
        public static void RenameOutput(IScriptComponent scriptComp, int index, string newName)
        {
            if (scriptComp == null)
                throw new ArgumentNullException(nameof(scriptComp));

            var ghComp = scriptComp as IGH_Component;
            if (ghComp == null || index < 0 || index >= ghComp.Params.Output.Count)
                return;

            var param = ghComp.Params.Output[index];
            param.NickName = newName ?? "output";

            if (param is ScriptVariableParam svp)
            {
                svp.Name = newName ?? "output";
            }

            RefreshScriptComponent(scriptComp);
            Debug.WriteLine($"[ScriptModifier] Renamed output {index} to '{newName}'");
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Converts AI format (JArray) to ParameterSettings list.
        /// </summary>
        private static List<ParameterSettings> ConvertToParameterSettings(JArray parameters, bool isInput)
        {
            var settings = new List<ParameterSettings>();

            foreach (var param in parameters)
            {
                var setting = new ParameterSettings
                {
                    ParameterName = param["name"]?.ToString() ?? (isInput ? "input" : "output"),
                    VariableName = param["name"]?.ToString() ?? (isInput ? "input" : "output"),
                    TypeHint = param["type"]?.ToString(),
                    Access = param["access"]?.ToString() ?? "item",
                    DataMapping = null,
                };

                // Normalize access value
                var access = param["access"]?.ToString()?.ToLowerInvariant();
                setting.Access = access switch
                {
                    "list" => "list",
                    "tree" => "tree",
                    _ => "item"
                };

                settings.Add(setting);
            }

            return settings;
        }

        /// <summary>
        /// Applies data mapping (Flatten/Graft) to a parameter.
        /// </summary>
        private static void ApplyParameterDataMapping(IGH_Param param, ParameterSettings settings)
        {
            if (!string.IsNullOrEmpty(settings.DataMapping))
            {
                if (Enum.TryParse<GH_DataMapping>(settings.DataMapping, true, out var dataMapping))
                {
                    param.DataMapping = dataMapping;
                    Debug.WriteLine($"[ScriptModifier] Applied data mapping '{dataMapping}' to parameter '{param.Name}'");
                }
            }
        }

        /// <summary>
        /// Applies additional parameter settings (reverse, simplify, locked).
        /// </summary>
        private static void ApplyAdditionalSettings(IGH_Param param, ParameterSettings settings)
        {
            if (settings?.AdditionalSettings == null)
                return;

            if (settings.AdditionalSettings.Reverse == true)
            {
                param.Reverse = true;
                Debug.WriteLine($"[ScriptModifier] Applied Reverse to parameter '{param.Name}'");
            }

            if (settings.AdditionalSettings.Simplify == true)
            {
                param.Simplify = true;
                Debug.WriteLine($"[ScriptModifier] Applied Simplify to parameter '{param.Name}'");
            }

            if (settings.AdditionalSettings.Locked == true)
            {
                param.Locked = true;
                Debug.WriteLine($"[ScriptModifier] Applied Locked to parameter '{param.Name}'");
            }
        }

        /// <summary>
        /// Refreshes the script component after modifications.
        /// </summary>
        private static void RefreshScriptComponent(IScriptComponent scriptComp)
        {
            var ghComp = scriptComp as IGH_Component;
            if (ghComp == null) return;

            // Trigger parameter maintenance for variable parameter components
            if (ghComp is IGH_VariableParameterComponent varParamComp)
            {
                varParamComp.VariableParameterMaintenance();
            }

            // Recreate attributes and expire solution
            ghComp.CreateAttributes();
            ghComp.ExpireSolution(true);

            Debug.WriteLine($"[ScriptModifier] Refreshed script component '{ghComp.Name}'");
        }

        #endregion
    }
}
