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
using Grasshopper.Kernel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartHopper.Config.Interfaces;
using SmartHopper.Config.Models;
using SmartHopper.Core.Graph;
using SmartHopper.Core.Grasshopper;

namespace SmartHopper.Core.Grasshopper.Tools
{
    /// <summary>
    /// Tool provider for Grasshopper component retrieval via AI Tool Manager
    /// </summary>
    public class GhTools : IAIToolProvider
    {
        // Synonyms for filter tags
        // Available filter tokens:
        //   selected/unselected: component selection on canvas
        //   enabled/disabled: whether the component can run (enabled = unlocked)
        //   error/warning/remark: runtime message levels
        //   previewcapable/notpreviewcapable: supports geometry preview
        //   previewon/previewoff: current preview toggle
        // Synonyms:
        //   locked → disabled
        //   unlocked → enabled
        //   remarks/info → remark
        //   warn/warnings → warning
        //   errors → error
        //   visible → previewon
        //   hidden → previewoff
        private static readonly Dictionary<string, string> FilterSynonyms = new Dictionary<string, string>
        {
            { "locked", "disabled" },
            { "unlocked", "enabled" },
            { "remarks", "remark" },
            { "info", "remark" },
            { "warn", "warning" },
            { "warnings", "warning" },
            { "errors", "error" },
            { "visible", "previewon" },
            { "hidden", "previewoff" },
        };

        // Synonyms for typeFilter tokens
        // Available typeFilter tokens:
        //   params: only parameter objects (IGH_Param)
        //   components: only component objects (GH_Component)
        //   input: components with no incoming connections (inputs only)
        //   output: components with no outgoing connections (outputs only)
        //   processing: components with both incoming and outgoing connections
        //   isolated: components with neither incoming nor outgoing connections (isolated)
        // Synonyms:
        //   param, parameter → params
        //   component, comp → components
        private static readonly Dictionary<string, string> TypeSynonyms = new Dictionary<string, string>
        {
            { "param", "params" },
            { "parameter", "params" },
            { "component", "components" },
            { "comp", "components" },
            { "inputs", "input" },
            { "inputcomponents", "input" },
            { "outputs", "output" },
            { "outputcomponents", "output" },
            { "processingcomponents", "processing" },
            { "intermediate", "processing" },
            { "middle", "processing" },
            { "middlecomponents", "processing" },
            { "isolatedcomponents", "isolated" }
        };

