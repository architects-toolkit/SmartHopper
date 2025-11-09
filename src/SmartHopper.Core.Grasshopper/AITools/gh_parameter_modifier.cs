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
using System.Linq;
using System.Threading.Tasks;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Newtonsoft.Json.Linq;
using Rhino;
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
    /// AI tools for modifying parameter data settings on Grasshopper components.
    /// Uses ParameterModifier for clean, reusable parameter manipulation.
    /// </summary>
    public class gh_parameter_modifier : IAIToolProvider
    {
        private readonly string toolName = "gh_parameter_modifier";
        private readonly AICapability toolCapabilityRequirements = AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput;

        /// <summary>
        /// Get all tools provided by this class.
        /// </summary>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: "gh_parameter_flatten",
                description: "Flatten a parameter's data tree into a single list",
                category: "ComponentModification",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""componentGuid"": { ""type"": ""string"", ""description"": ""GUID of the component"" },
                        ""parameterIndex"": { ""type"": ""integer"", ""description"": ""Index of the parameter (0-based)"" },
                        ""isInput"": { ""type"": ""boolean"", ""description"": ""true for input, false for output"", ""default"": true }
                    },
                    ""required"": [""componentGuid"", ""parameterIndex""]
                }",
                execute: this.FlattenParameterAsync,
                requiredCapabilities: this.toolCapabilityRequirements);

            yield return new AITool(
                name: "gh_parameter_graft",
                description: "Graft a parameter to add an extra branch level to the data tree",
                category: "ComponentModification",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""componentGuid"": { ""type"": ""string"", ""description"": ""GUID of the component"" },
                        ""parameterIndex"": { ""type"": ""integer"", ""description"": ""Index of the parameter (0-based)"" },
                        ""isInput"": { ""type"": ""boolean"", ""description"": ""true for input, false for output"", ""default"": true }
                    },
                    ""required"": [""componentGuid"", ""parameterIndex""]
                }",
                execute: this.GraftParameterAsync,
                requiredCapabilities: this.toolCapabilityRequirements);

            yield return new AITool(
                name: "gh_parameter_reverse",
                description: "Reverse the order of items in a parameter",
                category: "ComponentModification",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""componentGuid"": { ""type"": ""string"", ""description"": ""GUID of the component"" },
                        ""parameterIndex"": { ""type"": ""integer"", ""description"": ""Index of the parameter (0-based)"" },
                        ""isInput"": { ""type"": ""boolean"", ""description"": ""true for input, false for output"", ""default"": true },
                        ""enable"": { ""type"": ""boolean"", ""description"": ""true to reverse, false to disable"", ""default"": true }
                    },
                    ""required"": [""componentGuid"", ""parameterIndex""]
                }",
                execute: this.ReverseParameterAsync,
                requiredCapabilities: this.toolCapabilityRequirements);

            yield return new AITool(
                name: "gh_parameter_simplify",
                description: "Simplify geometry in a parameter (removes redundant control points)",
                category: "ComponentModification",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""componentGuid"": { ""type"": ""string"", ""description"": ""GUID of the component"" },
                        ""parameterIndex"": { ""type"": ""integer"", ""description"": ""Index of the parameter (0-based)"" },
                        ""isInput"": { ""type"": ""boolean"", ""description"": ""true for input, false for output"", ""default"": true },
                        ""enable"": { ""type"": ""boolean"", ""description"": ""true to simplify, false to disable"", ""default"": true }
                    },
                    ""required"": [""componentGuid"", ""parameterIndex""]
                }",
                execute: this.SimplifyParameterAsync,
                requiredCapabilities: this.toolCapabilityRequirements);

            yield return new AITool(
                name: "gh_parameter_bulk_inputs",
                description: "Apply data settings to all input parameters of a component",
                category: "ComponentModification",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""componentGuid"": { ""type"": ""string"", ""description"": ""GUID of the component"" },
                        ""flatten"": { ""type"": ""boolean"", ""description"": ""Flatten all inputs"" },
                        ""graft"": { ""type"": ""boolean"", ""description"": ""Graft all inputs"" },
                        ""reverse"": { ""type"": ""boolean"", ""description"": ""Reverse all inputs"" },
                        ""simplify"": { ""type"": ""boolean"", ""description"": ""Simplify all inputs"" }
                    },
                    ""required"": [""componentGuid""]
                }",
                execute: this.BulkModifyInputsAsync,
                requiredCapabilities: this.toolCapabilityRequirements);

            yield return new AITool(
                name: "gh_parameter_bulk_outputs",
                description: "Apply data settings to all output parameters of a component",
                category: "ComponentModification",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""componentGuid"": { ""type"": ""string"", ""description"": ""GUID of the component"" },
                        ""flatten"": { ""type"": ""boolean"", ""description"": ""Flatten all outputs"" },
                        ""graft"": { ""type"": ""boolean"", ""description"": ""Graft all outputs"" },
                        ""reverse"": { ""type"": ""boolean"", ""description"": ""Reverse all outputs"" },
                        ""simplify"": { ""type"": ""boolean"", ""description"": ""Simplify all outputs"" }
                    },
                    ""required"": [""componentGuid""]
                }",
                execute: this.BulkModifyOutputsAsync,
                requiredCapabilities: this.toolCapabilityRequirements);
        }

        private async Task<AIReturn> FlattenParameterAsync(AIToolCall toolCall)
        {
            return await ExecuteParameterModification(toolCall, "gh_parameter_flatten", (args) =>
            {
                var componentGuid = Guid.Parse(args["componentGuid"]?.ToString() ?? throw new ArgumentException("Missing componentGuid"));
                var parameterIndex = args["parameterIndex"]?.ToObject<int>() ?? throw new ArgumentException("Missing parameterIndex");
                var isInput = args["isInput"]?.ToObject<bool>() ?? true;

                var obj = CanvasAccess.FindInstance(componentGuid);
                if (obj == null) throw new ArgumentException("Component not found");
                if (!(obj is IGH_Component component)) throw new ArgumentException("Object is not a component");

                var paramList = isInput ? component.Params.Input : component.Params.Output;
                if (parameterIndex < 0 || parameterIndex >= paramList.Count)
                    throw new ArgumentException($"Parameter index {parameterIndex} out of range");

                var param = paramList[parameterIndex];
                component.RecordUndoEvent("[SH] Flatten Parameter");
                ParameterModifier.SetDataMapping(param, GH_DataMapping.Flatten);
                component.ExpireSolution(true);
                Instances.RedrawCanvas();

                return $"Flattened {(isInput ? "input" : "output")} parameter '{param.Name}'";
            });
        }

        private async Task<AIReturn> GraftParameterAsync(AIToolCall toolCall)
        {
            return await ExecuteParameterModification(toolCall, "gh_parameter_graft", (args) =>
            {
                var componentGuid = Guid.Parse(args["componentGuid"]?.ToString() ?? throw new ArgumentException("Missing componentGuid"));
                var parameterIndex = args["parameterIndex"]?.ToObject<int>() ?? throw new ArgumentException("Missing parameterIndex");
                var isInput = args["isInput"]?.ToObject<bool>() ?? true;

                var obj = CanvasAccess.FindInstance(componentGuid);
                if (obj == null) throw new ArgumentException("Component not found");
                if (!(obj is IGH_Component component)) throw new ArgumentException("Object is not a component");

                var paramList = isInput ? component.Params.Input : component.Params.Output;
                if (parameterIndex < 0 || parameterIndex >= paramList.Count)
                    throw new ArgumentException($"Parameter index {parameterIndex} out of range");

                var param = paramList[parameterIndex];
                component.RecordUndoEvent("[SH] Graft Parameter");
                ParameterModifier.SetDataMapping(param, GH_DataMapping.Graft);
                component.ExpireSolution(true);
                Instances.RedrawCanvas();

                return $"Grafted {(isInput ? "input" : "output")} parameter '{param.Name}'";
            });
        }

        private async Task<AIReturn> ReverseParameterAsync(AIToolCall toolCall)
        {
            return await ExecuteParameterModification(toolCall, "gh_parameter_reverse", (args) =>
            {
                var componentGuid = Guid.Parse(args["componentGuid"]?.ToString() ?? throw new ArgumentException("Missing componentGuid"));
                var parameterIndex = args["parameterIndex"]?.ToObject<int>() ?? throw new ArgumentException("Missing parameterIndex");
                var isInput = args["isInput"]?.ToObject<bool>() ?? true;
                var enable = args["enable"]?.ToObject<bool>() ?? true;

                var obj = CanvasAccess.FindInstance(componentGuid);
                if (obj == null) throw new ArgumentException("Component not found");
                if (!(obj is IGH_Component component)) throw new ArgumentException("Object is not a component");

                var paramList = isInput ? component.Params.Input : component.Params.Output;
                if (parameterIndex < 0 || parameterIndex >= paramList.Count)
                    throw new ArgumentException($"Parameter index {parameterIndex} out of range");

                var param = paramList[parameterIndex];
                component.RecordUndoEvent("[SH] Reverse Parameter");
                ParameterModifier.SetReverse(param, enable);
                component.ExpireSolution(true);
                Instances.RedrawCanvas();

                return $"{(enable ? "Enabled" : "Disabled")} reverse for {(isInput ? "input" : "output")} parameter '{param.Name}'";
            });
        }

        private async Task<AIReturn> SimplifyParameterAsync(AIToolCall toolCall)
        {
            return await ExecuteParameterModification(toolCall, "gh_parameter_simplify", (args) =>
            {
                var componentGuid = Guid.Parse(args["componentGuid"]?.ToString() ?? throw new ArgumentException("Missing componentGuid"));
                var parameterIndex = args["parameterIndex"]?.ToObject<int>() ?? throw new ArgumentException("Missing parameterIndex");
                var isInput = args["isInput"]?.ToObject<bool>() ?? true;
                var enable = args["enable"]?.ToObject<bool>() ?? true;

                var obj = CanvasAccess.FindInstance(componentGuid);
                if (obj == null) throw new ArgumentException("Component not found");
                if (!(obj is IGH_Component component)) throw new ArgumentException("Object is not a component");

                var paramList = isInput ? component.Params.Input : component.Params.Output;
                if (parameterIndex < 0 || parameterIndex >= paramList.Count)
                    throw new ArgumentException($"Parameter index {parameterIndex} out of range");

                var param = paramList[parameterIndex];
                component.RecordUndoEvent("[SH] Simplify Parameter");
                ParameterModifier.SetSimplify(param, enable);
                component.ExpireSolution(true);
                Instances.RedrawCanvas();

                return $"{(enable ? "Enabled" : "Disabled")} simplify for {(isInput ? "input" : "output")} parameter '{param.Name}'";
            });
        }

        private async Task<AIReturn> BulkModifyInputsAsync(AIToolCall toolCall)
        {
            return await ExecuteParameterModification(toolCall, "gh_parameter_bulk_inputs", (args) =>
            {
                var componentGuid = Guid.Parse(args["componentGuid"]?.ToString() ?? throw new ArgumentException("Missing componentGuid"));
                var flatten = args["flatten"]?.ToObject<bool?>();
                var graft = args["graft"]?.ToObject<bool?>();
                var reverse = args["reverse"]?.ToObject<bool?>();
                var simplify = args["simplify"]?.ToObject<bool?>();

                var obj = CanvasAccess.FindInstance(componentGuid);
                if (obj == null) throw new ArgumentException("Component not found");
                if (!(obj is IGH_Component component)) throw new ArgumentException("Object is not a component");

                component.RecordUndoEvent("[SH] Bulk Modify Inputs");

                GH_DataMapping? dataMapping = null;
                if (flatten == true) dataMapping = GH_DataMapping.Flatten;
                else if (graft == true) dataMapping = GH_DataMapping.Graft;

                ParameterModifier.BulkApply(component.Params.Input, dataMapping: dataMapping, reverse: reverse, simplify: simplify);
                component.ExpireSolution(true);
                Instances.RedrawCanvas();

                var actions = new List<string>();
                if (flatten == true) actions.Add("flattened");
                if (graft == true) actions.Add("grafted");
                if (reverse == true) actions.Add("reversed");
                if (simplify == true) actions.Add("simplified");

                var actionStr = actions.Any() ? string.Join(", ", actions) : "no changes";
                return $"Applied bulk settings ({actionStr}) to {component.Params.Input.Count} input parameters";
            });
        }

        private async Task<AIReturn> BulkModifyOutputsAsync(AIToolCall toolCall)
        {
            return await ExecuteParameterModification(toolCall, "gh_parameter_bulk_outputs", (args) =>
            {
                var componentGuid = Guid.Parse(args["componentGuid"]?.ToString() ?? throw new ArgumentException("Missing componentGuid"));
                var flatten = args["flatten"]?.ToObject<bool?>();
                var graft = args["graft"]?.ToObject<bool?>();
                var reverse = args["reverse"]?.ToObject<bool?>();
                var simplify = args["simplify"]?.ToObject<bool?>();

                var obj = CanvasAccess.FindInstance(componentGuid);
                if (obj == null) throw new ArgumentException("Component not found");
                if (!(obj is IGH_Component component)) throw new ArgumentException("Object is not a component");

                component.RecordUndoEvent("[SH] Bulk Modify Outputs");

                GH_DataMapping? dataMapping = null;
                if (flatten == true) dataMapping = GH_DataMapping.Flatten;
                else if (graft == true) dataMapping = GH_DataMapping.Graft;

                ParameterModifier.BulkApply(component.Params.Output, dataMapping: dataMapping, reverse: reverse, simplify: simplify);
                component.ExpireSolution(true);
                Instances.RedrawCanvas();

                var actions = new List<string>();
                if (flatten == true) actions.Add("flattened");
                if (graft == true) actions.Add("grafted");
                if (reverse == true) actions.Add("reversed");
                if (simplify == true) actions.Add("simplified");

                var actionStr = actions.Any() ? string.Join(", ", actions) : "no changes";
                return $"Applied bulk settings ({actionStr}) to {component.Params.Output.Count} output parameters";
            });
        }

        private async Task<AIReturn> ExecuteParameterModification(AIToolCall toolCall, string toolName, Func<JObject, string> operation)
        {
            var output = new AIReturn() { Request = toolCall };

            try
            {
                var toolInfo = toolCall.GetToolCall();
                var args = toolInfo.Arguments ?? new JObject();

                var tcs = new TaskCompletionSource<AIReturn>();

                RhinoApp.InvokeOnUiThread(() =>
                {
                    try
                    {
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
                    }
                    catch (Exception ex)
                    {
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
