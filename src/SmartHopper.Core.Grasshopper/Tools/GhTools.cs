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
using System.Diagnostics;
using Grasshopper;
using Grasshopper.Kernel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartHopper.Config.Interfaces;
using SmartHopper.Config.Models;
using SmartHopper.Core.Graph;
using SmartHopper.Core.Grasshopper.Utils;
using System.Text.RegularExpressions;

namespace SmartHopper.Core.Grasshopper.Tools
{

    /// <summary>
    /// Tool provider for Grasshopper component retrieval via AI Tool Manager.
    /// </summary>
    public class GhTools : IAIToolProvider
    {
        #region Synonyms
        /// <summary>
        /// Synonyms for filter tags.
        /// Available filter tokens:
        ///   selected/unselected: component selection on canvas
        ///   enabled/disabled: whether the component can run (enabled = unlocked)
        ///   error/warning/remark: runtime message levels
        ///   previewcapable/notpreviewcapable: supports geometry preview
        ///   previewon/previewoff: current preview toggle.
        /// Synonyms:
        ///   locked → disabled
        ///   unlocked → enabled
        ///   remarks/info → remark
        ///   warn/warnings → warning
        ///   errors → error
        ///   visible → previewon
        ///   hidden → previewoff
        /// </summary>
        private static readonly Dictionary<string, string> FilterSynonyms = new Dictionary<string, string>
        {
            { "LOCKED", "DISABLED" },
            { "UNLOCKED", "ENABLED" },
            { "REMARKS", "REMARK" },
            { "INFO", "REMARK" },
            { "WARN", "WARNING" },
            { "WARNINGS", "WARNING" },
            { "ERRORS", "ERROR" },
            { "VISIBLE", "PREVIEWON" },
            { "HIDDEN", "PREVIEWOFF" },
        };

        /// <summary>
        /// Synonyms for typeFilter tokens.
        /// Available typeFilter tokens:
        ///   params: only parameter objects (IGH_Param)
        ///   components: only component objects (GH_Component)
        ///   input: components with no incoming connections (inputs only)
        ///   output: components with no outgoing connections (outputs only)
        ///   processing: components with both incoming and outgoing connections
        ///   isolated: components with neither incoming nor outgoing connections (isolated)
        /// Synonyms:
        ///   param, parameter → params
        ///   component, comp → components
        ///   input, inputs → input
        ///   output, outputs → output
        ///   processingcomponents, intermediate, middle, middlecomponents → processing
        ///   isolatedcomponents → isolated
        /// </summary>
        private static readonly Dictionary<string, string> TypeSynonyms = new Dictionary<string, string>
        {
            { "PARAM", "PARAMS" },
            { "PARAMETER", "PARAMS" },
            { "COMPONENT", "COMPONENTS" },
            { "COMP", "COMPONENTS" },
            { "INPUTS", "INPUT" },
            { "INPUTCOMPONENTS", "INPUT" },
            { "OUTPUTS", "OUTPUT" },
            { "OUTPUTCOMPONENTS", "OUTPUT" },
            { "PROCESSINGCOMPONENTS", "PROCESSING" },
            { "INTERMEDIATE", "PROCESSING" },
            { "MIDDLE", "PROCESSING" },
            { "MIDDLECOMPONENTS", "PROCESSING" },
            { "ISOLATEDCOMPONENTS", "ISOLATED" },
        };

