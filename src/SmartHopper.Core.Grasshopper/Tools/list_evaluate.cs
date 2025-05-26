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
using System.Linq;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Config.Interfaces;
using SmartHopper.Config.Models;
using SmartHopper.Core.AI;
using SmartHopper.Core.Grasshopper.Models;

namespace SmartHopper.Core.Grasshopper.Tools
{
    /// <summary>
    /// Contains tools for list analysis and manipulation using AI.
    /// </summary>
    public class list_evaluate : IAIToolProvider
    {
        /// <summary>
        /// Get all tools provided by this class.
        /// </summary>
        /// <returns>Collection of AI tools.</returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: "list_evaluate",
                description: "Evaluates a list based on a natural language question",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""list"": { ""type"": ""array"", ""items"": { ""type"": ""string"" }, ""description"": ""Array of strings to evaluate (e.g., ['apple', 'banana', 'orange'])"" },
                        ""question"": { ""type"": ""string"", ""description"": ""The natural language question to answer about the list"" }
                    },
                    ""required"": [""list"", ""question""]
                }",
                execute: this.EvaluateListToolWrapper);
        }

        /// <summary>
        /// Tool wrapper for the EvaluateList function.
        /// </summary>
        /// <param name="parameters">Parameters passed from the AI.</param>
        /// <returns>Result object.</returns>
        private async Task<object> EvaluateListToolWrapper(JObject parameters)
        {
            try
            {
                Debug.WriteLine("[ListTools] Running EvaluateListToolWrapper");

                // Extract parameters
                string providerName = parameters["provider"]?.ToString() ?? string.Empty;
                string modelName = parameters["model"]?.ToString() ?? string.Empty;
                string? rawList = parameters["list"]?.ToString();
                string? question = parameters["question"]?.ToString();

                if (string.IsNullOrEmpty(rawList) || string.IsNullOrEmpty(question))
                {
                    // Return error object as JObject
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "Missing required parameters"
                    };
                }

                // Normalize list input
                var items = NormalizeListInput(parameters);

                // Convert to GH_String list
                var ghStringList = items.Select(s => new GH_String(s)).ToList();

                // Execute the tool
                var result = await EvaluateListAsync(
                    ghStringList,
                    new GH_String(question),
                    messages => AIUtils.GetResponse(providerName, modelName, messages)).ConfigureAwait(false);

                // Return standardized result
                return new JObject
                {
                    ["success"] = result.Success,
                    ["result"] = result.Success ? new JValue(result.Result) : JValue.CreateNull(),
                    ["error"] = result.Success ? JValue.CreateNull() : new JValue(result.ErrorMessage)
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ListTools] Error in EvaluateListToolWrapper: {ex.Message}");
                // Return error object as JObject
                return new JObject
                {
                    ["success"] = false,
                    ["error"] = $"Error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Evaluates a list based on a natural language question using AI with a custom GetResponse function, accepts raw GH_String list.
        /// </summary>
        /// <param name="inputList">The list of GH_String items to evaluate.</param>
        /// <param name="question">The natural language question to answer.</param>
        /// <param name="getResponse">Custom function to get AI response.</param>
        /// <returns>Evaluation result containing the AI response, boolean result, and any error information.</returns>
        public static async Task<AIEvaluationResult<bool>> EvaluateListAsync(
            List<GH_String> inputList,
            GH_String question,
            Func<List<KeyValuePair<string, string>>, Task<AIResponse>> getResponse)
        {
            try
            {
                // Convert list to JSON dictionary for AI prompt - process the list as a whole
                var dictJson = ParsingTools.ConcatenateItemsToJson(inputList);

                // Call the string-based method to handle the core logic
                return await EvaluateListAsync(dictJson, question, getResponse).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ListTools] Error in EvaluateListAsync (List<GH_String> overload): {ex.Message}");
                return AIEvaluationResult<bool>.CreateError(
                    $"Error evaluating list: {ex.Message}",
                    GH_RuntimeMessageLevel.Error);
            }
        }

        /// <summary>
        /// Evaluates a list based on a natural language question using AI with a custom GetResponse function.
        /// </summary>
        /// <param name="jsonList">The list of items to evaluate in JSON format.</param>
        /// <param name="question">The natural language question to answer.</param>
        /// <param name="getResponse">Custom function to get AI response.</param>
        /// <returns>Evaluation result containing the AI response, boolean result, and any error information.</returns>
        public static async Task<AIEvaluationResult<bool>> EvaluateListAsync(
            string jsonList,
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
                        "You are a list analyzer. Your task is to analyze a list of items and return a boolean value indicating whether the list matches the given criteria.\n\n" +
                        "The list will be provided as a JSON dictionary where the key is the index and the value is the item.\n\n" +
                        "Mainly you will base your answers on the item itself, unless the user asks for something regarding the position of items in the list.\n\n" +
                        "Respond with TRUE or FALSE, nothing else."),

                    // User message
                    new ("user",
                        $"This is my question: \"{question.Value}\"\n\n" +
                        $"Answer to the previous question with the following list:\n{jsonList}\n\n"),
                };

                // Get response using the provided function
                var response = await getResponse(messages).ConfigureAwait(false);

                // Check for API errors
                if (response.FinishReason == "error")
                {
                    return AIEvaluationResult<bool>.CreateError(
                        response.Response,
                        GH_RuntimeMessageLevel.Error,
                        response);
                }

                // Parse the boolean from the response
                var result = ParsingTools.ParseBooleanFromResponse(response.Response);

                if (result == null)
                {
                    return AIEvaluationResult<bool>.CreateError(
                        $"The AI returned an invalid response:\n{response.Response}",
                        GH_RuntimeMessageLevel.Error,
                        response);
                }

                // Success case
                return AIEvaluationResult<bool>.CreateSuccess(
                    response,
                    result.Value);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ListTools] Error in EvaluateListAsync: {ex.Message}");
                return AIEvaluationResult<bool>.CreateError(
                    $"Error evaluating list: {ex.Message}",
                    GH_RuntimeMessageLevel.Error);
            }
        }

        /// <summary>
        /// Normalizes the 'list' parameter into a list of strings, parsing malformed input.
        /// </summary>
        private static List<string> NormalizeListInput(JObject parameters)
        {
            var token = parameters["list"];
            if (token is JArray array)
            {
                return array.Select(t => t.ToString()).ToList();
            }

            var raw = token?.ToString();
            return ParsingTools.ParseStringArrayFromResponse(raw);
        }
    }
}
