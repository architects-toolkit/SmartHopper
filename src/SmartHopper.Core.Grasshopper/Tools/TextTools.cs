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
using SmartHopper.Core.Utils;
using SmartHopper.Config.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace SmartHopper.Core.Grasshopper.Tools
{
    /// <summary>
    /// Contains tools for text analysis and manipulation using AI
    /// </summary>
    public static class TextTools
    {
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
                        $"Answer the previous question based on the following input:\n{text.Value}\n\n")
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
