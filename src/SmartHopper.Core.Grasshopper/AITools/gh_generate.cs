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
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Messaging;
using SmartHopper.Infrastructure.Interfaces;
using SmartHopper.Infrastructure.Models;
using SmartHopper.Infrastructure.Managers.AITools;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Tool provider for generating Grasshopper definitions from natural language prompts.
    /// Orchestrates multiple steps to select components and generate GhJSON.
    /// </summary>
    public class gh_generate : IAIToolProvider
    {

        /// <summary>
        /// Returns the list of AI tools provided by this class.
        /// </summary>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: "gh_generate",
                description: "Generate and place Grasshopper components on the canvas based on a natural language description of what you want to create.",
                category: "Components",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""prompt"": {
                            ""type"": ""string"",
                            ""description"": ""Natural language description of what you want to create with Grasshopper components""
                        }
                    },
                    ""required"": [""prompt""]
                }",
                execute: this.GhGenerateToolAsync
            );
        }

        /// <summary>
        /// Executes the gh_generate tool: orchestrates the complete workflow from prompt to placed components.
        /// Following the exact 5-step approach with multiple targeted AI calls.
        /// </summary>
        private async Task<object> GhGenerateToolAsync(JObject parameters)
        {
            try
            {
                // Step 1: User prompt received
                var userPrompt = parameters["prompt"]?.ToString();
                if (string.IsNullOrWhiteSpace(userPrompt))
                {
                    return new { success = false, error = "Prompt is required" };
                }

                var providerName = parameters["provider"]?.ToString() ?? string.Empty;
                var modelName = parameters["model"]?.ToString() ?? string.Empty;
                var contextProviderFilter = parameters["contextProviderFilter"]?.ToString() ?? string.Empty;
                var contextKeyFilter = parameters["contextKeyFilter"]?.ToString() ?? string.Empty;

                Debug.WriteLine($"[gh_generate] Starting generation process for prompt: {userPrompt}");

                // Step 2: Get available categories using gh_list_categories AITool
                Debug.WriteLine($"[gh_generate] Step 2: Getting available categories");
                var categoriesResult = await GetAvailableCategoriesAsync();
                if (!categoriesResult.success)
                {
                    return new { success = false, error = "Failed to retrieve categories", details = categoriesResult.error };
                }

                // Step 3: Use AI to select relevant categories based on user prompt
                Debug.WriteLine($"[gh_generate] Step 3: Selecting relevant categories with AI");
                var selectedCategories = await SelectRelevantCategoriesWithAIAsync(userPrompt, categoriesResult.categories, 
                    providerName, modelName, contextProviderFilter, contextKeyFilter);

                if (selectedCategories == null || !selectedCategories.Any())
                {
                    return new { success = false, error = "No relevant categories found for the given prompt" };
                }

                Debug.WriteLine($"[gh_generate] Selected categories: {string.Join(", ", selectedCategories)}");

                // Step 4: Get components from selected categories using gh_list_components AITool
                Debug.WriteLine($"[gh_generate] Step 4: Getting components from selected categories");
                var componentsResult = await GetComponentsFromCategoriesAsync(selectedCategories);
                if (!componentsResult.success)
                {
                    return new { success = false, error = "Failed to retrieve components", details = componentsResult.error };
                }

                // Step 5: Use AI to generate GhJSON script based on user prompt and available components
                Debug.WriteLine($"[gh_generate] Step 5: Generating GhJSON script with AI");
                var ghJsonResult = await GenerateGhJsonWithAIAsync(userPrompt, componentsResult.components, 
                    providerName, modelName, contextProviderFilter, contextKeyFilter);

                if (!ghJsonResult.success)
                {
                    return new { success = false, error = "Failed to generate GhJSON", details = ghJsonResult.error };
                }

                // Step 6: Place components on canvas using gh_put AITool
                Debug.WriteLine($"[gh_generate] Step 6: Placing components on canvas");
                var putResult = await PlaceComponentsOnCanvasAsync(ghJsonResult.ghjson);
                if (!putResult.success)
                {
                    return new { success = false, error = "Failed to place components on canvas", details = putResult.error };
                }

                return new 
                { 
                    success = true, 
                    message = "Successfully generated and placed Grasshopper components",
                    selectedCategories = selectedCategories,
                    componentsPlaced = putResult.componentsPlaced,
                    analysis = putResult.analysis
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[gh_generate] Error: {ex.Message}");
                return new { success = false, error = ex.Message };
            }
        }

        /// <summary>
        /// Step 2: Get all available categories using gh_list_categories AITool.
        /// </summary>
        private async Task<(bool success, string categories, string error)> GetAvailableCategoriesAsync()
        {
            try
            {
                var result = await AIToolManager.ExecuteTool(
                    "gh_list_categories", 
                    new JObject(), 
                    new JObject());

                if (result != null)
                {
                    return (true, result.ToString(), null);
                }

                return (false, null, "No result from gh_list_categories");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[gh_generate] Error getting categories: {ex.Message}");
                return (false, null, ex.Message);
            }
        }

        /// <summary>
        /// Step 3: Use AI to select relevant categories based on user prompt.
        /// </summary>
        private async Task<List<string>> SelectRelevantCategoriesWithAIAsync(
            string userPrompt, string availableCategories, 
            string providerName, string modelName, 
            string contextProviderFilter, string contextKeyFilter)
        {
            try
            {
                var systemPrompt = @"You are an expert in Grasshopper 3D parametric design. Given a user's request and a list of available component categories, select the most relevant categories that would be needed to fulfill the request.

Your task:
1. Analyze the user's request to understand what they want to create
2. From the provided categories, select only the ones that contain components likely needed for this task
3. Be selective - only choose categories that are directly relevant
4. Return your response as a JSON array of category names

Example format: [""Maths"", ""Vector"", ""Curve""]";

                var userMessage = $@"User Request: {userPrompt}

Available Categories:
{availableCategories}

Please select the most relevant categories for this request and return them as a JSON array.";

                var messages = new List<KeyValuePair<string, string>>
                {
                    new("system", systemPrompt),
                    new("user", userMessage)
                };

                var aiResponse = await AIUtils.GetResponse(
                    providerName,
                    modelName,
                    messages,
                    jsonSchema: "",
                    endpoint: "gh_generate_categories",
                    contextProviderFilter: contextProviderFilter,
                    contextKeyFilter: contextKeyFilter).ConfigureAwait(false);
                    
                var content = aiResponse.Response?.Trim();
                if (string.IsNullOrEmpty(content))
                {
                    return new List<string>();
                }

                // Try to extract JSON array from response
                var startIndex = content.IndexOf('[');
                var endIndex = content.LastIndexOf(']');
                
                if (startIndex >= 0 && endIndex > startIndex)
                {
                    var jsonArray = content.Substring(startIndex, endIndex - startIndex + 1);
                    var categories = JArray.Parse(jsonArray).ToObject<List<string>>();
                    return categories ?? new List<string>();
                }

                return new List<string>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[gh_generate] Error selecting categories: {ex.Message}");
                return new List<string>();
            }
        }

        /// <summary>
        /// Step 4: Get components from selected categories using gh_list_components AITool.
        /// </summary>
        private async Task<(bool success, string components, string error)> GetComponentsFromCategoriesAsync(List<string> categories)
        {
            try
            {
                // Format categories for the filter (with + prefix to include)
                var categoryFilter = categories.Select(c => $"+{c}").ToArray();
                
                var parameters = new JObject
                {
                    ["categoryFilter"] = JArray.FromObject(categoryFilter)
                };

                var result = await AIToolManager.ExecuteTool(
                    "gh_list_components", 
                    parameters, 
                    new JObject());
                    
                if (result != null)
                {
                    return (true, result.ToString(), null);
                }
                return (false, null, "No result from gh_list_components");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[gh_generate] Error getting components: {ex.Message}");
                return (false, null, ex.Message);
            }
        }

        /// <summary>
        /// Step 5: Use AI to generate GhJSON script based on user prompt and available components.
        /// </summary>
        private async Task<(bool success, string ghjson, string error)> GenerateGhJsonWithAIAsync(
            string userPrompt, string availableComponents,
            string providerName, string modelName, 
            string contextProviderFilter, string contextKeyFilter)
        {
            try
            {
                var systemPrompt = @"You are an expert in Grasshopper 3D parametric design and GhJSON format. Given a user's request and available components, create a GhJSON script that fulfills their request.

Key requirements:
1. Use only the components provided in the available components list
2. Create a valid GhJSON format with proper structure
3. Position components logically on the canvas (use reasonable X, Y coordinates)
4. Wire components together appropriately to create the desired functionality
5. Include proper GUIDs for each component from the provided list
6. Set appropriate parameter values where needed

GhJSON structure:
- Must have a 'components' array
- Each component needs: 'guid', 'name', 'nickname', 'x', 'y', 'inputs', 'outputs'
- Connections are defined in the inputs section with 'source_id' and 'source_output'

Return only the GhJSON - no explanations or markdown formatting.";

                var userMessage = $@"User Request: {userPrompt}

Available Components:
{availableComponents}

Please create a GhJSON script that fulfills this request using only the available components.";

                var messages = new List<KeyValuePair<string, string>>
                {
                    new("system", systemPrompt),
                    new("user", userMessage)
                };

                var aiResponse = await AIUtils.GetResponse(
                    providerName,
                    modelName,
                    messages,
                    jsonSchema: "",
                    endpoint: "gh_generate_ghjson",
                    contextProviderFilter: contextProviderFilter,
                    contextKeyFilter: contextKeyFilter).ConfigureAwait(false);
                    
                var content = aiResponse.Response?.Trim();
                if (string.IsNullOrEmpty(content))
                {
                    return (false, null, "Empty response from AI");
                }

                // Try to extract JSON from response (in case it's wrapped in markdown)
                var startIndex = content.IndexOf('{');
                var lastIndex = content.LastIndexOf('}');
                
                if (startIndex >= 0 && lastIndex > startIndex)
                {
                    var ghjson = content.Substring(startIndex, lastIndex - startIndex + 1);
                    return (true, ghjson, null);
                }

                return (true, content, null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[gh_generate] Error generating GhJSON: {ex.Message}");
                return (false, null, ex.Message);
            }
        }

        /// <summary>
        /// Step 6: Place components on canvas using gh_put AITool.
        /// </summary>
        private async Task<(bool success, int componentsPlaced, string analysis, string error)> PlaceComponentsOnCanvasAsync(string ghjson)
        {
            try
            {
                var parameters = new JObject
                {
                    ["json"] = ghjson
                };

                var result = await AIToolManager.ExecuteTool(
                    "gh_put", 
                    parameters, 
                    new JObject()
                );

                if (result != null)
                {
                    var resultObj = JObject.Parse(result.ToString());
                    var success = resultObj["success"]?.Value<bool>() ?? false;
                    var components = resultObj["components"]?.Value<int>() ?? 0;
                    var analysis = resultObj["analysis"]?.Value<string>() ?? "";

                    if (success)
                    {
                        return (true, components, analysis, null);
                    }
                    else
                    {
                        return (false, 0, analysis, analysis);
                    }
                }
                return (false, 0, null, "No result from gh_put");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[gh_generate] Error placing components: {ex.Message}");
                return (false, 0, null, ex.Message);
            }
        }
    }
}
