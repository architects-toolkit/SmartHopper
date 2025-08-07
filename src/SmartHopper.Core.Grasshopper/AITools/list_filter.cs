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
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "list_filter";

        /// <summary>
        /// Defines the required capabilities for the AI tool provided by this class.
        /// </summary>
        private readonly AICapability toolCapabilityRequirements = AICapability.TextInput | AICapability.TextOutput;

        /// <summary>
        /// System prompt for the AI tool provided by this class.
        /// </summary>
        private readonly string systemPrompt =
            "You are a list filtering assistant. Your task is to filter a list of items based on natural language criteria and return the indices of items that match.\n\n" +
            "The list will be provided as a JSON dictionary where the key is the index and the value is the item.\n\n" +
            "Based on the filtering criteria, return ONLY the indices (as integers) of the items that should remain in the list. " +
            "Return the indices as a JSON array, for example: [0, 2, 5]. If no items match, return an empty array: [].\n\n" +
            "Do not include the actual items in your response, only the indices.";

        /// <summary>
        /// User prompt for the AI tool provided by this class. Use <criteria> and <list> placeholders.
        /// </summary>
        private readonly string userPrompt =
            $"Apply this filtering criteria: \"<criteria>\"\n\n" +
            $"To the following list:\n<list>\n\n" +
            $"Return only the indices of items that match the criteria as a JSON array.";

        /// <summary>
        /// Get all tools provided by this class.
        /// </summary>
        /// <returns>Collection of AI tools.</returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
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
                execute: this.FilterList,
                requiredCapabilities: this.toolCapabilityRequirements
            );
        }

        /// <summary>
        /// Tool wrapper for the FilterList function.
        /// </summary>
        /// <param name="parameters">Parameters passed from the AI.</param>
        /// <returns>Result object.</returns>
        private async Task<object> FilterList(JObject parameters)
        {
            try
            {
                Debug.WriteLine("[ListTools] Running FilterList tool");

                // Extract parameters
                string providerName = parameters["provider"]?.ToString() ?? string.Empty;
                string modelName = parameters["model"]?.ToString() ?? string.Empty;
                string endpoint = this.toolName;
                string? rawList = parameters["list"]?.ToString();
                string? criteria = parameters["criteria"]?.ToString();
                string? contextFilter = parameters["contextFilter"]?.ToString() ?? string.Empty;

                if (string.IsNullOrEmpty(rawList) || string.IsNullOrEmpty(criteria))
                {
                    // Return error object as JObject
                    return AIReturn<List<int>>.CreateError("Missing required parameters").ToJObject<List<int>>();
                }

                // Normalize list input
                var items = NormalizeListInput(parameters);

                // Convert to GH_String list
                var ghStringList = items.Select(s => new GH_String(s)).ToList();

                string itemsJsonDict = ParsingTools.ConcatenateItemsToJson(ghStringList);

                // Prepare the AI request
                var userPrompt = this.userPrompt;
                userPrompt = userPrompt.Replace("<criteria>", criteria);
                userPrompt = userPrompt.Replace("<list>", itemsJsonDict);

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

                // Parse indices from response
                var indices = ParsingTools.ParseIndicesFromResponse(cleanedResponse);

                if (indices == null)
                {
                    return AIReturn<List<int>>.CreateError(
                        $"The AI returned an invalid response:\n{result.Result}",
                        request: request,
                        metrics: result.Metrics).ToJObject<List<int>>();
                }

                Debug.WriteLine($"[ListTools] Got indices: {string.Join(", ", indices)}");

                // Success case
                var successResult = AIReturn<List<int>>.CreateSuccess(
                    result: indices,
                    request: request,
                    metrics: result.Metrics);

                // Return standardized result with custom mapping
                var mapping = new Dictionary<string, string>
                {
                    ["success"] = "Success",
                    ["indices"] = "Result",
                    ["error"] = "ErrorMessage",
                };

                var jobj = successResult.ToJObject<List<int>>(mapping);

                // Add a count field
                jobj["count"] = new JValue(indices.Count);

                return jobj;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ListTools] Error in FilterList: {ex.Message}");

                // Return error object as JObject
                return AIReturn<List<int>>.CreateError($"Error: {ex.Message}").ToJObject<List<int>>();
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
