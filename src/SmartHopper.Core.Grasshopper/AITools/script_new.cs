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
using RhinoCodePlatform.GH.Context;
using RhinoCodePluginGH.Components;
using SmartHopper.Core.Grasshopper.Models;
using SmartHopper.Core.Grasshopper.Utils.Canvas;
using SmartHopper.Core.Grasshopper.Utils.Internal;
using SmartHopper.Core.Models.Components;
using SmartHopper.Core.Models.Document;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.AITools;
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

                var langKey = language.Trim().ToLowerInvariant();
                IGH_Component tempComp = null;
                Guid componentGuid;
                string displayName = "Script Component"; // Default name, will be set in switch statement

                Debug.WriteLine($"[script_new] Creating component for language: {langKey}");

                // Create component using factory methods on UI thread
                RhinoApp.InvokeOnUiThread(() =>
                {
                    try
                    {
                        switch (langKey)
                        {
                            case "python":
                            case "python3":
                                displayName = "Python 3 Script";
                                Debug.WriteLine($"[script_new] Creating Python3Component using factory method...");
                                tempComp = Python3Component.Create(
                                    title: "AI Generated Script",
                                    source: string.Empty, // Will be set later
                                    icon: null,
                                    emptyParams: false
                                );
                                Debug.WriteLine($"[script_new] Python3Component created successfully");
                                break;

                            case "ironpython":
                            case "ironpython2":
                                displayName = "IronPython 2 Script";
                                Debug.WriteLine($"[script_new] Creating IronPython2Component using factory method...");
                                tempComp = IronPython2Component.Create(
                                    title: "AI Generated Script",
                                    source: string.Empty, // Will be set later
                                    icon: null,
                                    emptyParams: false
                                );
                                Debug.WriteLine($"[script_new] IronPython2Component created successfully");
                                break;

                            case "c#":
                            case "csharp":
                                displayName = "C# Script";
                                Debug.WriteLine($"[script_new] Creating CSharpComponent using factory method...");
                                tempComp = CSharpComponent.Create(
                                    title: "AI Generated Script",
                                    source: string.Empty, // Will be set later
                                    icon: null,
                                    emptyParams: false
                                );
                                Debug.WriteLine($"[script_new] CSharpComponent created successfully");
                                break;

                            case "vb":
                            case "vb.net":
                            case "vbnet":
                                // VB.NET uses legacy approach (not part of RhinoCodePlatform)
                                displayName = "VB Script";
                                Debug.WriteLine($"[script_new] Creating VB component using legacy ObjectFactory...");
                                var proxy = ObjectFactory.FindProxy(displayName)
                                           ?? throw new Exception($"VB Script component not found in this Grasshopper installation.");
                                var inst = ObjectFactory.CreateInstance(proxy);
                                tempComp = inst as IGH_Component;
                                Debug.WriteLine($"[script_new] VB component created successfully");
                                break;

                            default:
                                throw new ArgumentException($"Unsupported language: {language}. Supported: python, ironpython, c#, vb.");
                        }

                        if (tempComp == null)
                        {
                            throw new Exception($"Failed to create component for language: {langKey}");
                        }

#if DEBUG
                        // DEBUG: Log all component properties to discover API surface
                        LogComponentProperties(tempComp, langKey);
#endif
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[script_new] ERROR creating component: {ex.Message}");
                        Debug.WriteLine($"[script_new] Stack trace: {ex.StackTrace}");
                        throw;
                    }
                });

                if (tempComp == null)
                {
                    throw new Exception($"Failed to instantiate component for language: {langKey}");
                }

                componentGuid = tempComp.ComponentGuid;
                Debug.WriteLine($"[script_new] Component GUID: {componentGuid}");

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
                        ["access"] = input["access"]?.ToString()?.ToLowerInvariant() ?? "item",
                        ["type"] = inputType,
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
                    ComponentGuid = componentGuid,
                    InstanceGuid = Guid.NewGuid(),
                    Params = new Dictionary<string, object>
                    {
                        ["Script"] = scriptCode,
                        ["ScriptInputs"] = scriptInputs,
                        ["ScriptOutputs"] = scriptOutputs,
                    },
                };
                doc.Components.Add(comp);

                // Place the component and retrieve mapping on UI thread via TaskCompletionSource
                var tcs = new TaskCompletionSource<Dictionary<Guid, Guid>>();
                Rhino.RhinoApp.InvokeOnUiThread(() =>
                {
                    try
                    {
                        var map = GhJsonPlacer.PutObjectsOnCanvasWithMapping(doc);
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
                    toolResult.Add("inputs", scriptInputs);
                    toolResult.Add("outputs", scriptOutputs);
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

#if DEBUG
        /// <summary>
        /// Comprehensive debug logging to discover all available API properties, methods, and values at runtime.
        /// This helps complete the documentation of the RhinoCodePlatform API.
        /// </summary>
        private static void LogComponentProperties(IGH_Component component, string language)
        {
            try
            {
                Debug.WriteLine($"\n========== API DISCOVERY FOR {language.ToUpper()} COMPONENT ==========");
                Debug.WriteLine($"Component Type: {component.GetType().FullName}");
                Debug.WriteLine($"Component GUID: {component.ComponentGuid}");
                Debug.WriteLine($"Component Name: {component.Name}");
                Debug.WriteLine($"Component NickName: {component.NickName}");
                Debug.WriteLine($"Component Description: {component.Description}");
                Debug.WriteLine($"Component Category: {component.Category}");
                Debug.WriteLine($"Component SubCategory: {component.SubCategory}");

                // Try to cast to IScriptComponent to access LanguageSpec
                if (component is IScriptComponent scriptComp)
                {
                    Debug.WriteLine($"\n--- IScriptComponent Properties ---");
                    try
                    {
                        // Use reflection to avoid compile-time dependency on Rhino.Runtime.Code
                        var langSpecProperty = scriptComp.GetType().GetProperty("LanguageSpec");
                        var langSpec = langSpecProperty?.GetValue(scriptComp);
                        Debug.WriteLine($"LanguageSpec: {langSpec}");
                        Debug.WriteLine($"LanguageSpec Type: {langSpec?.GetType().FullName}");

                        // Try to discover LanguageSpec properties
                        if (langSpec != null)
                        {
                            var langSpecType = langSpec.GetType();
                            Debug.WriteLine($"\n--- LanguageSpec Properties ---");
                            foreach (var prop in langSpecType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                            {
                                try
                                {
                                    var value = prop.GetValue(langSpec);
                                    Debug.WriteLine($"  {prop.Name} ({prop.PropertyType.Name}): {value}");
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"  {prop.Name}: ERROR - {ex.Message}");
                                }
                            }

                            // Try to discover LanguageSpec static properties/constants
                            Debug.WriteLine($"\n--- LanguageSpec Static Properties ---");
                            foreach (var field in langSpecType.GetFields(BindingFlags.Public | BindingFlags.Static))
                            {
                                try
                                {
                                    var value = field.GetValue(null);
                                    Debug.WriteLine($"  {field.Name} ({field.FieldType.Name}): {value}");
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"  {field.Name}: ERROR - {ex.Message}");
                                }
                            }
                        }

                        // Check for Text property (script source code)
                        var textProp = scriptComp.GetType().GetProperty("Text");
                        if (textProp != null)
                        {
                            Debug.WriteLine($"Text Property Type: {textProp.PropertyType.FullName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error accessing IScriptComponent properties: {ex.Message}");
                    }
                }

                // Check for IScriptObject interface
                if (component is IScriptObject scriptObj)
                {
                    Debug.WriteLine($"\n--- IScriptObject Interface Detected ---");
                    var scriptObjType = component.GetType().GetInterface("IScriptObject");
                    if (scriptObjType != null)
                    {
                        Debug.WriteLine($"IScriptObject methods:");
                        foreach (var method in scriptObjType.GetMethods())
                        {
                            var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                            Debug.WriteLine($"  {method.Name}({parameters}): {method.ReturnType.Name}");
                        }
                    }
                }

                // Log all parameters
                Debug.WriteLine($"\n--- Component Parameters ---");
                Debug.WriteLine($"Input Parameters: {component.Params.Input.Count}");
                for (int i = 0; i < component.Params.Input.Count; i++)
                {
                    var param = component.Params.Input[i];
                    Debug.WriteLine($"  Input[{i}]: {param.Name} ({param.GetType().Name})");
                    Debug.WriteLine($"    NickName: {param.NickName}");
                    Debug.WriteLine($"    Description: {param.Description}");
                    Debug.WriteLine($"    Access: {param.Access}");
                    Debug.WriteLine($"    Optional: {param.Optional}");
                    
                    // Check for ScriptVariableParam
                    if (param.GetType().Name.Contains("ScriptVariable"))
                    {
                        Debug.WriteLine($"    ** ScriptVariableParam detected **");
                        LogScriptVariableParamProperties(param);
                    }
                }

                Debug.WriteLine($"Output Parameters: {component.Params.Output.Count}");
                for (int i = 0; i < component.Params.Output.Count; i++)
                {
                    var param = component.Params.Output[i];
                    Debug.WriteLine($"  Output[{i}]: {param.Name} ({param.GetType().Name})");
                    Debug.WriteLine($"    NickName: {param.NickName}");
                    Debug.WriteLine($"    Description: {param.Description}");
                }

                // Log all public properties of the component
                Debug.WriteLine($"\n--- All Public Component Properties ---");
                var componentType = component.GetType();
                foreach (var prop in componentType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    try
                    {
                        var value = prop.GetValue(component);
                        var valueStr = value?.ToString() ?? "null";
                        if (valueStr.Length > 100) valueStr = valueStr.Substring(0, 100) + "...";
                        Debug.WriteLine($"  {prop.Name} ({prop.PropertyType.Name}): {valueStr}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"  {prop.Name}: ERROR - {ex.Message}");
                    }
                }

                // Log base types and interfaces
                Debug.WriteLine($"\n--- Type Hierarchy ---");
                var baseType = componentType.BaseType;
                while (baseType != null && baseType != typeof(object))
                {
                    Debug.WriteLine($"  Base: {baseType.FullName}");
                    baseType = baseType.BaseType;
                }

                Debug.WriteLine($"\n--- Implemented Interfaces ---");
                foreach (var iface in componentType.GetInterfaces())
                {
                    Debug.WriteLine($"  {iface.FullName}");
                }

                Debug.WriteLine($"========== END API DISCOVERY ==========\n");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LogComponentProperties] ERROR: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Log properties specific to ScriptVariableParam to discover parameter API.
        /// </summary>
        private static void LogScriptVariableParamProperties(IGH_Param param)
        {
            try
            {
                var paramType = param.GetType();
                Debug.WriteLine($"      --- ScriptVariableParam Properties ---");
                
                // Try to get VariableName property
                var varNameProp = paramType.GetProperty("VariableName");
                if (varNameProp != null)
                {
                    Debug.WriteLine($"      VariableName: {varNameProp.GetValue(param)}");
                }

                // Try to get PrettyName property
                var prettyNameProp = paramType.GetProperty("PrettyName");
                if (prettyNameProp != null)
                {
                    Debug.WriteLine($"      PrettyName: {prettyNameProp.GetValue(param)}");
                }

                // Try to get Converter property
                var converterProp = paramType.GetProperty("Converter");
                if (converterProp != null)
                {
                    var converter = converterProp.GetValue(param);
                    Debug.WriteLine($"      Converter: {converter?.GetType().FullName ?? "null"}");
                    
                    // If converter exists, log its properties
                    if (converter != null)
                    {
                        var converterType = converter.GetType();
                        Debug.WriteLine($"      --- Converter Properties ---");
                        foreach (var prop in converterType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                        {
                            try
                            {
                                var value = prop.GetValue(converter);
                                Debug.WriteLine($"        {prop.Name}: {value}");
                            }
                            catch { }
                        }
                    }
                }

                // Try to get TypeHints property
                var typeHintsProp = paramType.GetProperty("TypeHints");
                if (typeHintsProp != null)
                {
                    Debug.WriteLine($"      TypeHints: {typeHintsProp.GetValue(param)?.GetType().FullName}");
                }

                // Log all properties
                Debug.WriteLine($"      --- All Properties ---");
                foreach (var prop in paramType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    try
                    {
                        var value = prop.GetValue(param);
                        var valueStr = value?.ToString() ?? "null";
                        if (valueStr.Length > 50) valueStr = valueStr.Substring(0, 50) + "...";
                        Debug.WriteLine($"        {prop.Name} ({prop.PropertyType.Name}): {valueStr}");
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"      [LogScriptVariableParamProperties] ERROR: {ex.Message}");
            }
        }
#endif
    }
}
