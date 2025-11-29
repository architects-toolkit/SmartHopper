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
    /// AI tool specialized for fixing bugs in a script component GhJSON based on error context.
    /// Does not modify the canvas - returns fixed GhJSON for downstream orchestration.
    /// </summary>
    public class script_fix : IAIToolProvider
    {
        private readonly string toolName = "script_fix";

        private readonly string systemPromptTemplate =
            "You are a Grasshopper script debugger. Your job is to fix bugs in the provided script based on error messages and context.\n\n" +
            "You will receive:\n" +
            "1. The current script component as GhJSON\n" +
            "2. Error messages or stack traces from runtime\n" +
            "3. Optional additional context about the problem\n\n" +
            "Analyze the errors carefully and fix the root cause, not just the symptoms.\n" +
            "Preserve the original language and component structure unless a fix requires changes.\n\n" +
            "Your response MUST be a valid JSON object with the following structure:\n" +
            "- script: The complete fixed script code\n" +
            "- inputs: Array of input parameters with name, type, description, and access (item/list/tree)\n" +
            "- outputs: Array of output parameters with name, type, and description\n" +
            "- diagnosis: Explanation of what was wrong and why it caused the error\n" +
            "- fixSummary: Brief description of the fix applied\n\n" +
            "The JSON object will be parsed programmatically, so it must be valid JSON with no additional text.";

        private readonly string userPromptTemplate =
            "Script component GhJSON:\n```json\n<ghjson>\n```\n\n" +
            "Error message:\n```\n<error>\n```\n\n" +
            "<context>";

        /// <inheritdoc/>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Fix bugs in a Grasshopper script component based on error messages. Takes GhJSON and error context, returns fixed GhJSON (does not modify canvas).",
                category: "Scripting",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""ghjson"": {
                            ""type"": ""string"",
                            ""description"": ""GhJSON string representing the script component with the bug.""
                        },
                        ""error"": {
                            ""type"": ""string"",
                            ""description"": ""Error message, exception, or stack trace from the script runtime.""
                        },
                        ""context"": {
                            ""type"": ""string"",
                            ""description"": ""Optional additional context about the problem (e.g., expected behavior, input data).""
                        }
                    },
                    ""required"": [""ghjson"", ""error""]
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
                var errorMessage = args["error"]?.ToString();
                var additionalContext = args["context"]?.ToString();
                var providerName = toolCall.Provider;
                var modelName = toolCall.Model;
                var contextFilter = args["contextFilter"]?.ToString() ?? "-*";

                if (string.IsNullOrWhiteSpace(ghJsonInput))
                {
                    output.CreateError("Missing required 'ghjson' parameter.");
                    return output;
                }

                if (string.IsNullOrWhiteSpace(errorMessage))
                {
                    output.CreateError("Missing required 'error' parameter.");
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

                Debug.WriteLine($"[script_fix] Language: {existingLanguage}, InstanceGuid: {existingInstanceGuid}");
                Debug.WriteLine($"[script_fix] Error: {errorMessage}");

                // Build user prompt with GhJSON and error
                var contextSection = string.IsNullOrWhiteSpace(additionalContext)
                    ? string.Empty
                    : $"Additional context:\n{additionalContext}";

                var userPrompt = this.userPromptTemplate
                    .Replace("<ghjson>", ghJsonInput)
                    .Replace("<error>", errorMessage)
                    .Replace("<context>", contextSection);

                var systemPrompt = this.systemPromptTemplate + $"\n\nThe script uses the '{existingLanguage}' language.";

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

                var fixedScriptCode = responseJson["script"]?.ToString() ?? string.Empty;
                var newInputs = responseJson["inputs"] as JArray ?? new JArray();
                var newOutputs = responseJson["outputs"] as JArray ?? new JArray();
                var diagnosis = responseJson["diagnosis"]?.ToString() ?? "Unknown issue";
                var fixSummary = responseJson["fixSummary"]?.ToString() ?? "Bug fixed";

                Debug.WriteLine($"[script_fix] Diagnosis: {diagnosis}");
                Debug.WriteLine($"[script_fix] Fix summary: {fixSummary}");
                Debug.WriteLine($"[script_fix] Fixed script length: {fixedScriptCode.Length}");

                // Get component info for the language
                var componentInfo = ScriptComponentFactory.GetComponentInfo(existingLanguage);
                if (componentInfo == null)
                {
                    output.CreateError($"Failed to get component info for language '{existingLanguage}'.");
                    return output;
                }

                // Build fixed GhJSON preserving instance GUID
                var fixedComp = ScriptComponentFactory.CreateScriptComponent(
                    existingLanguage,
                    fixedScriptCode,
                    newInputs,
                    newOutputs,
                    existingComp.NickName ?? "Fixed Script");

                // Preserve the original instance GUID
                fixedComp.InstanceGuid = existingInstanceGuid;

                // Preserve pivot if available
                if (existingComp.Pivot.X != 0 || existingComp.Pivot.Y != 0)
                {
                    fixedComp.Pivot = existingComp.Pivot;
                }

                var doc = new GrasshopperDocument();
                doc.Components.Add(fixedComp);

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
                    ["diagnosis"] = diagnosis,
                    ["fixSummary"] = fixSummary,
                    ["message"] = "Script bug fixed successfully. Use gh_put to apply the fix to the canvas.",
                };

                var outBuilder = AIBodyBuilder.Create();
                outBuilder.AddToolResult(toolResult, toolInfo.Id, toolInfo.Name, result.Metrics, result.Messages);
                output.CreateSuccess(outBuilder.Build(), toolCall);
                return output;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[script_fix] Error: {ex.Message}");
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
                        ""description"": ""The complete fixed script code""
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
                    ""diagnosis"": {
                        ""type"": ""string"",
                        ""description"": ""Explanation of what was wrong and why it caused the error""
                    },
                    ""fixSummary"": {
                        ""type"": ""string"",
                        ""description"": ""Brief description of the fix applied""
                    }
                },
                ""required"": [""script"", ""inputs"", ""outputs"", ""diagnosis"", ""fixSummary""],
                ""additionalProperties"": false
            }";
        }
    }
}
