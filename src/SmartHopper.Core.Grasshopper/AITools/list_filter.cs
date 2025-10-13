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
            "You are a list manipulation assistant. Your task is to process a list based on natural language criteria and return the POSITIONS (zero-based indices) of items.\n\n" +
            "IMPORTANT: The list is provided as a JSON dictionary where:\n" +
            "- The KEY is the POSITION (zero-based index) in the original list\n" +
            "- The VALUE is the actual ITEM content\n\n" +
            "Example: {\"0\":\"apple\", \"1\":\"banana\", \"2\":\"cherry\"}\n" +
            "- Position 0 contains the value \"apple\"\n" +
            "- Position 1 contains the value \"banana\"\n" +
            "- Position 2 contains the value \"cherry\"\n\n" +
            "Your task: Evaluate the criteria against the VALUES (item content), then return the KEYS (positions) based on the operation requested.\n\n" +
            "CRITICAL RULES:\n" +
            "1. Return ONLY the positions (keys) as integers in a JSON array, NOT the values\n" +
            "2. For FILTERING operations (e.g., \"keep only items > 5\", \"items starting with 'a'\"), return positions of matching items in ascending order\n" +
            "3. For REORDERING operations (e.g., \"sort alphabetically\", \"reverse order\", \"shuffle\"), return ALL positions reordered according to the criteria\n" +
            "4. For SELECTION operations (e.g., \"first 3 items\", \"every other item\"), return the selected positions\n" +
            "5. For EXPANSION operations (e.g., \"repeat each item twice\", \"duplicate the list\"), return positions with repetitions - the same index can appear multiple times\n\n" +
            "Examples:\n" +
            "- Filter: \"items starting with 'a'\" on {\"0\":\"banana\", \"1\":\"apple\", \"2\":\"cherry\"} → [1]\n" +
            "- Sort: \"sort alphabetically\" on {\"0\":\"banana\", \"1\":\"orange\", \"2\":\"apple\"} → [2, 0, 1] (apple, banana, orange)\n" +
            "- Select: \"first 2 items\" on {\"0\":\"a\", \"1\":\"b\", \"2\":\"c\"} → [0, 1]\n" +
            "- Expand: \"repeat each item twice\" on {\"0\":\"a\", \"1\":\"b\"} → [0, 0, 1, 1]\n\n" +
            "If no items match (for filters), return an empty array: [].";

        /// <summary>
        /// User prompt for the AI tool provided by this class. Use <criteria> and <list> placeholders.
        /// </summary>
        private readonly string userPrompt =
            $"Apply this operation to the list: \"<criteria>\"\n\n" +
            $"List (key=position, value=item):\n<list>\n\n" +
            $"Return the positions (keys) as a JSON array of integers based on the operation.";

        /// <summary>
        /// Get all tools provided by this class.
        /// </summary>
        /// <returns>Collection of AI tools.</returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Manipulates a list based on natural language criteria: filter, sort, reorder, select, shuffle, expand, or rearrange items",
                category: "DataProcessing",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""list"": { ""type"": ""array"", ""items"": { ""type"": ""string"" }, ""description"": ""Array of strings to process (e.g., ['apple', 'banana', 'orange'])"" },
                        ""criteria"": { ""type"": ""string"", ""description"": ""Natural language operation to apply (e.g., 'only items containing the word house', 'sort alphabetically', 'reverse order', 'first 3 items', 'every other item', 'repeat each item twice')"" }
                    },
                    ""required"": [""list"", ""criteria""]
                }",
                execute: this.FilterList,
                requiredCapabilities: this.toolCapabilityRequirements);
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
                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.Arguments ?? new JObject();
                string? rawList = args["list"]?.ToString();
                string? criteria = args["criteria"]?.ToString();
                string? contextFilter = args["contextFilter"]?.ToString() ?? string.Empty;

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

                // Initiate immutable AIBody
                var requestBody = AIBodyBuilder.Create()
                    .AddSystem(this.systemPrompt)
                    .AddUser(userPrompt)
                    .WithContextFilter(contextFilter)
                    .Build();

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

                if (!result.Success)
                {
                    // Propagate structured messages from AI call
                    output.Messages = result.Messages;
                    return output;
                }

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

                var toolBody = AIBodyBuilder.Create()
                    .AddToolResult(toolResult, id: toolInfo?.Id, name: this.toolName, metrics: result.Metrics, messages: result.Messages)
                    .Build();

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
            var args = toolCall.Arguments ?? new JObject();
            var token = args["list"];
            if (token is JArray array)
            {
                return array.Select(t => t.ToString()).ToList();
            }

            var raw = token?.ToString();
            return ParsingTools.ParseStringArrayFromResponse(raw);
        }
    }
}
