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
using GhJSON.Core;
using GhJSON.Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Special;
using Newtonsoft.Json.Linq;
using RhinoCodePlatform.GH;
using RhinoCodePluginGH.Parameters;
using SmartHopper.Core.Grasshopper.Utils.Canvas;
using SmartHopper.Core.Grasshopper.Utils.Internal;
using SmartHopper.Infrastructure.Dialogs;

namespace SmartHopper.Core.Grasshopper.Utils.Components
{
    /// <summary>
    /// Specialized modifier for script components (Python, C#, VB, IronPython).
    /// Handles the unique complexity of script parameters, type hints, and code updates.
    /// </summary>
    public static class ScriptModifier
    {
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
            {
                throw new ArgumentNullException(nameof(scriptComp));
            }

            var ghComp = scriptComp as IGH_Component;
            if (ghComp == null)
            {
                return;
            }

            // Create a generic parameter. The script component itself is responsible for:
            // 1. Binding variable names to parameters
            // 2. Applying type hints via reflection (TypeHint property)
            // 3. Setting access modes (item/list/tree)
            // This is handled by the script component's internal parameter management system.
            IGH_Param param = new Param_GenericObject
            {
                Name = name,
                NickName = name,
                Description = description ?? string.Empty,
                Optional = optional,
            };

            ghComp.Params.RegisterInputParam(param);
            Debug.WriteLine($"[ScriptModifier] Added input parameter '{name}' with type hint '{typeHint}'");

            // Apply type hint if the parameter supports it
            if (!string.IsNullOrWhiteSpace(typeHint) && !string.Equals(typeHint, "object", StringComparison.OrdinalIgnoreCase))
            {
                TrySetTypeHint(param, typeHint);
            }

            // Apply access mode
            SetInputAccess(scriptComp, ghComp.Params.Input.Count - 1, ParseAccess(access));

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
            {
                throw new ArgumentNullException(nameof(scriptComp));
            }

            var ghComp = scriptComp as IGH_Component;
            if (ghComp == null)
            {
                return;
            }

            // Create a generic output parameter. Type hints are applied via reflection.
            IGH_Param param = new Param_GenericObject
            {
                Name = name,
                NickName = name,
                Description = description ?? string.Empty,
            };

            ghComp.Params.RegisterOutputParam(param);
            Debug.WriteLine($"[ScriptModifier] Added output parameter '{name}' with type hint '{typeHint}'");

            // Apply type hint if the parameter supports it
            if (!string.IsNullOrWhiteSpace(typeHint) && !string.Equals(typeHint, "object", StringComparison.OrdinalIgnoreCase))
            {
                TrySetTypeHint(param, typeHint);
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
            TrySetTypeHint(param, typeHint);
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
            TrySetTypeHint(param, typeHint);
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

        #endregion

        private static GH_ParamAccess ParseAccess(string access)
        {
            if (string.IsNullOrWhiteSpace(access))
            {
                return GH_ParamAccess.item;
            }

            switch (access.Trim().ToLowerInvariant())
            {
                case "list":
                    return GH_ParamAccess.list;
                case "tree":
                    return GH_ParamAccess.tree;
                default:
                    return GH_ParamAccess.item;
            }
        }

        private static void TrySetTypeHint(IGH_Param param, string typeHint)
        {
            if (param == null || string.IsNullOrWhiteSpace(typeHint))
            {
                return;
            }

            try
            {
                var typeHintProp = param.GetType().GetProperty("TypeHint");
                if (typeHintProp != null && typeHintProp.CanWrite)
                {
                    typeHintProp.SetValue(param, typeHint);
                    Debug.WriteLine($"[ScriptModifier] Applied type hint '{typeHint}' to parameter '{param.Name}'");
                }
                else
                {
                    Debug.WriteLine($"[ScriptModifier] Parameter '{param.Name}' does not support TypeHint property");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScriptModifier] Failed to set type hint '{typeHint}' on parameter '{param.Name}': {ex.Message}");
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
    }
}
