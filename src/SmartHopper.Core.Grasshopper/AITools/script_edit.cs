/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024-2026 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this library; if not, see <https://www.gnu.org/licenses/lgpl-3.0.html>.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using GhJSON.Core;
using GhJSON.Core.SchemaModels;
using GhJSON.Core.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Utils.Constants;
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
        private readonly string wrapperToolName = "script_edit_and_replace_on_canvas";
        private const int MaxValidationRetries = 2;

        private readonly string systemPromptTemplate = """
            You are a Grasshopper script component editor for Rhino 3D. Edit existing scripts based on user instructions while preserving the script's environment and conventions.

            ## CRITICAL: Geometry Library Requirements

            You MUST use ONLY RhinoCommon geometry types from the `Rhino.Geometry` namespace.
            PRESERVE the existing geometry libraries, imports, and types from the original script.
            DO NOT introduce:
            - System.Numerics (Vector3, Matrix4x4)
            - UnityEngine (Vector3, Quaternion, Transform)
            - numpy arrays for geometry
            - shapely, scipy.spatial, trimesh, open3d, pyvista
            - Custom Vector3/Point3 classes

            ## Editing Guidelines

            1. Preserve the original scripting language unless explicitly asked to change it
            2. Preserve existing imports and using statements
            3. Maintain the same coding style and conventions as the original
            4. Only modify what is necessary to fulfill the user's request
            5. Keep existing functionality intact unless asked to remove/replace it

            ## Response Format

            Your response MUST be a valid JSON object with:
            - script: The complete updated script code
            - inputs: Array of input parameters
            - outputs: Array of output parameters
            - changesSummary: Brief description of changes made and design decisions
            - nickname: Updated nickname (or preserve original)

            ## Parameter Type Hints

            Use these RhinoCommon types for geometry parameters:
            - Point3d, Vector3d, Plane, Line, Circle, Arc, Rectangle3d
            - Curve, NurbsCurve, PolylineCurve, Polyline
            - Surface, NurbsSurface, Brep, Mesh
            - Box, Sphere, Cylinder, Cone, Torus
            - Transform, Interval, BoundingBox

            Use these for primitives: int, double, string, bool, Color

            ## INPUT PARAMETER SCHEMA (all fields except name are optional)
              - name: Parameter name (required)
              - type: RhinoCommon type hint or primitive
              - description: Parameter description
              - access: 'item', 'list', 'tree'. Default: 'item'
              - dataMapping: 'None', 'Flatten', 'Graft'
              - reverse, simplify, invert: Boolean flags
              - isPrincipal: Principal parameter for data matching
              - required: Parameter required before calculation
              - expression: Transform expression

            ## OUTPUT PARAMETER SCHEMA (all fields except name are optional)
              - name: Parameter name (required)
              - type: Expected element type hint (e.g. Curve, Mesh, Point3d). For list outputs, this is the element type.
              - description: Parameter description
              - dataMapping: 'None', 'Flatten', 'Graft'
              - reverse, simplify, invert: Boolean flags
            The JSON object will be parsed programmatically, so it must be valid JSON with no additional text.
            """;

        private readonly string userPromptTemplate =
            "Current component GhJSON:\n```json\n<ghjson>\n```\n\n" +
            "Instructions: <instructions>";

        /// <inheritdoc/>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Edit an existing Grasshopper script component based on instructions. Takes GhJSON input and returns updated GhJSON (does not modify canvas).",
                category: "Hidden",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""ghjson"": {
                            ""type"": ""string"",
                            ""description"": ""GhJSON string representing the script component to edit. Retrieve it using gh_get[categoryFilter=[+Script]], gh_get[guidFilter=[<guid>]] or gh_get_selected if the user is asking about the currently selected component.""
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

            // Wrapper tool that edits and replaces the component on canvas in one call.
            // Uses instanceGuid to automatically retrieve GhJSON, call script_edit, and then gh_put.
            yield return new AITool(
                name: this.wrapperToolName,
                description: "Edit an existing Grasshopper script component by instance GUID and replace it on the canvas. This wrapper automatically retrieves the component GhJSON (gh_get_by_guid), calls script_edit, and then gh_put with editMode=true.",
                category: "Scripting",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""instanceGuid"": {
                            ""type"": ""string"",
                            ""description"": ""Instance GUID of the script component to edit (retrieve it using gh_get or variants).""
                        },
                        ""instructions"": {
                            ""type"": ""string"",
                            ""description"": ""Natural language instructions describing how to edit the script.""
                        }
                    },
                    ""required"": [""instanceGuid"", ""instructions""]
                }",
                execute: this.ExecuteEditAndReplaceAsync,
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

                // Validate input GhJSON using GhJson facade
                if (!GhJson.IsValid(ghJsonInput, out var inputValidationError))
                {
                    output.CreateError($"Input GhJSON validation failed: {inputValidationError}");
                    return output;
                }

                // Parse input to extract language and existing component info
                var inputDoc = GhJson.FromJson(ghJsonInput);
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

                // Build system prompt with language-specific guidance
                var languageGuidance = ScriptCodeValidator.GetLanguageGuidance(existingLanguage);
                var systemPrompt = this.systemPromptTemplate + $"\n\nThe current script is written in '{existingLanguage}'.\n\n{languageGuidance}";

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

                // Parse AI response and validate with retry loop
                var response = result.Body.GetLastInteraction(AIAgent.Assistant).ToString();
                var responseJson = SanitizeAndParseJson(response);

                var newScriptCode = responseJson["script"]?.ToString() ?? string.Empty;
                var newInputs = responseJson["inputs"] as JArray ?? new JArray();
                var newOutputs = responseJson["outputs"] as JArray ?? new JArray();
                var changesSummary = responseJson["changesSummary"]?.ToString() ?? "Script updated";
                var nickname = responseJson["nickname"]?.ToString() ?? existingComp.NickName ?? "AI Script";

                Debug.WriteLine($"[script_edit] New script length: {newScriptCode.Length}");
                Debug.WriteLine($"[script_edit] New inputs: {newInputs.Count}, outputs: {newOutputs.Count}");

                // Validate script code for non-Rhino geometry patterns and retry if needed
                var validationResult = ScriptCodeValidator.Validate(newScriptCode, existingLanguage);
                var retryCount = 0;

                while (!validationResult.IsValid && retryCount < MaxValidationRetries)
                {
                    retryCount++;
                    Debug.WriteLine($"[script_edit] Validation failed (attempt {retryCount}/{MaxValidationRetries}): {string.Join("; ", validationResult.Issues)}");

                    // Build correction request
                    var correctionBuilder = AIBodyBuilder.Create()
                        .WithJsonOutputSchema(jsonSchema)
                        .WithContextFilter(contextFilter)
                        .AddSystem(systemPrompt)
                        .AddUser(userPrompt)
                        .AddAssistant(response)
                        .AddUser(validationResult.CorrectionPrompt);

                    var correctionRequest = new AIRequestCall();
                    correctionRequest.Initialize(
                        provider: providerName,
                        model: modelName,
                        capability: AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                        endpoint: this.toolName,
                        body: correctionBuilder.Build());

                    var correctionResult = await correctionRequest.Exec().ConfigureAwait(false);

                    if (!correctionResult.Success)
                    {
                        Debug.WriteLine($"[script_edit] Correction request failed, using original script");
                        break;
                    }

                    // Parse corrected response
                    response = correctionResult.Body.GetLastInteraction(AIAgent.Assistant).ToString();
                    responseJson = SanitizeAndParseJson(response);

                    newScriptCode = responseJson["script"]?.ToString() ?? string.Empty;
                    newInputs = responseJson["inputs"] as JArray ?? new JArray();
                    newOutputs = responseJson["outputs"] as JArray ?? new JArray();
                    changesSummary = responseJson["changesSummary"]?.ToString() ?? changesSummary;
                    nickname = responseJson["nickname"]?.ToString() ?? nickname;

                    // Re-validate
                    validationResult = ScriptCodeValidator.Validate(newScriptCode, existingLanguage);
                }

                if (!validationResult.IsValid)
                {
                    // Continue with the script but log a warning - it may still work
                    var validationWarning = $"Script validation failed after {retryCount} retries: {string.Join("; ", validationResult.Issues)}";
                    Debug.WriteLine($"[script_edit] {validationWarning}");
                }

                // Get component info for the language
                var ghJsonString = CreateScriptGhJson(
                    languageKey: existingLanguage,
                    scriptCode: newScriptCode,
                    inputs: newInputs,
                    outputs: newOutputs,
                    nickname: nickname,
                    instanceGuid: existingInstanceGuid,
                    pivot: existingComp.Pivot,
                    indented: false);

                // Validate output GhJSON using GhJson facade
                if (!GhJson.IsValid(ghJsonString, out var outputValidationError))
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

        private static string DetectLanguageFromComponentGuid(Guid? componentGuid)
        {
            if (componentGuid == null)
            {
                return "python";
            }

            var guid = componentGuid.Value;
            if (guid == new Guid("719467e6-7cf5-4848-99b0-c5dd57e5442c"))
            {
                return "python";
            }

            if (guid == new Guid("97aa26ef-88ae-4ba6-98a6-ed6ddeca11d1"))
            {
                return "ironpython";
            }

            if (guid == new Guid("b6ba1144-02d6-4a2d-b53c-ec62e290eeb7"))
            {
                return "c#";
            }

            if (guid == new Guid("079bd9bd-54a0-41d4-98af-db999015f63d"))
            {
                return "vb";
            }

            return "python";
        }

        private static string GetExtensionKey(string languageKey)
        {
            return languageKey?.Trim().ToLowerInvariant() switch
            {
                "python" => GhJsonExtensionKeys.Python,
                "ironpython" => GhJsonExtensionKeys.IronPython,
                "c#" => GhJsonExtensionKeys.CSharp,
                "csharp" => GhJsonExtensionKeys.CSharp,
                "vb" => GhJsonExtensionKeys.VBScript,
                "vbscript" => GhJsonExtensionKeys.VBScript,
                _ => GhJsonExtensionKeys.Python,
            };
        }

        /// <summary>
        /// Attempts to extract and parse a JSON object from an AI response that may contain
        /// markdown formatting, HTML tags, or other non-JSON wrapping.
        /// </summary>
        private static JObject SanitizeAndParseJson(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
            {
                throw new JsonException("AI response is empty.");
            }

            // Try direct parse first
            try
            {
                return JObject.Parse(response);
            }
            catch (JsonException)
            {
                // Continue with sanitization attempts
            }

            // Try extracting JSON from markdown code blocks (```json ... ``` or ``` ... ```)
            var trimmed = response.Trim();
            var jsonBlockPattern = new System.Text.RegularExpressions.Regex(
                @"```(?:json)?\s*\n?(.*?)\n?\s*```",
                System.Text.RegularExpressions.RegexOptions.Singleline);
            var match = jsonBlockPattern.Match(trimmed);
            if (match.Success)
            {
                try
                {
                    return JObject.Parse(match.Groups[1].Value.Trim());
                }
                catch (JsonException)
                {
                    // Continue
                }
            }

            // Try extracting first JSON object from the response
            var firstBrace = trimmed.IndexOf('{');
            var lastBrace = trimmed.LastIndexOf('}');
            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                try
                {
                    return JObject.Parse(trimmed.Substring(firstBrace, lastBrace - firstBrace + 1));
                }
                catch (JsonException)
                {
                    // Continue
                }
            }

            // All attempts failed - provide a descriptive error
            var preview = response.Length > 200 ? response.Substring(0, 200) + "..." : response;
            if (trimmed.StartsWith("<", StringComparison.Ordinal))
            {
                throw new JsonException(
                    $"AI returned HTML/XML instead of JSON. This may indicate a provider error. Preview: {preview}");
            }

            throw new JsonException($"AI response is not valid JSON. Preview: {preview}");
        }

        private static string CreateScriptGhJson(
            string languageKey,
            string scriptCode,
            JArray inputs,
            JArray outputs,
            string nickname,
            Guid? instanceGuid,
            GhJsonPivot? pivot,
            bool indented)
        {
            var component = new GhJsonComponent
            {
                Name = languageKey?.Trim().ToLowerInvariant() switch
                {
                    "python" => "Python",
                    "ironpython" => "IronPython",
                    "c#" => "C#",
                    "csharp" => "C#",
                    "vb" => "VB Script",
                    "vbscript" => "VB Script",
                    _ => "Python",
                },
                NickName = nickname,
                InstanceGuid = instanceGuid,
                Id = instanceGuid.HasValue ? null : 1,
                Pivot = pivot,
            };

            component.InputSettings = ParseParameters(inputs);
            component.OutputSettings = ParseParameters(outputs);

            component.ComponentState = new GhJsonComponentState
            {
                Extensions = new Dictionary<string, object>
                {
                    [GetExtensionKey(languageKey)] = new Dictionary<string, object>
                    {
                        [GhJsonExtensionKeys.CodeProperty] = scriptCode ?? string.Empty,
                    },
                },
            };

            var doc = GhJson.CreateDocumentBuilder()
                .AddComponent(component)
                .Build();

            return GhJson.ToJson(doc, new WriteOptions { Indented = indented });
        }

        private static List<GhJsonParameterSettings> ParseParameters(JArray array)
        {
            var list = new List<GhJsonParameterSettings>();

            if (array == null)
            {
                return list;
            }

            foreach (var t in array)
            {
                if (t is not JObject o)
                {
                    continue;
                }

                var name = o["name"]?.ToString();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var settings = new GhJsonParameterSettings
                {
                    ParameterName = name,
                    VariableName = o["variableName"]?.ToString(),
                    Description = o["description"]?.ToString(),
                    Access = o["access"]?.ToString(),
                    DataMapping = o["dataMapping"]?.ToString(),
                    TypeHint = o["type"]?.ToString(),
                    Expression = o["expression"]?.ToString(),
                    IsReversed = o["reverse"]?.ToObject<bool?>(),
                    IsSimplified = o["simplify"]?.ToObject<bool?>(),
                    IsInverted = o["invert"]?.ToObject<bool?>(),
                    IsPrincipal = o["isPrincipal"]?.ToObject<bool?>(),
                    IsRequired = o["required"]?.ToObject<bool?>(),
                };

                list.Add(settings);
            }

            return list;
        }

        /// <summary>
        /// Wrapper that edits the script and replaces it on the canvas in one call.
        /// Internally calls script_edit then gh_put with editMode=true.
        /// </summary>
        private async Task<AIReturn> ExecuteEditAndReplaceAsync(AIToolCall toolCall)
        {
            var output = new AIReturn { Request = toolCall };

            try
            {
                // Parse wrapper arguments (instanceGuid + instructions)
                var wrapperToolInfo = toolCall.GetToolCall();
                var wrapperArgs = wrapperToolInfo.Arguments ?? new JObject();
                var instanceGuidText = wrapperArgs["instanceGuid"]?.ToString();
                var instructions = wrapperArgs["instructions"]?.ToString();

                if (string.IsNullOrWhiteSpace(instanceGuidText))
                {
                    output.CreateError("Missing required 'instanceGuid' parameter.");
                    return output;
                }

                if (!Guid.TryParse(instanceGuidText, out var instanceGuid))
                {
                    output.CreateError("The provided 'instanceGuid' is not a valid GUID.");
                    return output;
                }

                if (string.IsNullOrWhiteSpace(instructions))
                {
                    output.CreateError("Missing required 'instructions' parameter.");
                    return output;
                }

                // Step 1: Call gh_get_by_guid to retrieve GhJSON for the target component
                Debug.WriteLine($"[{this.wrapperToolName}] Step 1: Calling gh_get_by_guid for {instanceGuid}");

                var ghGetArgs = new JObject
                {
                    ["guidFilter"] = new JArray(instanceGuidText),
                    ["connectionDepth"] = 0,
                };

                var ghGetInteraction = new AIInteractionToolCall
                {
                    Name = "gh_get_by_guid",
                    Arguments = ghGetArgs,
                    Agent = AIAgent.Assistant,
                };

                var ghGetToolCall = new AIToolCall
                {
                    Provider = toolCall.Provider,
                    Model = toolCall.Model,
                    Endpoint = "gh_get_by_guid",
                    SkipMetricsValidation = true,
                };

                ghGetToolCall.Body = AIBodyBuilder.Create()
                    .Add(ghGetInteraction)
                    .Build();

                var ghGetResult = await AIToolManager.ExecuteTool(ghGetToolCall).ConfigureAwait(false);

                if (!ghGetResult.Success)
                {
                    output.Messages = ghGetResult.Messages;
                    output.CreateError("gh_get_by_guid failed to retrieve the target component.");
                    return output;
                }

                var ghGetToolResult = ghGetResult.Body?.Interactions
                    .OfType<AIInteractionToolResult>()
                    .FirstOrDefault();

                var ghJsonInput = ghGetToolResult?.Result?["ghjson"]?.ToString();
                if (string.IsNullOrWhiteSpace(ghJsonInput))
                {
                    output.CreateError("gh_get_by_guid did not return valid GhJSON for the specified instanceGuid.");
                    return output;
                }

                // Step 2: Call script_edit with the retrieved GhJSON and original instructions
                Debug.WriteLine($"[{this.wrapperToolName}] Step 2: Calling script_edit");

                var scriptEditArgs = new JObject
                {
                    ["ghjson"] = ghJsonInput,
                    ["instructions"] = instructions,
                };

                var scriptEditInteraction = new AIInteractionToolCall
                {
                    Name = this.toolName,
                    Arguments = scriptEditArgs,
                    Agent = AIAgent.Assistant,
                };

                var scriptEditToolCall = new AIToolCall
                {
                    Provider = toolCall.Provider,
                    Model = toolCall.Model,
                    Endpoint = this.toolName,
                };

                scriptEditToolCall.Body = AIBodyBuilder.Create()
                    .Add(scriptEditInteraction)
                    .Build();

                var editResult = await this.ExecuteAsync(scriptEditToolCall).ConfigureAwait(false);

                if (!editResult.Success)
                {
                    Debug.WriteLine($"[{this.wrapperToolName}] script_edit failed");
                    output.Messages = editResult.Messages;
                    return output;
                }

                // Extract the updated GhJSON from the script_edit result
                var editToolResult = editResult.Body?.Interactions
                    .OfType<AIInteractionToolResult>()
                    .FirstOrDefault();

                if (editToolResult?.Result == null)
                {
                    output.CreateError("script_edit did not return a valid result.");
                    return output;
                }

                var updatedGhJson = editToolResult.Result["ghjson"]?.ToString();
                if (string.IsNullOrWhiteSpace(updatedGhJson))
                {
                    output.CreateError("script_edit did not return updated GhJSON.");
                    return output;
                }

                Debug.WriteLine($"[{this.wrapperToolName}] Step 3: Calling gh_put with editMode=true");

                // Step 3: Call gh_put internally using AIToolManager
                var ghPutArgs = new JObject
                {
                    ["ghjson"] = updatedGhJson,
                    ["editMode"] = true,
                };

                var ghPutToolCallInteraction = new AIInteractionToolCall
                {
                    Name = "gh_put",
                    Arguments = ghPutArgs,
                    Agent = AIAgent.Assistant,
                };

                var ghPutToolCall = new AIToolCall
                {
                    Provider = toolCall.Provider,
                    Model = toolCall.Model,
                    Endpoint = "gh_put",
                    SkipMetricsValidation = true,
                };
                ghPutToolCall.Body = AIBodyBuilder.Create()
                    .Add(ghPutToolCallInteraction)
                    .Build();

                var ghPutResult = await AIToolManager.ExecuteTool(ghPutToolCall).ConfigureAwait(false);

                if (!ghPutResult.Success)
                {
                    Debug.WriteLine($"[{this.wrapperToolName}] gh_put failed");

                    // Return combined result with edit success but put failure
                    var partialResult = new JObject
                    {
                        ["success"] = false,
                        ["editSuccess"] = true,
                        ["ghjson"] = updatedGhJson,
                        ["putSuccess"] = false,
                        ["putError"] = ghPutResult.Messages?.FirstOrDefault()?.Message ?? "gh_put failed",
                        ["message"] = "Script was edited successfully but failed to replace on canvas.",
                    };

                    var partialBody = AIBodyBuilder.Create()
                        .AddToolResult(partialResult, toolCall.GetToolCall().Id, this.wrapperToolName, editResult.Metrics, ghPutResult.Messages)
                        .Build();
                    output.CreateSuccess(partialBody, toolCall);
                    return output;
                }

                Debug.WriteLine($"[{this.wrapperToolName}] Both steps completed successfully");

                // Combine results from both operations
                var ghPutToolResult = ghPutResult.Body?.Interactions
                    .OfType<AIInteractionToolResult>()
                    .FirstOrDefault();

                var combinedResult = new JObject
                {
                    ["success"] = true,
                    ["ghjson"] = updatedGhJson,
                    ["instanceGuid"] = editToolResult.Result["instanceGuid"],
                    ["language"] = editToolResult.Result["language"],
                    ["inputCount"] = editToolResult.Result["inputCount"],
                    ["outputCount"] = editToolResult.Result["outputCount"],
                    ["changesSummary"] = editToolResult.Result["changesSummary"],
                    ["components"] = ghPutToolResult?.Result?["components"],
                    ["message"] = "Script component edited and replaced on canvas successfully.",
                };

                // Combine metrics from both operations
                var combinedMetrics = editResult.Metrics;

                var outBuilder = AIBodyBuilder.Create();
                outBuilder.AddToolResult(combinedResult, toolCall.GetToolCall().Id, this.wrapperToolName, combinedMetrics, editResult.Messages);
                output.CreateSuccess(outBuilder.Build(), toolCall);
                return output;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{this.wrapperToolName}] Error: {ex.Message}");
                output.CreateError(ex.Message);
                return output;
            }
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
                                ""name"": { ""type"": ""string"", ""description"": ""Parameter name (required)."" },
                                ""variableName"": { ""type"": ""string"", ""description"": ""Optional script variable name (identifier). If omitted, defaults to 'name'."" },
                                ""type"": { ""type"": ""string"", ""description"": ""Type hint (e.g., int, double, string, Point3d, Curve, etc.). Use 'object' when unsure."" },
                                ""description"": { ""type"": ""string"", ""description"": ""Parameter description. Use a short human-readable sentence."" },
                                ""access"": { ""type"": ""string"", ""enum"": [""item"", ""list"", ""tree""], ""description"": ""Data access mode. Use 'item' when unsure."" },
                                ""dataMapping"": { ""type"": ""string"", ""enum"": [""None"", ""Flatten"", ""Graft""], ""description"": ""Data tree manipulation. Use 'None' when no mapping is needed."" },
                                ""reverse"": { ""type"": ""boolean"", ""description"": ""Reverse list order. Use false when not needed."" },
                                ""simplify"": { ""type"": ""boolean"", ""description"": ""Simplify data tree paths. Use false when not needed."" },
                                ""invert"": { ""type"": ""boolean"", ""description"": ""Invert boolean values (only for bool type). Use false when not needed."" },
                                ""required"": { ""type"": ""boolean"", ""description"": ""If true, parameter cannot be removed. Use false for optional parameters."" },
                                ""expression"": { ""type"": ""string"", ""description"": ""Mathematical expression to transform data (e.g., 'x * 2'). Use empty string when no expression is needed."" }
                            },
                            ""required"": [""name"", ""type"", ""description"", ""access"", ""dataMapping"", ""reverse"", ""simplify"", ""invert"", ""required"", ""expression""],
                            ""additionalProperties"": false
                        }
                    },
                    ""outputs"": {
                        ""type"": ""array"",
                        ""items"": {
                            ""type"": ""object"",
                            ""properties"": {
                                ""name"": { ""type"": ""string"", ""description"": ""Parameter name (required)."" },
                                ""variableName"": { ""type"": ""string"", ""description"": ""Optional script variable name (identifier). If omitted, defaults to 'name'."" },
                                ""type"": { ""type"": ""string"", ""description"": ""Expected output type hint. Use 'object' when unsure."" },
                                ""description"": { ""type"": ""string"", ""description"": ""Parameter description. Use a short human-readable sentence."" },
                                ""dataMapping"": { ""type"": ""string"", ""enum"": [""None"", ""Flatten"", ""Graft""], ""description"": ""Data tree manipulation. Use 'None' when no mapping is needed."" },
                                ""reverse"": { ""type"": ""boolean"", ""description"": ""Reverse output list order. Use false when not needed."" },
                                ""simplify"": { ""type"": ""boolean"", ""description"": ""Simplify output data tree paths. Use false when not needed."" },
                                ""invert"": { ""type"": ""boolean"", ""description"": ""Invert boolean values (only for bool type). Use false when not needed."" }
                            },
                            ""required"": [""name"", ""type"", ""description"", ""dataMapping"", ""reverse"", ""simplify"", ""invert""],
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
