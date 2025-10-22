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
using SmartHopper.Core.Grasshopper.Models;
using SmartHopper.Core.Grasshopper.Utils.Canvas;
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

                // Find the component on UI thread
                IScriptComponent scriptComp = null;
                string language = "unknown";
                string currentCode = string.Empty;
                var currentInputs = new JArray();
                var currentOutputs = new JArray();

                var findTcs = new TaskCompletionSource<bool>();
                RhinoApp.InvokeOnUiThread(() =>
                {
                    try
                    {
                        var component = CanvasAccess.FindInstance(componentGuid);
                        if (component == null)
                        {
                            findTcs.SetResult(false);
                            return;
                        }

                        scriptComp = component as IScriptComponent;
                        if (scriptComp == null)
                        {
                            findTcs.SetResult(false);
                            return;
                        }

                        // TASK 2 IMPLEMENTATION: Use IScriptComponent.LanguageSpec instead of type name matching
                        language = DetectLanguageFromComponent(scriptComp);
                        Debug.WriteLine($"[script_edit] Detected language: {language}");

#if DEBUG
                        // DEBUG: Log LanguageSpec properties for API discovery
                        LogLanguageSpecProperties(scriptComp);
#endif

                        // Get current code
                        currentCode = scriptComp.Text ?? string.Empty;

                        // Get current inputs
                        var ghComp = (IGH_Component)scriptComp;
                        foreach (var param in ghComp.Params.Input)
                        {
                            if (param is ScriptVariableParam svp)
                            {
                                currentInputs.Add(new JObject
                                {
                                    ["name"] = svp.NickName,
                                    ["type"] = Generic,
                                    ["description"] = svp.Description,
                                    ["access"] = svp.Access.ToString().ToLowerInvariant(),
                                });
                            }
                        }

                        // Get current outputs
                        foreach (var param in ghComp.Params.Output)
                        {
                            if (param is ScriptVariableParam svp)
                            {
                                currentOutputs.Add(new JObject
                                {
                                    ["name"] = svp.NickName,
                                    ["type"] = Generic,
                                    ["description"] = svp.Description
                                });
                            }
                        }

                        findTcs.SetResult(true);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[script_edit] Error finding component: {ex.Message}");
                        findTcs.SetException(ex);
                    }
                });

                await findTcs.Task.ConfigureAwait(false);

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

                // Update the component on UI thread
                var updateTcs = new TaskCompletionSource<bool>();
                RhinoApp.InvokeOnUiThread(() =>
                {
                    try
                    {
                        var ghComp = (IGH_Component)scriptComp;

                        // Record undo event
                        ghComp.RecordUndoEvent("[SH] Edit Script");

                        // Update script code
                        scriptComp.Text = newScriptCode;

                        // Clear existing parameters
                        foreach (var p in ghComp.Params.Input.ToArray())
                            ghComp.Params.UnregisterInputParameter(p);
                        foreach (var p in ghComp.Params.Output.ToArray())
                            ghComp.Params.UnregisterOutputParameter(p);

                        // Add new inputs
                        foreach (var input in newInputs)
                        {
                            var inputType = input["type"]?.ToString() ?? Generic;
                            if (!SupportedDataTypes.IsValidType(inputType))
                                inputType = Generic;

                            var param = new ScriptVariableParam(input["name"]?.ToString() ?? "input")
                            {
                                PrettyName = input["name"]?.ToString() ?? "Input",
                                Description = input["description"]?.ToString() ?? string.Empty,
                                Access = Enum.TryParse<GH_ParamAccess>(input["access"]?.ToString(), true, out var pa) ? pa : GH_ParamAccess.item,
                            };
                            param.CreateAttributes();
                            ghComp.Params.RegisterInputParam(param);
                        }

                        // Add new outputs
                        foreach (var outputData in newOutputs)
                        {
                            var outputType = outputData["type"]?.ToString() ?? Generic;
                            if (!SupportedDataTypes.IsValidType(outputType))
                                outputType = Generic;

                            var param = new ScriptVariableParam(outputData["name"]?.ToString() ?? "output")
                            {
                                PrettyName = outputData["name"]?.ToString() ?? "Output",
                                Description = outputData["description"]?.ToString() ?? string.Empty,
                                Access = GH_ParamAccess.item,
                            };
                            param.CreateAttributes();
                            ghComp.Params.RegisterOutputParam(param);
                        }

                        // Rebuild variable parameter UI
                        ((dynamic)scriptComp).VariableParameterMaintenance();

                        // Expire solution to trigger recompilation
                        ghComp.ExpireSolution(true);

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

        /// <summary>
        /// Uses reflection to access LanguageSpec property, avoiding compile-time dependency on Rhino.Runtime.Code assembly.
        /// This approach works cross-platform (Windows + macOS) without requiring problematic assembly references.
        /// </summary>
        private static string DetectLanguageFromComponent(IScriptComponent scriptComp)
        {
            try
            {
                // Use reflection to get LanguageSpec property value (avoids compile-time dependency)
                var langSpecProperty = scriptComp.GetType().GetProperty("LanguageSpec");
                if (langSpecProperty == null)
                {
                    Debug.WriteLine("[script_edit] LanguageSpec property not found, falling back to type name");
                    return DetectLanguageFromTypeName(scriptComp);
                }

                var langSpec = langSpecProperty.GetValue(scriptComp);
                if (langSpec == null)
                {
                    Debug.WriteLine("[script_edit] LanguageSpec is null, falling back to type name");
                    return DetectLanguageFromTypeName(scriptComp);
                }

                // Call ToString() on the LanguageSpec object (no type reference needed)
                var langStr = langSpec.ToString()?.ToLowerInvariant() ?? "unknown";
                Debug.WriteLine($"[script_edit] Language detected via LanguageSpec.ToString(): {langStr}");

                // Match common language patterns
                if (langStr.Contains("python3") || langStr.Contains("python 3"))
                {
                    Debug.WriteLine("[script_edit] Detected as Python 3");
                    return "python";
                }

                if (langStr.Contains("ironpython") || langStr.Contains("python2") || langStr.Contains("python 2"))
                {
                    Debug.WriteLine("[script_edit] Detected as IronPython 2");
                    return "ironpython";
                }

                if (langStr.Contains("csharp") || langStr.Contains("c#"))
                {
                    Debug.WriteLine("[script_edit] Detected as C#");
                    return "c#";
                }

                if (langStr.Contains("visualbasic") || langStr.Contains("vb") || langStr.Contains("visual basic"))
                {
                    Debug.WriteLine("[script_edit] Detected as VB.NET");
                    return "vb";
                }

                Debug.WriteLine($"[script_edit] Unknown language, returning: {langStr}");
                return langStr;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[script_edit] Error detecting language from LanguageSpec: {ex.Message}");
                return DetectLanguageFromTypeName(scriptComp);
            }
        }

        /// <summary>
        /// Fallback method: detect language from type name (old fragile approach).
        /// </summary>
        private static string DetectLanguageFromTypeName(IScriptComponent scriptComp)
        {
            var typeName = scriptComp.GetType().Name;
            if (typeName.Contains("Python3") || typeName.Contains("PythonScript"))
                return "python";
            else if (typeName.Contains("IronPython"))
                return "ironpython";
            else if (typeName.Contains("CSharp"))
                return "c#";
            else if (typeName.Contains("VB"))
                return "vb";

            return "unknown";
        }

#if DEBUG
        /// <summary>
        /// Debug logging to discover LanguageSpec API properties and constants.
        /// </summary>
        private static void LogLanguageSpecProperties(IScriptComponent scriptComp)
        {
            try
            {
                Debug.WriteLine("\n========== LANGUAGESPEC API DISCOVERY ==========");

                // Use reflection to avoid compile-time dependency on Rhino.Runtime.Code
                var langSpecProperty = scriptComp.GetType().GetProperty("LanguageSpec");
                var langSpec = langSpecProperty?.GetValue(scriptComp);
                if (langSpec == null)
                {
                    Debug.WriteLine("LanguageSpec is NULL");
                    return;
                }

                var langSpecType = langSpec.GetType();
                Debug.WriteLine($"LanguageSpec Type: {langSpecType.FullName}");
                Debug.WriteLine($"LanguageSpec.ToString(): {langSpec}");

                // Log instance properties
                Debug.WriteLine("\n--- LanguageSpec Instance Properties ---");
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

                // Log static fields/constants
                Debug.WriteLine("\n--- LanguageSpec Static Constants ---");
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

                // Log static properties
                Debug.WriteLine("\n--- LanguageSpec Static Properties ---");
                foreach (var prop in langSpecType.GetProperties(BindingFlags.Public | BindingFlags.Static))
                {
                    try
                    {
                        var value = prop.GetValue(null);
                        Debug.WriteLine($"  {prop.Name} ({prop.PropertyType.Name}): {value}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"  {prop.Name}: ERROR - {ex.Message}");
                    }
                }

                // Try to get Language property if it exists
                var languageProp = langSpecType.GetProperty("Language");
                if (languageProp != null)
                {
                    try
                    {
                        var language = languageProp.GetValue(langSpec);
                        Debug.WriteLine($"\n--- Language Property ---");
                        Debug.WriteLine($"Language: {language}");
                        Debug.WriteLine($"Language Type: {language?.GetType().FullName}");
                    }
                    catch { }
                }

                Debug.WriteLine("========== END LANGUAGESPEC DISCOVERY ==========\n");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LogLanguageSpecProperties] ERROR: {ex.Message}");
            }
        }
#endif
    }
}
