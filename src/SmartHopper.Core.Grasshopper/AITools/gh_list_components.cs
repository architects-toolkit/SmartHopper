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
using System.Threading.Tasks;
using Grasshopper;
using Grasshopper.Kernel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Utils;
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
    public class gh_list_components : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "gh_list_components";
        /// <summary>
        /// Returns a list of AI tools provided by this plugin.
        /// </summary>
        /// <returns>Collection of AI tools.</returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Retrieve a list of all installed components in this current environment. Returns a JSON dictionary with names, GUIDs, categories, subcategories, descriptions, keywords, inputs and outputs. Use filters wisely to target the results and avoid wasting tokens.",
                category: "ComponentsRetrieval",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""categoryFilter"": {
                            ""type"": ""array"",
                            ""items"": { ""type"": ""string"" },
                            ""description"": ""Optionally filter components by category. '+' includes, '-' excludes. Most common categories: Params, Maths, Vector, Curve, Surface, Mesh, Intersect, Transform, Sets, Display, Rhino, Kangaroo. E.g. ['+Maths','-Params']. (note: use the tool 'gh_categories' to get the full list of available categories)""
                        },
                        ""nameFilter"": {
                            ""type"": ""string"",
                            ""description"": ""Partial name matching filter. Returns components whose name or nickname contains this text (case-insensitive)""
                        },
                        ""includeDetails"": {
                            ""type"": ""array"",
                            ""items"": { ""type"": ""string"" },
                            ""enum"": [""name"", ""nickname"", ""category"", ""subCategory"", ""guid"", ""description"", ""keywords"", ""inputs"", ""outputs""],
                            ""description"": ""Select which component details to include in response. If not specified, returns all details. Recommended 'name', 'description', 'inputs' and 'outputs' to avoid token overload.""
                        },
                        ""maxResults"": {
                            ""type"": ""integer"",
                            ""description"": ""Maximum number of components to return. Defaults to 100 to prevent token overload."",
                            ""default"": 100
                        }
                    }
                }",
                execute: this.GhRetrieveToolAsync
            );
        }

        /// <summary>
        /// Executes the Grasshopper list component types tool.
        /// </summary>
        private Task<AIReturn> GhRetrieveToolAsync(AIToolCall toolCall)
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
                var categoryFilters = toolInfo.Arguments["categoryFilter"]?.ToObject<List<string>>() ?? new List<string>();
                var nameFilter = toolInfo.Arguments["nameFilter"]?.ToString() ?? string.Empty;
                var includeDetails = toolInfo.Arguments["includeDetails"]?.ToObject<List<string>>() ?? new List<string>();
                var maxResults = toolInfo.Arguments["maxResults"]?.ToObject<int>() ?? 100;
                var (includeCats, excludeCats) = Get.ParseIncludeExclude(categoryFilters, Get.CategorySynonyms);

                // Retrieve all component proxies in one call
                var proxies = server.ObjectProxies.ToList();

                // Apply include filters
                if (includeCats.Any())
                {
                    proxies = proxies.Where(p => p.Desc.Category != null && includeCats.Contains(p.Desc.Category.ToUpperInvariant())).ToList();
                }

                // Apply exclude filters
                if (excludeCats.Any())
                {
                    proxies = proxies.Where(p => p.Desc.Category == null || !excludeCats.Contains(p.Desc.Category.ToUpperInvariant())).ToList();
                }

                // Apply name filter if provided
                if (!string.IsNullOrEmpty(nameFilter))
                {
                    var filterLower = nameFilter.ToLowerInvariant();
                    proxies = proxies.Where(p =>
                        p.Desc.Name.ToLowerInvariant().Contains(filterLower) ||
                        p.Desc.NickName.ToLowerInvariant().Contains(filterLower)
                    ).ToList();
                }

                // Apply max results limit
                if (maxResults > 0)
                {
                    proxies = proxies.Take(maxResults).ToList();
                }

                var list = proxies.Select(p =>
                {
                    var instance = GHObjectFactory.CreateInstance(p);
                    List<object> inputs;
                    List<object> outputs;
                    if (instance is IGH_Component comp)
                    {
                        inputs = GHParameterUtils.GetAllInputs(comp)
                            .Select(param => new
                            {
                                name = param.Name,
                                description = param.Description,
                                dataType = param.GetType().Name,
                                // access = param.Access.ToString(),
                            })
                            .Cast<object>()
                            .ToList();
                        outputs = GHParameterUtils.GetAllOutputs(comp)
                            .Select(param => new
                            {
                                name = param.Name,
                                description = param.Description,
                                dataType = param.GetType().Name,
                                // access = param.Access.ToString(),
                            })
                            .Cast<object>()
                            .ToList();
                    }
                    else if (instance is IGH_Param param)
                    {
                        inputs = new List<object>();
                        outputs = new List<object>
                        {
                            new
                            {
                                name = param.Name,
                                description = param.Description,
                                dataType = param.GetType().Name,
                                // access = param.Access.ToString(),
                            },
                        };
                    }
                    else
                    {
                        inputs = new List<object>();
                        outputs = new List<object>();
                    }

                    // Build component object based on includeDetails selection
                    var componentData = new Dictionary<string, object>();

                    if (includeDetails.Count == 0 || includeDetails.Contains("name"))
                        componentData["name"] = p.Desc.Name;
                    if (includeDetails.Count == 0 || includeDetails.Contains("nickname"))
                        componentData["nickname"] = p.Desc.NickName;
                    if (includeDetails.Count == 0 || includeDetails.Contains("category"))
                        componentData["category"] = p.Desc.Category;
                    if (includeDetails.Count == 0 || includeDetails.Contains("subCategory"))
                        componentData["subCategory"] = p.Desc.SubCategory;
                    if (includeDetails.Count == 0 || includeDetails.Contains("guid"))
                        componentData["guid"] = p.Guid.ToString();
                    if (includeDetails.Count == 0 || includeDetails.Contains("description"))
                        componentData["description"] = p.Desc.Description;
                    if (includeDetails.Count == 0 || includeDetails.Contains("keywords"))
                        componentData["keywords"] = p.Desc.Keywords;
                    if (includeDetails.Count == 0 || includeDetails.Contains("inputs"))
                        componentData["inputs"] = inputs;
                    if (includeDetails.Count == 0 || includeDetails.Contains("outputs"))
                        componentData["outputs"] = outputs;

                    return componentData;

                }).ToList();
                var names = list.Where(x => x.ContainsKey("name")).Select(x => x["name"].ToString()).Distinct().ToList();
                var guids = list.Where(x => x.ContainsKey("guid")).Select(x => x["guid"].ToString()).Distinct().ToList();
                var json = JsonConvert.SerializeObject(list, Formatting.None);
                var toolResult = new JObject
                {
                    ["count"] = list.Count,
                    ["names"] = JArray.FromObject(names),
                    ["guids"] = JArray.FromObject(guids),
                    ["json"] = json,
                };

                // Attach non-breaking result envelope
                toolResult.WithEnvelope(
                    ToolResultEnvelope.Create(
                        tool: this.toolName,
                        type: ToolResultContentType.Object,
                        payloadPath: "json"));

                var body = AIBodyBuilder.Create()
                    .AddToolResult(toolResult)
                    .Build();

                output.CreateSuccess(body, toolCall);
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
