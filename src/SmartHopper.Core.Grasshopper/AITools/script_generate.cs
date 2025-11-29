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
    /// AI tool that generates a new script component as GhJSON from natural language instructions.
    /// Does not place the component on the canvas - returns GhJSON for downstream orchestration.
    /// </summary>
    public class script_generate : IAIToolProvider
    {
        private readonly string toolName = "script_generate";

        private readonly string systemPromptTemplate =
            "You are a Grasshopper script component generator. Generate a complete script for a Grasshopper script component based on the user instructions.\n\n" +
            "You MUST choose the scripting language and return it in the \"language\" field.\n" +
            "The language MUST be one of: \"python\", \"ironpython\", \"c#\", \"vb\".\n" +
            "Use \"python\" unless the user explicitly requests another language.\n\n" +
            "Your response MUST be a valid JSON object with the following structure:\n" +
            "- language: The scripting language to use (python, ironpython, c#, vb)\n" +
            "- script: The complete script code\n" +
            "- inputs: Array of input parameters with name, type, description, and access (item/list/tree)\n" +
            "- outputs: Array of output parameters with name, type, and description\n" +
            "- nickname: Optional short name for the component\n" +
            "- summary: A brief summary (1-3 sentences) of what the component does and key design decisions made\n\n" +
            "The JSON object will be parsed programmatically, so it must be valid JSON with no additional text.";

        /// <inheritdoc/>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Generate a new Grasshopper script component from natural language instructions. Returns GhJSON representing the script component (does not place it on canvas).",
                category: "Scripting",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""instructions"": {
                            ""type"": ""string"",
                            ""description"": ""Natural language instructions describing what the script should do.""
                        },
                        ""language"": {
                            ""type"": ""string"",
                            ""description"": ""Optional preferred scripting language (python, ironpython, c#, vb). Defaults to python if not specified."",
                            ""enum"": [""python"", ""ironpython"", ""c#"", ""vb""]
                        }
                    },
                    ""required"": [""instructions""]
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
                var instructions = args["instructions"]?.ToString();
                var preferredLanguage = args["language"]?.ToString();
                var providerName = toolCall.Provider;
                var modelName = toolCall.Model;
                var contextFilter = args["contextFilter"]?.ToString() ?? "-*";

                if (string.IsNullOrWhiteSpace(instructions))
                {
                    output.CreateError("Missing required 'instructions' parameter.");
                    return output;
                }

                // Build system prompt with optional language preference
                var systemPrompt = this.systemPromptTemplate;
                if (!string.IsNullOrWhiteSpace(preferredLanguage))
                {
                    systemPrompt += $"\n\nThe user prefers the '{preferredLanguage}' scripting language.";
                }

                var jsonSchema = GetJsonSchema();

                var bodyBuilder = AIBodyBuilder.Create()
                    .WithJsonOutputSchema(jsonSchema)
                    .WithContextFilter(contextFilter)
                    .AddSystem(systemPrompt)
                    .AddUser(instructions);

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

                var language = responseJson["language"]?.ToString() ?? "python";
                var scriptCode = responseJson["script"]?.ToString() ?? string.Empty;
                var inputs = responseJson["inputs"] as JArray ?? new JArray();
                var outputs = responseJson["outputs"] as JArray ?? new JArray();
                var nickname = responseJson["nickname"]?.ToString() ?? "AI Script";
                var summary = responseJson["summary"]?.ToString() ?? string.Empty;

                // Validate language
                var componentInfo = ScriptComponentFactory.GetComponentInfo(language);
                if (componentInfo == null)
                {
                    var supported = string.Join(", ", ScriptComponentFactory.GetSupportedLanguages());
                    output.CreateError($"Unsupported language '{language}'. Supported: {supported}");
                    return output;
                }

                Debug.WriteLine($"[script_generate] Language: {language}, Script length: {scriptCode.Length}");
                Debug.WriteLine($"[script_generate] Inputs: {inputs.Count}, Outputs: {outputs.Count}");

                // Build GhJSON using ScriptComponentFactory
                var comp = ScriptComponentFactory.CreateScriptComponent(
                    language,
                    scriptCode,
                    inputs,
                    outputs,
                    nickname);

                var doc = new GrasshopperDocument();
                doc.Components.Add(comp);

                // Serialize to GhJSON string
                var ghJsonString = JsonConvert.SerializeObject(doc, Formatting.None);

                // Validate GhJSON output
                if (!GHJsonAnalyzer.Validate(ghJsonString, out var validationError))
                {
                    output.CreateError($"Generated GhJSON validation failed: {validationError}");
                    return output;
                }

                // Build tool result
                var toolResult = new JObject
                {
                    ["success"] = true,
                    ["ghjson"] = ghJsonString,
                    ["language"] = componentInfo.LanguageKey,
                    ["componentName"] = componentInfo.DisplayName,
                    ["instanceGuid"] = comp.InstanceGuid.ToString(),
                    ["inputCount"] = inputs.Count,
                    ["outputCount"] = outputs.Count,
                    ["summary"] = summary,
                    ["message"] = "Script component GhJSON generated successfully. Use gh_put to place it on the canvas.",
                };

                var outBuilder = AIBodyBuilder.Create();
                outBuilder.AddToolResult(toolResult, toolInfo.Id, toolInfo.Name, result.Metrics, result.Messages);
                output.CreateSuccess(outBuilder.Build(), toolCall);
                return output;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[script_generate] Error: {ex.Message}");
                output.CreateError(ex.Message);
                return output;
            }
        }

        private static string GetJsonSchema()
        {
            return @"{
                ""type"": ""object"",
                ""properties"": {
                    ""language"": {
                        ""type"": ""string"",
                        ""description"": ""Scripting language for the component. Must be one of: python, ironpython, c#, vb."",
                        ""enum"": [""python"", ""ironpython"", ""c#"", ""vb""],
                        ""default"": ""python""
                    },
                    ""script"": {
                        ""type"": ""string"",
                        ""description"": ""The complete script code for the component""
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
                    ""nickname"": {
                        ""type"": ""string"",
                        ""description"": ""Optional short name for the component""
                    },
                    ""summary"": {
                        ""type"": ""string"",
                        ""description"": ""A brief summary (1-3 sentences) of what the component does and key design decisions made""
                    }
                },
                ""required"": [""language"", ""script"", ""inputs"", ""outputs"", ""summary""],
                ""additionalProperties"": false
            }";
        }
    }
}
