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
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Utils.Parsing;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Requests;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.AIModels;
using SmartHopper.ProviderSdk.Utilities;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Contains tools for JSON generation from text using AI.
    /// </summary>
    public class text2json : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "text2json";

        /// <summary>
        /// Defines the required capabilities for the AI tool provided by this class.
        /// </summary>
        private readonly AICapability toolCapabilityRequirements = AICapability.TextInput | AICapability.JsonOutput;

        /// <summary>
        /// Default system prompt for the AI tool provided by this class.
        /// </summary>
        private readonly string defaultSystemPrompt =
            "You are a JSON generation assistant. Your task is to produce valid JSON that strictly conforms to the provided JSON Schema.\n\n" +
            "IMPORTANT REQUIREMENTS:\n" +
            "- Return ONLY valid JSON that conforms to the schema\n" +
            "- Do not include any extra text, explanations, or markdown formatting\n" +
            "- Do not wrap the output in code blocks\n" +
            "- Do not return a copy of the JSON Schema in the response\n" +
            "- Every required field in the schema must be present\n" +
            "- Field types must exactly match the schema types\n" +
            "- Null values are only allowed if the schema permits them";

        /// <summary>
        /// Get all tools provided by this class.
        /// </summary>
        /// <returns>Collection of AI tools.</returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Generates a JSON object from a prompt, conforming strictly to a provided JSON Schema. Example: text2json({ prompt: 'Describe a chair', jsonSchema: '{\"type\":\"object\",\"properties\":{\"material\":{\"type\":\"string\"}}}' }).",
                category: "DataProcessing",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""prompt"": {
                            ""type"": ""string"",
                            ""description"": ""The prompt describing the JSON data to generate""
                        },
                        ""instructions"": {
                            ""type"": ""string"",
                            ""description"": ""Optional custom system prompt override""
                        },
                        ""jsonSchema"": {
                            ""type"": ""string"",
                            ""description"": ""JSON Schema string the output must conform to""
                        }
                    },
                    ""required"": [""prompt"", ""jsonSchema""]
                }",
                execute: this.GenerateJson,
                requiredCapabilities: this.toolCapabilityRequirements,
                buildRequest: this.BuildGenerateRequest,
                mutatesCanvas: false,
                tags: new[] { "text", "json", "data-processing", "read-only" },
                outputSchema: @"{ ""type"": ""object"", ""properties"": { ""result"": { ""type"": ""object"", ""description"": ""Generated JSON object conforming to the provided schema."" } } }",
                annotations: new AIToolAnnotations(readOnlyHint: true));
        }

        /// <summary>
        /// Builds an <see cref="AIRequestCall"/> from the tool call parameters without executing it.
        /// Used during batch collection to aggregate multiple requests into a single batch submission.
        /// </summary>
        /// <param name="toolCall">The tool call containing provider, model, and arguments.</param>
        /// <returns>A fully-specified <see cref="AIRequestCall"/> ready for batch submission.</returns>
        private AIRequestCall BuildGenerateRequest(AIToolCall toolCall)
        {
            AIInteractionToolCall toolInfo = toolCall.GetToolCall();
            var args = toolInfo.GetArgumentsOrEmpty();
            string prompt = args["prompt"]?.ToString();
            string instructions = args["instructions"]?.ToString();
            string jsonSchema = args["jsonSchema"]?.ToString();
            string contextFilter = args["contextFilter"]?.ToString() ?? string.Empty;

            string systemPrompt = !string.IsNullOrWhiteSpace(instructions) ? instructions : this.defaultSystemPrompt;

            var requestBody = AIBodyBuilder.Create()
                .WithJsonOutputSchema(jsonSchema)
                .AddSystem(systemPrompt)
                .AddUser(prompt)
                .WithContextFilter(contextFilter)
                .Build();

            var request = new AIRequestCall();
            request.Initialize(
                provider: toolCall.Provider,
                model: toolCall.Model,
                body: requestBody,
                endpoint: this.toolName,
                capability: this.toolCapabilityRequirements);
            return request;
        }

        /// <summary>
        /// Tool wrapper for the GenerateJson function.
        /// </summary>
        /// <param name="toolCall">The tool call containing provider, model, and arguments.</param>
        /// <returns>Result object.</returns>
        private async Task<AIReturn> GenerateJson(AIToolCall toolCall)
        {
            var output = new AIReturn()
            {
                Request = toolCall,
            };

            try
            {
                Debug.WriteLine("[JsonTools] Running GenerateJson tool");

                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.GetArgumentsOrEmpty();
                string? prompt = args["prompt"]?.ToString();
                string? instructions = args["instructions"]?.ToString();
                string? jsonSchema = args["jsonSchema"]?.ToString();
                string contextFilter = args["contextFilter"]?.ToString() ?? string.Empty;

                if (string.IsNullOrEmpty(prompt))
                {
                    output.CreateToolError("Missing required parameter: prompt", toolCall);
                    return output;
                }

                if (string.IsNullOrEmpty(jsonSchema))
                {
                    output.CreateToolError("Missing required parameter: jsonSchema", toolCall);
                    return output;
                }

                var request = this.BuildGenerateRequest(toolCall);

                var result = await request.Exec().ConfigureAwait(false);

                if (!result.Success)
                {
                    output.Messages = result.Messages;
                    return output;
                }

                var response = result.Body.GetLastInteraction(AIAgent.Assistant)?.ToString() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(response))
                {
                    if (result.Messages != null)
                    {
                        output.Messages = result.Messages;
                    }

                    output.CreateToolError("Empty response from AI assistant.");
                    return output;
                }

                // Robustly parse JSON from the AI response, handling markdown code-block
                // wrapping (```json ... ```), prefatory text, and trailing garbage via
                // depth-based container extraction. Accepts both object and array roots.
                var jsonOutcome = JsonResultResolver.Resolve(response);
                if (!jsonOutcome.Success)
                {
                    output.CreateToolError($"AI response is not valid JSON: {jsonOutcome.Error}");
                    return output;
                }

                var toolResult = new JObject();
                toolResult.Add("json", jsonOutcome.Value.ToString(Formatting.None));

                toolResult.WithEnvelope(
                    ToolResultEnvelope.Create(
                        tool: this.toolName,
                        type: ToolResultContentType.Object,
                        payloadPath: "json",
                        provider: toolCall.Provider?.ToString() ?? "Unknown",
                        model: toolCall.Model.ToString(),
                        toolCallId: toolInfo?.Id));

                var toolBody = AIBodyBuilder.Create()
                    .AddToolResult(toolResult, id: toolInfo?.Id, name: this.toolName, metrics: result.Metrics, messages: result.Messages)
                    .Build();

                output.CreateSuccess(toolBody);
                return output;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[JsonTools] Error in GenerateJson: {ex.Message}");
                output.CreateError($"Error: {ex.Message}");
                return output;
            }
        }
    }
}