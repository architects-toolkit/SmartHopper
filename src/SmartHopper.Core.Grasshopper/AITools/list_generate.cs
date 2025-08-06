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
        private const string ListJsonSchema = "['item1', 'item2', 'item3', ...]";

        /// <summary>
        /// Get all tools provided by this class.
        /// </summary>
        /// <returns>An enumerable collection of AI tools provided by this class.</returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: "list_generate",
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
                execute: this.GenerateListToolWrapper,
                requiredCapabilities: AICapability.TextInput | AICapability.JsonOutput);
        }

        /// <summary>
        /// Generates a list of text items using AI, returning a JSON array of strings.
        /// Uses conversational approach to ensure the target count is met.
        /// </summary>
        private static async Task<AIReturn<List<string>>> GenerateTextListAsync(
            GH_String prompt,
            int count,
            Func<List<KeyValuePair<string, string>>, Task<AIResponse>> getResponse)
        {
            try
            {
                var allItems = new List<string>();
                var messages = new List<KeyValuePair<string, string>>
                {
                    new("system", $"You are a list generator assistant. Generate {count} items of text based on the prompt and return ONLY the JSON array. Include no extra text or formatting. Do not wrap the output in quotes or in a code block.\n\nIMPORTANT: Each item must be a quoted string in the JSON array, even if it contains commas or special characters.\n\nOUTPUT EXAMPLES: ['item1', 'item2', 'item3'] or ['{{1,0,0}}', '{{0.707,0.707,0}}', '{{0,1,0}}']"),
                    new("user", prompt.Value),
                };

                const int maxIterations = 10; // Prevent infinite loops
                int iteration = 0;
                AIResponse? lastResponse = null;

                while (allItems.Count < count && iteration < maxIterations)
                {
                    iteration++;
                    Debug.WriteLine($"[ListTools] Iteration {iteration}: Need {count - allItems.Count} more items (have {allItems.Count}/{count})");

                    // Call AI to generate list
                    var response = await getResponse(messages).ConfigureAwait(false);
                    lastResponse = response;

                    if (response.FinishReason == "error")
                    {
                        return AIReturn<List<string>>.CreateError(
                            response.Response,
                            response);
                    }

                    // Strip thinking tags from response before parsing
                    var cleanedResponse = AI.StripThinkTags(response.Response);

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
                            return AIReturn<List<string>>.CreateSuccess(lastResponse, allItems);
                        }

                        // Otherwise, return the error
                        return AIReturn<List<string>>.CreateError($"Error parsing AI response: {parseEx.Message}", response);
                    }

                    Debug.WriteLine($"[ListTools] Iteration {iteration} generated {newItems.Count} items: {string.Join(", ", newItems)}");

                    // Add new items to our collection
                    foreach (var item in newItems)
                    {
                        allItems.Add(item);
                    }

                    // If we have enough items, trim to exact count and break
                    if (allItems.Count >= count)
                    {
                        if (allItems.Count > count)
                        {
                            allItems = allItems.GetRange(0, count);
                        }

                        Debug.WriteLine($"[ListTools] Target count {count} reached after {iteration} iterations");
                        break;
                    }

                    // Add the AI's response to conversation history
                    messages.Add(new("assistant", cleanedResponse));

                    // Calculate how many more items we need
                    int stillNeeded = count - allItems.Count;

                    // Create follow-up message with context
                    var followUpMessage = $"I need {stillNeeded} more items to complete the list. Please generate {stillNeeded} additional items that are different from the ones already provided. Current list has {allItems.Count} items: [{string.Join(", ", allItems.Select(item => $"'{item}'"))}].\n\nGenerate {stillNeeded} NEW items as a JSON array, meeting the initial user's request: {prompt}.";

                    messages.Add(new("user", followUpMessage));

                    Debug.WriteLine($"[ListTools] Requesting {stillNeeded} more items in next iteration");
                }

                if (allItems.Count == 0)
                {
                    return AIReturn<List<string>>.CreateError(
                        "AI failed to generate any valid items",
                        lastResponse);
                }

                // Final safety check: trim list if it's longer than requested
                if (allItems.Count > count)
                {
                    Debug.WriteLine($"[ListTools] Trimming final list from {allItems.Count} to {count} items");
                    allItems = allItems.GetRange(0, count);
                }

                Debug.WriteLine($"[ListTools] Final result: {allItems.Count} items generated: {string.Join(", ", allItems)}");
                return AIReturn<List<string>>.CreateSuccess(lastResponse, allItems);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ListTools] Error in GenerateTextListAsync: {ex.Message}");
                return AIReturn<List<string>>.CreateError($"Error generating list: {ex.Message}");
            }
        }

        /// <summary>
        /// Tool wrapper for list_generate.
        /// </summary>
        private async Task<object> GenerateListToolWrapper(JObject parameters)
        {
            try
            {
                Debug.WriteLine("[ListTools] Running GenerateListToolWrapper");

                // Extract parameters
                string providerName = parameters["provider"]?.ToString() ?? string.Empty;
                string modelName = parameters["model"]?.ToString() ?? string.Empty;
                string endpoint = "list_generate";
                string prompt = parameters["prompt"]?.ToString() ?? string.Empty;
                int count = parameters["count"]?.ToObject<int>() ?? 0;
                string type = parameters["type"]?.ToString() ?? string.Empty;
                string? contextProviderFilter = parameters["contextProviderFilter"]?.ToString() ?? string.Empty;
                string? contextKeyFilter = parameters["contextKeyFilter"]?.ToString() ?? string.Empty;

                if (string.IsNullOrEmpty(prompt) || count <= 0 || string.IsNullOrEmpty(type))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "Missing or invalid parameters: prompt, count, or type",
                    };
                }

                if (!type.Equals("text", StringComparison.OrdinalIgnoreCase))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"Type '{type}' not supported",
                    };
                }

                var result = await GenerateTextListAsync(
                    new GH_String(prompt),
                    count,
                    messages => AIUtils.GetResponse(
                        providerName,
                        modelName,
                        messages,
                        jsonSchema: ListJsonSchema,
                        endpoint: endpoint,
                        contextProviderFilter: contextProviderFilter,
                        contextKeyFilter: contextKeyFilter)).ConfigureAwait(false);

                // Return standardized result
                var mapping = new Dictionary<string, string>
                {
                    ["success"] = "Success",
                    ["list"] = "Result", // Map Result to list
                    ["error"] = "ErrorMessage",
                };

                var jobj = result.ToJObject<List<string>>(mapping);

                // Add a count field
                jobj["count"] = new JValue(result.Success ? result.Result.Count : 0);

                return jobj;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ListTools] Error in GenerateListToolWrapper: {ex.Message}");
                return new JObject
                {
                    ["success"] = false,
                    ["error"] = $"Error: {ex.Message}",
                };
            }
        }
    }
}
