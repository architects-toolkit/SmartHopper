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
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Models;
using SmartHopper.Core.Grasshopper.Utils;
using SmartHopper.Core.Messaging;
using SmartHopper.Infrastructure.Interfaces;
using SmartHopper.Infrastructure.Models;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Contains tools for list generation using AI.
    /// </summary>
    public class list_generate : IAIToolProvider
    {
        private const string ListJsonSchema = "{\"type\":\"array\",\"items\":{\"type\":\"string\"}}";

        /// <summary>
        /// Get all tools provided by this class.
        /// </summary>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: "list_generate",
                description: "Generates a list of items based on a prompt, count and type",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""prompt"": { ""type"": ""string"", ""description"": ""The prompt to generate items from"" },
                        ""count"": { ""type"": ""integer"", ""description"": ""Number of items to generate"" },
                        ""type"": { ""type"": ""string"", ""description"": ""Type of items (e.g. 'text', 'number', 'integer', 'boolean')"", ""enum"": [""text"", ""number"", ""integer"", ""boolean""] }
                    },
                    ""required"": [""prompt"", ""count"", ""type""]
                }",
                execute: this.GenerateListToolWrapper);
        }

        /// <summary>
        /// Generates a list of text items using AI, returning a JSON array of strings.
        /// </summary>
        private static async Task<AIEvaluationResult<List<string>>> GenerateTextListAsync(
            GH_String prompt,
            int count,
            Func<List<KeyValuePair<string, string>>, Task<AIResponse>> getResponse)
        {
            try
            {
                var messages = new List<KeyValuePair<string, string>>
                {
                    new("system", $"You are a list generator assistant. Generate {count} items of text based on the prompt and return ONLY a valid JSON array of strings matching this schema: {ListJsonSchema}. Include no extra text or formatting."),
                    new("user", prompt.Value)
                };

                // Call AI to generate list
                var response = await getResponse(messages).ConfigureAwait(false);

                if (response.FinishReason == "error")
                {
                    return AIEvaluationResult<List<string>>.CreateError(
                        response.Response,
                        GH_RuntimeMessageLevel.Error,
                        response);
                }

                // Parse JSON array of strings
                var items = ParsingTools.ParseStringArrayFromResponse(response.Response);
                Debug.WriteLine($"[ListTools] Generated items: {string.Join(", ", items)}");

                return AIEvaluationResult<List<string>>.CreateSuccess(response, items);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ListTools] Error in GenerateTextListAsync: {ex.Message}");
                return AIEvaluationResult<List<string>>.CreateError(
                    $"Error generating list: {ex.Message}",
                    GH_RuntimeMessageLevel.Error);
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

                if (string.IsNullOrEmpty(prompt) || count <= 0 || string.IsNullOrEmpty(type))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = "Missing or invalid parameters: prompt, count, or type"
                    };
                }

                if (!type.Equals("text", StringComparison.OrdinalIgnoreCase))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"Type '{type}' not supported"
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
                        endpoint: endpoint)
                ).ConfigureAwait(false);

                // Build standardized result
                return new JObject
                {
                    ["success"] = result.Success,
                    ["list"] = result.Success ? JArray.FromObject(result.Result) : JValue.CreateNull(),
                    ["count"] = result.Success ? new JValue(result.Result.Count) : new JValue(0),
                    ["error"] = result.Success ? JValue.CreateNull() : new JValue(result.ErrorMessage),
                    ["rawResponse"] = JToken.FromObject(result.Response)
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ListTools] Error in GenerateListToolWrapper: {ex.Message}");
                return new JObject
                {
                    ["success"] = false,
                    ["error"] = $"Error: {ex.Message}"
                };
            }
        }
    }
}
