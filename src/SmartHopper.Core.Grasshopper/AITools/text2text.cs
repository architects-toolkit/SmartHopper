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
    /// Contains tools for text analysis and manipulation using AI.
    /// </summary>
    public class text2text : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "text2text";

        /// <summary>
        /// Defines the required capabilities for the AI tool provided by this class.
        /// </summary>
        private readonly AICapability toolCapabilityRequirements = AICapability.TextInput | AICapability.TextOutput;

        /// <summary>
        /// Default system prompt for the AI tool provided by this class.
        /// </summary>
        private readonly string defaultSystemPrompt =
            "- You will receive some user prompts and you have to do what the user asks for.\n- Generate clear, relevant, and well-structured text based on the user's prompt.\n- Provide thoughtful and accurate responses that directly address what the user is asking for.\n- Keep answers very short.";

        /// <summary>
        /// User prompt template for the AI tool provided by this class. Use <prompt> placeholder.
        /// </summary>
        private readonly string userPromptTemplate = "<prompt>";

        /// <summary>
        /// Get all tools provided by this class.
        /// </summary>
        /// <returns>Collection of AI tools.</returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Generates text based on a prompt and optional instructions. Example: text2text({ prompt: 'Name three pavilion forms', instructions: 'use architectural terms' }).",
                category: "DataProcessing",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""prompt"": {
                            ""type"": ""string"",
                            ""description"": ""The prompt to generate text from""
                        },
                        ""instructions"": {
                            ""type"": ""string"",
                            ""description"": ""Optional instructions for the AI (system prompt)""
                        }
                    },
                    ""required"": [""prompt""]
                }",
                execute: this.GenerateText,
                requiredCapabilities: this.toolCapabilityRequirements,
                buildRequest: this.BuildGenerateRequest,
                mutatesCanvas: false,
                tags: new[] { "text", "data-processing", "read-only", "ai-generation" },
                outputSchema: @"{ ""type"": ""object"", ""properties"": { ""result"": { ""type"": ""string"", ""description"": ""Generated text response."" } } }",
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
            string contextFilter = args["contextFilter"]?.ToString() ?? string.Empty;

            string systemPrompt = !string.IsNullOrWhiteSpace(instructions) ? instructions : this.defaultSystemPrompt;
            var userPrompt = this.userPromptTemplate.Replace("<prompt>", prompt);

            var requestBody = AIBodyBuilder.Create()
                .AddSystem(systemPrompt)
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
        /// Tool wrapper for the GenerateText function.
        /// </summary>
        /// <param name="parameters">Parameters passed from the AI.</param>
        /// <returns>Result object.</returns>
        private async Task<AIReturn> GenerateText(AIToolCall toolCall)
        {
            // Prepare the output
            var output = new AIReturn()
            {
                Request = toolCall,
            };

            try
            {
                Debug.WriteLine("[TextTools] Running GenerateText tool");

                // Extract parameters
                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.GetArgumentsOrEmpty();
                string? prompt = args["prompt"]?.ToString();

                if (string.IsNullOrEmpty(prompt))
                {
                    output.CreateToolError("Missing required parameter: prompt", toolCall);
                    return output;
                }

                // Build and execute the AIRequestCall
                var request = this.BuildGenerateRequest(toolCall);

                // Execute the AIRequestCall
                var result = await request.Exec().ConfigureAwait(false);

                if (!result.Success)
                {
                    // Propagate structured messages from AI call
                    output.Messages = result.Messages;
                    return output;
                }

                var response = result.Body.GetLastInteraction(AIAgent.Assistant).ToString();

                // Success case
                var toolResult = new JObject();
                toolResult.Add("result", response);

                // Attach non-breaking result envelope
                toolResult.WithEnvelope(
                    ToolResultEnvelope.Create(
                        tool: this.toolName,
                        type: ToolResultContentType.Text,
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
                Debug.WriteLine($"[TextTools] Error in GenerateText: {ex.Message}");

                output.CreateError($"Error: {ex.Message}");
                return output;
            }
        }
    }
}
