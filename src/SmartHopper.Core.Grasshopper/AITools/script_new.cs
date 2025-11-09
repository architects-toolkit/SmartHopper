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
using Newtonsoft.Json.Linq;
using Rhino;
using SmartHopper.Core.Grasshopper.Serialization.Canvas;
using SmartHopper.Core.Grasshopper.Serialization.GhJson;
using SmartHopper.Core.Grasshopper.Serialization.GhJson.ScriptComponents;
using SmartHopper.Core.Grasshopper.Utils.Canvas;
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
    /// Provides the "script_new" AI tool for generating new script components based on user instructions.
    /// </summary>
    public class script_new : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "script_new";

        /// <summary>
        /// Returns the list of AI tools provided by this class.
        /// </summary>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Generate a script component in the specified language (default python) based on user instructions and place it on the canvas.",
                category: "Scripting",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""prompt"": { ""type"": ""string"", ""description"": ""Instructions for generating the script."" },
                        ""language"": { ""type"": ""string"", ""description"": ""Python (default), C#, IronPython or VB."" }
                    },
                    ""required"": [""prompt""]
                }",
                execute: this.ScriptNewToolAsync,
                requiredCapabilities: AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput);
        }

        /// <summary>
        /// Executes the "script_new" tool: generates a script component based on user instructions and places it on the canvas.
        /// </summary>
        private async Task<AIReturn> ScriptNewToolAsync(AIToolCall toolCall)
        {
            // Prepare the output
            var output = new AIReturn()
            {
                Request = toolCall,
            };

            try
            {
                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.Arguments ?? new JObject();
                var prompt = args["prompt"]?.ToString() ?? throw new ArgumentException("Missing 'prompt' parameter.");
                var language = args["language"]?.ToString() ?? "python";
                var providerName = toolCall.Provider;
                var modelName = toolCall.Model;
                var endpoint = this.toolName;
                string? contextFilter = args["contextFilter"]?.ToString() ?? string.Empty;

                // Validate language is supported
                var componentInfo = ScriptComponentFactory.GetComponentInfo(language);
                if (componentInfo == null)
                {
                    var supported = string.Join(", ", ScriptComponentFactory.GetSupportedLanguages());
                    output.CreateError($"Unsupported language: {language}. Supported: {supported}");
                    return output;
                }

                Debug.WriteLine($"[script_new] Creating {componentInfo.DisplayName} component...");

                // Define JSON schema for structured output
                var jsonSchema = @"{
                    ""type"": ""object"",
                    ""properties"": {
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
                    ""required"": [""script"", ""inputs"", ""outputs""],
                    ""additionalProperties"": false
                }".Replace('"', '"');

                // Build AIRequestCall with schema and context filter using immutable body
                var bodyBuilder = AIBodyBuilder.Create()
                    .WithJsonOutputSchema(jsonSchema)
                    .WithContextFilter(contextFilter)
                    .AddSystem($"""
                    You are a Grasshopper script component generator. Generate a complete {language} script for a Grasshopper script component based on the user prompt.

                    Your response MUST be a valid JSON object with the following structure:
                    - script: The complete script code
                    - inputs: Array of input parameters with name, type, description, and access (item/list/tree)
                    - outputs: Array of output parameters with name, type, and description

                    The JSON object will be parsed programmatically, so it must be valid JSON with no additional text.
                    """)
                    .AddUser(prompt);
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
                    // Propagate structured messages from AI call
                    output.Messages = result.Messages;
                    return output;
                }

                var response = result.Body.GetLastInteraction(AIAgent.Assistant).ToString();
                var responseJson = JObject.Parse(response);
                var scriptCode = responseJson["script"]?.ToString() ?? string.Empty;
                var inputs = responseJson["inputs"] as JArray ?? new JArray();
                var outputs = responseJson["outputs"] as JArray ?? new JArray();

                // Log the parsed values for debugging
                Debug.WriteLine($"[script_new] Parsed script length: {scriptCode.Length}");
                Debug.WriteLine($"[script_new] Inputs: {inputs.Count}, Outputs: {outputs.Count}");

                // Use ScriptComponentFactory to create ComponentProperties
                // This centralizes script component knowledge and eliminates duplication
                var comp = ScriptComponentFactory.CreateScriptComponent(
                    language,
                    scriptCode,
                    inputs,
                    outputs,
                    nickname: "AI Generated Script");

                // Create document with the script component
                var doc = new GrasshopperDocument();
                doc.Components.Add(comp);

                // Place the component and retrieve mapping on UI thread via TaskCompletionSource
                var tcs = new TaskCompletionSource<Dictionary<Guid, Guid>>();
                Rhino.RhinoApp.InvokeOnUiThread(() =>
                {
                    try
                    {
                        var result = GhJsonDeserializer.Deserialize(doc, DeserializationOptions.Standard);
                        ComponentPlacer.PlaceComponents(result);
                        // Convert GuidMapping to Dictionary<Guid, Guid>
                        var map = result.GuidMapping.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.InstanceGuid);
                        tcs.SetResult(map);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                });
                var mapping = await tcs.Task.ConfigureAwait(false);

                // Retrieve actual placed GUID
                if (mapping.TryGetValue(comp.InstanceGuid, out var actualGuid))
                {
                    // Force the component to refresh its solution after script is set
                    // This ensures the script is properly compiled without requiring restart
                    var refreshTcs = new TaskCompletionSource<bool>();
                    Rhino.RhinoApp.InvokeOnUiThread(() =>
                    {
                        try
                        {
                            var placedComponent = CanvasAccess.FindInstance(actualGuid);
                            if (placedComponent != null)
                            {
                                // Expire the solution to trigger recompilation
                                placedComponent.ExpireSolution(true);
                                Debug.WriteLine($"[script_new] Expired solution for component {actualGuid}");
                            }
                            refreshTcs.SetResult(true);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[script_new] Could not refresh component: {ex.Message}");
                            refreshTcs.SetResult(false);
                        }
                    });
                    await refreshTcs.Task.ConfigureAwait(false);

                    var toolResult = new JObject();
                    toolResult.Add("script", scriptCode);
                    toolResult.Add("guid", actualGuid.ToString());
                    toolResult.Add("language", componentInfo.LanguageKey);
                    toolResult.Add("componentName", componentInfo.DisplayName);
                    toolResult.Add("inputCount", inputs.Count);
                    toolResult.Add("outputCount", outputs.Count);
                    toolResult.Add("message", "Script component created successfully. Double-click the component to view/edit the code.");

                    var outBuilder = AIBodyBuilder.Create();
                    outBuilder.AddToolResult(toolResult, toolInfo.Id, toolInfo.Name, result.Metrics, result.Messages);
                    var outImmutable = outBuilder.Build();
                    output.CreateSuccess(outImmutable, toolCall);
                    return output;
                }

                output.CreateError("Failed to retrieve placed component GUID.");
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
