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
    /// Contains tools for list generation using AI.
    /// </summary>
    public class list_generate : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "list_generate";

        /// <summary>
        /// Defines the required capabilities for the AI tool provided by this class.
        /// </summary>
        private readonly AICapability toolCapabilityRequirements = AICapability.TextInput | AICapability.JsonOutput;

        /// <summary>
        /// JSON schema for list output.
        /// </summary>
        private readonly string listJsonSchema = "['item1', 'item2', 'item3', ...]";

        /// <summary>
        /// System prompt for the AI tool provided by this class.
        /// </summary>
        private readonly string systemPrompt =
            "You are a list generator assistant. Your task is to generate a specific number of items based on the user's prompt and return them as a JSON array.\n\n" +
            "IMPORTANT REQUIREMENTS:\n" +
            "- Return ONLY a valid JSON array of strings\n" +
            "- Each item must be a quoted string, even if it contains commas or special characters\n" +
            "- Generate exactly the requested number of items\n" +
            "- Do not include any extra text, explanations, or formatting\n" +
            "- Do not wrap the output in code blocks or additional quotes\n\n" +
            "OUTPUT EXAMPLES:\n" +
            "['item1', 'item2', 'item3']\n" +
            "['{1,0,0}', '{0.707,0.707,0}', '{0,1,0}']\n" +
            "['apple', 'banana with, comma', 'orange']";

        /// <summary>
        /// User prompt for the AI tool provided by this class. Use <prompt> and <count> placeholders.
        /// </summary>
        private readonly string userPrompt =
            "Generate exactly <count> items based on this prompt: \"<prompt>\"\n\n" +
            "Return only the JSON array of strings.";

        /// <summary>
        /// Get all tools provided by this class.
        /// </summary>
        /// <returns>An enumerable collection of AI tools provided by this class.</returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Generates a list of items based on a prompt, count and type",
                category: "DataProcessing",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""prompt"": { ""type"": ""string"", ""description"": ""The prompt to generate items from"" },
                        ""count"": { ""type"": ""integer"", ""description"": ""Number of items to generate"" },
                        ""type"": { ""type"": ""string"", ""description"": ""Type of items (e.g. 'text', 'number', 'integer', 'boolean')"", ""enum"": [""text"", ""number"", ""integer"", ""boolean""] }
                    },
                    ""required"": [""prompt"", ""count"", ""type""]
                }",
                execute: this.GenerateList,
                requiredCapabilities: this.toolCapabilityRequirements);
        }

        /// <summary>
        /// Tool wrapper for the GenerateList function.
        /// </summary>
        /// <param name="parameters">Parameters passed from the AI.</param>
        /// <returns>Result object.</returns>
        private async Task<AIToolCall> GenerateList(AIToolCall toolCall)
        {
            try
            {
                Debug.WriteLine("[ListTools] Running GenerateList tool");

                // Extract parameters
                string providerName = toolCall.Arguments["provider"]?.ToString() ?? string.Empty;
                string modelName = toolCall.Arguments["model"]?.ToString() ?? string.Empty;
                string endpoint = this.toolName;
                string? prompt = toolCall.Arguments["prompt"]?.ToString();
                int count = toolCall.Arguments["count"]?.ToObject<int>() ?? 0;
                string? type = toolCall.Arguments["type"]?.ToString();
                string? contextFilter = toolCall.Arguments["contextFilter"]?.ToString() ?? string.Empty;

                if (string.IsNullOrEmpty(prompt) || count <= 0 || string.IsNullOrEmpty(type))
                {
                    toolCall.ErrorMessage = "Missing or invalid parameters: prompt, count, or type";
                    return toolCall;
                }

                if (!type.Equals("text", StringComparison.OrdinalIgnoreCase))
                {
                    toolCall.ErrorMessage = $"Type '{type}' not supported";
                    return toolCall;
                }

                // Use iterative approach to ensure we get the exact count with conversational logic
                var allItems = new List<string>();
                const int maxIterations = 10;
                int iteration = 0;
                AIReturn<string>? result = null;

                // 1. Generate initial request
                var initialUserPrompt = this.userPrompt;
                initialUserPrompt = initialUserPrompt.Replace("<prompt>", prompt);
                initialUserPrompt = initialUserPrompt.Replace("<count>", count.ToString());

                var requestBody = new AIRequestBody();
                requestBody.JsonOutputSchema = this.listJsonSchema;
                requestBody.AddInteraction("system", this.systemPrompt);
                requestBody.AddInteraction("user", initialUserPrompt);

                var request = new AIRequest
                {
                    Provider = providerName,
                    Model = modelName,
                    Capability = this.toolCapabilityRequirements,
                    Endpoint = endpoint,
                    Body = requestBody,
                };

                while (allItems.Count < count && iteration < maxIterations)
                {
                    iteration++;
                    var stillNeeded = count - allItems.Count;
                    Debug.WriteLine($"[ListTools] Iteration {iteration}: Need {stillNeeded} more items (have {allItems.Count}/{count})");

                    // 2. Execute the request
                    result = await request.Do<string>().ConfigureAwait(false);

                    if (!result.Success)
                    {
                        toolCall.ErrorMessage = $"AI request failed: {result.ErrorMessage}";
                        return toolCall;
                    }

                    // 3. Parse the output and check if count is reached
                    var cleanedResponse = AI.StripThinkTags(result.Result);
                    Debug.WriteLine($"[ListTools] AI response: {cleanedResponse}");

                    // Parse JSON array of strings
                    List<string> newItems;
                    try
                    {
                        newItems = ParsingTools.ParseStringArrayFromResponse(cleanedResponse);
                    }
                    catch (Exception parseEx)
                    {
                        Debug.WriteLine($"[ListTools] Error parsing response in iteration {iteration}: {parseEx.Message}");
                        Debug.WriteLine($"[ListTools] Raw response: {cleanedResponse}");

                        // If we have some items already, return what we have
                        if (allItems.Count > 0)
                        {
                            Debug.WriteLine($"[ListTools] Returning partial list with {allItems.Count} items due to parsing error");
                            toolCall.Result = allItems;
                            toolCall.Metrics = result.Metrics;
                            return toolCall;
                        }

                        // Otherwise, return the error
                        toolCall.ErrorMessage = $"Error parsing AI response: {parseEx.Message}";
                        return toolCall;
                    }

                    Debug.WriteLine($"[ListTools] Iteration {iteration} generated {newItems.Count} items: {string.Join(", ", newItems)}");

                    // Add new items to our collection
                    foreach (var item in newItems)
                    {
                        allItems.Add(item);
                    }

                    // If we have enough items, trim to exact count and return
                    if (allItems.Count >= count)
                    {
                        if (allItems.Count > count)
                        {
                            allItems = allItems.GetRange(0, count);
                        }

                        Debug.WriteLine($"[ListTools] Target count {count} reached after {iteration} iterations");
                        break;
                    }

                    // 4. If count not reached, build conversation for next iteration
                    // Use the request from the last return and add assistant + user interactions
                    stillNeeded = count - allItems.Count;
                    Debug.WriteLine($"[ListTools] Requesting {stillNeeded} more items in next iteration");

                    // Get the request from the result and add the assistant response
                    request = result.Request;
                    request.Body.AddInteraction("assistant", result.Result);
                    
                    // Add follow-up user message asking for more items
                    var followUpMessage = $"I need {stillNeeded} more items to complete the list. Please generate {stillNeeded} additional items to the ones already provided. Current list has {allItems.Count} items: [{string.Join(", ", allItems.Select(item => $"'{item}'"))}].\n\nGenerate {stillNeeded} NEW items as a JSON array, meeting my initial request: {prompt}.\n\nReturn only the JSON array of the new items, nothing else.";
                    request.Body.AddInteraction("user", followUpMessage);
                }

                if (allItems.Count == 0)
                {
                    toolCall.ErrorMessage = "AI failed to generate any valid items";
                    return toolCall;
                }

                // Final safety check: trim list if it's longer than requested
                if (allItems.Count > count)
                {
                    Debug.WriteLine($"[ListTools] Trimming final list from {allItems.Count} to {count} items");
                    allItems = allItems.GetRange(0, count);
                }

                Debug.WriteLine($"[ListTools] Final result: {allItems.Count} items generated: {string.Join(", ", allItems)}");

                // Success case
                toolCall.Result = allItems;
                toolCall.Metrics = lastResult?.Metrics;
                return toolCall;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ListTools] Error in GenerateList: {ex.Message}");

                // Return error object as JObject
                toolCall.ErrorMessage = $"Error: {ex.Message}";
                return toolCall;
            }
        }
    }
}
