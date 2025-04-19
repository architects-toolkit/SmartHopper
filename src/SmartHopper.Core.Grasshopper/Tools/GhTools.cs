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
using SmartHopper.Core.Grasshopper;

namespace SmartHopper.Core.Grasshopper.Tools
{
    /// <summary>
    /// Tool provider for Grasshopper component retrieval via AI Tool Manager
    /// </summary>
    public class GhTools : IAIToolProvider
    {
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: "ghget",
                description: "Retrieve Grasshopper components as GhJSON with optional filters. By default, it returns all components.",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""filters"": {
                            ""type"": ""array"",
                            ""items"": { ""type"": ""string"" },
                            ""description"": ""Array of filter tokens. '+' includes, '-' excludes. Available tags:\n  selected/unselected: component selection state on canvas;\n  enabled/disabled: whether the component can run (enabled = unlocked);\n  error/warning/remark: runtime message levels;\n  previewcapable/notpreviewcapable: supports geometry preview;\n  previewon/previewoff: current preview toggle.\nSynonyms: locked→disabled, unlocked→enabled, remarks/info→remark, warn/warnings→warning, errors→error, visible→previewon, hidden→previewoff. Examples: '+error' → only components with errors; '+error +warning' → errors OR warnings; '+error -warning' → errors excluding warnings; '+error -previewoff' → errors with preview on.""
                        }
                    }
                }",
                execute: ExecuteGhGetToolAsync
            );
        }

        private Task<object> ExecuteGhGetToolAsync(JObject parameters)
        {
            // Parse filters
            var filters = parameters["filters"]?.ToObject<List<string>>() ?? new List<string>();
            var objects = GHCanvasUtils.GetCurrentObjects();

            // Prepare tag sets
            var includeTags = new HashSet<string>();
            var excludeTags = new HashSet<string>();
            foreach (var rawGroup in filters)
            {
                if (string.IsNullOrWhiteSpace(rawGroup)) continue;
                var parts = rawGroup.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    var tok = part.Trim();
                    if (string.IsNullOrEmpty(tok)) continue;
                    bool include = !tok.StartsWith("-");
                    var tag = tok.TrimStart('+', '-').ToLowerInvariant();
                    // Synonyms
                    if (tag == "locked") tag = "disabled";
                    if (tag == "unlocked") tag = "enabled";
                    if (tag == "remarks" || tag == "info") tag = "remark";
                    if (tag == "warn" || tag == "warnings") tag = "warning";
                    if (tag == "errors") tag = "error";
                    if (tag == "visible") tag = "previewon";
                    if (tag == "hidden") tag = "previewoff";
                    // classification synonyms
                    if (tag == "onlyparams" || tag == "param" || tag == "parameter") tag = "params";
                    if (tag == "onlycomponents" || tag == "component" || tag == "comp") tag = "components";
                    // NO NEED TO MAP:
                    // previewcapable
                    // notpreviewcapable
                    // previewon
                    // previewoff
                    // selected
                    // unselected
                    // enabled
                    // disabled
                    // remark
                    // error
                    // warning
                    // previewcapable
                    // notpreviewcapable
                    // previewon
                    // previewoff
                    if (include) includeTags.Add(tag);
                    else excludeTags.Add(tag);
                }
            }

            // Classification filters: params vs components
            if (includeTags.Contains("params"))
                objects = objects.OfType<IGH_Param>().Cast<IGH_ActiveObject>().ToList();
            if (includeTags.Contains("components"))
                objects = objects.OfType<GH_Component>().Cast<IGH_ActiveObject>().ToList();
            // Remove classification tags so they don't affect other filters
            includeTags.Remove("params");
            includeTags.Remove("components");
            excludeTags.Remove("params");
            excludeTags.Remove("components");

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

            var distinct = resultObjects.Distinct().ToList();
            var document = GHDocumentUtils.GetObjectsDetails(distinct);
            var names = document.Components.Select(c => c.Name).Distinct().ToList();
            var guids = document.Components.Select(c => c.ComponentGuid.ToString()).Distinct().ToList();
            var json = JsonConvert.SerializeObject(document, Formatting.None);

            // Classify by object type
            var paramIds = document.Components
                .Where(cp => cp.Type == "IGH_Param")
                .Select(cp => cp.InstanceGuid.ToString())
                .ToList();
            var compIds = document.Components
                .Where(cp => cp.Type == "IGH_Component")
                .Select(cp => cp.InstanceGuid)
                .ToList();
            // Build connection counts
            var incoming = new Dictionary<Guid, int>();
            var outgoing = new Dictionary<Guid, int>();
            foreach (var conn in document.Connections)
            {
                outgoing[conn.From.ComponentId] = (outgoing.TryGetValue(conn.From.ComponentId, out var ov) ? ov : 0) + 1;
                incoming[conn.To.ComponentId] = (incoming.TryGetValue(conn.To.ComponentId, out var iv) ? iv : 0) + 1;
            }
            // Categorize components
            var inputComponents = compIds
                .Where(id => !incoming.ContainsKey(id))
                .Select(id => id.ToString())
                .ToList();
            var outputComponents = compIds
                .Where(id => !outgoing.ContainsKey(id))
                .Select(id => id.ToString())
                .ToList();
            var processingComponents = compIds
                .Where(id => incoming.ContainsKey(id) && outgoing.ContainsKey(id))
                .Select(id => id.ToString())
                .ToList();
            // Package result with classifications
            var result = new JObject
            {
                ["names"] = JArray.FromObject(names),
                ["guids"] = JArray.FromObject(guids),
                ["params"] = JArray.FromObject(paramIds),
                ["components"] = JArray.FromObject(compIds.Select(g => g.ToString())),
                ["inputComponents"] = JArray.FromObject(inputComponents),
                ["outputComponents"] = JArray.FromObject(outputComponents),
                ["processingComponents"] = JArray.FromObject(processingComponents),
                ["json"] = json
            };

            return Task.FromResult<object>(result);
        }
    }
}
