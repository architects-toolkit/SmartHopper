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
using SmartHopper.Core.Grasshopper.Models;
using SmartHopper.Core.Messaging;
using SmartHopper.Infrastructure.Interfaces;
using SmartHopper.Infrastructure.Models;
using SmartHopper.Infrastructure.Utils;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Contains tools for text analysis and manipulation using AI.
    /// </summary>
    public class text_generate : IAIToolProvider
    {
        /// <summary>
        /// Get all tools provided by this class.
        /// </summary>
        /// <returns>Collection of AI tools.</returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: "text_generate",
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
                execute: this.GenerateTextToolWrapper,
                requiredCapabilities: new[] { AIModelCapability.TextInput, AIModelCapability.TextOutput }
            );
        }

        /// <summary>
        /// Generates text from a prompt and optional instructions using AI with a custom GetResponse function.
        /// </summary>
        /// <param name="prompt">The user's prompt.</param>
        /// <param name="instructions">Optional instructions for the AI.</param>
        /// <param name="getResponse">Custom function to get AI response.</param>
        /// <returns>The generated text as a GH_String.</returns>
        private static async Task<AIEvaluationResult<GH_String>> GenerateTextAsync(
            GH_String prompt,
            GH_String instructions,
            Func<List<KeyValuePair<string, string>>, Task<AIResponse>> getResponse)
        {
            try
            {
                // Initiate the messages array
                var messages = new List<KeyValuePair<string, string>>();

                // Add system prompt if available
                var systemPrompt = instructions.Value;
                if (!string.IsNullOrWhiteSpace(systemPrompt))
                {
                    messages.Add(new KeyValuePair<string, string>("system", systemPrompt));
                }

                // Add the user prompt
                messages.Add(new KeyValuePair<string, string>("user", prompt.Value));

                // Get response using the provided function
                var response = await getResponse(messages).ConfigureAwait(false);

                // Check for API errors
                if (response.FinishReason == "error")
                {
                    return AIEvaluationResult<GH_String>.CreateError(
                        response.Response,
                        GH_RuntimeMessageLevel.Error,
                        response);
                }

                // Strip thinking tags from response before using
                var cleanedResponse = AI.StripThinkTags(response.Response);
                
                // Success case
                return AIEvaluationResult<GH_String>.CreateSuccess(
                    response,
                    new GH_String(cleanedResponse));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TextTools] Error in GenerateTextAsync: {ex.Message}");
                return AIEvaluationResult<GH_String>.CreateError(
                    $"Error generating text: {ex.Message}",
                    GH_RuntimeMessageLevel.Error);
            }
        }

        /// <summary>
        /// Tool wrapper for the GenerateText function.
        /// </summary>
        /// <param name="parameters">Parameters passed from the AI.</param>
        /// <returns>Result object.</returns>
        private async Task<object> GenerateTextToolWrapper(JObject parameters)
        {
            try
            {
                Debug.WriteLine("[TextTools] Running GenerateTextToolWrapper");

                // Extract parameters
                string providerName = parameters["provider"]?.ToString() ?? string.Empty;
                string modelName = parameters["model"]?.ToString() ?? string.Empty;
                string endpoint = "text_generate";
                string? prompt = parameters["prompt"]?.ToString();
                string instructions = parameters["instructions"]?.ToString() ?? string.Empty;
                string? contextProviderFilter = parameters["contextProviderFilter"]?.ToString() ?? string.Empty;
                string? contextKeyFilter = parameters["contextKeyFilter"]?.ToString() ?? string.Empty;

                if (string.IsNullOrEmpty(prompt))
                {
                    // Return error object as JObject
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "Missing required parameter: prompt"
                    };
                }

                // Execute the tool
                var result = await GenerateTextAsync(
                    new GH_String(prompt),
                    new GH_String(instructions),
                    messages => AIUtils.GetResponse(
                        providerName,
                        modelName,
                        messages,
                        endpoint: endpoint,
                        contextProviderFilter: contextProviderFilter,
                        contextKeyFilter: contextKeyFilter)
                ).ConfigureAwait(false);

                // Build standardized result as JObject
                var responseObj = new JObject
                {
                    ["success"] = result.Success,
                    ["result"] = result.Success ? new JValue(result.Result.Value) : JValue.CreateNull(),
                    ["error"] = result.Success ? JValue.CreateNull() : new JValue(result.ErrorMessage),
                    ["rawResponse"] = JToken.FromObject(result.Response),
                };
                return responseObj;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TextTools] Error in GenerateTextToolWrapper: {ex.Message}");
                // Return error object as JObject
                return new JObject
                {
                    ["success"] = false,
                    ["error"] = $"Error: {ex.Message}",
                };
            }
        }
    }
}
