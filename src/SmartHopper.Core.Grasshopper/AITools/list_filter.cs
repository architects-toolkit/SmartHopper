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
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Tools;
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
        private async Task<AIReturn> FilterList(AIToolCall toolCall)
        {
            // Prepare the output
            var output = new AIReturn()
            {
                Request = toolCall,
            };

            try
            {
                Debug.WriteLine("[ListTools] Running FilterList tool");

                // Extract parameters
                string providerName = toolCall.Provider;
                string modelName = toolCall.Model;
                string endpoint = this.toolName;
                AIInteractionToolCall toolInfo = toolCall.GetToolCall();;
                string? rawList = toolInfo.Arguments["list"]?.ToString();
                string? criteria = toolInfo.Arguments["criteria"]?.ToString();
                string? contextFilter = toolInfo.Arguments["contextFilter"]?.ToString() ?? string.Empty;

                if (string.IsNullOrEmpty(rawList) || string.IsNullOrEmpty(criteria))
                {
                    output.CreateError("Missing required parameters");
                    return output;
                }

                // Normalize list input
                var items = NormalizeListInput(toolInfo);

                // Convert to GH_String list
                var ghStringList = items.Select(s => new GH_String(s)).ToList();

                string itemsJsonDict = ParsingTools.ConcatenateItemsToJson(ghStringList);

                // Prepare the AI request
                var userPrompt = this.userPrompt;
                userPrompt = userPrompt.Replace("<criteria>", criteria);
                userPrompt = userPrompt.Replace("<list>", itemsJsonDict);

                // Initiate AIBody
                var requestBody = new AIBody();
                requestBody.AddInteraction("system", this.systemPrompt);
                requestBody.AddInteraction("user", userPrompt);
                requestBody.ContextFilter = contextFilter;

                // Initiate AIRequestCall
                var request = new AIRequestCall();
                request.Initialize(
                    provider: providerName,
                    model: modelName,
                    capability: this.toolCapabilityRequirements,
                    endpoint: endpoint,
                    body: requestBody);

                // Execute the AIRequestCall
                var result = await request.Exec().ConfigureAwait(false);

                var response = result.Body.GetLastInteraction(AIAgent.Assistant).ToString();

                // Parse indices from response
                var indices = ParsingTools.ParseIndicesFromResponse(response);

                if (indices == null)
                {
                    output.CreateError($"The AI returned an invalid response:\n{result}");
                    return output;
                }

                Debug.WriteLine($"[ListTools] Got indices: {string.Join(", ", indices)}");

                // Success case
                var toolResult = new JObject();
                toolResult.Add("result", new JArray(indices));

                var toolBody = new AIBody();
                toolBody.AddInteractionToolResult(toolResult, result.Metrics, result.Messages);

                output.CreateSuccess(toolBody);
                return output;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ListTools] Error in FilterList: {ex.Message}");

                output.CreateError($"Error: {ex.Message}");
                return output;
            }
        }

        /// <summary>
        /// Normalizes the 'list' parameter into a list of strings, parsing malformed input.
        /// </summary>
        private static List<string> NormalizeListInput(AIInteractionToolCall toolCall)
        {
            var token = toolCall.Arguments["list"];
            if (token is JArray array)
            {
                return array.Select(t => t.ToString()).ToList();
            }

            var raw = token?.ToString();
            return ParsingTools.ParseStringArrayFromResponse(raw);
        }
    }
}

