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
using System.Reflection;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;
using Rhino;
using RhinoCodePlatform.GH;
using RhinoCodePluginGH.Parameters;
using SmartHopper.Core.Grasshopper.Serialization.GhJson;
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
    /// Provides the "script_edit" AI tool for editing existing script components based on user instructions.
    /// </summary>
    public class script_edit : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "script_edit";

        /// <summary>
        /// Returns the list of AI tools provided by this class.
        /// </summary>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Edit an existing script component by GUID. Modifies the script code, inputs, and outputs based on user instructions. The component will be updated without opening the editor, avoiding UI locks.",
                category: "Scripting",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""guid"": { 
                            ""type"": ""string"", 
                            ""description"": ""GUID of the script component to edit. Use gh_get_selected or gh_get to find it."" 
                        },
                        ""prompt"": { 
                            ""type"": ""string"", 
                            ""description"": ""Instructions for editing the script. Describe what changes to make."" 
                        },
                        ""includeCurrentCode"": {
                            ""type"": ""boolean"",
                            ""description"": ""Whether to include the current script code in the AI prompt for context. Default is true."",
                            ""default"": true
                        }
                    },
                    ""required"": [""guid"", ""prompt""]
                }",
                execute: this.ScriptEditToolAsync,
                requiredCapabilities: AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput);
        }

        /// <summary>
        /// Executes the "script_edit" tool: edits an existing script component based on user instructions.
        /// </summary>
        private async Task<AIReturn> ScriptEditToolAsync(AIToolCall toolCall)
        {
            var output = new AIReturn()
            {
                Request = toolCall,
            };

            try
            {
                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.Arguments ?? new JObject();
                var guidStr = args["guid"]?.ToString() ?? throw new ArgumentException("Missing 'guid' parameter.");
                var prompt = args["prompt"]?.ToString() ?? throw new ArgumentException("Missing 'prompt' parameter.");
                var includeCurrentCode = args["includeCurrentCode"]?.ToObject<bool>() ?? true;
                var providerName = toolCall.Provider;
                var modelName = toolCall.Model;
                var endpoint = this.toolName;
                string? contextFilter = args["contextFilter"]?.ToString() ?? string.Empty;

                if (!Guid.TryParse(guidStr, out var componentGuid))
                {
                    output.CreateError($"Invalid GUID format: {guidStr}");
                    return output;
                }

                // Find the component and extract current state using GhJsonSerializer
                IScriptComponent scriptComp = null;
                string language = "unknown";
                string currentCode = string.Empty;
                var currentInputs = new JArray();
                var currentOutputs = new JArray();

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

                        // Use ScriptComponentFactory for language detection
                        language = ScriptComponentFactory.DetectLanguage(scriptComp);
                        Debug.WriteLine($"[script_edit] Detected language: {language}");

                        // Use GhJsonSerializer to extract current state (preserves type hints!)
                        var componentsList = new List<IGH_ActiveObject> { (IGH_ActiveObject)scriptComp };
                        var document = GhJsonSerializer.Serialize(componentsList, SerializationOptions.Standard);
                        var currentProps = document.Components.FirstOrDefault();

                        if (currentProps != null)
                        {
                            // Extract script code from componentState.value
                            currentCode = currentProps.ComponentState?.Value?.ToString() ?? string.Empty;

                            // Convert InputSettings to AI format (preserves type hints)
                            if (currentProps.InputSettings != null)
                            {
                                foreach (var setting in currentProps.InputSettings)
                                {
                                    currentInputs.Add(new JObject
                                    {
                                        ["name"] = setting.VariableName ?? setting.ParameterName,
                                        ["type"] = setting.TypeHint ?? "object", // Actual type hint preserved!
                                        ["description"] = "",
                                        ["access"] = setting.Access ?? "item"
                                    });
                                }
                            }

                            // Convert OutputSettings to AI format (preserves type hints)
                            if (currentProps.OutputSettings != null)
                            {
                                foreach (var setting in currentProps.OutputSettings)
                                {
                                    currentOutputs.Add(new JObject
                                    {
                                        ["name"] = setting.VariableName ?? setting.ParameterName,
                                        ["type"] = setting.TypeHint ?? "object", // Actual type hint preserved!
                                        ["description"] = ""
                                    });
                                }
                            }
                        }
                        else
                        {
                            // Fallback if serialization fails
                            currentCode = scriptComp.Text ?? string.Empty;
                            Debug.WriteLine($"[script_edit] Warning: GhJsonSerializer returned no data, using fallback");
                        }

                        extractTcs.SetResult(true);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[script_edit] Error extracting component: {ex.Message}");
                        extractTcs.SetException(ex);
                    }
                });

                await extractTcs.Task.ConfigureAwait(false);

                if (scriptComp == null)
                {
                    output.CreateError($"Component with GUID {guidStr} not found or is not a script component.");
                    return output;
                }

                // Define JSON schema for structured output
                var jsonSchema = @"{
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
                }".Replace('"', '"');

                // Build AI request
                var bodyBuilder = AIBodyBuilder.Create()
                    .WithJsonOutputSchema(jsonSchema)
                    .WithContextFilter(contextFilter)
                    .AddSystem($"""
                    You are a Grasshopper script component editor. Edit the existing {language} script based on the user's instructions.

                    Your response MUST be a valid JSON object with the following structure:
                    - script: The complete updated script code
                    - inputs: Array of input parameters with name, type, description, and access (item/list/tree)
                    - outputs: Array of output parameters with name, type, and description
                    - changesSummary: Brief description of what was changed

                    The JSON object will be parsed programmatically, so it must be valid JSON with no additional text.
                    """);

                if (includeCurrentCode)
                {
                    bodyBuilder.AddUser($"""
                    Current script code:
                    ```{language}
                    {currentCode}
                    ```

                    Current inputs: {currentInputs}
                    Current outputs: {currentOutputs}

                    User instructions: {prompt}
                    """);
                }
                else
                {
                    bodyBuilder.AddUser(prompt);
                }

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

                Debug.WriteLine($"[script_edit] New script length: {newScriptCode.Length}");
                Debug.WriteLine($"[script_edit] New inputs: {newInputs.Count}, outputs: {newOutputs.Count}");

                // Update the component on UI thread using ScriptModifier
                var updateTcs = new TaskCompletionSource<bool>();
                RhinoApp.InvokeOnUiThread(() =>
                {
                    try
                    {
                        var ghComp = (IGH_Component)scriptComp;

                        // Record undo event
                        ghComp.RecordUndoEvent("[SH] Edit Script");

                        // Use ScriptModifier to update the script component
                        ScriptModifier.UpdateScript(
                            scriptComp,
                            newCode: newScriptCode,
                            newInputs: newInputs,
                            newOutputs: newOutputs);

                        Debug.WriteLine($"[script_edit] Successfully updated component {componentGuid}");
                        updateTcs.SetResult(true);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[script_edit] Error updating component: {ex.Message}");
                        updateTcs.SetException(ex);
                    }
                });

                await updateTcs.Task.ConfigureAwait(false);

                var toolResult = new JObject
                {
                    ["guid"] = guidStr,
                    ["script"] = newScriptCode,
                    ["inputs"] = newInputs,
                    ["outputs"] = newOutputs,
                    ["changesSummary"] = changesSummary,
                    ["message"] = "Script component updated successfully. The changes are immediately active. Double-click the component to view the updated code in the editor."
                };

                var outBuilder = AIBodyBuilder.Create();
                outBuilder.AddToolResult(toolResult, toolInfo.Id, toolInfo.Name, result.Metrics, result.Messages);
                var outImmutable = outBuilder.Build();
                output.CreateSuccess(outImmutable, toolCall);
                return output;
            }
            catch (Exception ex)
            {
                output.CreateError(ex.Message);
                return output;
            }
        }
    }
}
