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
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Utils.Parsing;
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
    /// Provides a tool for boolean classification of input text.
    /// </summary>
    public class boolean_classify : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "boolean_classify";

        /// <summary>
        /// Defines the required capabilities for the AI tool provided by this class.
        /// </summary>
        private readonly AICapability toolCapabilityRequirements = AICapability.TextInput | AICapability.TextOutput;

        /// <summary>
        /// Default system prompt for the AI tool provided by this class.
        /// </summary>
        private readonly string defaultSystemPrompt =
            "You are a boolean classifier. Your task is to analyze input text and classify it as true or false based on the given criteria.\n\n" +
            "Respond with only 'true' or 'false', nothing else.";

        /// <summary>
        /// Get all tools provided by this class.
        /// </summary>
        /// <returns>Collection of AI tools.</returns>
        public IEnumerable<AITool> GetTools()
        {
            var schema = @"{
                ""type"": ""object"",
                ""properties"": {
                    ""input"": { ""type"": ""string"", ""description"": ""The input text to classify"" },
                    ""criteria"": { ""type"": ""string"", ""description"": ""The criteria for classification (optional)"" },
                    ""instructions"": { ""type"": ""string"", ""description"": ""Optional system instructions for the AI"" },
                    ""fallback"": { ""type"": ""boolean"", ""description"": ""Optional fallback value to use when the AI response cannot be parsed. Defaults to false."" }
                },
                ""required"": [""input""]
            }";
            yield return new AITool(
                name: this.toolName,
                description: "Classifies input text as true or false based on given criteria",
                category: "DataProcessing",
                parametersSchema: schema,
                execute: this.ClassifyBoolean,
                requiredCapabilities: this.toolCapabilityRequirements,
                buildRequest: this.BuildClassifyRequest);
        }

        /// <summary>
        /// Builds an <see cref="AIRequestCall"/> from the tool call parameters without executing it.
        /// Used during batch collection to aggregate multiple requests into a single batch submission.
        /// </summary>
        /// <param name="toolCall">The tool call containing provider, model, and arguments.</param>
        /// <returns>A fully-specified <see cref="AIRequestCall"/> ready for batch submission.</returns>
        private AIRequestCall BuildClassifyRequest(AIToolCall toolCall)
        {
            AIInteractionToolCall toolInfo = toolCall.GetToolCall();
            var args = toolInfo.Arguments ?? new JObject();
            string input = args["input"]?.ToString();
            string criteria = args["criteria"]?.ToString();
            string instructions = args["instructions"]?.ToString();

            var systemPrompt = string.IsNullOrEmpty(instructions) ? this.defaultSystemPrompt : instructions;
            var userPrompt = string.IsNullOrEmpty(criteria)
                ? $"Classify the following text as true or false:\n\n{input}"
                : $"Classify the following text as true or false based on this criteria: {criteria}\n\nText: {input}";

            var requestBody = AIBodyBuilder.Create()
                .AddSystem(systemPrompt)
                .AddUser(userPrompt)
                .Build();

            var request = new AIRequestCall();
            request.Initialize(
                provider: toolCall.Provider,
                model: toolCall.Model,
                body: requestBody,
                endpoint: this.toolName,
                capability: this.toolCapabilityRequirements);
            request.Parameters = toolCall.Parameters;
            return request;
        }

        /// <summary>
        /// Classifies input text as true or false.
        /// </summary>
        /// <param name="toolCall">The tool call information.</param>
        /// <returns>AIReturn with the classification result.</returns>
        private async Task<AIReturn> ClassifyBoolean(AIToolCall toolCall)
        {
            var output = new AIReturn()
            {
                Request = toolCall,
            };

            try
            {
                Debug.WriteLine("[BooleanTools] Running ClassifyBoolean tool");

                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.Arguments ?? new JObject();
                string input = args["input"]?.ToString();

                if (string.IsNullOrEmpty(input))
                {
                    output.CreateToolError("Missing required parameter: input", toolCall);
                    return output;
                }

                var request = this.BuildClassifyRequest(toolCall);
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

                // Read optional user-supplied fallback (defaults to false). The fallback is
                // applied only when the AI response cannot be parsed; when parsing succeeds
                // the AI's value wins regardless of the fallback.
                bool fallback = args["fallback"]?.ToObject<bool?>() ?? false;
                var (classificationResult, usedFallback) = BooleanClassificationResolver.ClassifyWithFallback(response, fallback);

                var toolResult = new JObject();
                toolResult.Add("result", classificationResult.ToString().ToLowerInvariant());
                toolResult.Add("usedFallback", usedFallback);

                toolResult.WithEnvelope(
                    ToolResultEnvelope.Create(
                        tool: this.toolName,
                        type: ToolResultContentType.Object,
                        payloadPath: "result",
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
                Debug.WriteLine($"[BooleanTools] Error in ClassifyBoolean: {ex.Message}");
                output.CreateError($"Error: {ex.Message}");
                return output;
            }
        }
    }
}
