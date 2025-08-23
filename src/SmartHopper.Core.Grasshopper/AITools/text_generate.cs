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
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.Infrastructure.Utils;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Contains tools for text analysis and manipulation using AI.
    /// </summary>
    public class text_generate : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "text_generate";

        /// <summary>
        /// Defines the required capabilities for the AI tool provided by this class.
        /// </summary>
        private readonly AICapability toolCapabilityRequirements = AICapability.TextInput | AICapability.TextOutput;

        /// <summary>
        /// Default system prompt for the AI tool provided by this class.
        /// </summary>
        private readonly string defaultSystemPrompt =
            "You are a helpful AI assistant. Generate clear, relevant, and well-structured text based on the user's prompt. " +
            "Provide thoughtful and accurate responses that directly address what the user is asking for.";

        /// <summary>
        /// Get all tools provided by this class.
        /// </summary>
        /// <returns>Collection of AI tools.</returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Generates text based on a prompt and optional instructions",
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
                requiredCapabilities: this.toolCapabilityRequirements
            );
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
                string providerName = toolCall.Provider;
                string modelName = toolCall.Model;
                string endpoint = this.toolName;
                AIInteractionToolCall toolInfo = toolCall.GetToolCall();;
                string? prompt = toolInfo.Arguments["prompt"]?.ToString();
                string? instructions = toolInfo.Arguments["instructions"]?.ToString();
                string? contextFilter = toolInfo.Arguments["contextFilter"]?.ToString() ?? string.Empty;

                if (string.IsNullOrEmpty(prompt))
                {
                    output.CreateToolError("Missing required parameter: prompt", toolCall);
                    return output;
                }

                // Use custom instructions if provided, otherwise use default system prompt
                string systemPrompt = !string.IsNullOrWhiteSpace(instructions) ? instructions : this.defaultSystemPrompt;

                // Initiate AIBody
                var requestBody = new AIBody();
                requestBody.AddInteraction(AIAgent.System, systemPrompt);
                requestBody.AddInteraction(AIAgent.User, prompt);
                requestBody.ContextFilter = contextFilter;

                // Initiate AIRequestCall
                var request = new AIRequestCall();
                request.Initialize(
                    provider: providerName,
                    model: modelName,
                    capability: this.toolCapabilityRequirements,
                    endpoint: endpoint,
                    body: requestBody);

                // Execute the AIRequestCall
                var result = await request.Exec().ConfigureAwait(false);

                // Early exit on provider error to avoid null deref (standardize as tool error)
                if (!string.IsNullOrEmpty(result.ErrorMessage))
                {
                    output.CreateToolError(result.ErrorMessage, toolCall);
                    return output;
                }

                // Get the assistant response safely
                var last = result.Body?.GetLastInteraction(AIAgent.Assistant);
                if (last == null)
                {
                    output.CreateToolError("Provider returned no content", toolCall);
                    return output;
                }

                var response = last.ToString();

                // Success case
                var toolResult = new JObject();
                toolResult.Add("result", response);

                var toolBody = new AIBody();
                toolBody.AddInteractionToolResult(toolResult, result.Metrics, result.Messages);

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
