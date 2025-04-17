/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using SmartHopper.Core.AI;
using SmartHopper.Config.Models;
using SmartHopper.Config.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Config.Interfaces;

namespace SmartHopper.Core.Grasshopper.Tools
{
    /// <summary>
    /// Contains tools for text analysis and manipulation using AI
    /// </summary>
    public static class TextTools
    {
        #region AI Tool Provider Implementation

        /// <summary>
        /// Get all tools provided by this class
        /// </summary>
        /// <returns>Collection of AI tools</returns>
        public static IEnumerable<AITool> GetTools()
        {
            // Define the evaluate text tool
            yield return new AITool(
                name: "evaluateText",
                description: "Evaluates a text against a true/false question",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""text"": {
                            ""type"": ""string"",
                            ""description"": ""The text to evaluate""
                        },
                        ""question"": {
                            ""type"": ""string"",
                            ""description"": ""The true/false question to evaluate""
                        }
                    },
                    ""required"": [""text"", ""question""]
                }",
                execute: EvaluateTextToolWrapper
            );
            
            // Define the generate text tool
            yield return new AITool(
                name: "generateText",
                description: "Generates text based on a prompt and optional instructions",
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
                execute: GenerateTextToolWrapper
            );
        }
        
        /// <summary>
        /// Tool wrapper for the EvaluateText function
        /// </summary>
        /// <param name="parameters">Parameters passed from the AI</param>
        /// <returns>Result object</returns>
        private static async Task<object> EvaluateTextToolWrapper(JObject parameters)
        {
            try
            {
                Debug.WriteLine("[TextTools] Running EvaluateTextToolWrapper");
                
                // Extract parameters
                string text = parameters["text"]?.ToString();
                string question = parameters["question"]?.ToString();
                
                if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(question))
                {
                    return new { 
                        success = false, 
                        error = "Missing required parameters"
                    };
                }
                
                // Execute the tool
                var result = await EvaluateTextAsync(
                    new GH_String(text),
                    new GH_String(question),
                    messages => AIUtils.GetResponse("default", "", messages)
                );
                
                // Return standardized result
                return new {
                    success = result.Success,
                    result = result.Success ? result.Result.Value : false,
                    error = result.Success ? null : result.ErrorMessage
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TextTools] Error in EvaluateTextToolWrapper: {ex.Message}");
                return new { 
                    success = false, 
                    error = $"Error: {ex.Message}"
                };
            }
        }
        
        /// <summary>
        /// Tool wrapper for the GenerateText function
        /// </summary>
        /// <param name="parameters">Parameters passed from the AI</param>
        /// <returns>Result object</returns>
        private static async Task<object> GenerateTextToolWrapper(JObject parameters)
        {
            try
            {
                Debug.WriteLine("[TextTools] Running GenerateTextToolWrapper");
                
                // Extract parameters
                string prompt = parameters["prompt"]?.ToString();
                string instructions = parameters["instructions"]?.ToString() ?? "";
                
                if (string.IsNullOrEmpty(prompt))
                {
                    return new { 
                        success = false, 
                        error = "Missing required parameter: prompt"
                    };
                }
                
                // Execute the tool
                var result = await GenerateTextAsync(
                    new GH_String(prompt),
                    new GH_String(instructions),
                    messages => AIUtils.GetResponse("default", "", messages)
                );
                
                // Return standardized result
                return new {
                    success = result.Success,
                    text = result.Success ? result.Result.Value : null,
                    error = result.Success ? null : result.ErrorMessage
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TextTools] Error in GenerateTextToolWrapper: {ex.Message}");
                return new { 
                    success = false, 
                    error = $"Error: {ex.Message}"
                };
            }
        }
        
        #endregion

        #region Text Evaluation

        /// <summary>
        /// Evaluates text against a true/false question using AI with a custom GetResponse function
        /// </summary>
        /// <param name="text">The text to analyze</param>
        /// <param name="question">The true/false question to evaluate</param>
        /// <param name="getResponse">Custom function to get AI response</param>
        /// <returns>Evaluation result containing the AI response, parsed result, and any error information</returns>
        public static async Task<AIEvaluationResult<GH_Boolean>> EvaluateTextAsync(
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
                    new KeyValuePair<string, string>("system", 
                        "You are a text evaluator. Your task is to analyze a text and return a boolean value indicating whether the text matches the given criteria.\n\n" +
                        "Respond with TRUE or FALSE, nothing else.\n\n" +
                        "In case the text does not match the criteria, respond with FALSE."),

                    // User message
                    new KeyValuePair<string, string>("user", 
                        $"This is my question: \"{question.Value}\"\n\n" +
                        $"Answer to the previous question on the following text:\n{text.Value}\n\n")
                };

                // Get response using the provided function
                var response = await getResponse(messages);

                // Check for API errors
                if (response.FinishReason == "error")
                {
                    return AIEvaluationResult<GH_Boolean>.CreateError(
                        response.Response,
                        GH_RuntimeMessageLevel.Error,
                        response);
                }

                // Parse the response
                var parsedResult = ParsingTools.ParseBooleanFromResponse(response.Response);
                if (parsedResult == null)
                {
                    return AIEvaluationResult<GH_Boolean>.CreateError(
                        $"The AI returned an invalid response:\n{response.Response}",
                        GH_RuntimeMessageLevel.Error,
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
                    $"Error evaluating text: {ex.Message}",
                    GH_RuntimeMessageLevel.Error);
            }
        }

        /// <summary>
        /// Evaluates text against a true/false question using AI with the default AIUtils.GetResponse
        /// </summary>
        /// <param name="text">The text to analyze</param>
        /// <param name="question">The true/false question to evaluate</param>
        /// <param name="provider">The AI provider to use</param>
        /// <param name="model">The model to use, or empty for default</param>
        /// <returns>Evaluation result containing the AI response, parsed result, and any error information</returns>
        public static Task<AIEvaluationResult<GH_Boolean>> EvaluateTextAsync(
            GH_String text,
            GH_String question,
            string provider,
            string model = "")
        {
            return EvaluateTextAsync(text, question, 
                messages => AIUtils.GetResponse(provider, model, messages));
        }

        #endregion

        #region Text Generation

        /// <summary>
        /// Generates text from a prompt and optional instructions using AI with a custom GetResponse function
        /// </summary>
        /// <param name="prompt">The user's prompt</param>
        /// <param name="instructions">Optional instructions for the AI</param>
        /// <param name="getResponse">Custom function to get AI response</param>
        /// <returns>The generated text as a GH_String</returns>
        public static async Task<AIEvaluationResult<GH_String>> GenerateTextAsync(
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
                var response = await getResponse(messages);

                // Check for API errors
                if (response.FinishReason == "error")
                {
                    return AIEvaluationResult<GH_String>.CreateError(
                        response.Response,
                        GH_RuntimeMessageLevel.Error,
                        response);
                }

                // Success case
                return AIEvaluationResult<GH_String>.CreateSuccess(
                    response,
                    new GH_String(response.Response));
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
        /// Generates text from a prompt and optional instructions using AI with the default AIUtils.GetResponse
        /// </summary>
        /// <param name="prompt">The user's prompt</param>
        /// <param name="instructions">Optional instructions for the AI</param>
        /// <param name="provider">The AI provider to use</param>
        /// <param name="model">The model to use, or empty for default</param>
        /// <returns>The generated text as a GH_String</returns>
        public static Task<AIEvaluationResult<GH_String>> GenerateTextAsync(
            GH_String prompt,
            GH_String instructions,
            string provider,
            string model = "")
        {
            return GenerateTextAsync(prompt, instructions,
                messages => AIUtils.GetResponse(provider, model, messages));
        }

        #endregion
    }
}