        /// <summary>
        /// Synonyms for categoryFilter tokens.
        /// Available Grasshopper component categories (e.g. Params, Maths, Vector, Curve, Surface, Mesh, etc.).
        /// Maps common abbreviations or alternate names to canonical category tokens.
        /// </summary>
        private static readonly Dictionary<string, string> CategorySynonyms = new Dictionary<string, string>
        {
            { "PARAM", "PARAMS" },
            { "PARAMETERS", "PARAMS" },
            { "MATH", "MATHS" },
            { "VEC", "VECTOR" },
            { "VECTORS", "VECTOR" },
            { "CRV", "CURVE" },
            { "CURVES", "CURVE" },
            { "SURF", "SURFACE" },
            { "SURFS", "SURFACE" },
            { "MESHES", "MESH" },
            { "INT", "INTERSECT" },
            { "TRANS", "TRANSFORM" },
            { "TREE", "SETS" },
            { "TREES", "SETS" },
            { "DATA", "SETS" },
            { "DATASETS", "SETS" },
            { "DIS", "DISPLAY" },
            { "DISP", "DISPLAY" },
            { "VISUALIZATION", "DISPLAY" },
            { "RH", "RHINO" },
            { "RHINOCEROS", "RHINO" },
            { "KANGAROOPHYSICS", "KANGAROO" }
        };
        #endregion

        #region HelperMethods
        /// <summary>
        /// Helper to parse include/exclude tokens.
        /// </summary>
        /// <param name="rawGroups">List of raw filter groups.</param>
        /// <param name="synonyms">Dictionary of synonyms for tokens.</param>
        /// <returns>Tuple of include and exclude sets.</returns>
        private static (HashSet<string> Include, HashSet<string> Exclude) ParseIncludeExclude(IEnumerable<string> rawGroups, Dictionary<string, string> synonyms)
        {
            var include = new HashSet<string>();
            var exclude = new HashSet<string>();
            foreach (var rawGroup in rawGroups)
            {
                if (string.IsNullOrWhiteSpace(rawGroup)) continue;
                var parts = rawGroup.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    var tok = part.Trim();
                    if (string.IsNullOrEmpty(tok)) continue;
                    bool inc = !tok.StartsWith("-");
                    var tag = tok.TrimStart('+', '-').ToUpperInvariant();
                    if (synonyms != null && synonyms.TryGetValue(tag, out var mapped))
                        tag = mapped;
                    if (inc) include.Add(tag);
                    else exclude.Add(tag);
                }
            }
            return (include, exclude);
        }
        #endregion

