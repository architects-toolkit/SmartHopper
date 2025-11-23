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
using Grasshopper.Kernel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rhino;
using RhinoCodePlatform.GH;
using SmartHopper.Core.Grasshopper.Serialization.Canvas;
using SmartHopper.Core.Grasshopper.Serialization.GhJson;
using SmartHopper.Core.Grasshopper.Serialization.GhJson.ScriptComponents;
using SmartHopper.Core.Grasshopper.Utils.Canvas;
using SmartHopper.Core.Grasshopper.Utils.Components;
using SmartHopper.Core.Models.Document;
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
    /// Provides the "script_generator" AI tool for creating or editing script components based on user instructions.
    /// </summary>
    public class script_generator : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "script_generator";

        /// <summary>
        /// System prompt template for creating new script components.
        /// </summary>
        private readonly string createSystemPromptTemplate =
            "You are a Grasshopper script component generator. Generate a complete script for a Grasshopper script component based on the user instructions.\n\n" +
            "You MUST choose the scripting language and return it in the \"language\" field.\n" +
            "The language MUST be one of: \"python\", \"ironpython\", \"c#\", \"vb\".\n" +
            "Use \"python\" unless the user explicitly requests another language.\n\n" +
            "Your response MUST be a valid JSON object with the following structure:\n" +
            "- language: The scripting language to use (python, ironpython, c#, vb)\n" +
            "- script: The complete script code\n" +
            "- inputs: Array of input parameters with name, type, description, and access (item/list/tree)\n" +
            "- outputs: Array of output parameters with name, type, and description\n\n" +
            "The JSON object will be parsed programmatically, so it must be valid JSON with no additional text.";

        /// <summary>
        /// System prompt template for editing existing script components. Use <language> placeholder.
        /// </summary>
        private readonly string editSystemPromptTemplate =
            "You are a Grasshopper script component editor. Edit the existing <language> script based on the user's instructions.\n\n" +
            "Your response MUST be a valid JSON object with the following structure:\n" +
            "- script: The complete updated script code\n" +
            "- inputs: Array of input parameters with name, type, description, and access (item/list/tree)\n" +
            "- outputs: Array of output parameters with name, type, and description\n" +
            "- changesSummary: Brief description of what was changed\n\n" +
            "The JSON object will be parsed programmatically, so it must be valid JSON with no additional text.";

        /// <summary>
        /// User prompt template for editing existing script components. Use <ghjson> and <instructions> placeholders.
        /// </summary>
        private readonly string editUserPromptTemplate =
            "Current component GhJSON (the component script you are editing):\n```json\n<ghjson>\n```\n\n" +
            "User instructions: <instructions>";

        /// <summary>
        /// Returns the list of AI tools provided by this class.
        /// </summary>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Create or edit Grasshopper script components based on instructions. If guid is omitted, a new component is created. If guid is provided, the existing script component is edited.",
                category: "Scripting",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""instructions"": {
                            ""type"": ""string"",
                            ""description"": ""What you want to do with the script (create, modify, or review).""
                        },
                        ""guid"": {
                            ""type"": ""string"",
                            ""description"": ""Optional GUID of the existing script component. If omitted, a new script component will be created.""
                        }
                    },
                    ""required"": [""instructions""]
                }",
                execute: this.ScriptGeneratorToolAsync,
                requiredCapabilities: AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput);
        }

        /// <summary>
        /// Executes the "script_generator" tool.
        /// If guid is omitted, creates a new script component. If guid is provided, edits the existing script component.
        /// </summary>
        private async Task<AIReturn> ScriptGeneratorToolAsync(AIToolCall toolCall)
        {
            var output = new AIReturn
            {
                Request = toolCall,
            };

            try
            {
                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.Arguments ?? new JObject();
                var instructions = args["instructions"]?.ToString() ?? throw new ArgumentException("Missing 'instructions' parameter.");
                var guidStr = args["guid"]?.ToString();
                var providerName = toolCall.Provider;
                var modelName = toolCall.Model;
                var endpoint = this.toolName;
                string? contextFilter = args["contextFilter"]?.ToString() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(guidStr))
                {
                    return await this.CreateNewScriptComponentAsync(
                        output,
                        toolCall,
                        toolInfo,
                        providerName,
                        modelName,
                        endpoint,
                        contextFilter,
                        instructions).ConfigureAwait(false);
                }

                if (!Guid.TryParse(guidStr, out var componentGuid))
                {
                    output.CreateError($"Invalid GUID format: {guidStr}");
                    return output;
                }

                return await this.EditExistingScriptComponentAsync(
                    output,
                    toolCall,
                    toolInfo,
                    providerName,
                    modelName,
                    endpoint,
                    contextFilter,
                    instructions,
                    componentGuid,
                    guidStr).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                output.CreateError(ex.Message);
                return output;
            }
        }

        private static string GetCreateJsonSchema()
        {
            return @"{
                ""type"": ""object"",
                ""properties"": {
                    ""language"": {
                        ""type"": ""string"",
                        ""description"": ""Scripting language for the component. Must be one of: python, ironpython, c#, vb. Use python by default."",
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
                    }
                },
                ""required"": [""language"", ""script"", ""inputs"", ""outputs""],
                ""additionalProperties"": false
            }";
        }

        private static string GetEditJsonSchema()
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
                    }
                },
                ""required"": [""script"", ""inputs"", ""outputs""],
                ""additionalProperties"": false
            }";
        }

        private async Task<AIReturn> CreateNewScriptComponentAsync(
            AIReturn output,
            AIToolCall toolCall,
            AIInteractionToolCall toolInfo,
            string providerName,
            string modelName,
            string endpoint,
            string contextFilter,
            string instructions)
        {
            var jsonSchema = GetCreateJsonSchema();

            var bodyBuilder = AIBodyBuilder.Create()
                .WithJsonOutputSchema(jsonSchema)
                .WithContextFilter(contextFilter)
                .AddSystem(this.createSystemPromptTemplate)
                .AddUser(instructions);
            var immutableRequestBody = bodyBuilder.Build();

            var request = new AIRequestCall();
            request.Initialize(
                provider: providerName,
                model: modelName,
                capability: AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                endpoint: endpoint,
                body: immutableRequestBody);

            var result = await request.Exec().ConfigureAwait(false);

            if (!result.Success)
            {
                output.Messages = result.Messages;
                return output;
            }

            var response = result.Body.GetLastInteraction(AIAgent.Assistant).ToString();
            var responseJson = JObject.Parse(response);
            var scriptCode = responseJson["script"]?.ToString() ?? string.Empty;
            var inputs = responseJson["inputs"] as JArray ?? new JArray();
            var outputs = responseJson["outputs"] as JArray ?? new JArray();

            var language = responseJson["language"]?.ToString();
            if (string.IsNullOrWhiteSpace(language))
            {
                language = "python";
            }

            var componentInfo = ScriptComponentFactory.GetComponentInfo(language);
            if (componentInfo == null)
            {
                var supported = string.Join(", ", ScriptComponentFactory.GetSupportedLanguages());
                output.CreateError($"Unsupported language returned by AI: {language}. Supported: {supported}");
                return output;
            }

            Debug.WriteLine($"[script_generator] Parsed script length: {scriptCode.Length}");
            Debug.WriteLine($"[script_generator] Inputs: {inputs.Count}, Outputs: {outputs.Count}");
            Debug.WriteLine($"[script_generator] Language: {language}");

            var comp = ScriptComponentFactory.CreateScriptComponent(
                language,
                scriptCode,
                inputs,
                outputs,
                nickname: "AI Generated Script");

            var doc = new GrasshopperDocument();
            doc.Components.Add(comp);

            var tcs = new TaskCompletionSource<Dictionary<Guid, Guid>>();
            RhinoApp.InvokeOnUiThread(() =>
            {
                try
                {
                    var deserializeResult = GhJsonDeserializer.Deserialize(doc, DeserializationOptions.Standard);
                    ComponentPlacer.PlaceComponents(deserializeResult);
                    var map = deserializeResult.GuidMapping.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.InstanceGuid);
                    tcs.SetResult(map);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            var mapping = await tcs.Task.ConfigureAwait(false);

            if (mapping.TryGetValue(comp.InstanceGuid, out var actualGuid))
            {
                var refreshTcs = new TaskCompletionSource<bool>();
                RhinoApp.InvokeOnUiThread(() =>
                {
                    try
                    {
                        var placedComponent = CanvasAccess.FindInstance(actualGuid);
                        if (placedComponent != null)
                        {
                            placedComponent.ExpireSolution(true);
                            Debug.WriteLine($"[script_generator] Expired solution for component {actualGuid}");
                        }

                        refreshTcs.SetResult(true);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[script_generator] Could not refresh component: {ex.Message}");
                        refreshTcs.SetResult(false);
                    }
                });
                await refreshTcs.Task.ConfigureAwait(false);

                var toolResult = new JObject
                {
                    ["mode"] = "create",
                    ["script"] = scriptCode,
                    ["guid"] = actualGuid.ToString(),
                    ["language"] = componentInfo.LanguageKey,
                    ["componentName"] = componentInfo.DisplayName,
                    ["inputCount"] = inputs.Count,
                    ["outputCount"] = outputs.Count,
                    ["message"] = "Script component created successfully. Double-click the component to view/edit the code.",
                };

                var outBuilder = AIBodyBuilder.Create();
                outBuilder.AddToolResult(toolResult, toolInfo.Id, toolInfo.Name, result.Metrics, result.Messages);
                var outImmutable = outBuilder.Build();
                output.CreateSuccess(outImmutable, toolCall);
                return output;
            }

            output.CreateError("Failed to retrieve placed component GUID.");
            return output;
        }

        private async Task<AIReturn> EditExistingScriptComponentAsync(
            AIReturn output,
            AIToolCall toolCall,
            AIInteractionToolCall toolInfo,
            string providerName,
            string modelName,
            string endpoint,
            string contextFilter,
            string instructions,
            Guid componentGuid,
            string guidStr)
        {
            IScriptComponent scriptComp = null;
            string language = "unknown";
            string ghJson = string.Empty;

            var extractTcs = new TaskCompletionSource<bool>();
            RhinoApp.InvokeOnUiThread(() =>
            {
                try
                {
                    var component = CanvasAccess.FindInstance(componentGuid);
                    if (component == null)
                    {
                        extractTcs.SetResult(false);
                        return;
                    }

                    scriptComp = component as IScriptComponent;
                    if (scriptComp == null)
                    {
                        extractTcs.SetResult(false);
                        return;
                    }

                    language = ScriptComponentFactory.DetectLanguage(scriptComp);
                    Debug.WriteLine($"[script_generator] Detected language: {language}");

                    var componentsList = new List<IGH_ActiveObject> { (IGH_ActiveObject)scriptComp };
                    var document = GhJsonSerializer.Serialize(componentsList, SerializationOptions.Standard);
                    ghJson = JsonConvert.SerializeObject(document, Formatting.None);

                    extractTcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[script_generator] Error extracting component: {ex.Message}");
                    extractTcs.SetException(ex);
                }
            });

            await extractTcs.Task.ConfigureAwait(false);

            if (scriptComp == null)
            {
                output.CreateError($"Component with GUID {guidStr} not found or is not a script component.");
                return output;
            }

            var jsonSchema = GetEditJsonSchema();

            var bodyBuilder = AIBodyBuilder.Create()
                .WithJsonOutputSchema(jsonSchema)
                .WithContextFilter(contextFilter)
                .AddSystem(this.editSystemPromptTemplate.Replace("<language>", language))
                .AddUser(this.editUserPromptTemplate.Replace("<ghjson>", ghJson).Replace("<instructions>", instructions));

            var immutableRequestBody = bodyBuilder.Build();

            var request = new AIRequestCall();
            request.Initialize(
                provider: providerName,
                model: modelName,
                capability: AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                endpoint: endpoint,
                body: immutableRequestBody);

            var result = await request.Exec().ConfigureAwait(false);

            if (!result.Success)
            {
                output.Messages = result.Messages;
                return output;
            }

            var response = result.Body.GetLastInteraction(AIAgent.Assistant).ToString();
            var responseJson = JObject.Parse(response);
            var newScriptCode = responseJson["script"]?.ToString() ?? string.Empty;
            var newInputs = responseJson["inputs"] as JArray ?? new JArray();
            var newOutputs = responseJson["outputs"] as JArray ?? new JArray();
            var changesSummary = responseJson["changesSummary"]?.ToString() ?? "Script updated";

            Debug.WriteLine($"[script_generator] New script length: {newScriptCode.Length}");
            Debug.WriteLine($"[script_generator] New inputs: {newInputs.Count}, outputs: {newOutputs.Count}");

            var updateTcs = new TaskCompletionSource<bool>();
            RhinoApp.InvokeOnUiThread(() =>
            {
                try
                {
                    var ghComp = (IGH_Component)scriptComp;
                    ghComp.RecordUndoEvent("[SH] Edit Script");

                    ScriptModifier.UpdateScript(
                        scriptComp,
                        newCode: newScriptCode,
                        newInputs: newInputs,
                        newOutputs: newOutputs);

                    Debug.WriteLine($"[script_generator] Successfully updated component {componentGuid}");
                    updateTcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[script_generator] Error updating component: {ex.Message}");
                    updateTcs.SetException(ex);
                }
            });

            await updateTcs.Task.ConfigureAwait(false);

            var toolResult = new JObject
            {
                ["mode"] = "edit",
                ["guid"] = guidStr,
                ["script"] = newScriptCode,
                ["inputs"] = newInputs,
                ["outputs"] = newOutputs,
                ["changesSummary"] = changesSummary,
                ["message"] = "Script component updated successfully. The changes are immediately active. Double-click the component to view the updated code in the editor.",
            };

            var outBuilder = AIBodyBuilder.Create();
            outBuilder.AddToolResult(toolResult, toolInfo.Id, toolInfo.Name, result.Metrics, result.Messages);
            var outImmutable = outBuilder.Build();
            output.CreateSuccess(outImmutable, toolCall);
            return output;
        }
    }
}
