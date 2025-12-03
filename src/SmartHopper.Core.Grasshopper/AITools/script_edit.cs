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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Serialization.GhJson.ScriptComponents;
using SmartHopper.Core.Models.Document;
using SmartHopper.Core.Models.Serialization;
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
    /// AI tool that edits an existing script component GhJSON based on natural language instructions.
    /// Does not modify the canvas - returns updated GhJSON for downstream orchestration.
    /// </summary>
    public class script_edit : IAIToolProvider
    {
        private readonly string toolName = "script_edit";
        private readonly string wrapperToolName = "script_edit_and_replace_on_canvas";

        private readonly string systemPromptTemplate =
            "You are a Grasshopper script component editor. Edit the existing script based on the user's instructions.\n\n" +
            "You will receive the current component as GhJSON. Analyze it and apply the requested changes.\n" +
            "Preserve the original language unless the user explicitly asks to change it.\n\n" +
            "Your response MUST be a valid JSON object with the following structure:\n" +
            "- script: The complete updated script code\n" +
            "- inputs: Array of input parameters (see input parameter schema below)\n" +
            "- outputs: Array of output parameters (see output parameter schema below)\n" +
            "- changesSummary: Brief description of what was changed and design decisions made\n" +
            "- nickname: Optional updated nickname for the component\n\n" +
            "INPUT PARAMETER SCHEMA (all fields except name are optional):\n" +
            "  - name: Parameter name (required)\n" +
            "  - type: Type hint (e.g., int, double, string, bool, Point3d, Curve, Surface, Brep, Mesh, Vector3d, Plane, Line, etc.)\n" +
            "  - description: Parameter description\n" +
            "  - access: Data access mode - 'item' (single value), 'list' (list of values), 'tree' (data tree). Default: 'item'\n" +
            "  - dataMapping: Data tree manipulation - 'None', 'Flatten' (collapse tree to list), 'Graft' (wrap each item in branch)\n" +
            "  - reverse: If true, reverses the order of items in lists\n" +
            "  - simplify: If true, simplifies data tree paths by removing unnecessary branches\n" +
            "  - invert: If true, inverts boolean values (true becomes false, vice versa). Only for boolean parameters.\n" +
            "  - isPrincipal: If true, marks this as the principal/master parameter for component data matching\n" +
            "  - required: If true, parameter cannot be removed by user (default: false = optional)\n" +
            "  - expression: Mathematical expression to transform input data (e.g., 'x * 2', 'Math.Sin(x)')\n\n" +
            "OUTPUT PARAMETER SCHEMA (all fields except name are optional):\n" +
            "  - name: Parameter name (required)\n" +
            "  - type: Type hint for expected output type\n" +
            "  - description: Parameter description\n" +
            "  - dataMapping: Data tree manipulation - 'None', 'Flatten', 'Graft'\n" +
            "  - reverse: If true, reverses the order of output items\n" +
            "  - simplify: If true, simplifies output data tree paths\n" +
            "  - invert: If true, inverts boolean values (true becomes false, vice versa). Only for boolean parameters.\n\n" +
            "The JSON object will be parsed programmatically, so it must be valid JSON with no additional text.";

        private readonly string userPromptTemplate =
            "Current component GhJSON:\n```json\n<ghjson>\n```\n\n" +
            "Instructions: <instructions>";

        /// <inheritdoc/>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Edit an existing Grasshopper script component based on instructions. Takes GhJSON input and returns updated GhJSON (does not modify canvas).",
                category: "Hidden",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""ghjson"": {
                            ""type"": ""string"",
                            ""description"": ""GhJSON string representing the script component to edit. Retrieve it using gh_get[categoryFilter=[+Script]], gh_get[guidFilter=[<guid>]] or gh_get_selected if the user is asking about the currently selected component.""
                        },
                        ""instructions"": {
                            ""type"": ""string"",
                            ""description"": ""Natural language instructions describing how to edit the script.""
                        }
                    },
                    ""required"": [""ghjson"", ""instructions""]
                }",
                execute: this.ExecuteAsync,
                requiredCapabilities: AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput);

            // Wrapper tool that edits and replaces the component on canvas in one call.
            // Uses instanceGuid to automatically retrieve GhJSON, call script_edit, and then gh_put.
            yield return new AITool(
                name: this.wrapperToolName,
                description: "Edit an existing Grasshopper script component by instance GUID and replace it on the canvas. This wrapper automatically retrieves the component GhJSON (gh_get_by_guid), calls script_edit, and then gh_put with editMode=true.",
                category: "Scripting",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""instanceGuid"": {
                            ""type"": ""string"",
                            ""description"": ""Instance GUID of the script component to edit (retrieve it using gh_get or variants).""
                        },
                        ""instructions"": {
                            ""type"": ""string"",
                            ""description"": ""Natural language instructions describing how to edit the script.""
                        }
                    },
                    ""required"": [""instanceGuid"", ""instructions""]
                }",
                execute: this.ExecuteEditAndReplaceAsync,
                requiredCapabilities: AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput);
        }

        private async Task<AIReturn> ExecuteAsync(AIToolCall toolCall)
        {
            var output = new AIReturn { Request = toolCall };

            try
            {
                var toolInfo = toolCall.GetToolCall();
                var args = toolInfo.Arguments ?? new JObject();
                var ghJsonInput = args["ghjson"]?.ToString();
                var instructions = args["instructions"]?.ToString();
                var providerName = toolCall.Provider;
                var modelName = toolCall.Model;
                var contextFilter = args["contextFilter"]?.ToString() ?? "-*";

                if (string.IsNullOrWhiteSpace(ghJsonInput))
                {
                    output.CreateError("Missing required 'ghjson' parameter.");
                    return output;
                }

                if (string.IsNullOrWhiteSpace(instructions))
                {
                    output.CreateError("Missing required 'instructions' parameter.");
                    return output;
                }

                // Validate input GhJSON
                if (!GHJsonAnalyzer.Validate(ghJsonInput, out var inputValidationError))
                {
                    output.CreateError($"Input GhJSON validation failed: {inputValidationError}");
                    return output;
                }

                // Parse input to extract language and existing component info
                var inputDoc = JsonConvert.DeserializeObject<GrasshopperDocument>(ghJsonInput);
                if (inputDoc?.Components == null || inputDoc.Components.Count == 0)
                {
                    output.CreateError("Input GhJSON contains no components.");
                    return output;
                }

                var existingComp = inputDoc.Components[0];
                var existingLanguage = DetectLanguageFromComponentGuid(existingComp.ComponentGuid);
                var existingInstanceGuid = existingComp.InstanceGuid;

                Debug.WriteLine($"[script_edit] Existing language: {existingLanguage}, InstanceGuid: {existingInstanceGuid}");

                // Build user prompt with GhJSON
                var userPrompt = this.userPromptTemplate
                    .Replace("<ghjson>", ghJsonInput)
                    .Replace("<instructions>", instructions);

                var systemPrompt = this.systemPromptTemplate + $"\n\nThe current script is written in '{existingLanguage}'.";

                var jsonSchema = GetJsonSchema();

                var bodyBuilder = AIBodyBuilder.Create()
                    .WithJsonOutputSchema(jsonSchema)
                    .WithContextFilter(contextFilter)
                    .AddSystem(systemPrompt)
                    .AddUser(userPrompt);

                var request = new AIRequestCall();
                request.Initialize(
                    provider: providerName,
                    model: modelName,
                    capability: AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    endpoint: this.toolName,
                    body: bodyBuilder.Build());

                var result = await request.Exec().ConfigureAwait(false);

                if (!result.Success)
                {
                    output.Messages = result.Messages;
                    return output;
                }

                // Parse AI response
                var response = result.Body.GetLastInteraction(AIAgent.Assistant).ToString();
                var responseJson = JObject.Parse(response);

                var newScriptCode = responseJson["script"]?.ToString() ?? string.Empty;
                var newInputs = responseJson["inputs"] as JArray ?? new JArray();
                var newOutputs = responseJson["outputs"] as JArray ?? new JArray();
                var changesSummary = responseJson["changesSummary"]?.ToString() ?? "Script updated";
                var nickname = responseJson["nickname"]?.ToString() ?? existingComp.NickName ?? "AI Script";

                Debug.WriteLine($"[script_edit] New script length: {newScriptCode.Length}");
                Debug.WriteLine($"[script_edit] New inputs: {newInputs.Count}, outputs: {newOutputs.Count}");

                // Get component info for the language
                var componentInfo = ScriptComponentFactory.GetComponentInfo(existingLanguage);
                if (componentInfo == null)
                {
                    output.CreateError($"Failed to get component info for language '{existingLanguage}'.");
                    return output;
                }

                // Build updated GhJSON preserving instance GUID
                var updatedComp = ScriptComponentFactory.CreateScriptComponent(
                    existingLanguage,
                    newScriptCode,
                    newInputs,
                    newOutputs,
                    nickname);

                // Preserve the original instance GUID for gh_put to update in place
                updatedComp.InstanceGuid = existingInstanceGuid;

                // Preserve pivot if available
                if (existingComp.Pivot.X != 0 || existingComp.Pivot.Y != 0)
                {
                    updatedComp.Pivot = existingComp.Pivot;
                }

                var doc = new GrasshopperDocument();
                doc.Components.Add(updatedComp);

                var ghJsonString = JsonConvert.SerializeObject(doc, Formatting.None);

                // Validate output GhJSON
                if (!GHJsonAnalyzer.Validate(ghJsonString, out var outputValidationError))
                {
                    output.CreateError($"Output GhJSON validation failed: {outputValidationError}");
                    return output;
                }

                // Build tool result
                var toolResult = new JObject
                {
                    ["success"] = true,
                    ["ghjson"] = ghJsonString,
                    ["instanceGuid"] = existingInstanceGuid.ToString(),
                    ["language"] = existingLanguage,
                    ["inputCount"] = newInputs.Count,
                    ["outputCount"] = newOutputs.Count,
                    ["changesSummary"] = changesSummary,
                    ["message"] = "Script component GhJSON updated successfully. Use gh_put to apply changes to the canvas.",
                };

                var outBuilder = AIBodyBuilder.Create();
                outBuilder.AddToolResult(toolResult, toolInfo.Id, toolInfo.Name, result.Metrics, result.Messages);
                output.CreateSuccess(outBuilder.Build(), toolCall);
                return output;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[script_edit] Error: {ex.Message}");
                output.CreateError(ex.Message);
                return output;
            }
        }

        /// <summary>
        /// Wrapper that edits the script and replaces it on the canvas in one call.
        /// Internally calls script_edit then gh_put with editMode=true.
        /// </summary>
        private async Task<AIReturn> ExecuteEditAndReplaceAsync(AIToolCall toolCall)
        {
            var output = new AIReturn { Request = toolCall };

            try
            {
                // Parse wrapper arguments (instanceGuid + instructions)
                var wrapperToolInfo = toolCall.GetToolCall();
                var wrapperArgs = wrapperToolInfo.Arguments ?? new JObject();
                var instanceGuidText = wrapperArgs["instanceGuid"]?.ToString();
                var instructions = wrapperArgs["instructions"]?.ToString();

                if (string.IsNullOrWhiteSpace(instanceGuidText))
                {
                    output.CreateError("Missing required 'instanceGuid' parameter.");
                    return output;
                }

                if (!Guid.TryParse(instanceGuidText, out var instanceGuid))
                {
                    output.CreateError("The provided 'instanceGuid' is not a valid GUID.");
                    return output;
                }

                if (string.IsNullOrWhiteSpace(instructions))
                {
                    output.CreateError("Missing required 'instructions' parameter.");
                    return output;
                }

                // Step 1: Call gh_get_by_guid to retrieve GhJSON for the target component
                Debug.WriteLine($"[{this.wrapperToolName}] Step 1: Calling gh_get_by_guid for {instanceGuid}");

                var ghGetArgs = new JObject
                {
                    ["guidFilter"] = new JArray(instanceGuidText),
                    ["connectionDepth"] = 0,
                };

                var ghGetInteraction = new AIInteractionToolCall
                {
                    Name = "gh_get_by_guid",
                    Arguments = ghGetArgs,
                    Agent = AIAgent.Assistant,
                };

                var ghGetToolCall = new AIToolCall
                {
                    Provider = toolCall.Provider,
                    Model = toolCall.Model,
                    Endpoint = "gh_get_by_guid",
                    SkipMetricsValidation = true,
                };

                ghGetToolCall.Body = AIBodyBuilder.Create()
                    .Add(ghGetInteraction)
                    .Build();

                var ghGetResult = await AIToolManager.ExecuteTool(ghGetToolCall).ConfigureAwait(false);

                if (!ghGetResult.Success)
                {
                    output.Messages = ghGetResult.Messages;
                    output.CreateError("gh_get_by_guid failed to retrieve the target component.");
                    return output;
                }

                var ghGetToolResult = ghGetResult.Body?.Interactions
                    .OfType<AIInteractionToolResult>()
                    .FirstOrDefault();

                var ghJsonInput = ghGetToolResult?.Result?["ghjson"]?.ToString();
                if (string.IsNullOrWhiteSpace(ghJsonInput))
                {
                    output.CreateError("gh_get_by_guid did not return valid GhJSON for the specified instanceGuid.");
                    return output;
                }

                // Step 2: Call script_edit with the retrieved GhJSON and original instructions
                Debug.WriteLine($"[{this.wrapperToolName}] Step 2: Calling script_edit");

                var scriptEditArgs = new JObject
                {
                    ["ghjson"] = ghJsonInput,
                    ["instructions"] = instructions,
                };

                var scriptEditInteraction = new AIInteractionToolCall
                {
                    Name = this.toolName,
                    Arguments = scriptEditArgs,
                    Agent = AIAgent.Assistant,
                };

                var scriptEditToolCall = new AIToolCall
                {
                    Provider = toolCall.Provider,
                    Model = toolCall.Model,
                    Endpoint = this.toolName,
                };

                scriptEditToolCall.Body = AIBodyBuilder.Create()
                    .Add(scriptEditInteraction)
                    .Build();

                var editResult = await this.ExecuteAsync(scriptEditToolCall).ConfigureAwait(false);

                if (!editResult.Success)
                {
                    Debug.WriteLine($"[{this.wrapperToolName}] script_edit failed");
                    output.Messages = editResult.Messages;
                    return output;
                }

                // Extract the updated GhJSON from the script_edit result
                var editToolResult = editResult.Body?.Interactions
                    .OfType<AIInteractionToolResult>()
                    .FirstOrDefault();

                if (editToolResult?.Result == null)
                {
                    output.CreateError("script_edit did not return a valid result.");
                    return output;
                }

                var updatedGhJson = editToolResult.Result["ghjson"]?.ToString();
                if (string.IsNullOrWhiteSpace(updatedGhJson))
                {
                    output.CreateError("script_edit did not return updated GhJSON.");
                    return output;
                }

                Debug.WriteLine($"[{this.wrapperToolName}] Step 3: Calling gh_put with editMode=true");

                // Step 3: Call gh_put internally using AIToolManager
                var ghPutArgs = new JObject
                {
                    ["ghjson"] = updatedGhJson,
                    ["editMode"] = true,
                };

                var ghPutToolCallInteraction = new AIInteractionToolCall
                {
                    Name = "gh_put",
                    Arguments = ghPutArgs,
                    Agent = AIAgent.Assistant,
                };

                var ghPutToolCall = new AIToolCall
                {
                    Provider = toolCall.Provider,
                    Model = toolCall.Model,
                    Endpoint = "gh_put",
                    SkipMetricsValidation = true,
                };
                ghPutToolCall.Body = AIBodyBuilder.Create()
                    .Add(ghPutToolCallInteraction)
                    .Build();

                var ghPutResult = await AIToolManager.ExecuteTool(ghPutToolCall).ConfigureAwait(false);

                if (!ghPutResult.Success)
                {
                    Debug.WriteLine($"[{this.wrapperToolName}] gh_put failed");

                    // Return combined result with edit success but put failure
                    var partialResult = new JObject
                    {
                        ["success"] = false,
                        ["editSuccess"] = true,
                        ["ghjson"] = updatedGhJson,
                        ["putSuccess"] = false,
                        ["putError"] = ghPutResult.Messages?.FirstOrDefault()?.Message ?? "gh_put failed",
                        ["message"] = "Script was edited successfully but failed to replace on canvas.",
                    };

                    var partialBody = AIBodyBuilder.Create()
                        .AddToolResult(partialResult, toolCall.GetToolCall().Id, this.wrapperToolName, editResult.Metrics, ghPutResult.Messages)
                        .Build();
                    output.CreateSuccess(partialBody, toolCall);
                    return output;
                }

                Debug.WriteLine($"[{this.wrapperToolName}] Both steps completed successfully");

                // Combine results from both operations
                var ghPutToolResult = ghPutResult.Body?.Interactions
                    .OfType<AIInteractionToolResult>()
                    .FirstOrDefault();

                var combinedResult = new JObject
                {
                    ["success"] = true,
                    ["ghjson"] = updatedGhJson,
                    ["instanceGuid"] = editToolResult.Result["instanceGuid"],
                    ["language"] = editToolResult.Result["language"],
                    ["inputCount"] = editToolResult.Result["inputCount"],
                    ["outputCount"] = editToolResult.Result["outputCount"],
                    ["changesSummary"] = editToolResult.Result["changesSummary"],
                    ["components"] = ghPutToolResult?.Result?["components"],
                    ["message"] = "Script component edited and replaced on canvas successfully.",
                };

                // Combine metrics from both operations
                var combinedMetrics = editResult.Metrics;

                var outBuilder = AIBodyBuilder.Create();
                outBuilder.AddToolResult(combinedResult, toolCall.GetToolCall().Id, this.wrapperToolName, combinedMetrics, editResult.Messages);
                output.CreateSuccess(outBuilder.Build(), toolCall);
                return output;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{this.wrapperToolName}] Error: {ex.Message}");
                output.CreateError(ex.Message);
                return output;
            }
        }

        private static string DetectLanguageFromComponentGuid(Guid componentGuid)
        {
            if (componentGuid == ScriptComponentFactory.Python3Guid)
            {
                return "python";
            }

            if (componentGuid == ScriptComponentFactory.IronPython2Guid)
            {
                return "ironpython";
            }

            if (componentGuid == ScriptComponentFactory.CSharpGuid)
            {
                return "c#";
            }

            if (componentGuid == ScriptComponentFactory.VBNetGuid)
            {
                return "vb";
            }

            return "python"; // Default fallback
        }

        private static string GetJsonSchema()
        {
            return @"{
                ""type"": ""object"",
                ""properties"": {
                    ""script"": {
                        ""type"": ""string"",
                        ""description"": ""The complete updated script code""
                    },
                    ""inputs"": {
                        ""type"": ""array"",
                        ""items"": {
                            ""type"": ""object"",
                            ""properties"": {
                                ""name"": { ""type"": ""string"", ""description"": ""Parameter name (required)."" },
                                ""type"": { ""type"": ""string"", ""description"": ""Type hint (e.g., int, double, string, Point3d, Curve, etc.). Use 'object' when unsure."" },
                                ""description"": { ""type"": ""string"", ""description"": ""Parameter description. Use a short human-readable sentence."" },
                                ""access"": { ""type"": ""string"", ""enum"": [""item"", ""list"", ""tree""], ""description"": ""Data access mode. Use 'item' when unsure."" },
                                ""dataMapping"": { ""type"": ""string"", ""enum"": [""None"", ""Flatten"", ""Graft""], ""description"": ""Data tree manipulation. Use 'None' when no mapping is needed."" },
                                ""reverse"": { ""type"": ""boolean"", ""description"": ""Reverse list order. Use false when not needed."" },
                                ""simplify"": { ""type"": ""boolean"", ""description"": ""Simplify data tree paths. Use false when not needed."" },
                                ""invert"": { ""type"": ""boolean"", ""description"": ""Invert boolean values (only for bool type). Use false when not needed."" },
                                ""required"": { ""type"": ""boolean"", ""description"": ""If true, parameter cannot be removed. Use false for optional parameters."" },
                                ""expression"": { ""type"": ""string"", ""description"": ""Mathematical expression to transform data (e.g., 'x * 2'). Use empty string when no expression is needed."" }
                            },
                            ""required"": [""name"", ""type"", ""description"", ""access"", ""dataMapping"", ""reverse"", ""simplify"", ""invert"", ""required"", ""expression""],
                            ""additionalProperties"": false
                        }
                    },
                    ""outputs"": {
                        ""type"": ""array"",
                        ""items"": {
                            ""type"": ""object"",
                            ""properties"": {
                                ""name"": { ""type"": ""string"", ""description"": ""Parameter name (required)."" },
                                ""type"": { ""type"": ""string"", ""description"": ""Expected output type hint. Use 'object' when unsure."" },
                                ""description"": { ""type"": ""string"", ""description"": ""Parameter description. Use a short human-readable sentence."" },
                                ""dataMapping"": { ""type"": ""string"", ""enum"": [""None"", ""Flatten"", ""Graft""], ""description"": ""Data tree manipulation. Use 'None' when no mapping is needed."" },
                                ""reverse"": { ""type"": ""boolean"", ""description"": ""Reverse output list order. Use false when not needed."" },
                                ""simplify"": { ""type"": ""boolean"", ""description"": ""Simplify output data tree paths. Use false when not needed."" },
                                ""invert"": { ""type"": ""boolean"", ""description"": ""Invert boolean values (only for bool type). Use false when not needed."" }
                            },
                            ""required"": [""name"", ""type"", ""description"", ""dataMapping"", ""reverse"", ""simplify"", ""invert""],
                            ""additionalProperties"": false
                        }
                    },
                    ""changesSummary"": {
                        ""type"": ""string"",
                        ""description"": ""Brief summary of what was changed in the script""
                    },
                    ""nickname"": {
                        ""type"": ""string"",
                        ""description"": ""Optional updated nickname for the component""
                    }
                },
                ""required"": [""script"", ""inputs"", ""outputs"", ""changesSummary"", ""nickname""],
                ""additionalProperties"": false
            }";
        }
    }
}
