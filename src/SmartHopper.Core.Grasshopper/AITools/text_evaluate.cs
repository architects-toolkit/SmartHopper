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
using SmartHopper.Core.Grasshopper.Utils;
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
    public class text_evaluate : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "text_evaluate";

        /// <summary>
        /// Defines the required capabilities for the AI tool provided by this class.
        /// </summary>
        private readonly AICapability toolCapabilityRequirements = AICapability.TextInput | AICapability.TextOutput;

        /// <summary>
        /// System prompt for the AI tool provided by this class.
        /// </summary>
        private readonly string systemPrompt =
            "You are a text evaluator. Your task is to analyze a text and return a boolean value indicating whether the text matches the given criteria.\n\n" +
            "Respond with TRUE or FALSE, nothing else.\n\n" +
            "In case the text does not match the criteria, respond with FALSE.";

        /// <summary>
        /// User prompt for the AI tool provided by this class. Use <question> and <text> placeholders.
        /// </summary>
        private readonly string userPrompt =
            "This is my question: \"<question>\"\n\n" +
            "Answer the previous question based on the following input:\n<text>\n\n";

        /// <summary>
        /// Get all tools provided by this class.
        /// </summary>
        /// <returns>Collection of AI tools.</returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Evaluates a text against a true/false question",
                category: "DataProcessing",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""text"": { ""type"": ""string"", ""description"": ""The text to evaluate"" },
                        ""question"": { ""type"": ""string"", ""description"": ""The true/false question to evaluate"" }
                    },
                    ""required"": [""text"", ""question"" ]
                }",
                execute: this.EvaluateText,
                requiredCapabilities: this.toolCapabilityRequirements
            );
        }

        /// <summary>
        /// Tool wrapper for the EvaluateText function.
        /// </summary>
        /// <param name="parameters">Parameters passed from the AI.</param>
        /// <returns>Result object.</returns>
        private async Task<object> EvaluateText(JObject parameters)
        {
            try
            {
                Debug.WriteLine("[TextTools] Running EvaluateText tool");

                // Extract parameters
                string providerName = parameters["provider"]?.ToString() ?? string.Empty;
                string modelName = parameters["model"]?.ToString() ?? string.Empty;
                string endpoint = this.toolName;
                string? text = parameters["text"]?.ToString();
                string? question = parameters["question"]?.ToString();
                string? contextFilter = parameters["contextFilter"]?.ToString() ?? string.Empty;

                if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(question))
                {
                    return AIReturn<bool>.CreateError("Missing required parameters").ToJObject<bool>();
                }

                // Prepare the AI request
                var userPrompt = this.userPrompt;
                userPrompt = userPrompt.Replace("<question>", question);
                userPrompt = userPrompt.Replace("<text>", text);

                var requestBody = new AIRequestBody();
                requestBody.AddInteraction("system", this.systemPrompt);
                requestBody.AddInteraction("user", userPrompt);

                var request = new AIRequest
                {
                    Provider = providerName,
                    Model = modelName,
                    Capability = this.toolCapabilityRequirements,
                    Endpoint = endpoint,
                    Body = requestBody,
                };

                // Execute the tool
                var result = await request.Do<string>().ConfigureAwait(false);

                // Strip thinking tags from response before parsing
                var cleanedResponse = AI.StripThinkTags(result.Result);

                // Parse the boolean from the response
                var parsedResult = ParsingTools.ParseBooleanFromResponse(cleanedResponse);

                if (parsedResult == null)
                {
                    return AIReturn<bool>.CreateError(
                        $"The AI returned an invalid response:\n{result.Result}",
                        request: request,
                        metrics: result.Metrics).ToJObject<bool>();
                }

                // Success case
                return AIReturn<bool>.CreateSuccess(
                    result: parsedResult.Value,
                    request: request,
                    metrics: result.Metrics).ToJObject<bool>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TextTools] Error in EvaluateText: {ex.Message}");

                // Return error object as JObject
                return AIReturn<bool>.CreateError($"Error: {ex.Message}").ToJObject<bool>();
            }
        }
    }
}
