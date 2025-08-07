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
        private async Task<object> EvaluateList(JObject parameters)
        {
            try
            {
                Debug.WriteLine("[ListTools] Running EvaluateList tool");

                // Extract parameters
                string providerName = parameters["provider"]?.ToString() ?? string.Empty;
                string modelName = parameters["model"]?.ToString() ?? string.Empty;
                string endpoint = this.toolName;
                string? rawList = parameters["list"]?.ToString();
                string? question = parameters["question"]?.ToString();
                string? contextFilter = parameters["contextFilter"]?.ToString() ?? string.Empty;

                if (string.IsNullOrEmpty(rawList) || string.IsNullOrEmpty(question))
                {
                    // Return error object as JObject
                    return AIReturn<bool>.CreateError("Missing required parameters").ToJObject<bool>();
                }

                // Normalize list input
                var items = NormalizeListInput(parameters);

                // Convert to GH_String list
                var ghStringList = items.Select(s => new GH_String(s)).ToList();

                string itemsJsonDict = ParsingTools.ConcatenateItemsToJson(ghStringList);

                // Prepare the AI request
                var userPrompt = this.userPrompt;
                userPrompt = userPrompt.Replace("<question>", question);
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

                // Parse the boolean from the response
                var parsedResult = ParsingTools.ParseBooleanFromResponse(cleanedResponse);

                if (parsedResult == null)
                {
                    return AIReturn<bool>.CreateError(
                        $"The AI returned an invalid response:\n{result.Result}",
                        request: request,
                        metrics: result.Metrics).ToJObject<bool>();
                }

                // Success case
                return AIReturn<bool>.CreateSuccess(
                    result: parsedResult.Value,
                    request: request,
                    metrics: result.Metrics).ToJObject<bool>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ListTools] Error in EvaluateListToolWrapper: {ex.Message}");

                // Return error object as JObject
                return AIReturn<bool>.CreateError($"Error: {ex.Message}").ToJObject<bool>();
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
