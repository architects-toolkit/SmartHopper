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
    /// Provides a tool for formatting text as markdown.
    /// </summary>
    public class markdown_format : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "markdown_format";

        /// <summary>
        /// Defines the required capabilities for the AI tool provided by this class.
        /// </summary>
        private readonly AICapability toolCapabilityRequirements = AICapability.TextInput | AICapability.TextOutput;

        /// <summary>
        /// System prompt for the AI tool provided by this class.
        /// </summary>
        private readonly string systemPrompt =
            "You are a markdown formatting assistant. Your task is to format text as well-structured markdown.\n\n" +
            "Use proper markdown syntax with headers, lists, emphasis, code blocks, and other formatting as appropriate.";

        /// <summary>
        /// Get all tools provided by this class.
        /// </summary>
        /// <returns>Collection of AI tools.</returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Formats input text as well-structured markdown",
                category: "DataProcessing",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""input"": { ""type"": ""string"", ""description"": ""The input text to format as markdown"" },
                        ""style"": { ""type"": ""string"", ""description"": ""Optional markdown style (e.g., 'technical', 'narrative', 'structured')"" },
                        ""instructions"": { ""type"": ""string"", ""description"": ""Optional system instructions for the AI"" }
                    },
                    ""required"": [""input""]
                }",
                execute: this.FormatMarkdown,
                requiredCapabilities: this.toolCapabilityRequirements,
                buildRequest: this.BuildFormatRequest);
        }

        /// <summary>
        /// Builds an <see cref="AIRequestCall"/> from the tool call parameters without executing it.
        /// Used during batch collection to aggregate multiple requests into a single batch submission.
        /// </summary>
        /// <param name="toolCall">The tool call containing provider, model, and arguments.</param>
        /// <returns>A fully-specified <see cref="AIRequestCall"/> ready for batch submission.</returns>
        private AIRequestCall BuildFormatRequest(AIToolCall toolCall)
        {
            AIInteractionToolCall toolInfo = toolCall.GetToolCall();
            var args = toolInfo.Arguments ?? new JObject();
            string input = args["input"]?.ToString();
            string style = args["style"]?.ToString();
            string instructions = args["instructions"]?.ToString();

            var systemPrompt = string.IsNullOrEmpty(instructions) ? this.systemPrompt : instructions;
            var userPrompt = string.IsNullOrEmpty(style)
                ? $"Format the following text as markdown:\n\n{input}"
                : $"Format the following text as markdown in a {style} style:\n\n{input}";

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
        /// Formats input text as markdown.
        /// </summary>
        /// <param name="toolCall">The tool call information.</param>
        /// <returns>AIReturn with the formatted markdown.</returns>
        private async Task<AIReturn> FormatMarkdown(AIToolCall toolCall)
        {
            var output = new AIReturn()
            {
                Request = toolCall,
            };

            try
            {
                Debug.WriteLine("[MarkdownTools] Running FormatMarkdown tool");

                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.Arguments ?? new JObject();
                string input = args["input"]?.ToString();

                if (string.IsNullOrEmpty(input))
                {
                    output.CreateToolError("Missing required parameter: input", toolCall);
                    return output;
                }

                var request = this.BuildFormatRequest(toolCall);
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

                var toolResult = new JObject();
                toolResult.Add("markdown", response);

                toolResult.WithEnvelope(
                    ToolResultEnvelope.Create(
                        tool: this.toolName,
                        type: ToolResultContentType.Object,
                        payloadPath: "markdown",
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
                Debug.WriteLine($"[MarkdownTools] Error in FormatMarkdown: {ex.Message}");
                output.CreateError($"Error: {ex.Message}");
                return output;
            }
        }
    }
}
