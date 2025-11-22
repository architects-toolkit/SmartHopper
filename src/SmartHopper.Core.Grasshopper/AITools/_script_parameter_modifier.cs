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
using System.Threading.Tasks;
using Grasshopper;
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;
using Rhino;
using RhinoCodePlatform.GH;
using SmartHopper.Core.Grasshopper.Serialization.GhJson.ScriptComponents;
using SmartHopper.Core.Grasshopper.Utils.Canvas;
using SmartHopper.Core.Grasshopper.Utils.Components;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.AITools;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// AI tools for modifying script component parameters using ScriptModifier.
    /// </summary>
    public class script_parameter_modifier : IAIToolProvider
    {
        private readonly AICapability toolCapabilityRequirements = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput;

        /// <summary>
        /// Get all tools provided by this class.
        /// </summary>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: "script_parameter_add_input",
                description: "Add a new input parameter to a script component",
                category: "Scripting",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""scriptGuid"": { ""type"": ""string"", ""description"": ""GUID of the script component"" },
                        ""name"": { ""type"": ""string"", ""description"": ""Name of the parameter"" },
                        ""typeHint"": { ""type"": ""string"", ""description"": ""Type hint (Point3d, Curve, double, etc.)"", ""default"": ""object"" },
                        ""access"": { ""type"": ""string"", ""description"": ""item, list, or tree"", ""default"": ""item"" },
                        ""description"": { ""type"": ""string"", ""description"": ""Parameter description"", ""default"": """" },
                        ""optional"": { ""type"": ""boolean"", ""description"": ""Is parameter optional"", ""default"": true }
                    },
                    ""required"": [""scriptGuid"", ""name""]
                }",
                execute: this.AddInputParameterAsync,
                requiredCapabilities: this.toolCapabilityRequirements);

            yield return new AITool(
                name: "script_parameter_add_output",
                description: "Add a new output parameter to a script component",
                category: "Scripting",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""scriptGuid"": { ""type"": ""string"", ""description"": ""GUID of the script component"" },
                        ""name"": { ""type"": ""string"", ""description"": ""Name of the parameter"" },
                        ""typeHint"": { ""type"": ""string"", ""description"": ""Type hint (Point3d, Curve, etc.)"", ""default"": ""object"" },
                        ""description"": { ""type"": ""string"", ""description"": ""Parameter description"", ""default"": """" }
                    },
                    ""required"": [""scriptGuid"", ""name""]
                }",
                execute: this.AddOutputParameterAsync,
                requiredCapabilities: this.toolCapabilityRequirements);

            yield return new AITool(
                name: "script_parameter_remove_input",
                description: "Remove an input parameter from a script component",
                category: "Scripting",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""scriptGuid"": { ""type"": ""string"", ""description"": ""GUID of the script component"" },
                        ""index"": { ""type"": ""integer"", ""description"": ""Index of the parameter to remove (0-based)"" }
                    },
                    ""required"": [""scriptGuid"", ""index""]
                }",
                execute: this.RemoveInputParameterAsync,
                requiredCapabilities: this.toolCapabilityRequirements);

            yield return new AITool(
                name: "script_parameter_remove_output",
                description: "Remove an output parameter from a script component",
                category: "Scripting",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""scriptGuid"": { ""type"": ""string"", ""description"": ""GUID of the script component"" },
                        ""index"": { ""type"": ""integer"", ""description"": ""Index of the parameter to remove (0-based)"" }
                    },
                    ""required"": [""scriptGuid"", ""index""]
                }",
                execute: this.RemoveOutputParameterAsync,
                requiredCapabilities: this.toolCapabilityRequirements);

            yield return new AITool(
                name: "script_parameter_set_type_input",
                description: "Set the type hint for a script component input parameter",
                category: "Scripting",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""scriptGuid"": { ""type"": ""string"", ""description"": ""GUID of the script component"" },
                        ""index"": { ""type"": ""integer"", ""description"": ""Index of the input parameter (0-based)"" },
                        ""typeHint"": { ""type"": ""string"", ""description"": ""Type hint to apply"" }
                    },
                    ""required"": [""scriptGuid"", ""index"", ""typeHint""]
                }",
                execute: this.SetInputTypeHintAsync,
                requiredCapabilities: this.toolCapabilityRequirements);

            yield return new AITool(
                name: "script_parameter_set_type_output",
                description: "Set the type hint for a script component output parameter",
                category: "Scripting",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""scriptGuid"": { ""type"": ""string"", ""description"": ""GUID of the script component"" },
                        ""index"": { ""type"": ""integer"", ""description"": ""Index of the output parameter (0-based)"" },
                        ""typeHint"": { ""type"": ""string"", ""description"": ""Type hint to apply"" }
                    },
                    ""required"": [""scriptGuid"", ""index"", ""typeHint""]
                }",
                execute: this.SetOutputTypeHintAsync,
                requiredCapabilities: this.toolCapabilityRequirements);

            yield return new AITool(
                name: "script_parameter_set_access",
                description: "Set how an input parameter receives data (item/list/tree)",
                category: "Scripting",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""scriptGuid"": { ""type"": ""string"", ""description"": ""GUID of the script component"" },
                        ""index"": { ""type"": ""integer"", ""description"": ""Index of the input parameter (0-based)"" },
                        ""access"": { ""type"": ""string"", ""description"": ""item, list, or tree"" }
                    },
                    ""required"": [""scriptGuid"", ""index"", ""access""]
                }",
                execute: this.SetInputAccessAsync,
                requiredCapabilities: this.toolCapabilityRequirements);

            yield return new AITool(
                name: "script_toggle_std_output",
                description: "Show or hide the standard output parameter ('out') in a script component",
                category: "Scripting",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""scriptGuid"": { ""type"": ""string"", ""description"": ""GUID of the script component"" },
                        ""show"": { ""type"": ""boolean"", ""description"": ""true to show, false to hide"" }
                    },
                    ""required"": [""scriptGuid"", ""show""]
                }",
                execute: this.ToggleStandardOutputAsync,
                requiredCapabilities: this.toolCapabilityRequirements);

            yield return new AITool(
                name: "script_set_principal_input",
                description: "Set which input parameter drives the component's iteration",
                category: "Scripting",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""scriptGuid"": { ""type"": ""string"", ""description"": ""GUID of the script component"" },
                        ""index"": { ""type"": ""integer"", ""description"": ""Index of the input to set as principal (0-based)"" }
                    },
                    ""required"": [""scriptGuid"", ""index""]
                }",
                execute: this.SetPrincipalInputAsync,
                requiredCapabilities: this.toolCapabilityRequirements); // TODO: move to component modifiers

            yield return new AITool(
                name: "script_parameter_set_optional",
                description: "Set whether a script input parameter is required or optional",
                category: "Scripting",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""scriptGuid"": { ""type"": ""string"", ""description"": ""GUID of the script component"" },
                        ""index"": { ""type"": ""integer"", ""description"": ""Index of the input parameter (0-based)"" },
                        ""optional"": { ""type"": ""boolean"", ""description"": ""true for optional, false for required"" }
                    },
                    ""required"": [""scriptGuid"", ""index"", ""optional""]
                }",
                execute: this.SetInputOptionalAsync,
                requiredCapabilities: this.toolCapabilityRequirements);
        }

        private async Task<AIReturn> AddInputParameterAsync(AIToolCall toolCall) => await ExecuteScriptModification(toolCall, "script_parameter_add_input", args => ExecuteScriptOp(args, (scriptComp, comp) => {
            var name = args["name"]?.ToString() ?? throw new ArgumentException("Missing name");
            var typeHint = args["typeHint"]?.ToString() ?? "object";
            var access = args["access"]?.ToString() ?? "item";
            var description = args["description"]?.ToString() ?? "";
            var optional = args["optional"]?.ToObject<bool>() ?? true;
            ScriptModifier.AddInputParameter(scriptComp, name, typeHint, access, description, optional);
            return $"Added input '{name}' ({typeHint}, {access})";
        }));

        private async Task<AIReturn> AddOutputParameterAsync(AIToolCall toolCall) => await ExecuteScriptModification(toolCall, "script_parameter_add_output", args => ExecuteScriptOp(args, (scriptComp, comp) => {
            var name = args["name"]?.ToString() ?? throw new ArgumentException("Missing name");
            var typeHint = args["typeHint"]?.ToString() ?? "object";
            var description = args["description"]?.ToString() ?? "";
            ScriptModifier.AddOutputParameter(scriptComp, name, typeHint, description);
            return $"Added output '{name}' ({typeHint})";
        }));

        private async Task<AIReturn> RemoveInputParameterAsync(AIToolCall toolCall) => await ExecuteScriptModification(toolCall, "script_parameter_remove_input", args => ExecuteScriptOp(args, (scriptComp, comp) => {
            var index = args["index"]?.ToObject<int>() ?? throw new ArgumentException("Missing index");
            if (index < 0 || index >= comp.Params.Input.Count) throw new ArgumentException($"Index {index} out of range");
            var paramName = comp.Params.Input[index].Name;
            ScriptModifier.RemoveInputParameter(scriptComp, index);
            return $"Removed input '{paramName}' at index {index}";
        }));

        private async Task<AIReturn> RemoveOutputParameterAsync(AIToolCall toolCall) => await ExecuteScriptModification(toolCall, "script_parameter_remove_output", args => ExecuteScriptOp(args, (scriptComp, comp) => {
            var index = args["index"]?.ToObject<int>() ?? throw new ArgumentException("Missing index");
            if (index < 0 || index >= comp.Params.Output.Count) throw new ArgumentException($"Index {index} out of range");
            var paramName = comp.Params.Output[index].Name;
            ScriptModifier.RemoveOutputParameter(scriptComp, index);
            return $"Removed output '{paramName}' at index {index}";
        }));

        private async Task<AIReturn> SetInputTypeHintAsync(AIToolCall toolCall) => await ExecuteScriptModification(toolCall, "script_parameter_set_type_input", args => ExecuteScriptOp(args, (scriptComp, comp) => {
            var index = args["index"]?.ToObject<int>() ?? throw new ArgumentException("Missing index");
            var typeHint = args["typeHint"]?.ToString() ?? throw new ArgumentException("Missing typeHint");
            if (index < 0 || index >= comp.Params.Input.Count) throw new ArgumentException($"Index {index} out of range");
            var paramName = comp.Params.Input[index].Name;
            ScriptModifier.SetInputTypeHint(scriptComp, index, typeHint);
            return $"Set type hint '{typeHint}' for input '{paramName}'";
        }));

        private async Task<AIReturn> SetOutputTypeHintAsync(AIToolCall toolCall) => await ExecuteScriptModification(toolCall, "script_parameter_set_type_output", args => ExecuteScriptOp(args, (scriptComp, comp) => {
            var index = args["index"]?.ToObject<int>() ?? throw new ArgumentException("Missing index");
            var typeHint = args["typeHint"]?.ToString() ?? throw new ArgumentException("Missing typeHint");
            if (index < 0 || index >= comp.Params.Output.Count) throw new ArgumentException($"Index {index} out of range");
            var paramName = comp.Params.Output[index].Name;
            ScriptModifier.SetOutputTypeHint(scriptComp, index, typeHint);
            return $"Set type hint '{typeHint}' for output '{paramName}'";
        }));

        private async Task<AIReturn> SetInputAccessAsync(AIToolCall toolCall) => await ExecuteScriptModification(toolCall, "script_parameter_set_access", args => ExecuteScriptOp(args, (scriptComp, comp) => {
            var index = args["index"]?.ToObject<int>() ?? throw new ArgumentException("Missing index");
            var access = args["access"]?.ToString() ?? throw new ArgumentException("Missing access");
            if (index < 0 || index >= comp.Params.Input.Count) throw new ArgumentException($"Index {index} out of range");
            var accessType = access.ToLower() switch {
                "item" => GH_ParamAccess.item,
                "list" => GH_ParamAccess.list,
                "tree" => GH_ParamAccess.tree,
                _ => throw new ArgumentException($"Invalid access '{access}'")
            };
            var paramName = comp.Params.Input[index].Name;
            ScriptModifier.SetInputAccess(scriptComp, index, accessType);
            return $"Set access '{access}' for input '{paramName}'";
        }));

        private async Task<AIReturn> ToggleStandardOutputAsync(AIToolCall toolCall) => await ExecuteScriptModification(toolCall, "script_toggle_std_output", args => ExecuteScriptOp(args, (scriptComp, comp) => {
            var show = args["show"]?.ToObject<bool>() ?? throw new ArgumentException("Missing show");
            ScriptModifier.SetShowStandardOutput(scriptComp, show);
            return $"{(show ? "Showed" : "Hid")} standard output parameter";
        }));

        private async Task<AIReturn> SetPrincipalInputAsync(AIToolCall toolCall) => await ExecuteScriptModification(toolCall, "script_set_principal_input", args => ExecuteScriptOp(args, (scriptComp, comp) => {
            var index = args["index"]?.ToObject<int>() ?? throw new ArgumentException("Missing index");
            if (index < 0 || index >= comp.Params.Input.Count) throw new ArgumentException($"Index {index} out of range");
            var paramName = comp.Params.Input[index].Name;
            ScriptModifier.SetPrincipalInput(scriptComp, index);
            return $"Set principal input to '{paramName}' at index {index}";
        }));

        private async Task<AIReturn> SetInputOptionalAsync(AIToolCall toolCall) => await ExecuteScriptModification(toolCall, "script_parameter_set_optional", args => ExecuteScriptOp(args, (scriptComp, comp) => {
            var index = args["index"]?.ToObject<int>() ?? throw new ArgumentException("Missing index");
            var optional = args["optional"]?.ToObject<bool>() ?? throw new ArgumentException("Missing optional");
            if (index < 0 || index >= comp.Params.Input.Count) throw new ArgumentException($"Index {index} out of range");
            var paramName = comp.Params.Input[index].Name;
            ScriptModifier.SetInputOptional(scriptComp, index, optional);
            return $"Set input '{paramName}' to {(optional ? "optional" : "required")}";
        }));

        private string ExecuteScriptOp(JObject args, Func<IScriptComponent, IGH_Component, string> operation)
        {
            var scriptGuid = Guid.Parse(args["scriptGuid"]?.ToString() ?? throw new ArgumentException("Missing scriptGuid"));
            var obj = CanvasAccess.FindInstance(scriptGuid);
            if (obj == null) throw new ArgumentException("Script component not found");
            if (!(obj is IScriptComponent scriptComp)) throw new ArgumentException("Object is not a script component");
            var comp = scriptComp as IGH_Component;
            comp?.RecordUndoEvent("[SH] Modify Script Parameter");
            var result = operation(scriptComp, comp);
            comp?.ExpireSolution(true);
            Instances.RedrawCanvas();
            return result;
        }

        private async Task<AIReturn> ExecuteScriptModification(AIToolCall toolCall, string toolName, Func<JObject, string> operation)
        {
            var output = new AIReturn() { Request = toolCall };
            try
            {
                var toolInfo = toolCall.GetToolCall();
                var args = toolInfo.Arguments ?? new JObject();
                var tcs = new TaskCompletionSource<AIReturn>();
                RhinoApp.InvokeOnUiThread(() => {
                    try {
                        string message = operation(args);
                        Debug.WriteLine($"[{toolName}] {message}");
                        var body = AIBodyBuilder.Create()
                            .AddToolResult(new JObject
                            {
                                ["tool"] = toolName,
                                ["result"] = message,
                            })
                            .Build();
                        output.CreateSuccess(body, toolCall);
                        tcs.SetResult(output);
                    } catch (Exception ex) {
                        tcs.SetException(ex);
                    }
                });
                return await tcs.Task;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{toolName}] Error: {ex.Message}");
                output.CreateError(ex.Message);
                return output;
            }
        }
    }
}
