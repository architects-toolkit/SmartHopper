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
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Converters;
using SmartHopper.Core.Grasshopper.Utils.Parsing;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Requests;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.AIModels;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Contains tools for text analysis and manipulation using AI.
    /// </summary>
    public class text2boolean : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "text2boolean";

        /// <summary>
        /// Defines the required capabilities for the AI tool provided by this class.
        /// </summary>
        private readonly AICapability toolCapabilityRequirements = AICapability.TextInput | AICapability.TextOutput;

        /// <summary>
        /// System prompt for the AI tool provided by this class.
        /// </summary>
        private readonly string systemPrompt =
            "You are a text evaluator. Your task is to analyze a text and return a boolean value indicating whether the text matches the given criteria.\n\n" +
            "Respond with TRUE or FALSE, nothing else.";

        /// <summary>
        /// User prompt for the AI tool provided by this class. Use <question> and <text> placeholders.
        /// </summary>
        private readonly string userPrompt =
            "TEXT TO EVALUATE:\n\n---\n\n<text>\n\n---\n\n" +
            "QUESTION TO ANSWER:\n\n---\n\n\"<question>\"\n\n---\n\n" +
            "Remember, you must answer with TRUE or FALSE, nothing else.";

        /// <summary>
        /// Get all tools provided by this class.
        /// </summary>
        /// <returns>Collection of AI tools.</returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Evaluates a text against a true/false question with optional fallback value. Example: text2boolean({ text: 'The building is 30m tall', question: 'Is the building taller than 25m?', fallback: true }).",
                category: "DataProcessing",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""text"": { ""type"": ""string"", ""description"": ""The text to evaluate"" },
                        ""question"": { ""type"": ""string"", ""description"": ""The true/false question to evaluate"" },
                        ""fallback"": { ""type"": ""boolean"", ""description"": ""Optional fallback value to use when AI response cannot be parsed as true/false. If not provided, the result will be null for unparsable responses"" }
                    },
                    ""required"": [""text"", ""question"" ]
                }",
                execute: this.Text2Boolean,
                requiredCapabilities: this.toolCapabilityRequirements,
                buildRequest: this.BuildEvaluateRequest,
                mutatesCanvas: false,
                tags: new[] { "text", "boolean", "data-processing", "read-only" },
                outputSchema: @"{ ""type"": ""object"", ""properties"": { ""result"": { ""type"": ""boolean"", ""description"": ""Boolean evaluation result or fallback value."" } } }",
                annotations: new AIToolAnnotations(readOnlyHint: true));
        }

        /// <summary>
        /// Builds an <see cref="AIRequestCall"/> from the tool call parameters without executing it.
        /// Used during batch collection to aggregate multiple requests into a single batch submission.
        /// </summary>
        /// <param name="toolCall">The tool call containing provider, model, and arguments.</param>
        /// <returns>A fully-specified <see cref="AIRequestCall"/> ready for batch submission.</returns>
        private AIRequestCall BuildEvaluateRequest(AIToolCall toolCall)
        {
            AIInteractionToolCall toolInfo = toolCall.GetToolCall();
            var args = toolInfo.GetArgumentsOrEmpty();
            string text = args["text"]?.ToString();
            string question = args["question"]?.ToString();
            string contextFilter = args["contextFilter"]?.ToString() ?? string.Empty;

            var userPrompt = this.userPrompt;
            userPrompt = userPrompt.Replace("<question>", question);
            userPrompt = userPrompt.Replace("<text>", text);

            var requestBody = AIBodyBuilder.Create()
                .AddSystem(this.systemPrompt)
                .AddUser(userPrompt)
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
        /// Tool wrapper for the Text2Boolean function.
        /// </summary>
        /// <param name="toolCall">The tool call containing parameters.</param>
        /// <returns>Result object.</returns>
        private async Task<AIReturn> Text2Boolean(AIToolCall toolCall)
        {
            // Build the request first (shared logic between batch and non-batch paths)
            var request = this.BuildEvaluateRequest(toolCall);

            // Prepare the output
            var output = new AIReturn()
            {
                Request = toolCall,
            };

            try
            {
                Debug.WriteLine("[TextTools] Running Text2Boolean tool");

                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.GetArgumentsOrEmpty();
                string? text = args["text"]?.ToString();
                string? question = args["question"]?.ToString();

                // Parse fallback as boolean using centralized helper
                string? fallbackStr = args["fallback"]?.ToString();
                bool? fallback = StringConverter.StringToBoolean(fallbackStr);

                if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(question))
                {
                    Debug.WriteLine($"[TextTools.Text2Boolean] Missing required parameters - text: '{text ?? "null"}', question: '{question ?? "null"}'");
                    output.CreateError("Missing required parameters");
                    return output;
                }

                Debug.WriteLine($"[TextTools.Text2Boolean] System prompt: {this.systemPrompt}");
                Debug.WriteLine($"[TextTools.Text2Boolean] User prompt: {request.Body?.Interactions?.LastOrDefault()}");
                Debug.WriteLine($"[TextTools.Text2Boolean] Fallback value: '{fallback?.ToString() ?? "null"}'");

                // Execute the pre-built request
                var result = await request.Exec().ConfigureAwait(false);

                if (!result.Success)
                {
                    Debug.WriteLine($"[TextTools.Text2Boolean] AI call failed. Messages: {result.Messages?.Count ?? 0}");

                    // Propagate structured messages from AI call
                    output.Messages = result.Messages;
                    return output;
                }

                var response = result.Body.GetLastInteraction(AIAgent.Assistant).ToString();
                Debug.WriteLine($"[TextTools.Text2Boolean] AI response: '{response}'");

                // Centralized parse + fallback resolution (shared with batch path).
                var toolResult = BooleanResultResolver.BuildToolResult(response, fallback);
                Debug.WriteLine($"[TextTools.Text2Boolean] Resolved: {toolResult}");

                var toolBody = AIBodyBuilder.Create()
                    .AddToolResult(toolResult, id: toolInfo?.Id, name: this.toolName, metrics: result.Metrics, messages: result.Messages)
                    .Build();

                output.CreateSuccess(toolBody);
                return output;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TextTools] Error in Text2Boolean: {ex.Message}");

                output.CreateError($"Error: {ex.Message}");
                return output;
            }
        }
    }
}