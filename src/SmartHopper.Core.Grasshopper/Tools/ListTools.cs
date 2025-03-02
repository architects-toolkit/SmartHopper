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
using System.Linq;
using System.Threading.Tasks;

namespace SmartHopper.Core.Grasshopper.Tools
{
    /// <summary>
    /// Contains tools for list processing and manipulation using AI
    /// </summary>
    public static class ListTools
    {
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
    }
}
