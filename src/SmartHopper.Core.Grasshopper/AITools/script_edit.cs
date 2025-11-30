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

        private readonly string systemPromptTemplate =
            "You are a Grasshopper script component editor. Edit the existing script based on the user's instructions.\n\n" +
            "You will receive the current component as GhJSON. Analyze it and apply the requested changes.\n" +
            "Preserve the original language unless the user explicitly asks to change it.\n\n" +
            "Your response MUST be a valid JSON object with the following structure:\n" +
            "- script: The complete updated script code\n" +
            "- inputs: Array of input parameters with name, type, description, and access (item/list/tree)\n" +
            "- outputs: Array of output parameters with name, type, and description\n" +
            "- changesSummary: Brief description of what was changed and design decisions made\n" +
            "- nickname: Optional updated nickname for the component\n\n" +
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
                category: "Scripting",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""ghjson"": {
                            ""type"": ""string"",
                            ""description"": ""GhJSON string representing the script component to edit.""
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
                                ""name"": { ""type"": ""string"" },
                                ""type"": { ""type"": ""string"" },
                                ""description"": { ""type"": ""string"" },
                                ""access"": { ""type"": ""string"", ""enum"": [""item"", ""list"", ""tree""] }
                            },
                            ""required"": [""name"", ""type"", ""description"", ""access""],
                            ""additionalProperties"": false
                        }
                    },
                    ""outputs"": {
                        ""type"": ""array"",
                        ""items"": {
                            ""type"": ""object"",
                            ""properties"": {
                                ""name"": { ""type"": ""string"" },
                                ""type"": { ""type"": ""string"" },
                                ""description"": { ""type"": ""string"" }
                            },
                            ""required"": [""name"", ""type"", ""description""],
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
