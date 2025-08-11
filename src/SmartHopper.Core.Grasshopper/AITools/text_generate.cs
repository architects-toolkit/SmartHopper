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
using SmartHopper.Core.Messaging;
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
        private async Task<AIToolCall> GenerateText(AIToolCall toolCall)
        {
            try
            {
                Debug.WriteLine("[TextTools] Running GenerateText tool");

                // Extract parameters
                string providerName = toolCall.Provider;
                string modelName = toolCall.Model;
                string endpoint = this.toolName;
                string? prompt = toolCall.Arguments["prompt"]?.ToString();
                string? instructions = toolCall.Arguments["instructions"]?.ToString();
                string? contextFilter = toolCall.Arguments["contextFilter"]?.ToString() ?? string.Empty;

                if (string.IsNullOrEmpty(prompt))
                {
                    toolCall.ErrorMessage = "Missing required parameter: prompt";
                    return toolCall;
                }

                // Use custom instructions if provided, otherwise use default system prompt
                string systemPrompt = !string.IsNullOrWhiteSpace(instructions) ? instructions : this.defaultSystemPrompt;

                // Prepare the AI request
                var requestBody = new AIBody();
                requestBody.AddInteraction("system", systemPrompt);
                requestBody.AddInteraction("user", prompt);

                var request = new AIRequestCall
                {
                    Provider = providerName,
                    Model = modelName,
                    Capability = this.toolCapabilityRequirements,
                    Endpoint = endpoint,
                    Body = requestBody,
                };

                // Execute the tool
                var result = await request.Do<string>().ConfigureAwait(false);

                // Strip thinking tags from response before using
                var cleanedResponse = AI.StripThinkTags(result.Result);

                // Success case
                toolCall.Result = cleanedResponse;
                toolCall.Metrics = result.Metrics;
                return toolCall;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TextTools] Error in GenerateText: {ex.Message}");

                // Return error object as JObject
                toolCall.ErrorMessage = $"Error: {ex.Message}";
                return toolCall;
            }
        }
    }
}
