/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

/*
 * ScriptTools.cs
 * Provides AI tools for reviewing C# script components within Grasshopper.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Grasshopper.GUI.Script;
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;
using Rhino;
using RhinoCodePlatform.GH;
using SmartHopper.Config.Interfaces;
using SmartHopper.Config.Models;
using SmartHopper.Core.AI;
using SmartHopper.Core.Grasshopper.Models;
using SmartHopper.Core.Grasshopper.Utils;
using SmartHopper.Core.Models.Components;
using SmartHopper.Core.Models.Document;

using static SmartHopper.Core.Grasshopper.Models.SupportedDataTypes;


namespace SmartHopper.Core.Grasshopper.Tools
{
    /// <summary>
    /// Provides the "script_review" AI tool for code reviewing script components.
    /// </summary>
    public class ScriptTools : IAIToolProvider
    {
        #region Tool Registration
        
        /// <summary>
        /// Returns the list of AI tools provided by this class.
        /// </summary>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: "script_review",
                description: "Return a code review for the script component specified by its GUID.",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""guid"": {
                            ""type"": ""string"",
                            ""description"": ""Instance GUID of the component to review.""
                        },
                        ""question"": {
                            ""type"": ""string"",
                            ""description"": ""Optional user question or focus for the review.""
                        }
                    },
                    ""required"": [""guid""]
                }",
                execute: this.ScriptReviewToolAsync
            );
            yield return new AITool(
                name: "script_edit",
                description: "Modify a script component's code per user instructions and apply changes to the component.",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""guid"": { ""type"": ""string"", ""description"": ""Instance GUID of the component to edit."" },
                        ""instructions"": { ""type"": ""string"", ""description"": ""User instructions for code modifications."" }
                    },
                    ""required"": [""guid"", ""instructions""]
                }",
                execute: this.ScriptEditToolAsync);
            yield return new AITool(
                name: "script_new",
                description: "Generate a script component in the specified language (default python) based on user instructions and place it on the canvas.",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""prompt"": { ""type"": ""string"", ""description"": ""Instructions for generating the script."" },
                        ""language"": { ""type"": ""string"", ""description"": ""Python (default), C#, IronPython or VB."" }
                    },
                    ""required"": [""prompt""]
                }",
                execute: this.ScriptNewToolAsync);
        }

        #endregion

        #region ScriptReview

        /// <summary>
        /// Executes the "script_review" tool: retrieves the component by GUID, runs coded checks, and obtains an AI-based review.
        /// </summary>
        /// <param name="parameters">JSON object with "guid" field.</param>
        /// <returns>Result containing success flag, codedIssues array, and aiReview string, or error details.</returns>
        private async Task<object> ScriptReviewToolAsync(JObject parameters)
        {
            try
            {
                // Parse and validate parameters
                var guidStr = parameters.Value<string>("guid") ?? throw new ArgumentException("Missing 'guid' parameter.");
                if (!Guid.TryParse(guidStr, out var scriptGuid))
                    throw new ArgumentException($"Invalid GUID: {guidStr}");
                var providerName = parameters["provider"]?.ToString() ?? string.Empty;
                var modelName = parameters["model"]?.ToString() ?? string.Empty;
                var question = parameters["question"]?.ToString();

                // Retrieve the script component from the current canvas
                var objects = GHCanvasUtils.GetCurrentObjects();
                var target = objects.FirstOrDefault(o => o.InstanceGuid == scriptGuid) as IScriptComponent;
                if (target == null)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"Script component with GUID {scriptGuid} not found."
                    };
                }

                var scriptCode = target.Text ?? string.Empty;

                // Coded static checks by language
                var codedIssues = new List<string>();

                // Always check for TODO comments
                if (scriptCode.IndexOf("TODO", StringComparison.OrdinalIgnoreCase) >= 0)
                    codedIssues.Add("Found TODO comments in script.");

                // Check line count
                var lineCount = scriptCode.Split('\n').Length;
                if (lineCount > 200)
                    codedIssues.Add($"Script has {lineCount} lines; consider refactoring into smaller methods.");

                // Language-specific debug checks
                if (Regex.IsMatch(scriptCode, @"^\s*def\s+", RegexOptions.Multiline | RegexOptions.IgnoreCase) 
                    || scriptCode.IndexOf("import ", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // Python/IronPython
                    var debugPy = Regex.Matches(scriptCode, @"print\(", RegexOptions.IgnoreCase).Count;
                    if (debugPy > 0)
                        codedIssues.Add("Remove debug 'print' statements from Python script.");
                }
                else if (Regex.IsMatch(scriptCode, @"void\s+RunScript", RegexOptions.IgnoreCase)
                         || scriptCode.IndexOf("using ", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // C#
                    var debugCs = Regex.Matches(scriptCode, @"Console\.WriteLine", RegexOptions.IgnoreCase).Count;
                    if (debugCs > 0)
                        codedIssues.Add("Remove debug 'Console.WriteLine' statements from C# script.");
                }
                else if (Regex.IsMatch(scriptCode, @"sub\s+RunScript", RegexOptions.IgnoreCase)
                         || scriptCode.IndexOf("imports ", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // VB.NET
                    var debugVb = Regex.Matches(scriptCode, @"Debug\.Print|Console\.WriteLine", RegexOptions.IgnoreCase).Count;
                    if (debugVb > 0)
                        codedIssues.Add("Remove debug statements (Debug.Print or Console.WriteLine) from VB script.");
                }
                else
                {
                    // Unknown language
                    codedIssues.Add("Warning: script language not recognized; static checks may be incomplete.");
                }

                // AI-based code review
                var messages = new List<KeyValuePair<string, string>>
                {
                    new("system", "You are a code review assistant. Provide concise feedback on the code."),
                };
                string userPrompt;
                if (string.IsNullOrWhiteSpace(question))
                    userPrompt = $"Perform a general review of the following script code:\n```\n{scriptCode}\n```" +
                                 "\n\nPlease: (1) describe the main purpose; (2) detect potential bugs or incoherences; (3) suggest an improved code block.";
                else
                    userPrompt = $"Review the following script code with respect to this question: \"{question}\"\n```\n{scriptCode}\n```";
                messages.Add(new("user", userPrompt));
                Func<List<KeyValuePair<string, string>>, Task<AIResponse>> getResponse = msgs => AIUtils.GetResponse(providerName, modelName, msgs);
                var aiResponse = await getResponse(messages).ConfigureAwait(false);

                return new JObject
                {
                    ["success"] = true,
                    ["codedIssues"] = JArray.FromObject(codedIssues),
                    ["aiReview"] = aiResponse.Response
                };
            }
            catch (Exception ex)
            {
                return new JObject
                {
                    ["success"] = false,
                    ["error"] = ex.Message
                };
            }
        }

        #endregion

        #region ScriptEdit

        /// <summary>
        /// Executes the "script_edit" tool: applies user instructions to modify a script component and updates it on the canvas.
        /// </summary>
        private async Task<object> ScriptEditToolAsync(JObject parameters)
        {
            try
            {
                var guidStr = parameters.Value<string>("guid") ?? throw new ArgumentException("Missing 'guid' parameter.");
                if (!Guid.TryParse(guidStr, out var scriptGuid))
                    throw new ArgumentException($"Invalid GUID: {guidStr}");
                var instructions = parameters.Value<string>("instructions") ?? throw new ArgumentException("Missing 'instructions' parameter.");
                var providerName = parameters["provider"]?.ToString() ?? string.Empty;
                var modelName = parameters["model"]?.ToString() ?? string.Empty;

                var objects = GHCanvasUtils.GetCurrentObjects();
                var target = objects.FirstOrDefault(o => o.InstanceGuid == scriptGuid) as IScriptComponent;
                if (target == null)
                    return new JObject { ["success"] = false, ["error"] = $"Component with GUID {scriptGuid} not found." };

                var scriptCode = target.Text ?? string.Empty;
                var messages = new List<KeyValuePair<string, string>>
                {
                    new("system", "You are a code modification assistant. Apply the user instructions to the script code and only return the full modified code."),
                    new("user", $"Instructions: {instructions}\n```\n{scriptCode}\n```")
                };
                Func<List<KeyValuePair<string,string>>, Task<AIResponse>> getResponse = msgs => AIUtils.GetResponse(providerName, modelName, msgs);
                var aiResponse = await getResponse(messages).ConfigureAwait(false);
                var modifiedCode = aiResponse.Response?.Trim() ?? string.Empty;

                // Remove markdown code fences
                var cleanedCode = Regex.Replace(modifiedCode, @"```[\w]*\r?\n", string.Empty);
                cleanedCode = Regex.Replace(cleanedCode, @"```", string.Empty);

                Debug.WriteLine($"[ScriptEditTool] Before setting code on component {scriptGuid}, old length: {target.Text?.Length ?? 0}");
                Debug.WriteLine($"[ScriptEditTool] New cleaned code length: {cleanedCode.Length}");

                Rhino.RhinoApp.InvokeOnUiThread(() => target.Text = cleanedCode);

                // grab the open editor for that component and close it to allow for further modifications
                var editor = GH_ScriptEditor.FindScriptEditor((IGH_DocumentObject)target);
                if (editor != null)
                {
                    // must run on UI thread
                    Rhino.RhinoApp.InvokeOnUiThread(() => editor.Close());
                    Debug.WriteLine($"[ScriptEditTool] Closed editor for component {scriptGuid}");
                }
                else
                {
                    Debug.WriteLine($"[ScriptEditTool] No editor found for component {scriptGuid}");
                }

                Debug.WriteLine($"[ScriptEditTool] After setting code on component {scriptGuid}, new length: {target.Text?.Length ?? 0}");
                return new JObject { ["success"] = true, ["modifiedCode"] = cleanedCode };
            }
            catch (Exception ex)
            {
                return new JObject { ["success"] = false, ["error"] = ex.Message };
            }
        }

        #endregion

        #region ScriptNew

        /// <summary>
        /// Executes the "script_new" tool: generates a script component based on user instructions and places it on the canvas.
        /// </summary>
        private async Task<object> ScriptNewToolAsync(JObject parameters)
        {
            try
            {
                var prompt = parameters.Value<string>("prompt") ?? throw new ArgumentException("Missing 'prompt' parameter.");
                var language = parameters.Value<string>("language") ?? "python";
                var providerName = parameters["provider"]?.ToString() ?? string.Empty;
                var modelName = parameters["model"]?.ToString() ?? string.Empty;

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
                                ""required"": [""name"", ""type""],
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
                                ""required"": [""name"", ""type""],
                                ""additionalProperties"": false
                            }
                        }
                    },
                    ""required"": [""script"", ""inputs"", ""outputs""],
                    ""additionalProperties"": false
                }".Replace('"', '"');

                // Prepare AI messages with instructions for structured output
                var messages = new List<KeyValuePair<string, string>>
                {
                    new("system", $"""
                    You are a Grasshopper script component generator. Generate a complete {language} script for a Grasshopper script component based on the user prompt.
                    
                    Your response MUST be a valid JSON object with the following structure:
                    - script: The complete script code
                    - inputs: Array of input parameters with name, type, description, and access (item/list/tree)
                    - outputs: Array of output parameters with name, type, and description
                    
                    The JSON object will be parsed programmatically, so it must be valid JSON with no additional text.
                    """),
                    new("user", prompt)
                };

                // Get AI response with JSON schema
                var aiResponse = await AIUtils.GetResponse(providerName, modelName, messages, jsonSchema).ConfigureAwait(false);
                var responseJson = JObject.Parse(aiResponse.Response);
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
                foreach (var output in outputs)
                {
                    var outputType = output["type"]?.ToString() ?? Generic;
                    
                    // Validate output type
                    if (!SupportedDataTypes.IsValidType(outputType))
                    {
                        outputType = Generic; // Fallback to Generic for unsupported types
                        Debug.WriteLine($"[ScriptNewTool] Unsupported output type: {outputType}, falling back to Generic");
                    }

                    var outputObj = new JObject
                    {
                        ["variableName"] = output["name"]?.ToString() ?? "output",
                        ["name"] = output["name"]?.ToString() ?? "Output",
                        ["description"] = output["description"]?.ToString() ?? string.Empty,
                        ["type"] = outputType
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

                // Place the component on the canvas on UI thread
                List<string> placed = null;
                RhinoApp.InvokeOnUiThread(() =>
                {
                    placed = Put.PutObjectsOnCanvas(doc);
                });
                return new { success = true, script = scriptCode, components = placed };
            }
            catch (Exception ex)
            {
                return new { success = false, error = ex.Message };
            }
        }

        #endregion
    }
}
