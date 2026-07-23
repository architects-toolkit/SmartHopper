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
using System.Threading;
using System.Threading.Tasks;
using GhJSON.Core;
using GhJSON.Core.SchemaModels;
using GhJSON.Core.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.GhJsonSpec;
using SmartHopper.Core.Grasshopper.Utils.Parsing;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Requests;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.AIModels;
using SmartHopper.ProviderSdk.Diagnostics;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// AI tool that generates a GhJSON document from natural language instructions.
    /// The generated GhJSON can be passed to gh_put to place components on the canvas.
    /// </summary>
    public class gh_generate : IAIToolProvider
    {
        private readonly string toolName = "gh_generate";
        private readonly string wrapperToolName = "gh_generate_and_place_on_canvas";
        private const int MaxValidationRetries = 2;

        /// <inheritdoc/>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Generate a GhJSON document from natural language instructions. Returns a valid GhJSON string describing Grasshopper components, positions, state, and connections. Pass the result to gh_put to place it on the canvas. Example: gh_generate({ instructions: 'Create a number slider connected to a panel' }).",
                category: "Components",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""instructions"": {
                            ""type"": ""string"",
                            ""description"": ""Natural language description of the Grasshopper network to generate. Be specific about component names, inputs/outputs, values, and wiring.""
                        },
                        ""language"": {
                            ""type"": ""string"",
                            ""description"": ""Optional language for any script components generated (python, ironpython, c#, vb)."",
                            ""enum"": [""python"", ""ironpython"", ""c#"", ""vb""]
                        }
                    },
                    ""required"": [""instructions""]
                }",
                execute: this.ExecuteGenerateAsync,
                requiredCapabilities: AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                mutatesCanvas: false,
                tags: new[] { "canvas", "components", "ghjson", "read-only", "ai-generation" },
                outputSchema: @"{ ""type"": ""object"", ""properties"": { ""success"": { ""type"": ""boolean"" }, ""ghjson"": { ""type"": ""string"", ""description"": ""Generated GhJSON document as a compact string."" }, ""componentCount"": { ""type"": ""integer"" }, ""message"": { ""type"": ""string"" } }, ""required"": [""success""] }",
                annotations: new AIToolAnnotations(readOnlyHint: true));

            yield return new AITool(
                name: this.wrapperToolName,
                description: "Generate a GhJSON document from instructions and immediately place it on the canvas. This wraps gh_generate followed by gh_put with editMode=false. Example: gh_generate_and_place_on_canvas({ instructions: 'Create a number slider connected to a panel' }).",
                category: "Components",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""instructions"": {
                            ""type"": ""string"",
                            ""description"": ""Natural language description of the Grasshopper network to generate and place.""
                        },
                        ""language"": {
                            ""type"": ""string"",
                            ""description"": ""Optional language for any script components generated (python, ironpython, c#, vb)."",
                            ""enum"": [""python"", ""ironpython"", ""c#"", ""vb""]
                        },
                        ""autoOffset"": {
                            ""type"": ""boolean"",
                            ""default"": true,
                            ""description"": ""When true, newly placed components are offset so they do not overlap existing objects.""
                        }
                    },
                    ""required"": [""instructions""]
                }",
                execute: this.ExecuteGenerateAndPlaceAsync,
                requiredCapabilities: AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                mutatesCanvas: true,
                tags: new[] { "canvas", "components", "ghjson", "mutating", "ai-generation" },
                outputSchema: @"{ ""type"": ""object"", ""properties"": { ""success"": { ""type"": ""boolean"" }, ""ghjson"": { ""type"": [""string"", ""null""] }, ""instanceGuids"": { ""type"": ""array"", ""items"": { ""type"": ""string"" } }, ""components"": { ""type"": ""array"", ""items"": { ""type"": ""string"" } }, ""message"": { ""type"": ""string"" } }, ""required"": [""success""] }",
                annotations: new AIToolAnnotations(destructiveHint: false));
        }

        private async Task<AIReturn> ExecuteGenerateAsync(AIToolCall toolCall)
        {
            var output = new AIReturn { Request = toolCall };

            try
            {
                var toolInfo = toolCall.GetToolCall();
                var args = toolInfo.GetArgumentsOrEmpty();
                var instructions = args["instructions"]?.ToString();
                var language = args["language"]?.ToString();
                var providerName = toolCall.Provider;
                var modelName = toolCall.Model;

                if (string.IsNullOrWhiteSpace(instructions))
                {
                    output.CreateError("Missing required 'instructions' parameter.");
                    return output;
                }

                var (success, ghJsonString, componentCount, message) = await this.GenerateGhJsonAsync(
                    providerName,
                    modelName,
                    instructions,
                    language,
                    toolCall.CancellationToken).ConfigureAwait(false);

                if (!success)
                {
                    output.CreateError(message);
                    return output;
                }

                var toolResult = new JObject
                {
                    ["success"] = true,
                    ["ghjson"] = ghJsonString,
                    ["componentCount"] = componentCount,
                    ["message"] = message,
                };

                var outBuilder = AIBodyBuilder.Create();
                outBuilder.AddToolResult(toolResult, toolInfo.Id, toolInfo.Name ?? this.toolName);
                output.CreateSuccess(outBuilder.Build(), toolCall);
                return output;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{this.toolName}] Error: {ex.Message}");
                output.CreateError($"Failed to generate GhJSON: {ex.Message}");
                return output;
            }
        }

        private async Task<AIReturn> ExecuteGenerateAndPlaceAsync(AIToolCall toolCall)
        {
            var output = new AIReturn { Request = toolCall };

            try
            {
                var wrapperToolInfo = toolCall.GetToolCall();
                var wrapperArgs = wrapperToolInfo.GetArgumentsOrEmpty();
                var instructions = wrapperArgs["instructions"]?.ToString();
                var language = wrapperArgs["language"]?.ToString();
                var autoOffset = wrapperArgs["autoOffset"]?.ToObject<bool?>() ?? true;

                if (string.IsNullOrWhiteSpace(instructions))
                {
                    output.CreateError("Missing required 'instructions' parameter.");
                    return output;
                }

                // Step 1: Call gh_generate to produce the GhJSON.
                Debug.WriteLine($"[{this.wrapperToolName}] Step 1: Calling {this.toolName}");

                var generateArgs = new JObject
                {
                    ["instructions"] = instructions,
                };
                if (!string.IsNullOrWhiteSpace(language))
                {
                    generateArgs["language"] = language;
                }

                var generateInteraction = new AIInteractionToolCall
                {
                    Name = this.toolName,
                    Arguments = generateArgs,
                    Agent = AIAgent.Assistant,
                };

                var generateToolCall = new AIToolCall
                {
                    Provider = toolCall.Provider,
                    Model = toolCall.Model,
                    Endpoint = this.toolName,
                };
                generateToolCall.Body = AIBodyBuilder.Create()
                    .Add(generateInteraction)
                    .Build();

                var generateResult = await this.ExecuteGenerateAsync(generateToolCall).ConfigureAwait(false);

                if (!generateResult.Success)
                {
                    Debug.WriteLine($"[{this.wrapperToolName}] {this.toolName} failed");
                    var generateError = generateResult.Messages?.FirstOrDefault(m => m?.Severity == SHRuntimeMessageSeverity.Error)?.Message ?? "Unknown error";
                    output.CreateError($"gh_generate failed: {generateError}");
                    return output;
                }

                var generateToolResult = generateResult.Body?.Interactions
                    .OfType<AIInteractionToolResult>()
                    .FirstOrDefault();

                if (generateToolResult?.Result == null)
                {
                    output.CreateError("gh_generate did not return a valid result.");
                    return output;
                }

                var generatedGhJson = generateToolResult.Result["ghjson"]?.ToString();
                if (string.IsNullOrWhiteSpace(generatedGhJson))
                {
                    output.CreateError("gh_generate did not return generated GhJSON.");
                    return output;
                }

                // Step 2: Call gh_put to place the components on canvas.
                Debug.WriteLine($"[{this.wrapperToolName}] Step 2: Calling gh_put");

                var ghPutArgs = new JObject
                {
                    ["ghjson"] = generatedGhJson,
                    ["editMode"] = false,
                    ["autoOffset"] = autoOffset,
                };

                var ghPutInteraction = new AIInteractionToolCall
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
                    .Add(ghPutInteraction)
                    .Build();

                var ghPutResult = await AIToolManager.ExecuteTool(ghPutToolCall).ConfigureAwait(false);

                if (!ghPutResult.Success)
                {
                    Debug.WriteLine($"[{this.wrapperToolName}] gh_put failed");

                    var putError = ghPutResult.Messages?.FirstOrDefault(m => m?.Severity == SHRuntimeMessageSeverity.Error)?.Message ?? "gh_put failed";
                    var partialResult = new JObject
                    {
                        ["success"] = false,
                        ["generateSuccess"] = true,
                        ["ghjson"] = generatedGhJson,
                        ["putSuccess"] = false,
                        ["putError"] = putError,
                        ["message"] = "GhJSON was generated successfully but failed to place on canvas.",
                    };

                    var partialBody = AIBodyBuilder.Create()
                        .AddToolResult(partialResult, wrapperToolInfo.Id, this.wrapperToolName, generateResult.Metrics, ghPutResult.Messages)
                        .Build();
                    output.CreateSuccess(partialBody, toolCall);
                    return output;
                }

                var ghPutToolResult = ghPutResult.Body?.Interactions
                    .OfType<AIInteractionToolResult>()
                    .FirstOrDefault();

                var combinedResult = new JObject
                {
                    ["success"] = true,
                    ["ghjson"] = generatedGhJson,
                    ["instanceGuids"] = ghPutToolResult?.Result?["instanceGuids"] ?? new JArray(),
                    ["components"] = ghPutToolResult?.Result?["components"] ?? new JArray(),
                    ["message"] = $"Generated and placed {ghPutToolResult?.Result?["components"]?.Count() ?? 0} component(s) on the canvas.",
                };

                var outBuilder = AIBodyBuilder.Create();
                outBuilder.AddToolResult(combinedResult, wrapperToolInfo.Id, this.wrapperToolName, generateResult.Metrics, generateResult.Messages);
                output.CreateSuccess(outBuilder.Build(), toolCall);
                return output;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{this.wrapperToolName}] Error: {ex.Message}");
                output.CreateError($"Failed to generate and place GhJSON: {ex.Message}");
                return output;
            }
        }

        private async Task<(bool Success, string GhJson, int ComponentCount, string Message)> GenerateGhJsonAsync(
            string provider,
            string model,
            string instructions,
            string language,
            CancellationToken cancellationToken)
        {
            var reference = await GhJsonSpecLoader.LoadTopicAsync("specification", false, cancellationToken).ConfigureAwait(false);

            var languageHint = string.IsNullOrWhiteSpace(language)
                ? string.Empty
                : $" Prefer the '{language}' language when generating script components.";

            var systemPrompt = $@"
You are a Grasshopper GhJSON generator for Rhino 3D. Your job is to translate natural language descriptions of Grasshopper component networks into a valid GhJSON document string.

Follow the GhJSON specification below precisely.

{reference}

Instructions to fulfill:
{instructions}{languageHint}

Return a single JSON object with this exact schema:
{GetJsonOutputSchema()}

The ""ghjson"" field must contain the complete GhJSON document as a compact JSON string (no markdown fences).";

            var jsonSchema = GetJsonOutputSchema();
            var contextFilter = "-*";

            var bodyBuilder = AIBodyBuilder.Create()
                .WithJsonOutputSchema(jsonSchema)
                .WithContextFilter(contextFilter)
                .AddSystem(systemPrompt)
                .AddUser(instructions);

            var request = new AIRequestCall();
            request.Initialize(
                provider: provider,
                model: model,
                capability: AICapability.TextInput | AICapability.TextOutput | AICapability.JsonOutput,
                endpoint: this.toolName,
                body: bodyBuilder.Build());

            string lastValidationError = null;
            string lastResponse = null;

            for (var attempt = 0; attempt <= MaxValidationRetries; attempt++)
            {
                if (attempt > 0)
                {
                    var retryBuilder = AIBodyBuilder.Create()
                        .WithJsonOutputSchema(jsonSchema)
                        .WithContextFilter(contextFilter)
                        .AddSystem(systemPrompt)
                        .AddUser(instructions)
                        .AddAssistant(lastResponse)
                        .AddUser($"The previous GhJSON was invalid. Validation error: {lastValidationError}. Please fix it and return only the corrected JSON object with the \"ghjson\" field.");
                    request.OverrideInteractions(retryBuilder.Build().Interactions.ToList());
                }

                var result = await request.Exec(cancellationToken).ConfigureAwait(false);

                if (!result.Success)
                {
                    var requestError = result.Messages?.FirstOrDefault(m => m?.Severity == SHRuntimeMessageSeverity.Error)?.Message ?? "Unknown error";
                    return (false, null, 0, $"AI request failed: {requestError}");
                }

                lastResponse = result.Body.GetLastInteraction(AIAgent.Assistant).ToString();

                string ghJsonString;
                try
                {
                    var responseJson = AIResponseParser.ParseJsonFromResponse(lastResponse) as JObject;
                    if (responseJson == null || !responseJson.TryGetValue("ghjson", out var ghJsonToken))
                    {
                        lastValidationError = "Response did not contain a JSON object with a 'ghjson' string field.";
                        continue;
                    }

                    ghJsonString = ghJsonToken.ToString();
                }
                catch (Exception ex)
                {
                    lastValidationError = $"Failed to parse AI response as JSON: {ex.Message}";
                    continue;
                }

                if (!GhJson.IsValid(ghJsonString, out var validationError))
                {
                    lastValidationError = validationError ?? "Generated GhJSON is invalid.";
                    continue;
                }

                var document = GhJson.FromJson(ghJsonString);
                document = GhJson.Fix(document).Document;

                var componentCount = document.Components?.Count ?? 0;
                var compactJson = GhJson.ToJson(document, new WriteOptions { Indented = false });

                return (true, compactJson, componentCount, $"Generated valid GhJSON with {componentCount} component(s).");
            }

            return (false, null, 0, $"Failed to generate valid GhJSON after {MaxValidationRetries} retries. Last error: {lastValidationError}");
        }

        private static string GetJsonOutputSchema()
        {
            return @"{
                ""type"": ""object"",
                ""properties"": {
                    ""ghjson"": {
                        ""type"": ""string"",
                        ""description"": ""The complete GhJSON document as a compact JSON string.""
                    }
                },
                ""required"": [""ghjson""],
                ""additionalProperties"": false
            }";
        }
    }
}