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
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Grasshopper;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Tool provider for Grasshopper component retrieval via AI Tool Manager.
    /// </summary>
    public class gh_list_categories : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "gh_list_categories";
        /// <summary>
        /// Returns a list of AI tools provided by this plugin.
        /// </summary>
        /// <returns>Collection of AI tools.</returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "List all available categories and subcategories for components in the current environment with optional soft string filter. Use filters wisely to target the results and avoid wasting tokens.",
                category: "ComponentsRetrieval",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""filter"": {
                            ""type"": ""string"",
                            ""description"": ""Soft filter: return categories or subcategories containing the search tokens (split by space).""
                        },
                        ""includeSubcategories"": {
                            ""type"": ""boolean"",
                            ""description"": ""Whether to include subcategories in the response. When false, only returns category names. Defaults to false."",
                            ""default"": false
                        }
                    }
                }",
                execute: this.GhCategoriesToolAsync);
        }

        /// <summary>
        /// Executes the Grasshopper categories listing tool with optional soft string filter.
        /// </summary>
        private Task<AIReturn> GhCategoriesToolAsync(AIToolCall toolCall)
        {
            // Prepare the output
            var output = new AIReturn()
            {
                Request = toolCall,
            };

            try
            {
                // Extract parameters
                AIInteractionToolCall toolInfo = toolCall.GetToolCall();;
                var server = Instances.ComponentServer;
                var filterString = toolInfo.Arguments["filter"]?.ToObject<string>() ?? string.Empty;
                var includeSubcategories = toolInfo.Arguments["includeSubcategories"]?.ToObject<bool>() ?? false;
                var tokens = Regex.Replace(filterString, @"[,;\-_]", " ")
                    .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.ToLowerInvariant())
                    .ToList();

                var proxies = server.ObjectProxies;
                var dict = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in proxies)
                {
                    var cat = p.Desc.Category ?? string.Empty;
                    var sub = p.Desc.SubCategory ?? string.Empty;
                    if (!dict.TryGetValue(cat, out var subs))
                    {
                        subs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        dict[cat] = subs;
                    }

                    if (!string.IsNullOrEmpty(sub))
                    {
                        subs.Add(sub);
                    }
                }

                var result = new List<object>();
                foreach (var kv in dict)
                {
                    var cat = kv.Key;
                    var subs = kv.Value;

                    // Skip if filtering and no matches
                    if (tokens.Any())
                    {
                        bool catMatch = tokens.Any(tok => cat.ToLowerInvariant().Contains(tok));
                        var matchingSubs = subs.Where(s => tokens.Any(tok => s.ToLowerInvariant().Contains(tok))).ToList();

                        if (!catMatch && !matchingSubs.Any())
                        {
                            continue;
                        }

                        // Use filtered subcategories if includeSubcategories is true
                        if (includeSubcategories)
                        {
                            var filteredSubs = catMatch ? subs.ToList() : matchingSubs;
                            result.Add(new { category = cat, subCategories = filteredSubs });
                        }
                        else
                        {
                            result.Add(new { category = cat });
                        }
                    }
                    else
                    {
                        // No filter applied
                        if (includeSubcategories)
                        {
                            result.Add(new { category = cat, subCategories = subs.ToList() });
                        }
                        else
                        {
                            result.Add(new { category = cat });
                        }
                    }
                }

                var toolResult = new JObject
                {
                    ["count"] = result.Count,
                    ["categories"] = JArray.FromObject(result),
                };

                var toolBody = new AIBody();
                toolBody.AddInteractionToolResult(toolResult);

                output.CreateSuccess(toolBody);
                return Task.FromResult(output);
            }
            catch (Exception ex)
            {
                output.CreateError($"Error: {ex.Message}");
                return Task.FromResult(output);
            }
        }
    }
}