        // Helper to parse include/exclude tokens
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
                    var tag = tok.TrimStart('+', '-').ToLowerInvariant();
                    if (synonyms != null && synonyms.TryGetValue(tag, out var mapped))
                        tag = mapped;
                    if (inc) include.Add(tag);
                    else exclude.Add(tag);
                }
            }
            return (include, exclude);
        }

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
                            ""description"": ""Array of attribute filter tokens. '+' includes, '-' excludes. Available tags:\n  selected/unselected: component selection state on canvas;\n  enabled/disabled: whether the component can run (enabled = unlocked);\n  error/warning/remark: runtime message levels;\n  previewcapable/notpreviewcapable: supports geometry preview;\n  previewon/previewoff: current preview toggle.\nSynonyms: locked→disabled, unlocked→enabled, remarks/info→remark, warn/warnings→warning, errors→error, visible→previewon, hidden→previewoff. Examples: '+error' → only components with errors; '+error +warning' → errors OR warnings; '+error -warning' → errors excluding warnings; '+error -previewoff' → errors with preview on.""
                        },
                        ""typeFilter"": {
                            ""type"": ""array"",
                            ""items"": { ""type"": ""string"" },
                            ""description"": ""Optional array of classification tokens with include/exclude syntax. Available tokens:\n  params: only parameter objects;\n  components: only component objects;\n  input: components with no incoming connections (inputs only);\n  output: components with no outgoing connections (outputs only);\n  processing: components with both incoming and outgoing connections;\n  isolated: components with neither incoming nor outgoing connections (isolated).\nExamples: ['+params', '-components'] to include parameters and exclude components.\nWhen omitted, no type filtering is applied (all objects returned).""
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
        }

        private Task<object> ExecuteGhGetToolAsync(JObject parameters)
        {
            // Parse filters
            var attrFilters = parameters["attrFilters"]?.ToObject<List<string>>() ?? new List<string>();
            var typeFilters = parameters["typeFilter"]?.ToObject<List<string>>() ?? new List<string>();
            var objects = GHCanvasUtils.GetCurrentObjects();
            var connectionDepth = parameters["connectionDepth"]?.ToObject<int>() ?? 0;
            var (includeTypes, excludeTypes) = ParseIncludeExclude(typeFilters, TypeSynonyms);
            var (includeTags, excludeTags) = ParseIncludeExclude(attrFilters, FilterSynonyms);

            // Apply typeFilters on base objects
            var typeFiltered = new List<IGH_ActiveObject>(objects);
            if (includeTypes.Any())
            {
                var tf = new List<IGH_ActiveObject>();
                if (includeTypes.Contains("params")) tf.AddRange(objects.OfType<IGH_Param>());
                if (includeTypes.Contains("components")) tf.AddRange(objects.OfType<GH_Component>());
                if (includeTypes.Overlaps(new[] { "input", "output", "processing", "isolated" }))
                {
                    var tempDoc = GHDocumentUtils.GetObjectsDetails(objects);
                    var incd = new Dictionary<Guid, int>();
                    var outd = new Dictionary<Guid, int>();
                    foreach (var conn in tempDoc.Connections)
                    {
                        outd[conn.From.ComponentId] = (outd.TryGetValue(conn.From.ComponentId, out var ov) ? ov : 0) + 1;
                        incd[conn.To.ComponentId] = (incd.TryGetValue(conn.To.ComponentId, out var iv) ? iv : 0) + 1;
                    }
                    if (includeTypes.Contains("input")) tf.AddRange(objects.Where(c => !incd.ContainsKey(c.InstanceGuid) && outd.ContainsKey(c.InstanceGuid)));
                    if (includeTypes.Contains("output")) tf.AddRange(objects.Where(c => !outd.ContainsKey(c.InstanceGuid) && incd.ContainsKey(c.InstanceGuid)));
                    if (includeTypes.Contains("processing")) tf.AddRange(objects.Where(c => incd.ContainsKey(c.InstanceGuid) && outd.ContainsKey(c.InstanceGuid)));
                    if (includeTypes.Contains("isolated")) tf.AddRange(objects.Where(c => !incd.ContainsKey(c.InstanceGuid) && !outd.ContainsKey(c.InstanceGuid)));
                }
                typeFiltered = tf.Distinct().ToList();
            }
            if (excludeTypes.Any())
            {
                if (!includeTypes.Any()) typeFiltered = new List<IGH_ActiveObject>(objects);
                if (excludeTypes.Contains("params")) typeFiltered.RemoveAll(o => o is IGH_Param);
                if (excludeTypes.Contains("components")) typeFiltered.RemoveAll(o => o is GH_Component);
                if (excludeTypes.Overlaps(new[] { "input", "output", "processing", "isolated" }))
                {
                    var tempDoc = GHDocumentUtils.GetObjectsDetails(typeFiltered);
                    var incd = new Dictionary<Guid, int>();
                    var outd = new Dictionary<Guid, int>();
                    foreach (var conn in tempDoc.Connections)
                    {
                        outd[conn.From.ComponentId] = (outd.TryGetValue(conn.From.ComponentId, out var ov) ? ov : 0) + 1;
                        incd[conn.To.ComponentId] = (incd.TryGetValue(conn.To.ComponentId, out var iv) ? iv : 0) + 1;
                    }
                    if (excludeTypes.Contains("input")) typeFiltered.RemoveAll(c => !incd.ContainsKey(c.InstanceGuid) && outd.ContainsKey(c.InstanceGuid));
                    if (excludeTypes.Contains("output")) typeFiltered.RemoveAll(c => !outd.ContainsKey(c.InstanceGuid) && incd.ContainsKey(c.InstanceGuid));
                    if (excludeTypes.Contains("processing")) typeFiltered.RemoveAll(c => incd.ContainsKey(c.InstanceGuid) && outd.ContainsKey(c.InstanceGuid));
                    if (excludeTypes.Contains("isolated")) typeFiltered.RemoveAll(c => !incd.ContainsKey(c.InstanceGuid) && !outd.ContainsKey(c.InstanceGuid));
                }
            }
            objects = typeFiltered;

            // Apply includes
            List<IGH_ActiveObject> resultObjects;
            if (includeTags.Any())
            {
                resultObjects = new List<IGH_ActiveObject>();
                if (includeTags.Contains("selected"))
                    resultObjects.AddRange(objects.Where(o => o.Attributes.Selected));
                if (includeTags.Contains("unselected"))
                    resultObjects.AddRange(objects.Where(o => !o.Attributes.Selected));
                if (includeTags.Contains("enabled"))
                    resultObjects.AddRange(objects.OfType<GH_Component>().Where(c => !c.Locked).Cast<IGH_ActiveObject>());
                if (includeTags.Contains("disabled"))
                    resultObjects.AddRange(objects.OfType<GH_Component>().Where(c => c.Locked).Cast<IGH_ActiveObject>());
                if (includeTags.Contains("error"))
                    resultObjects.AddRange(objects.Where(o => o.RuntimeMessages(GH_RuntimeMessageLevel.Error).Any()));
                if (includeTags.Contains("warning"))
                    resultObjects.AddRange(objects.Where(o => o.RuntimeMessages(GH_RuntimeMessageLevel.Warning).Any()));
                if (includeTags.Contains("remark"))
                    resultObjects.AddRange(objects.Where(o => o.RuntimeMessages(GH_RuntimeMessageLevel.Remark).Any()));
                if (includeTags.Contains("previewcapable"))
                    resultObjects.AddRange(objects.OfType<GH_Component>().Where(c => c.IsPreviewCapable).Cast<IGH_ActiveObject>());
                if (includeTags.Contains("notpreviewcapable"))
                    resultObjects.AddRange(objects.OfType<GH_Component>().Where(c => !c.IsPreviewCapable).Cast<IGH_ActiveObject>());
                if (includeTags.Contains("previewon"))
                    resultObjects.AddRange(objects.OfType<GH_Component>().Where(c => c.IsPreviewCapable && !c.Hidden).Cast<IGH_ActiveObject>());
                if (includeTags.Contains("previewoff"))
                    resultObjects.AddRange(objects.OfType<GH_Component>().Where(c => c.IsPreviewCapable && c.Hidden).Cast<IGH_ActiveObject>());
            }
            else
            {
                resultObjects = new List<IGH_ActiveObject>(objects);
            }

            // Apply excludes
            if (excludeTags.Contains("selected"))
                resultObjects.RemoveAll(o => o.Attributes.Selected);
            if (excludeTags.Contains("unselected"))
                resultObjects.RemoveAll(o => !o.Attributes.Selected);
            if (excludeTags.Contains("enabled"))
                resultObjects.RemoveAll(o => o is GH_Component c && !c.Locked);
            if (excludeTags.Contains("disabled"))
                resultObjects.RemoveAll(o => o is GH_Component c && c.Locked);
            if (excludeTags.Contains("error"))
                resultObjects.RemoveAll(o => o.RuntimeMessages(GH_RuntimeMessageLevel.Error).Any());
            if (excludeTags.Contains("warning"))
                resultObjects.RemoveAll(o => o.RuntimeMessages(GH_RuntimeMessageLevel.Warning).Any());
            if (excludeTags.Contains("remark"))
                resultObjects.RemoveAll(o => o.RuntimeMessages(GH_RuntimeMessageLevel.Remark).Any());
            if (excludeTags.Contains("previewcapable"))
                resultObjects.RemoveAll(o => o is GH_Component c && c.IsPreviewCapable);
            if (excludeTags.Contains("notpreviewcapable"))
                resultObjects.RemoveAll(o => o is GH_Component c && !c.IsPreviewCapable);
            if (excludeTags.Contains("previewon"))
                resultObjects.RemoveAll(o => o is GH_Component c && c.IsPreviewCapable && !c.Hidden);
            if (excludeTags.Contains("previewoff"))
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
            var guids = document.Components.Select(c => c.ComponentGuid.ToString()).Distinct().ToList();
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
    }
}
