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
 * along with this library; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using GhJSON.Core;
using GhJSON.Grasshopper;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Utils.Internal;
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
        private readonly string wrapperToolName = "script_generate_and_place_on_canvas";

        private const int MaxValidationRetries = 2;

        private readonly string systemPromptTemplate = """
            You are a Grasshopper script component generator for Rhino 3D. Generate complete, production-ready scripts that run inside Grasshopper script components.

            ## CRITICAL: Geometry Library Requirements

            You MUST use ONLY RhinoCommon geometry types from the `Rhino.Geometry` namespace.
            DO NOT use:
            - System.Numerics (Vector3, Matrix4x4)
            - UnityEngine (Vector3, Quaternion, Transform)
            - numpy arrays for geometry
            - shapely, scipy.spatial, trimesh, open3d, pyvista
            - Custom Vector3/Point3 classes

            ## Language Selection

            Choose the scripting language and return it in the "language" field.
            The language MUST be one of: "python", "ironpython", "c#", "vb".
            Use "python" unless the user explicitly requests another language.

            ## Response Format

            Your response MUST be a valid JSON object with:
            - language: The scripting language (python, ironpython, c#, vb)
            - script: The complete script code
            - inputs: Array of input parameters
            - outputs: Array of output parameters
            - nickname: Short name for the component
            - summary: Brief summary (1-3 sentences) of functionality and design decisions

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
              - type: RhinoCommon type hint (Point3d, Curve, Mesh, etc.) or primitive (int, double, string, bool)
              - description: Parameter description
              - access: Data access mode - 'item', 'list', 'tree'. Default: 'item'
              - dataMapping: 'None', 'Flatten', 'Graft'. Default: 'None'
              - reverse: Reverse list order (default: false)
              - simplify: Simplify data tree paths (default: false)
              - invert: Invert boolean values (default: false)
              - required: Parameter required before calculation (default: false)
              - expression: Transform expression (e.g., 'x * 2')

            ## OUTPUT PARAMETER SCHEMA (all fields except name are optional)
              - name: Parameter name (required)
              - type: Expected element type hint (e.g. Curve, Mesh, Point3d). For list outputs, this is the element type.
              - description: Parameter description
              - dataMapping: 'None', 'Flatten', 'Graft'
              - reverse, simplify, invert: Same as inputs
            The JSON object will be parsed programmatically, so it must be valid JSON with no additional text.
            """;

        /// <inheritdoc/>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Generate a new Grasshopper script component from natural language instructions. Returns GhJSON representing the script component (does not place it on canvas).",
                category: "Hidden",
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

            // Wrapper tool that generates and places the component on canvas in one call.
            yield return new AITool(
                name: this.wrapperToolName,
                description: "Generate a new Grasshopper script component from natural language instructions and place it on the canvas. This wrapper combines script_generate and gh_put into a single operation.",
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
                execute: this.ExecuteGenerateAndPlaceAsync,
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

                // Build system prompt with language-specific guidance using centralized language mapping
                var effectiveLanguage = GhJsonGrasshopper.Script.NormalizeLanguageKeyOrDefault(preferredLanguage, "python");
                var languageGuidance = ScriptCodeValidator.GetLanguageGuidance(effectiveLanguage);
                var systemPrompt = this.systemPromptTemplate + "\n\n" + languageGuidance;

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

                // Parse AI response and validate with retry loop
                var response = result.Body.GetLastInteraction(AIAgent.Assistant).ToString();
                var responseJson = JObject.Parse(response);

                var language = responseJson["language"]?.ToString() ?? "python";
                var scriptCode = responseJson["script"]?.ToString() ?? string.Empty;
                var inputs = responseJson["inputs"] as JArray ?? new JArray();
                var outputs = responseJson["outputs"] as JArray ?? new JArray();
                var nickname = responseJson["nickname"]?.ToString() ?? "AI Script";
                var summary = responseJson["summary"]?.ToString() ?? string.Empty;

                // Detect language or use default
                var componentInfo = GhJsonGrasshopper.Script.GetComponentInfo(language);
                if (componentInfo == null)
                {
                    output.CreateError($"Unsupported script language: {language}. Supported: python, ironpython, c#, vb.");
                    return output;
                }

                Debug.WriteLine($"[script_generate] Language: {language}, Script length: {scriptCode.Length}");
                Debug.WriteLine($"[script_generate] Inputs: {inputs.Count}, Outputs: {outputs.Count}");

                // Validate script code for non-Rhino geometry patterns and retry if needed
                var validationResult = ScriptCodeValidator.Validate(scriptCode, language);
                var retryCount = 0;

                while (!validationResult.IsValid && retryCount < MaxValidationRetries)
                {
                    retryCount++;
                    Debug.WriteLine($"[script_generate] Validation failed (attempt {retryCount}/{MaxValidationRetries}): {string.Join("; ", validationResult.Issues)}");

                    // Build correction request
                    var correctionBuilder = AIBodyBuilder.Create()
                        .WithJsonOutputSchema(jsonSchema)
                        .WithContextFilter(contextFilter)
                        .AddSystem(systemPrompt)
                        .AddUser(instructions)
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
                        Debug.WriteLine($"[script_generate] Correction request failed, using original script");
                        break;
                    }

                    // Parse corrected response
                    response = correctionResult.Body.GetLastInteraction(AIAgent.Assistant).ToString();
                    responseJson = JObject.Parse(response);

                    scriptCode = responseJson["script"]?.ToString() ?? string.Empty;
                    inputs = responseJson["inputs"] as JArray ?? new JArray();
                    outputs = responseJson["outputs"] as JArray ?? new JArray();
                    nickname = responseJson["nickname"]?.ToString() ?? nickname;
                    summary = responseJson["summary"]?.ToString() ?? summary;

                    // Re-validate
                    validationResult = ScriptCodeValidator.Validate(scriptCode, language);
                }

                if (!validationResult.IsValid)
                {
                    Debug.WriteLine($"[script_generate] Script validation failed after {retryCount} retries: {string.Join("; ", validationResult.Issues)}");

                    // Continue with the script but log a warning - it may still work
                }

                // Build GhJSON using Script façade
                var ghJsonString = GhJsonGrasshopper.Script.CreateGhJson(
                    language,
                    scriptCode,
                    inputs,
                    outputs,
                    nickname,
                    instanceGuid: null,
                    pivot: null,
                    indented: false);

                // Validate GhJSON output using GhJson facade
                if (!GhJson.IsValid(ghJsonString, out var validationError))
                {
                    output.CreateError($"Generated GhJSON validation failed: {validationError}");
                    return output;
                }

                // Build tool result (GhJSON only; canvas instance GUIDs are assigned later by gh_put)
                var toolResult = new JObject
                {
                    ["success"] = true,
                    ["ghjson"] = ghJsonString,
                    ["language"] = componentInfo.LanguageKey,
                    ["componentName"] = componentInfo.DisplayName,
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

        /// <summary>
        /// Wrapper that generates the script and places it on the canvas in one call.
        /// Internally calls script_generate then gh_put.
        /// </summary>
        private async Task<AIReturn> ExecuteGenerateAndPlaceAsync(AIToolCall toolCall)
        {
            var output = new AIReturn { Request = toolCall };

            try
            {
                // Parse wrapper arguments
                var wrapperToolInfo = toolCall.GetToolCall();
                var wrapperArgs = wrapperToolInfo.Arguments ?? new JObject();
                var instructions = wrapperArgs["instructions"]?.ToString();
                var language = wrapperArgs["language"]?.ToString();

                if (string.IsNullOrWhiteSpace(instructions))
                {
                    output.CreateError("Missing required 'instructions' parameter.");
                    return output;
                }

                // Step 1: Call script_generate to generate the GhJSON
                Debug.WriteLine($"[{this.wrapperToolName}] Step 1: Calling script_generate");

                var scriptGenerateArgs = new JObject
                {
                    ["instructions"] = instructions,
                };
                if (!string.IsNullOrWhiteSpace(language))
                {
                    scriptGenerateArgs["language"] = language;
                }

                var scriptGenerateInteraction = new AIInteractionToolCall
                {
                    Name = this.toolName,
                    Arguments = scriptGenerateArgs,
                    Agent = AIAgent.Assistant,
                };

                var scriptGenerateToolCall = new AIToolCall
                {
                    Provider = toolCall.Provider,
                    Model = toolCall.Model,
                    Endpoint = this.toolName,
                };

                scriptGenerateToolCall.Body = AIBodyBuilder.Create()
                    .Add(scriptGenerateInteraction)
                    .Build();

                var generateResult = await this.ExecuteAsync(scriptGenerateToolCall).ConfigureAwait(false);

                if (!generateResult.Success)
                {
                    Debug.WriteLine($"[{this.wrapperToolName}] script_generate failed");
                    output.Messages = generateResult.Messages;
                    return output;
                }

                // Extract the generated GhJSON from the script_generate result
                var generateToolResult = generateResult.Body?.Interactions
                    .OfType<AIInteractionToolResult>()
                    .FirstOrDefault();

                if (generateToolResult?.Result == null)
                {
                    output.CreateError("script_generate did not return a valid result.");
                    return output;
                }

                var generatedGhJson = generateToolResult.Result["ghjson"]?.ToString();
                if (string.IsNullOrWhiteSpace(generatedGhJson))
                {
                    output.CreateError("script_generate did not return generated GhJSON.");
                    return output;
                }

                Debug.WriteLine($"[{this.wrapperToolName}] Step 2: Calling gh_put");

                // Step 2: Call gh_put to place the component on canvas
                var ghPutArgs = new JObject
                {
                    ["ghjson"] = generatedGhJson,
                    ["editMode"] = false,
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

                    // Return combined result with generate success but put failure
                    var partialResult = new JObject
                    {
                        ["success"] = false,
                        ["generateSuccess"] = true,
                        ["ghjson"] = generatedGhJson,
                        ["putSuccess"] = false,
                        ["putError"] = ghPutResult.Messages?.FirstOrDefault()?.Message ?? "gh_put failed",
                        ["message"] = "Script was generated successfully but failed to place on canvas.",
                    };

                    var partialBody = AIBodyBuilder.Create()
                        .AddToolResult(partialResult, toolCall.GetToolCall().Id, this.wrapperToolName, generateResult.Metrics, ghPutResult.Messages)
                        .Build();
                    output.CreateSuccess(partialBody, toolCall);
                    return output;
                }

                Debug.WriteLine($"[{this.wrapperToolName}] Both steps completed successfully");

                // Combine results from both operations
                var ghPutToolResult = ghPutResult.Body?.Interactions
                    .OfType<AIInteractionToolResult>()
                    .FirstOrDefault();

                // Use the actual instanceGuid from gh_put (the real GUID after placement)
                var placedGuids = ghPutToolResult?.Result?["instanceGuids"] as JArray;
                var actualInstanceGuid = placedGuids?.FirstOrDefault()?.ToString();

                var combinedResult = new JObject
                {
                    ["success"] = true,
                    ["ghjson"] = generatedGhJson,
                    ["instanceGuid"] = actualInstanceGuid,
                    ["language"] = generateToolResult.Result["language"],
                    ["componentName"] = generateToolResult.Result["componentName"],
                    ["inputCount"] = generateToolResult.Result["inputCount"],
                    ["outputCount"] = generateToolResult.Result["outputCount"],
                    ["summary"] = generateToolResult.Result["summary"],
                    ["components"] = ghPutToolResult?.Result?["components"],
                    ["message"] = "Script component generated and placed on canvas successfully.",
                };

                // Combine metrics from both operations
                var combinedMetrics = generateResult.Metrics;

                var outBuilder = AIBodyBuilder.Create();
                outBuilder.AddToolResult(combinedResult, toolCall.GetToolCall().Id, this.wrapperToolName, combinedMetrics, generateResult.Messages);
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
                    ""language"": {
                        ""type"": ""string"",
                        ""description"": ""Scripting language for the component. Must be one of: python, ironpython, c#, vb. Use 'python' as the default when unsure."",
                        ""enum"": [""python"", ""ironpython"", ""c#"", ""vb""]
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
                    ""nickname"": {
                        ""type"": ""string"",
                        ""description"": ""Optional short name for the component""
                    },
                    ""summary"": {
                        ""type"": ""string"",
                        ""description"": ""A brief summary (1-3 sentences) of what the component does and key design decisions made""
                    }
                },
                ""required"": [""language"", ""script"", ""inputs"", ""outputs"", ""nickname"", ""summary""],
                ""additionalProperties"": false
            }";
        }
    }
}
