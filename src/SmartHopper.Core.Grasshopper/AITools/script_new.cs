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
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;
using Rhino;
using SmartHopper.Core.Grasshopper.Models;
using SmartHopper.Core.Grasshopper.Utils;
using SmartHopper.Core.Models.Components;
using SmartHopper.Core.Models.Document;
using SmartHopper.Infrastructure.AICall;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.Infrastructure.Utils;
using static SmartHopper.Core.Grasshopper.Models.SupportedDataTypes;


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
                requiredCapabilities: AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput
            );
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
                AIInteractionToolCall toolInfo = toolCall.GetToolCall();;
                var prompt = toolInfo.Arguments["prompt"]?.ToString() ?? throw new ArgumentException("Missing 'prompt' parameter.");
                var language = toolInfo.Arguments["language"]?.ToString() ?? "python";
                var providerName = toolCall.Provider;
                var modelName = toolCall.Model;
                var endpoint = this.toolName;
                string? contextFilter = toolInfo.Arguments["contextFilter"]?.ToString() ?? string.Empty;

                var langKey = language.Trim().ToLowerInvariant();
                string objectType;
                string displayName;
                Guid componentGuid;
                switch (langKey)
                {
                    case "python":
                    case "python3":
                        objectType = "RhinoCodePluginGH.Components.Python3Component";
                        displayName = "Python 3 Script";
                        break;
                    case "ironpython":
                    case "ironpython2":
                        objectType = "RhinoCodePluginGH.Components.IronPython2Component";
                        displayName = "IronPython 2 Script";
                        break;
                    case "c#":
                    case "csharp":
                        objectType = "RhinoCodePluginGH.Components.CSharpComponent";
                        displayName = "C# Script";
                        break;
                    case "vb":
                    case "vb.net":
                    case "vbnet":
                        objectType = "ScriptComponents.Component_VBNET_Script";
                        displayName = "VB Script";
                        break;
                    default:
                        throw new ArgumentException($"Unsupported language: {language}. Supported: python, ironpython, c#, vb.");
                }

                // Discover component GUID dynamically
                var proxy = GHObjectFactory.FindProxy(displayName)
                               ?? throw new Exception($"Component type '{displayName}' not found in this Grasshopper installation.");
                IGH_Component tempComp = null;
                // Instantiate component proxy on UI thread
                RhinoApp.InvokeOnUiThread(() =>
                {
                    var inst = GHObjectFactory.CreateInstance(proxy);
                    tempComp = inst as IGH_Component;
                });
                if (tempComp == null)
                    throw new Exception($"Proxy for '{displayName}' did not create an IGH_Component.");
                componentGuid = tempComp.ComponentGuid;

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

                // Build AIRequestCall with schema and context filter
                var requestBody = new AIBody
                {
                    JsonOutputSchema = jsonSchema,
                    ContextFilter = contextFilter,
                };
                requestBody.AddInteraction(AIAgent.System, $"""
                    You are a Grasshopper script component generator. Generate a complete {language} script for a Grasshopper script component based on the user prompt.
                    
                    Your response MUST be a valid JSON object with the following structure:
                    - script: The complete script code
                    - inputs: Array of input parameters with name, type, description, and access (item/list/tree)
                    - outputs: Array of output parameters with name, type, and description
                    
                    The JSON object will be parsed programmatically, so it must be valid JSON with no additional text.
                    """);
                requestBody.AddInteraction(AIAgent.User, prompt);

                var request = new AIRequestCall();
                request.Initialize(
                    provider: providerName,
                    model: modelName,
                    capability: AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                    endpoint: endpoint,
                    body: requestBody);

                var result = await request.Exec().ConfigureAwait(false);
                if (!result.Success)
                {
                    output.CreateError(result.ErrorMessage);
                    return output;
                }

                var response = result.Body.GetLastInteraction(AIAgent.Assistant).ToString();
                var responseJson = JObject.Parse(response);
                var scriptCode = responseJson["script"]?.ToString() ?? string.Empty;
                var inputs = responseJson["inputs"] as JArray ?? new JArray();
                var outputs = responseJson["outputs"] as JArray ?? new JArray();

                // Log the parsed values for debugging
                Debug.WriteLine($"[ScriptNewTool] Parsed script length: {scriptCode.Length}");
                Debug.WriteLine($"[ScriptNewTool] Inputs: {inputs.Count}, Outputs: {outputs.Count}");

                // Create script inputs with type validation
                var scriptInputs = new JArray();
                foreach (var input in inputs)
                {
                    var inputType = input["type"]?.ToString() ?? Generic;

                    // Validate input type
                    if (!SupportedDataTypes.IsValidType(inputType))
                    {
                        inputType = Generic; // Fallback to Generic for unsupported types
                        Debug.WriteLine($"[ScriptNewTool] Unsupported input type: {inputType}, falling back to Generic");
                    }

                    var inputObj = new JObject
                    {
                        ["variableName"] = input["name"]?.ToString() ?? "input",
                        ["name"] = input["name"]?.ToString() ?? "Input",
                        ["description"] = input["description"]?.ToString() ?? string.Empty,
                        ["access"] = input["access"]?.ToString()?.ToLower() ?? "item",
                        ["type"] = inputType
                    };
                    scriptInputs.Add(inputObj);
                }

                var scriptOutputs = new JArray();
                foreach (var data in outputs)
                {
                    var outputType = data["type"]?.ToString() ?? Generic;

                    // Validate output type
                    if (!SupportedDataTypes.IsValidType(outputType))
                    {
                        outputType = Generic; // Fallback to Generic for unsupported types
                        Debug.WriteLine($"[ScriptNewTool] Unsupported output type: {outputType}, falling back to Generic");
                    }

                    var outputObj = new JObject
                    {
                        ["variableName"] = data["name"]?.ToString() ?? "output",
                        ["name"] = data["name"]?.ToString() ?? "Output",
                        ["description"] = data["description"]?.ToString() ?? string.Empty,
                        ["type"] = outputType,
                    };
                    scriptOutputs.Add(outputObj);
                }

                // Create document with one script component
                var doc = new GrasshopperDocument();
                var comp = new ComponentProperties
                {
                    Name = displayName,
                    Type = "IGH_Component",
                    ObjectType = objectType,
                    ComponentGuid = componentGuid,
                    InstanceGuid = Guid.NewGuid(),
                    Properties = new Dictionary<string, ComponentProperty>
                    {
                        ["Script"] = new ComponentProperty
                        {
                            Value = scriptCode,
                            Type = typeof(string).Name,
                            HumanReadable = scriptCode
                        },
                        ["ScriptInputs"] = new ComponentProperty
                        {
                            Value = scriptInputs,
                            Type = typeof(JArray).Name
                        },
                        ["ScriptOutputs"] = new ComponentProperty
                        {
                            Value = scriptOutputs,
                            Type = typeof(JArray).Name
                        },
                    }
                };
                doc.Components.Add(comp);

                // Place the component and retrieve mapping on UI thread via TaskCompletionSource
                var tcs = new TaskCompletionSource<Dictionary<Guid, Guid>>();
                Rhino.RhinoApp.InvokeOnUiThread(() =>
                {
                    try
                    {
                        var map = Put.PutObjectsOnCanvasWithMapping(doc);
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
                    var toolResult = new JObject();
                    toolResult.Add("script", scriptCode);
                    toolResult.Add("guid", actualGuid.ToString());
                    toolResult.Add("inputs", scriptInputs);
                    toolResult.Add("outputs", scriptOutputs);

                    var toolBody = new AIBody();
                    toolBody.AddInteractionToolResult(toolResult, result.Metrics);

                    output.CreateSuccess(toolBody);
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
