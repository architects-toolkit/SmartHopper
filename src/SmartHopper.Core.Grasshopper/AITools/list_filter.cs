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
using SmartHopper.Core.Grasshopper.Utils;
using SmartHopper.Core.Messaging;
using SmartHopper.Infrastructure.AICall;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.Infrastructure.Utils;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Contains tools for list analysis and manipulation using AI.
    /// </summary>
    public class list_filter : IAIToolProvider
    {
        /// <summary>
        /// Get all tools provided by this class.
        /// </summary>
        /// <returns>Collection of AI tools.</returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: "list_filter",
                description: "Filters a list based on natural language criteria",
                category: "DataProcessing",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""list"": { ""type"": ""array"", ""items"": { ""type"": ""string"" }, ""description"": ""Array of strings to filter (e.g., ['apple', 'banana', 'orange'])"" },
                        ""criteria"": { ""type"": ""string"", ""description"": ""Natural language criteria to apply (e.g., 'only items containing the word house', 'sort alphabetically', 'remove duplicates')"" }
                    },
                    ""required"": [""list"", ""criteria""]
                }",
                execute: this.FilterListToolWrapper,
                requiredCapabilities: AICapability.TextInput | AICapability.TextOutput
            );
        }

        /// <summary>
        /// Filters a list based on natural language criteria using AI with a custom GetResponse function, accepts raw GH_String list.
        /// </summary>
        /// <param name="inputList">The list of GH_String items to filter.</param>
        /// <param name="criteria">The natural language criteria to apply.</param>
        /// <param name="getResponse">Custom function to get AI response.</param>
        /// <returns>Evaluation result containing the AI response, list of indices, and any error information.</returns>
        private static async Task<AIEvaluationResult<List<int>>> FilterListAsync(
            List<GH_String> inputList,
            GH_String criteria,
            Func<List<KeyValuePair<string, string>>, Task<AIResponse>> getResponse)
        {
            try
            {
                // Convert list to JSON dictionary for AI prompt - process the list as a whole
                var dictJson = ParsingTools.ConcatenateItemsToJson(inputList);

                // Call the string-based method to handle the core logic
                return await FilterListAsync(dictJson, criteria, getResponse).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ListTools] Error in FilterListAsync (List<GH_String> overload): {ex.Message}");
                return AIEvaluationResult<List<int>>.CreateError(
                    $"Error filtering list: {ex.Message}", "Error");
            }
        }

        /// <summary>
        /// Filters a list based on natural language criteria using AI with a custom GetResponse function, accepts JSON list string.
        /// </summary>
        /// <param name="jsonList">The list of items to filter in JSON format.</param>
        /// <param name="criteria">The natural language criteria to apply.</param>
        /// <param name="getResponse">Custom function to get AI response.</param>
        /// <returns>Evaluation result containing the AI response, list of indices, and any error information.</returns>
        private static async Task<AIEvaluationResult<List<int>>> FilterListAsync(
            string jsonList,
            GH_String criteria,
            Func<List<KeyValuePair<string, string>>, Task<AIResponse>> getResponse)
        {
            try
            {
                // Prepare messages for the AI
                var messages = new List<KeyValuePair<string, string>>
                {
                    // System prompt
                    new ("system",
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
                    new ("user",
                        $"Return the indices of items that match the following prompt: \"{criteria.Value}\"\n\n" +
                        $"Apply the previous prompt to the following list:\n{jsonList}\n\n"),
                };

                // Get response using the provided function
                var response = await getResponse(messages).ConfigureAwait(false);

                // Check for API errors
                if (response.FinishReason == "error")
                {
                    return AIEvaluationResult<List<int>>.CreateError(
                        response.Response,
                        "Error",
                        response);
                }

                // Strip thinking tags from response before parsing
                var cleanedResponse = AI.StripThinkTags(response.Response);
                
                // Parse indices from response
                var indices = ParsingTools.ParseIndicesFromResponse(cleanedResponse);
                Debug.WriteLine($"[ListTools] Got indices: {string.Join(", ", indices)}");

                // Success case - return the indices directly
                return AIEvaluationResult<List<int>>.CreateSuccess(
                    response,
                    indices);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ListTools] Error in FilterListAsync: {ex.Message}");
                return AIEvaluationResult<List<int>>.CreateError(
                    $"Error filtering list: {ex.Message}", "Error");
            }
        }

        /// <summary>
        /// Tool wrapper for the FilterList function.
        /// </summary>
        /// <param name="parameters">Parameters passed from the AI.</param>
        /// <returns>Result object.</returns>
        private async Task<object> FilterListToolWrapper(JObject parameters)
        {
            try
            {
                Debug.WriteLine("[ListTools] Running FilterListToolWrapper");

                // Extract parameters
                string providerName = parameters["provider"]?.ToString() ?? string.Empty;
                string modelName = parameters["model"]?.ToString() ?? string.Empty;
                string endpoint = "list_filter";
                string? rawList = parameters["list"]?.ToString();
                string? criteria = parameters["criteria"]?.ToString();
                string? contextProviderFilter = parameters["contextProviderFilter"]?.ToString() ?? string.Empty;
                string? contextKeyFilter = parameters["contextKeyFilter"]?.ToString() ?? string.Empty;

                if (string.IsNullOrEmpty(rawList) || string.IsNullOrEmpty(criteria))
                {
                    // Return error object as JObject
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "Missing required parameters",
                    };
                }

                // Normalize list JSON
                var parsed = ParsingTools.ParseStringArrayFromResponse(rawList);

                // Convert to GH_String list
                var ghStringList = parsed.Select(s => new GH_String(s)).ToList();

                // Execute the tool
                var result = await FilterListAsync(
                    ghStringList,
                    new GH_String(criteria),
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
                    ["indices"] = result.Success ? JArray.FromObject(result.Result) : JValue.CreateNull(),
                    ["count"] = new JValue(result.Success ? result.Result.Count : 0),
                    ["error"] = result.Success ? JValue.CreateNull() : new JValue(result.ErrorMessage),
                    ["rawResponse"] = JToken.FromObject(result.Response),
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ListTools] Error in FilterListToolWrapper: {ex.Message}");
                // Return error object as JObject
                return new JObject
                {
                    ["success"] = false,
                    ["error"] = $"Error: {ex.Message}",
                };
            }
        }

        ///// <summary>
        ///// Builds a filtered list of GH_String items based on a list of indices.
        ///// </summary>
        ///// <param name="items">Original list of items.</param>
        ///// <param name="indices">List of indices to select.</param>
        ///// <returns>Filtered list of items.</returns>
        //public static List<GH_String> BuildFilteredListFromIndices(List<GH_String> items, List<int> indices)
        //{
        //    var result = new List<GH_String>();
        //    foreach (var idx in indices)
        //    {
        //        if (idx >= 0 && idx < items.Count)
        //        {
        //            result.Add(items[idx]);
        //        }
        //        else
        //        {
        //            Debug.WriteLine($"[ListTools] Invalid index {idx}. Skipping.");
        //        }
        //    }

        //    return result;
        //}
    }
}
