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
using SmartHopper.Config.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace SmartHopper.Core.Grasshopper.Tools
{
    /// <summary>
    /// Contains tools for list analysis and manipulation using AI
    /// </summary>
    public static class ListTools
    {
        #region AI Tool Provider Implementation

        /// <summary>
        /// Get all tools provided by this class
        /// </summary>
        /// <returns>Collection of AI tools</returns>
        public static IEnumerable<AITool> GetTools()
        {
            // Define the evaluate list tool
            yield return new AITool(
                name: "evaluateList",
                description: "Evaluates a list based on a natural language question",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""list"": {
                            ""type"": ""string"",
                            ""description"": ""JSON string representing the list to evaluate""
                        },
                        ""question"": {
                            ""type"": ""string"",
                            ""description"": ""The natural language question to answer about the list""
                        }
                    },
                    ""required"": [""list"", ""question""]
                }",
                execute: EvaluateListToolWrapper
            );
            
            // Define the filter list tool
            yield return new AITool(
                name: "filterList",
                description: "Filters a list based on natural language criteria",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""list"": {
                            ""type"": ""array"",
                            ""items"": {
                                ""type"": ""string""
                            },
                            ""description"": ""Array of strings to filter""
                        },
                        ""criteria"": {
                            ""type"": ""string"",
                            ""description"": ""Natural language criteria to apply (e.g., 'only items containing the word house', 'sort alphabetically', 'remove duplicates')""
                        }
                    },
                    ""required"": [""list"", ""criteria""]
                }",
                execute: FilterListToolWrapper
            );
        }
        
        /// <summary>
        /// Tool wrapper for the EvaluateList function
        /// </summary>
        /// <param name="parameters">Parameters passed from the AI</param>
        /// <returns>Result object</returns>
        private static async Task<object> EvaluateListToolWrapper(JObject parameters)
        {
            try
            {
                Debug.WriteLine("[ListTools] Running EvaluateListToolWrapper");
                
                // Extract parameters
                string jsonList = parameters["list"]?.ToString();
                string question = parameters["question"]?.ToString();
                
                if (string.IsNullOrEmpty(jsonList) || string.IsNullOrEmpty(question))
                {
                    return new { 
                        success = false, 
                        error = "Missing required parameters"
                    };
                }
                
                // Execute the tool
                var result = await EvaluateListAsync(
                    jsonList,
                    new GH_String(question),
                    messages => AIUtils.GetResponse("default", "", messages)
                );
                
                // Return standardized result
                return new {
                    success = result.Success,
                    result = result.Success ? result.Result : false,
                    error = result.Success ? null : result.ErrorMessage
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ListTools] Error in EvaluateListToolWrapper: {ex.Message}");
                return new { 
                    success = false, 
                    error = $"Error: {ex.Message}"
                };
            }
        }
        
        /// <summary>
        /// Tool wrapper for the FilterList function
        /// </summary>
        /// <param name="parameters">Parameters passed from the AI</param>
        /// <returns>Result object</returns>
        private static async Task<object> FilterListToolWrapper(JObject parameters)
        {
            try
            {
                Debug.WriteLine("[ListTools] Running FilterListToolWrapper");
                
                // Extract parameters
                JArray listArray = parameters["list"] as JArray;
                string criteria = parameters["criteria"]?.ToString();
                
                if (listArray == null || string.IsNullOrEmpty(criteria))
                {
                    return new { 
                        success = false, 
                        error = "Missing required parameters"
                    };
                }
                
                // Convert JArray to List<GH_String>
                var ghStringList = new List<GH_String>();
                foreach (var item in listArray)
                {
                    ghStringList.Add(new GH_String(item.ToString()));
                }
                
                // Execute the tool
                var result = await FilterListAsync(
                    ghStringList,
                    new GH_String(criteria),
                    messages => AIUtils.GetResponse("default", "", messages)
                );
                
                // Convert result back to string array for JSON serialization
                var filteredStrings = new List<string>();
                if (result.Success && result.Result != null)
                {
                    foreach (var ghString in result.Result)
                    {
                        filteredStrings.Add(ghString.Value);
                    }
                }
                
                // Return standardized result
                return new {
                    success = result.Success,
                    filteredList = result.Success ? filteredStrings : null,
                    count = result.Success ? filteredStrings.Count : 0,
                    error = result.Success ? null : result.ErrorMessage
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ListTools] Error in FilterListToolWrapper: {ex.Message}");
                return new { 
                    success = false, 
                    error = $"Error: {ex.Message}"
                };
            }
        }
        
        #endregion

        #region List Filtering

        /// <summary>
        /// Filters a list based on natural language criteria using AI with a custom GetResponse function
        /// </summary>
        /// <param name="list">The list of items to filter</param>
        /// <param name="criteria">The natural language criteria to apply</param>
        /// <param name="getResponse">Custom function to get AI response</param>
        /// <returns>Evaluation result containing the AI response, filtered list, and any error information</returns>
        public static async Task<AIEvaluationResult<List<GH_String>>> FilterListAsync(
            List<GH_String> list,
            GH_String criteria,
            Func<List<KeyValuePair<string, string>>, Task<AIResponse>> getResponse)
        {
            try
            {
                // Convert list to JSON format
                var jsonList = ParsingTools.ConcatenateItemsToJson(list);
                
                // Prepare messages for the AI
                var messages = new List<KeyValuePair<string, string>>
                {
                    // System prompt
                    new KeyValuePair<string, string>("system", 
                        "You are a list processor assistant. Your task is to analyze a list of items and return the indices of items that match the given criteria.\n\n" +
                        "The list will be provided as a JSON dictionary where the key is the index and the value is the item.\n\n" +
                        "You can be asked to:\n" +
                        "- Reorder the list (return the same number of indices in a different order)\n" +
                        "- Filter the list (return less items than the original list)\n" +
                        "- Repeat some items (return some indices multiple times)\n" +
                        "- Shuffle the list (return a random order of indices)\n" +
                        "- Combination of the above\n\n" +
                        "Return ONLY the comma-separated indices of the selected items in the order specified by the user, or in the original order if the user didn't specify an order.\n\n" +
                        "DO NOT RETURN ANYTHING ELSE APART FROM THE COMMA-SEPARATED INDICES."),

                    // User message
                    new KeyValuePair<string, string>("user", 
                        $"Return the indices of items that match the following prompt: \"{criteria.Value}\"\n\n" +
                        $"Apply the previous prompt to the following list:\n{jsonList}\n\n")
                };

                // Get response using the provided function
                var response = await getResponse(messages);

                // Check for API errors
                if (response.FinishReason == "error")
                {
                    return AIEvaluationResult<List<GH_String>>.CreateError(
                        response.Response,
                        GH_RuntimeMessageLevel.Error,
                        response);
                }

                // Parse the indices from the response
                var indices = ParsingTools.ParseIndicesFromResponse(response.Response);
                
                Debug.WriteLine($"[ListTools] Got indices: {string.Join(", ", indices)}");

                // Create the filtered list based on the indices
                var result = new List<GH_String>();
                foreach (var index in indices)
                {
                    if (index >= 0 && index < list.Count)
                    {
                        result.Add(list[index]);
                    }
                    else
                    {
                        Debug.WriteLine($"[ListTools] Invalid index {index}. Skipping.");
                    }
                }

                // Success case
                return AIEvaluationResult<List<GH_String>>.CreateSuccess(
                    response,
                    result);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ListTools] Error in FilterListAsync: {ex.Message}");
                return AIEvaluationResult<List<GH_String>>.CreateError(
                    $"Error filtering list: {ex.Message}",
                    GH_RuntimeMessageLevel.Error);
            }
        }

        /// <summary>
        /// Filters a list based on natural language criteria using AI with the default AIUtils.GetResponse
        /// </summary>
        /// <param name="list">The list of items to filter</param>
        /// <param name="criteria">The natural language criteria to apply</param>
        /// <param name="provider">The AI provider to use</param>
        /// <param name="model">The model to use, or empty for default</param>
        /// <returns>Evaluation result containing the AI response, filtered list, and any error information</returns>
        public static Task<AIEvaluationResult<List<GH_String>>> FilterListAsync(
            List<GH_String> list,
            GH_String criteria,
            string provider,
            string model = "")
        {
            return FilterListAsync(list, criteria,
                messages => AIUtils.GetResponse(provider, model, messages));
        }

        #endregion

        #region List Evaluation

        /// <summary>
        /// Evaluates a list based on a natural language question using AI with a custom GetResponse function
        /// </summary>
        /// <param name="jsonList">The list of items to evaluate in JSON format</param>
        /// <param name="question">The natural language question to answer</param>
        /// <param name="getResponse">Custom function to get AI response</param>
        /// <returns>Evaluation result containing the AI response, boolean result, and any error information</returns>
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
                    new KeyValuePair<string, string>("system", 
                        "You are a list analyzer. Your task is to analyze a list of items and return a boolean value indicating whether the list matches the given criteria.\n\n" +
                        "The list will be provided as a JSON dictionary where the key is the index and the value is the item.\n\n" +
                        "Mainly you will base your answers on the item itself, unless the user asks for something regarding the position of items in the list.\n\n" +
                        "Respond with TRUE or FALSE, nothing else."),

                    // User message
                    new KeyValuePair<string, string>("user", 
                        $"This is my question: \"{question.Value}\"\n\n" +
                        $"Answer to the previous question with the following list:\n{jsonList}\n\n")
                };

                // Get response using the provided function
                var response = await getResponse(messages);

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
        /// Evaluates a list based on a natural language question using AI with the default AIUtils.GetResponse
        /// </summary>
        /// <param name="jsonList">The list of items to evaluate in JSON format</param>
        /// <param name="question">The natural language question to answer</param>
        /// <param name="provider">The AI provider to use</param>
        /// <param name="model">The model to use, or empty for default</param>
        /// <returns>Evaluation result containing the AI response, boolean result, and any error information</returns>
        public static Task<AIEvaluationResult<bool>> EvaluateListAsync(
            string jsonList,
            GH_String question,
            string provider,
            string model = "")
        {
            return EvaluateListAsync(jsonList, question,
                messages => AIUtils.GetResponse(provider, model, messages));
        }

        #endregion
    }
}
