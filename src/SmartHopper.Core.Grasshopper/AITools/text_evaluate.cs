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
        /// Get all tools provided by this class.
        /// </summary>
        /// <returns>Collection of AI tools.</returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: "text_evaluate",
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
                execute: this.EvaluateTextToolWrapper,
                requiredCapabilities: AICapability.TextInput | AICapability.TextOutput
            );
        }

        /// <summary>
        /// Evaluates text against a true/false question using AI with a custom GetResponse function.
        /// </summary>
        /// <param name="text">The text to analyze.</param>
        /// <param name="question">The true/false question to evaluate.</param>
        /// <param name="getResponse">Custom function to get AI response.</param>
        /// <returns>Evaluation result containing the AI response, parsed result, and any error information.</returns>
        private static async Task<AIEvaluationResult<GH_Boolean>> EvaluateTextAsync(
            GH_String text,
            GH_String question,
            Func<List<KeyValuePair<string, string>>, Task<AIResponse>> getResponse)
        {
            try
            {
                // Prepare messages for the AI
                var messages = new List<KeyValuePair<string, string>>
                {
                    // System prompt
                    new ("system",
                        "You are a text evaluator. Your task is to analyze a text and return a boolean value indicating whether the text matches the given criteria.\n\n" +
                        "Respond with TRUE or FALSE, nothing else.\n\n" +
                        "In case the text does not match the criteria, respond with FALSE."),

                    // User message
                    new ("user",
                        $"This is my question: \"{question.Value}\"\n\n" +
                        $"Answer the previous question based on the following input:\n{text.Value}\n\n"),
                };

                // Get response using the provided function
                var response = await getResponse(messages).ConfigureAwait(false);

                // Check for API errors
                if (response.FinishReason == "error")
                {
                    return AIEvaluationResult<GH_Boolean>.CreateError(
                        response.Response,
                        "Error",
                        response);
                }

                // Strip thinking tags from response before parsing
                var cleanedResponse = AI.StripThinkTags(response.Response);
                
                // Parse the response
                var parsedResult = ParsingTools.ParseBooleanFromResponse(cleanedResponse);
                if (parsedResult == null)
                {
                    return AIEvaluationResult<GH_Boolean>.CreateError(
                        $"The AI returned an invalid response:\n{response.Response}",
                        "Error",
                        response);
                }

                // Success case
                return AIEvaluationResult<GH_Boolean>.CreateSuccess(
                    response,
                    new GH_Boolean(parsedResult.Value));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TextTools] Error in EvaluateTextAsync: {ex.Message}");
                return AIEvaluationResult<GH_Boolean>.CreateError(
                    $"Error evaluating text: {ex.Message}", "Error");
            }
        }

        /// <summary>
        /// Tool wrapper for the EvaluateText function.
        /// </summary>
        /// <param name="parameters">Parameters passed from the AI.</param>
        /// <returns>Result object.</returns>
        private async Task<object> EvaluateTextToolWrapper(JObject parameters)
        {
            try
            {
                Debug.WriteLine("[TextTools] Running EvaluateTextToolWrapper");

                // Extract parameters
                string providerName = parameters["provider"]?.ToString() ?? string.Empty;
                string modelName = parameters["model"]?.ToString() ?? string.Empty;
                string endpoint = "text_evaluate";
                string? text = parameters["text"]?.ToString();
                string? question = parameters["question"]?.ToString();
                string? contextProviderFilter = parameters["contextProviderFilter"]?.ToString() ?? string.Empty;
                string? contextKeyFilter = parameters["contextKeyFilter"]?.ToString() ?? string.Empty;

                if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(question))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "Missing required parameters",
                    };
                }

                // Execute the tool
                var result = await EvaluateTextAsync(
                    new GH_String(text),
                    new GH_String(question),
                    messages => AIUtils.GetResponse(
                        providerName,
                        modelName,
                        messages,
                        endpoint: endpoint,
                        contextProviderFilter: contextProviderFilter,
                        contextKeyFilter: contextKeyFilter)
                ).ConfigureAwait(false);

                // Return standardized result
                return new JObject
                {
                    ["success"] = result.Success,
                    ["result"] = result.Success ? new JValue(result.Result.Value) : JValue.CreateNull(),
                    ["error"] = result.Success ? JValue.CreateNull() : new JValue(result.ErrorMessage),
                    ["rawResponse"] = JToken.FromObject(result.Response),
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TextTools] Error in EvaluateTextToolWrapper: {ex.Message}");
                return new JObject
                {
                    ["success"] = false,
                    ["error"] = $"Error: {ex.Message}",
                };
            }
        }
    }
}