        #region ToolRegistration
        /// <summary>
        /// Returns a list of AI tools provided by this plugin.
        /// </summary>
        /// <returns>Collection of AI tools.</returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: "ghget",
                description: "Retrieve Grasshopper components as GhJSON with optional filters. By default, it returns all components.",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""attrFilters"": {
                            ""type"": ""array"",
                            ""items"": { ""type"": ""string"" },
                            ""description"": ""Optional array of attribute filter tokens. '+' includes, '-' excludes. Defaults to all components. Available tags:\n  selected/unselected: component selection state on canvas;\n  enabled/disabled: whether the component can run (enabled = unlocked);\n  error/warning/remark: runtime message levels;\n  previewcapable/notpreviewcapable: supports geometry preview;\n  previewon/previewoff: current preview toggle.\nSynonyms: locked→disabled, unlocked→enabled, remarks/info→remark, warn/warnings→warning, errors→error, visible→previewon, hidden→previewoff. Examples: '+error' → only components with errors; '+error +warning' → errors OR warnings; '+error -warning' → errors excluding warnings; '+error -previewoff' → errors with preview on; no filter → all components.""
                        },
                        ""typeFilter"": {
                            ""type"": ""array"",
                            ""items"": { ""type"": ""string"" },
                            ""description"": ""Optional array of type tokens with include/exclude syntax. Defaults to all types. Available tokens:\n  params: only parameter objects;\n  components: only component objects;\n  input: components with no incoming connections;\n  output: components with no outgoing connections;\n  processing: components with both incoming and outgoing connections;\n  isolated: components with neither incoming nor outgoing connections.\nExamples: ['+params', '-components'] to include parameters and exclude components.""
                        },
                        ""guidFilter"": {
                            ""type"": ""array"",
                            ""items"": { ""type"": ""string"" },
                            ""description"": ""Optional list of component GUIDs for initial filtering. When provided, only components with these GUIDs are processed. If not provided, all components are processed.""
                        },
                        ""connectionDepth"": {
                            ""type"": ""integer"",
                            ""default"": 0,
                            ""description"": ""Depth of connections to include: 0 (default) only matching components; 1 includes directly connected components; 2 includes two-level connected components, etc.""
                        }
                    }
                }",
                execute: this.ExecuteGhGetToolAsync
            );

            // New tool to list installed component types
            yield return new AITool(
                name: "ghretrievecomponents",
                description: "Retrieve all installed Grasshopper components in the user's environment as JSON with names, GUIDs, categories, subcategories, descriptions, and keywords.",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""categoryFilter"": {
                            ""type"": ""array"",
                            ""items"": { ""type"": ""string"" },
                            ""description"": ""Optionally filter components by category. '+' includes, '-' excludes. Most common categories: Params, Maths, Vector, Curve, Surface, Mesh, Intersect, Transform, Sets, Display, Rhino, Kangaroo. E.g. ['+Maths','-Params']. (note: use the tool 'ghcategories' to get the full list of available categories)""
                        }
                    }
                }",
                execute: this.ExecuteGhRetrieveToolAsync
            );

            // New tool to list Grasshopper categories and subcategories
            yield return new AITool(
                name: "ghcategories",
                description: "List Grasshopper component categories and subcategories with optional soft string filter.",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""filter"": {
                            ""type"": ""string"",
                            ""description"": ""Soft filter: return categories or subcategories containing the search tokens (split by space, ignores , ; - _).""
                        }
                    }
                }",
                execute: this.ExecuteGhCategoriesToolAsync
            );
        }
        #endregion

        #region GhGet
        /// <summary>
        /// Executes the Grasshopper get components tool.
        /// </summary>
        /// <param name="parameters">Parameters object containing filter settings.</param>
        /// <returns>Task that returns the result of the operation.</returns>
        private Task<object> ExecuteGhGetToolAsync(JObject parameters)
        {
            // Parse filters
            var attrFilters = parameters["attrFilters"]?.ToObject<List<string>>() ?? new List<string>();
            var typeFilters = parameters["typeFilter"]?.ToObject<List<string>>() ?? new List<string>();
            var objects = GHCanvasUtils.GetCurrentObjects();

            // Filter by manual UI selection if provided
            var selectedGuids = parameters["guidFilter"]?.ToObject<List<string>>() ?? new List<string>();
            if (selectedGuids.Any())
            {
                var set = new HashSet<string>(selectedGuids);
                objects = objects.Where(o => set.Contains(o.InstanceGuid.ToString())).ToList();
            }
            var connectionDepth = parameters["connectionDepth"]?.ToObject<int>() ?? 0;
            var (includeTypes, excludeTypes) = ParseIncludeExclude(typeFilters, TypeSynonyms);
            var (includeTags, excludeTags) = ParseIncludeExclude(attrFilters, FilterSynonyms);

            // Apply typeFilters on base objects
            var typeFiltered = new List<IGH_ActiveObject>(objects);
            if (includeTypes.Any())
            {
                var tf = new List<IGH_ActiveObject>();
                if (includeTypes.Contains("PARAMS")) tf.AddRange(objects.OfType<IGH_Param>());
                if (includeTypes.Contains("COMPONENTS")) tf.AddRange(objects.OfType<GH_Component>());
                if (includeTypes.Overlaps(new[] { "INPUT", "OUTPUT", "PROCESSING", "ISOLATED" }))
                {
                    var tempDoc = GHDocumentUtils.GetObjectsDetails(objects);
                    var incd = new Dictionary<Guid, int>();
                    var outd = new Dictionary<Guid, int>();
                    foreach (var conn in tempDoc.Connections)
                    {
                        outd[conn.From.ComponentId] = (outd.TryGetValue(conn.From.ComponentId, out var ov) ? ov : 0) + 1;
                        incd[conn.To.ComponentId] = (incd.TryGetValue(conn.To.ComponentId, out var iv) ? iv : 0) + 1;
                    }
                    if (includeTypes.Contains("INPUT")) tf.AddRange(objects.Where(c => !incd.ContainsKey(c.InstanceGuid) && outd.ContainsKey(c.InstanceGuid)));
                    if (includeTypes.Contains("OUTPUT")) tf.AddRange(objects.Where(c => !outd.ContainsKey(c.InstanceGuid) && incd.ContainsKey(c.InstanceGuid)));
                    if (includeTypes.Contains("PROCESSING")) tf.AddRange(objects.Where(c => incd.ContainsKey(c.InstanceGuid) && outd.ContainsKey(c.InstanceGuid)));
                    if (includeTypes.Contains("ISOLATED")) tf.AddRange(objects.Where(c => !incd.ContainsKey(c.InstanceGuid) && !outd.ContainsKey(c.InstanceGuid)));
                }
                typeFiltered = tf.Distinct().ToList();
            }
            if (excludeTypes.Any())
            {
                if (!includeTypes.Any()) typeFiltered = new List<IGH_ActiveObject>(objects);
                if (excludeTypes.Contains("PARAMS")) typeFiltered.RemoveAll(o => o is IGH_Param);
                if (excludeTypes.Contains("COMPONENTS")) typeFiltered.RemoveAll(o => o is GH_Component);
                if (excludeTypes.Overlaps(new[] { "INPUT", "OUTPUT", "PROCESSING", "ISOLATED" }))
                {
                    var tempDoc = GHDocumentUtils.GetObjectsDetails(typeFiltered);
                    var incd = new Dictionary<Guid, int>();
                    var outd = new Dictionary<Guid, int>();
                    foreach (var conn in tempDoc.Connections)
                    {
                        outd[conn.From.ComponentId] = (outd.TryGetValue(conn.From.ComponentId, out var ov) ? ov : 0) + 1;
                        incd[conn.To.ComponentId] = (incd.TryGetValue(conn.To.ComponentId, out var iv) ? iv : 0) + 1;
                    }
                    if (excludeTypes.Contains("INPUT")) typeFiltered.RemoveAll(c => !incd.ContainsKey(c.InstanceGuid) && outd.ContainsKey(c.InstanceGuid));
                    if (excludeTypes.Contains("OUTPUT")) typeFiltered.RemoveAll(c => !outd.ContainsKey(c.InstanceGuid) && incd.ContainsKey(c.InstanceGuid));
                    if (excludeTypes.Contains("PROCESSING")) typeFiltered.RemoveAll(c => incd.ContainsKey(c.InstanceGuid) && outd.ContainsKey(c.InstanceGuid));
                    if (excludeTypes.Contains("ISOLATED")) typeFiltered.RemoveAll(c => !incd.ContainsKey(c.InstanceGuid) && !outd.ContainsKey(c.InstanceGuid));
                }
            }
            objects = typeFiltered;

            // Apply includes
            List<IGH_ActiveObject> resultObjects;
            if (includeTags.Any())
            {
                resultObjects = new List<IGH_ActiveObject>();
                if (includeTags.Contains("SELECTED"))
                    resultObjects.AddRange(objects.Where(o => o.Attributes.Selected));
                if (includeTags.Contains("UNSELECTED"))
                    resultObjects.AddRange(objects.Where(o => !o.Attributes.Selected));
                if (includeTags.Contains("ENABLED"))
                    resultObjects.AddRange(objects.OfType<GH_Component>().Where(c => !c.Locked).Cast<IGH_ActiveObject>());
                if (includeTags.Contains("DISABLED"))
                    resultObjects.AddRange(objects.OfType<GH_Component>().Where(c => c.Locked).Cast<IGH_ActiveObject>());
                if (includeTags.Contains("ERROR"))
                    resultObjects.AddRange(objects.Where(o => o.RuntimeMessages(GH_RuntimeMessageLevel.Error).Any()));
                if (includeTags.Contains("WARNING"))
                    resultObjects.AddRange(objects.Where(o => o.RuntimeMessages(GH_RuntimeMessageLevel.Warning).Any()));
                if (includeTags.Contains("REMARK"))
                    resultObjects.AddRange(objects.Where(o => o.RuntimeMessages(GH_RuntimeMessageLevel.Remark).Any()));
                if (includeTags.Contains("PREVIEWCAPABLE"))
                    resultObjects.AddRange(objects.OfType<GH_Component>().Where(c => c.IsPreviewCapable).Cast<IGH_ActiveObject>());
                if (includeTags.Contains("NOTPREVIEWCAPABLE"))
                    resultObjects.AddRange(objects.OfType<GH_Component>().Where(c => !c.IsPreviewCapable).Cast<IGH_ActiveObject>());
                if (includeTags.Contains("PREVIEWON"))
                    resultObjects.AddRange(objects.OfType<GH_Component>().Where(c => c.IsPreviewCapable && !c.Hidden).Cast<IGH_ActiveObject>());
                if (includeTags.Contains("PREVIEWOFF"))
                    resultObjects.AddRange(objects.OfType<GH_Component>().Where(c => c.IsPreviewCapable && c.Hidden).Cast<IGH_ActiveObject>());
            }
            else
            {
                resultObjects = new List<IGH_ActiveObject>(objects);
            }

            // Apply excludes
            if (excludeTags.Contains("SELECTED"))
                resultObjects.RemoveAll(o => o.Attributes.Selected);
            if (excludeTags.Contains("UNSELECTED"))
                resultObjects.RemoveAll(o => !o.Attributes.Selected);
            if (excludeTags.Contains("ENABLED"))
                resultObjects.RemoveAll(o => o is GH_Component c && !c.Locked);
            if (excludeTags.Contains("DISABLED"))
                resultObjects.RemoveAll(o => o is GH_Component c && c.Locked);
            if (excludeTags.Contains("ERROR"))
                resultObjects.RemoveAll(o => o.RuntimeMessages(GH_RuntimeMessageLevel.Error).Any());
            if (excludeTags.Contains("WARNING"))
                resultObjects.RemoveAll(o => o.RuntimeMessages(GH_RuntimeMessageLevel.Warning).Any());
            if (excludeTags.Contains("REMARK"))
                resultObjects.RemoveAll(o => o.RuntimeMessages(GH_RuntimeMessageLevel.Remark).Any());
            if (excludeTags.Contains("PREVIEWCAPABLE"))
                resultObjects.RemoveAll(o => o is GH_Component c && c.IsPreviewCapable);
            if (excludeTags.Contains("NOTPREVIEWCAPABLE"))
                resultObjects.RemoveAll(o => o is GH_Component c && !c.IsPreviewCapable);
            if (excludeTags.Contains("PREVIEWON"))
                resultObjects.RemoveAll(o => o is GH_Component c && c.IsPreviewCapable && !c.Hidden);
            if (excludeTags.Contains("PREVIEWOFF"))
                resultObjects.RemoveAll(o => o is GH_Component c && c.IsPreviewCapable && c.Hidden);

            if (connectionDepth > 0)
            {
                var allObjects = GHCanvasUtils.GetCurrentObjects();
                var fullDoc = GHDocumentUtils.GetObjectsDetails(allObjects);
                var edges = fullDoc.Connections.Select(c => (c.From.ComponentId, c.To.ComponentId));
                var initialIds = resultObjects.Select(o => o.InstanceGuid);
                var expandedIds = ConnectionGraphUtils.ExpandByDepth(edges, initialIds, connectionDepth);
                var idMap = allObjects.ToDictionary(o => o.InstanceGuid, o => o);
                resultObjects = expandedIds
                    .Select(g => idMap.TryGetValue(g, out var obj) ? obj : null)
                    .Where(o => o != null)
                    .ToList();
            }

            var distinct = resultObjects.Distinct().ToList();
            var document = GHDocumentUtils.GetObjectsDetails(distinct);
            var names = document.Components.Select(c => c.Name).Distinct().ToList();
            var guids = document.Components.Select(c => c.InstanceGuid.ToString()).Distinct().ToList();
            var json = JsonConvert.SerializeObject(document, Formatting.None);

            // Package result with classifications
            var result = new JObject
            {
                ["names"] = JArray.FromObject(names),
                ["guids"] = JArray.FromObject(guids),
                ["json"] = json
            };

            return Task.FromResult<object>(result);
        }
        #endregion

        #region GhRetrieve
        /// <summary>
        /// Executes the Grasshopper list component types tool.
        /// </summary>
        private Task<object> ExecuteGhRetrieveToolAsync(JObject parameters)
        {
            var server = Instances.ComponentServer;
            var categoryFilters = parameters["categoryFilter"]?.ToObject<List<string>>() ?? new List<string>();
            var (includeCats, excludeCats) = ParseIncludeExclude(categoryFilters, CategorySynonyms);

            // Retrieve all component proxies in one call
            var proxies = server.ObjectProxies.ToList();

            // Apply include filters
            if (includeCats.Any())
                proxies = proxies.Where(p => p.Desc.Category != null && includeCats.Contains(p.Desc.Category.ToUpperInvariant())).ToList();

            // Apply exclude filters
            if (excludeCats.Any())
                proxies = proxies.Where(p => p.Desc.Category == null || !excludeCats.Contains(p.Desc.Category.ToUpperInvariant())).ToList();

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
                            access = param.Access.ToString()
                        })
                        .Cast<object>()
                        .ToList();
                    outputs = GHParameterUtils.GetAllOutputs(comp)
                        .Select(param => new
                        {
                            name = param.Name,
                            description = param.Description,
                            dataType = param.GetType().Name,
                            access = param.Access.ToString()
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
                            access = param.Access.ToString()
                        }
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
                    outputs
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
                ["json"] = json
            };
            return Task.FromResult<object>(result);
        }
        #endregion

        #region GhCategories
        /// <summary>
        /// Executes the Grasshopper categories listing tool with optional soft string filter.
        /// </summary>
        private Task<object> ExecuteGhCategoriesToolAsync(JObject parameters)
        {
            var server = Instances.ComponentServer;
            var filterString = parameters["filter"]?.ToObject<string>() ?? string.Empty;
            var tokens = Regex.Replace(filterString, @"[,;\-_]", " ")
                .Split(new[]{' '}, StringSplitOptions.RemoveEmptyEntries)
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
                    subs.Add(sub);
            }

            var result = new List<object>();
            foreach (var kv in dict)
            {
                var cat = kv.Key;
                var subs = kv.Value;
                if (!tokens.Any())
                {
                    result.Add(new { category = cat, subCategories = subs.ToList() });
                }
                else
                {
                    bool catMatch = tokens.Any(tok => cat.ToLowerInvariant().Contains(tok));
                    var matchingSubs = subs.Where(s => tokens.Any(tok => s.ToLowerInvariant().Contains(tok))).ToList();
                    if (catMatch)
                    {
                        result.Add(new { category = cat, subCategories = subs.ToList() });
                    }
                    else if (matchingSubs.Any())
                    {
                        result.Add(new { category = cat, subCategories = matchingSubs });
                    }
                }
            }

            var jResult = new JObject
            {
                ["count"] = result.Count,
                ["categories"] = JArray.FromObject(result)
            };
            return Task.FromResult<object>(jResult);
        }
        #endregion
    }
}
