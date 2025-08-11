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
using SmartHopper.Core.Grasshopper.Models;
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
    public class list_evaluate : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "list_evaluate";

        /// <summary>
        /// Defines the required capabilities for the AI tool provided by this class.
        /// </summary>
        private readonly AICapability toolCapabilityRequirements = AICapability.TextInput | AICapability.TextOutput;

        /// <summary>
        /// System prompt for the AI tool provided by this class.
        /// </summary>
        private readonly string systemPrompt =
            "You are a list analyzer. Your task is to analyze a list of items and return a boolean value indicating whether the list matches the given criteria.\n\n" +
            "The list will be provided as a JSON dictionary where the key is the index and the value is the item.\n\n" +
            "Mainly you will base your answers on the item itself, unless the user asks for something regarding the position of items in the list.\n\n" +
            "Respond with TRUE or FALSE, nothing else.";

        /// <summary>
        /// User prompt for the AI tool provided by this class. Use <question> and <list> placeholders.
        /// </summary>
        private readonly string userPrompt =
            $"This is my question: \"<question>\"\n\n" +
            $"Answer to the previous question with the following list:\n<list>\n\n";

        /// <summary>
        /// Get all tools provided by this class.
        /// </summary>
        /// <returns>Collection of AI tools.</returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Evaluates a list based on a natural language question",
                category: "DataProcessing",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""list"": { ""type"": ""array"", ""items"": { ""type"": ""string"" }, ""description"": ""Array of strings to evaluate (e.g., ['apple', 'banana', 'orange'])"" },
                        ""question"": { ""type"": ""string"", ""description"": ""The natural language question to answer about the list"" }
                    },
                    ""required"": [""list"", ""question""]
                }",
                execute: this.EvaluateList,
                requiredCapabilities: this.toolCapabilityRequirements
            );
        }

        /// <summary>
        /// Tool wrapper for the EvaluateList function.
        /// </summary>
        /// <param name="parameters">Parameters passed from the AI.</param>
        /// <returns>Result object.</returns>
        private async Task<AIToolCall> EvaluateList(AIToolCall toolCall)
        {
            try
            {
                Debug.WriteLine("[ListTools] Running EvaluateList tool");

                // Extract parameters
                string providerName = toolCall.Provider;
                string modelName = toolCall.Model;
                string endpoint = this.toolName;
                string? rawList = toolCall.Arguments["list"]?.ToString();
                string? question = toolCall.Arguments["question"]?.ToString();
                string? contextFilter = toolCall.Arguments["contextFilter"]?.ToString() ?? string.Empty;

                if (string.IsNullOrEmpty(rawList) || string.IsNullOrEmpty(question))
                {
                    toolCall.ErrorMessage = "Missing required parameters";
                    return toolCall;
                }

                // Normalize list input
                var items = NormalizeListInput(toolCall);

                // Convert to GH_String list
                var ghStringList = items.Select(s => new GH_String(s)).ToList();

                string itemsJsonDict = ParsingTools.ConcatenateItemsToJson(ghStringList);

                // Prepare the AI request
                var userPrompt = this.userPrompt;
                userPrompt = userPrompt.Replace("<question>", question);
                userPrompt = userPrompt.Replace("<list>", itemsJsonDict);

                // Initiate AIBody
                var requestBody = new AIBody();
                requestBody.AddInteraction("system", this.systemPrompt);
                requestBody.AddInteraction("user", userPrompt);

                // Initiate AIRequestCall
                var request = new AIRequestCall
                {
                    Provider = providerName,
                    Model = modelName,
                    Capability = this.toolCapabilityRequirements,
                    Endpoint = endpoint,
                    Body = requestBody,
                };

                // Execute the AIRequestCall
                var result = await request.Do<string>().ConfigureAwait(false);

                // Parse the boolean from the response
                var parsedResult = ParsingTools.ParseBooleanFromResponse(result);

                if (parsedResult == null)
                {
                    toolCall.ErrorMessage = $"The AI returned an invalid response:\n{result}";
                    return toolCall;
                }

                // Success case
                toolCall.Result = parsedResult.Value;
                toolCall.Metrics = result.Metrics;
                return toolCall;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ListTools] Error in EvaluateListToolWrapper: {ex.Message}");

                toolCall.ErrorMessage = $"Error: {ex.Message}";
                return toolCall;
            }
        }

        /// <summary>
        /// Normalizes the 'list' parameter into a list of strings, parsing malformed input.
        /// </summary>
        private static List<string> NormalizeListInput(AIToolCall toolCall)
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
