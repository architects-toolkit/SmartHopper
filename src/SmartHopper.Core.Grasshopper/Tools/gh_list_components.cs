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
using Grasshopper.Kernel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.Interfaces;
using SmartHopper.Infrastructure.Models;
using SmartHopper.Core.Grasshopper.Graph;
using SmartHopper.Core.Grasshopper.Utils;

namespace SmartHopper.Core.Grasshopper.Tools
{
    /// <summary>
    /// Tool provider for Grasshopper component retrieval via AI Tool Manager.
    /// </summary>
    public class gh_list_components : IAIToolProvider
    {
        /// <summary>
        /// Returns a list of AI tools provided by this plugin.
        /// </summary>
        /// <returns>Collection of AI tools.</returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: "gh_list_components",
                description: "Retrieve a list of all installed components in this current environment. Returns a JSON dictionary with names, GUIDs, categories, subcategories, descriptions, and keywords.",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""categoryFilter"": {
                            ""type"": ""array"",
                            ""items"": { ""type"": ""string"" },
                            ""description"": ""Optionally filter components by category. '+' includes, '-' excludes. Most common categories: Params, Maths, Vector, Curve, Surface, Mesh, Intersect, Transform, Sets, Display, Rhino, Kangaroo. E.g. ['+Maths','-Params']. (note: use the tool 'gh_categories' to get the full list of available categories)""
                        }
                    }
                }",
                execute: this.GhRetrieveToolAsync
            );
        }

        /// <summary>
        /// Executes the Grasshopper list component types tool.
        /// </summary>
        private Task<object> GhRetrieveToolAsync(JObject parameters)
        {
            var server = Instances.ComponentServer;
            var categoryFilters = parameters["categoryFilter"]?.ToObject<List<string>>() ?? new List<string>();
            var (includeCats, excludeCats) = GhGetTools.ParseIncludeExclude(categoryFilters, GhGetTools.CategorySynonyms);

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
                            access = param.Access.ToString(),
                        })
                        .Cast<object>()
                        .ToList();
                    outputs = GHParameterUtils.GetAllOutputs(comp)
                        .Select(param => new
                        {
                            name = param.Name,
                            description = param.Description,
                            dataType = param.GetType().Name,
                            access = param.Access.ToString(),
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
                            access = param.Access.ToString(),
                        },
                    };
                }
                else
                {
                    inputs = new List<object>();
                    outputs = new List<object>();
                }

                return new
                {
                    name = p.Desc.Name,
                    nickname = p.Desc.NickName,
                    category = p.Desc.Category,
                    subCategory = p.Desc.SubCategory,
                    guid = p.Guid.ToString(),
                    description = p.Desc.Description,
                    keywords = p.Desc.Keywords,
                    inputs,
                    outputs,
                };
            }).ToList();
            var names = list.Select(x => x.name).Distinct().ToList();
            var guids = list.Select(x => x.guid).Distinct().ToList();
            var json = JsonConvert.SerializeObject(list, Formatting.None);
            var result = new JObject
            {
                ["count"] = list.Count,
                ["names"] = JArray.FromObject(names),
                ["guids"] = JArray.FromObject(guids),
                ["json"] = json,
            };
            return Task.FromResult<object>(result);
        }
    }
}
