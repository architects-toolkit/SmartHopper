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
using SmartHopper.Core.Grasshopper.Graph;
using SmartHopper.Core.Grasshopper.Utils.Canvas;
using SmartHopper.Core.Grasshopper.Utils.Internal;
using SmartHopper.Core.Grasshopper.Utils.Serialization;
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
    /// Provides both a generic gh_get tool and specialized wrapper tools for common use cases.
    /// </summary>
    public class gh_get : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "gh_get";
        /// <summary>
        /// Returns a list of AI tools provided by this plugin.
        /// </summary>
        /// <returns>Collection of AI tools.</returns>
        public IEnumerable<AITool> GetTools()
        {
            // Generic gh_get tool with all options
            yield return new AITool(
                name: this.toolName,
                description: "Read the current Grasshopper file with optional filters. By default, it returns all components. Returns a GhJSON structure of the file.",
                category: "Components",
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
                        },
                        ""includeMetadata"": {
                            ""type"": ""boolean"",
                            ""default"": false,
                            ""description"": ""Whether to include document metadata (schema version, timestamps, Rhino/Grasshopper versions, plugin dependencies). Default is false.""
                        }
                    }
                }",
                execute: (toolCall) => this.GhGetToolAsync(toolCall, null, null));

            // Specialized wrapper: gh_get_selected
            yield return new AITool(
                name: "gh_get_selected",
                description: "Read only the selected components from the Grasshopper canvas. Use this when the user asks about 'selected', 'this', or 'these' components. Returns a GhJSON structure.",
                category: "Components",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""connectionDepth"": {
                            ""type"": ""integer"",
                            ""default"": 0,
                            ""description"": ""Depth of connections to include: 0 (default) only selected components; 1 includes directly connected components; 2 includes two-level connected components, etc.""
                        }
                    }
                }",
                execute: (toolCall) => this.GhGetToolAsync(toolCall, new[] { "+selected" }));

            // Specialized wrapper: gh_get_by_guid
            yield return new AITool(
                name: "gh_get_by_guid",
                description: "Read specific components by their GUIDs. Use this when you have component GUIDs from a previous query. Returns a GhJSON structure.",
                category: "Components",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""guidFilter"": {
                            ""type"": ""array"",
                            ""items"": { ""type"": ""string"" },
                            ""description"": ""Required list of component GUIDs to retrieve.""
                        },
                        ""connectionDepth"": {
                            ""type"": ""integer"",
                            ""default"": 0,
                            ""description"": ""Depth of connections to include: 0 (default) only specified components; 1 includes directly connected components; 2 includes two-level connected components, etc.""
                        }
                    },
                    ""required"": [""guidFilter""]
                }",
                execute: (toolCall) => this.GhGetToolAsync(toolCall, null, null));

            // Specialized wrapper: gh_get_errors
            yield return new AITool(
                name: "gh_get_errors",
                description: "Read only components that have error messages. Use this when debugging or when the user asks about errors or broken components. Returns a GhJSON structure.",
                category: "Components",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""connectionDepth"": {
                            ""type"": ""integer"",
                            ""default"": 0,
                            ""description"": ""Depth of connections to include: 0 (default) only error components; 1 includes directly connected components; 2 includes two-level connected components, etc.""
                        }
                    }
                }",
                execute: (toolCall) => this.GhGetToolAsync(toolCall, new[] { "+error" }));

            // Specialized wrapper: gh_get_locked
            yield return new AITool(
                name: "gh_get_locked",
                description: "Read only locked (disabled) components from the Grasshopper canvas. Use this when the user asks about locked or disabled components. Returns a GhJSON structure.",
                category: "Components",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""connectionDepth"": {
                            ""type"": ""integer"",
                            ""default"": 0,
                            ""description"": ""Depth of connections to include: 0 (default) only locked components; 1 includes directly connected components; 2 includes two-level connected components, etc.""
                        }
                    }
                }",
                execute: (toolCall) => this.GhGetToolAsync(toolCall, new[] { "+disabled" }));

            // Specialized wrapper: gh_get_hidden
            yield return new AITool(
                name: "gh_get_hidden",
                description: "Read only components with preview turned off (hidden geometry). Use this when the user asks about hidden components or components with disabled preview. Returns a GhJSON structure.",
                category: "Components",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""connectionDepth"": {
                            ""type"": ""integer"",
                            ""default"": 0,
                            ""description"": ""Depth of connections to include: 0 (default) only hidden components; 1 includes directly connected components; 2 includes two-level connected components, etc.""
                        }
                    }
                }",
                execute: (toolCall) => this.GhGetToolAsync(toolCall, new[] { "+previewoff" }));

            // Specialized wrapper: gh_get_visible
            yield return new AITool(
                name: "gh_get_visible",
                description: "Read only components with preview turned on (visible geometry). Use this when the user asks about visible components or components with enabled preview. Returns a GhJSON structure.",
                category: "Components",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""connectionDepth"": {
                            ""type"": ""integer"",
                            ""default"": 0,
                            ""description"": ""Depth of connections to include: 0 (default) only visible components; 1 includes directly connected components; 2 includes two-level connected components, etc.""
                        }
                    }
                }",
                execute: (toolCall) => this.GhGetToolAsync(toolCall, new[] { "+previewon" }));
        }

        /// <summary>
        /// Executes the Grasshopper get components tool with optional predefined filters.
        /// </summary>
        /// <param name="toolCall">The tool call containing parameters.</param>
        /// <param name="predefinedAttrFilters">Predefined attribute filters to apply (used by wrapper tools).</param>
        /// <param name="predefinedTypeFilters">Predefined type filters to apply (used by wrapper tools).</param>
        /// <returns>Task that returns the result of the operation.</returns>
        private Task<AIReturn> GhGetToolAsync(AIToolCall toolCall, string[] predefinedAttrFilters = null, string[] predefinedTypeFilters = null)
        {
            // Prepare the output
            var output = new AIReturn()
            {
                Request = toolCall,
            };

            try
            {
                // Parse filters
                AIInteractionToolCall toolInfo = toolCall.GetToolCall();

                // Arguments may be null when calling gh_get with no parameters; default to empty filters
                var args = toolInfo.Arguments ?? new JObject();
                
                // Use predefined filters if provided (for wrapper tools), otherwise use filters from arguments
                var attrFilters = predefinedAttrFilters != null
                    ? new List<string>(predefinedAttrFilters)
                    : args["attrFilters"]?.ToObject<List<string>>() ?? new List<string>();
                var typeFilters = predefinedTypeFilters != null
                    ? new List<string>(predefinedTypeFilters)
                    : args["typeFilter"]?.ToObject<List<string>>() ?? new List<string>();
                var objects = CanvasAccess.GetCurrentObjects();

                // Filter by manual UI selection if provided
                var selectedGuids = args["guidFilter"]?.ToObject<List<string>>() ?? new List<string>();
                if (selectedGuids.Any())
                {
                    var set = new HashSet<string>(selectedGuids);
                    objects = objects.Where(o => set.Contains(o.InstanceGuid.ToString())).ToList();
                }

                var connectionDepth = args["connectionDepth"]?.ToObject<int>() ?? 0;
                var includeMetadata = args["includeMetadata"]?.ToObject<bool>() ?? false;
                var (includeTypes, excludeTypes) = ComponentRetriever.ParseIncludeExclude(typeFilters, ComponentRetriever.TypeSynonyms);
                var (includeTags, excludeTags) = ComponentRetriever.ParseIncludeExclude(attrFilters, ComponentRetriever.FilterSynonyms);

                // Apply typeFilters on base objects
                var typeFiltered = new List<IGH_ActiveObject>(objects);
                if (includeTypes.Any())
                {
                    var tf = new List<IGH_ActiveObject>();
                    if (includeTypes.Contains("PARAMS"))
                    {
                        tf.AddRange(objects.OfType<IGH_Param>());
                    }

                    if (includeTypes.Contains("COMPONENTS"))
                    {
                        tf.AddRange(objects.OfType<GH_Component>());
                    }

                    if (includeTypes.Overlaps(new[] { "INPUT", "OUTPUT", "PROCESSING", "ISOLATED" }))
                    {
                        var tempDoc = DocumentIntrospection.GetObjectsDetails(objects, includeMetadata: false, includeGroups: false);
                        var incd = new Dictionary<Guid, int>();
                        var outd = new Dictionary<Guid, int>();
                        foreach (var conn in tempDoc.Connections)
                        {
                            outd[conn.From.InstanceId] = (outd.TryGetValue(conn.From.InstanceId, out var ov) ? ov : 0) + 1;
                            incd[conn.To.InstanceId] = (incd.TryGetValue(conn.To.InstanceId, out var iv) ? iv : 0) + 1;
                        }

                        if (includeTypes.Contains("INPUT"))
                        {
                            tf.AddRange(objects.Where(c => !incd.ContainsKey(c.InstanceGuid) && outd.ContainsKey(c.InstanceGuid)));
                        }

                        if (includeTypes.Contains("OUTPUT"))
                        {
                            tf.AddRange(objects.Where(c => !outd.ContainsKey(c.InstanceGuid) && incd.ContainsKey(c.InstanceGuid)));
                        }

                        if (includeTypes.Contains("PROCESSING"))
                        {
                            tf.AddRange(objects.Where(c => incd.ContainsKey(c.InstanceGuid) && outd.ContainsKey(c.InstanceGuid)));
                        }

                        if (includeTypes.Contains("ISOLATED"))
                        {
                            tf.AddRange(objects.Where(c => !incd.ContainsKey(c.InstanceGuid) && !outd.ContainsKey(c.InstanceGuid)));
                        }
                    }

                    typeFiltered = tf.ToList();
                }

                if (excludeTypes.Any())
                {
                    if (!includeTypes.Any())
                    {
                        typeFiltered = new List<IGH_ActiveObject>(objects);
                    }

                    if (excludeTypes.Contains("PARAMS"))
                    {
                        typeFiltered.RemoveAll(o => o is IGH_Param);
                    }

                    if (excludeTypes.Contains("COMPONENTS"))
                    {
                        typeFiltered.RemoveAll(o => o is GH_Component);
                    }

                    if (excludeTypes.Overlaps(new[] { "INPUT", "OUTPUT", "PROCESSING", "ISOLATED" }))
                    {
                        var tempDoc = DocumentIntrospection.GetObjectsDetails(typeFiltered, includeMetadata: false, includeGroups: false);
                        var incd = new Dictionary<Guid, int>();
                        var outd = new Dictionary<Guid, int>();
                        foreach (var conn in tempDoc.Connections)
                        {
                            outd[conn.From.InstanceId] = (outd.TryGetValue(conn.From.InstanceId, out var ov) ? ov : 0) + 1;
                            incd[conn.To.InstanceId] = (incd.TryGetValue(conn.To.InstanceId, out var iv) ? iv : 0) + 1;
                        }

                        if (excludeTypes.Contains("INPUT"))
                        {
                            typeFiltered.RemoveAll(c => !incd.ContainsKey(c.InstanceGuid) && outd.ContainsKey(c.InstanceGuid));
                        }

                        if (excludeTypes.Contains("OUTPUT"))
                        {
                            typeFiltered.RemoveAll(c => !outd.ContainsKey(c.InstanceGuid) && incd.ContainsKey(c.InstanceGuid));
                        }

                        if (excludeTypes.Contains("PROCESSING"))
                        {
                            typeFiltered.RemoveAll(c => incd.ContainsKey(c.InstanceGuid) && outd.ContainsKey(c.InstanceGuid));
                        }

                        if (excludeTypes.Contains("ISOLATED"))
                        {
                            typeFiltered.RemoveAll(c => !incd.ContainsKey(c.InstanceGuid) && !outd.ContainsKey(c.InstanceGuid));
                        }
                    }
                }

                objects = typeFiltered;

                // Apply includes
                List<IGH_ActiveObject> resultObjects;
                if (includeTags.Any())
                {
                    resultObjects = new List<IGH_ActiveObject>();
                    if (includeTags.Contains("SELECTED"))
                    {
                        resultObjects.AddRange(objects.Where(o => o.Attributes.Selected));
                    }

                    if (includeTags.Contains("UNSELECTED"))
                    {
                        resultObjects.AddRange(objects.Where(o => !o.Attributes.Selected));
                    }

                    if (includeTags.Contains("ENABLED"))
                    {
                        resultObjects.AddRange(objects.OfType<GH_Component>().Where(c => !c.Locked).Cast<IGH_ActiveObject>());
                    }

                    if (includeTags.Contains("DISABLED"))
                    {
                        resultObjects.AddRange(objects.OfType<GH_Component>().Where(c => c.Locked).Cast<IGH_ActiveObject>());
                    }

                    if (includeTags.Contains("ERROR"))
                    {
                        resultObjects.AddRange(objects.Where(o => o.RuntimeMessages(GH_RuntimeMessageLevel.Error).Any()));
                    }

                    if (includeTags.Contains("WARNING"))
                    {
                        resultObjects.AddRange(objects.Where(o => o.RuntimeMessages(GH_RuntimeMessageLevel.Warning).Any()));
                    }

                    if (includeTags.Contains("REMARK"))
                    {
                        resultObjects.AddRange(objects.Where(o => o.RuntimeMessages(GH_RuntimeMessageLevel.Remark).Any()));
                    }

                    if (includeTags.Contains("PREVIEWCAPABLE"))
                    {
                        resultObjects.AddRange(objects.OfType<GH_Component>().Where(c => c.IsPreviewCapable).Cast<IGH_ActiveObject>());
                    }

                    if (includeTags.Contains("NOTPREVIEWCAPABLE"))
                    {
                        resultObjects.AddRange(objects.OfType<GH_Component>().Where(c => !c.IsPreviewCapable).Cast<IGH_ActiveObject>());
                    }

                    if (includeTags.Contains("PREVIEWON"))
                    {
                        resultObjects.AddRange(objects.OfType<GH_Component>().Where(c => c.IsPreviewCapable && !c.Hidden).Cast<IGH_ActiveObject>());
                    }

                    if (includeTags.Contains("PREVIEWOFF"))
                    {
                        resultObjects.AddRange(objects.OfType<GH_Component>().Where(c => c.IsPreviewCapable && c.Hidden).Cast<IGH_ActiveObject>());
                    }
                }
                else
                {
                    resultObjects = new List<IGH_ActiveObject>(objects);
                }

                // Apply excludes
                if (excludeTags.Contains("SELECTED"))
                {
                    resultObjects.RemoveAll(o => o.Attributes.Selected);
                }

                if (excludeTags.Contains("UNSELECTED"))
                {
                    resultObjects.RemoveAll(o => !o.Attributes.Selected);
                }

                if (excludeTags.Contains("ENABLED"))
                {
                    resultObjects.RemoveAll(o => o is GH_Component c && !c.Locked);
                }

                if (excludeTags.Contains("DISABLED"))
                {
                    resultObjects.RemoveAll(o => o is GH_Component c && c.Locked);
                }

                if (excludeTags.Contains("ERROR"))
                {
                    resultObjects.RemoveAll(o => o.RuntimeMessages(GH_RuntimeMessageLevel.Error).Any());
                }

                if (excludeTags.Contains("WARNING"))
                {
                    resultObjects.RemoveAll(o => o.RuntimeMessages(GH_RuntimeMessageLevel.Warning).Any());
                }

                if (excludeTags.Contains("REMARK"))
                {
                    resultObjects.RemoveAll(o => o.RuntimeMessages(GH_RuntimeMessageLevel.Remark).Any());
                }

                if (excludeTags.Contains("PREVIEWCAPABLE"))
                {
                    resultObjects.RemoveAll(o => o is GH_Component c && c.IsPreviewCapable);
                }

                if (excludeTags.Contains("NOTPREVIEWCAPABLE"))
                {
                    resultObjects.RemoveAll(o => o is GH_Component c && !c.IsPreviewCapable);
                }

                if (excludeTags.Contains("PREVIEWON"))
                {
                    resultObjects.RemoveAll(o => o is GH_Component c && c.IsPreviewCapable && !c.Hidden);
                }

                if (excludeTags.Contains("PREVIEWOFF"))
                {
                    resultObjects.RemoveAll(o => o is GH_Component c && c.IsPreviewCapable && c.Hidden);
                }

                if (connectionDepth > 0)
                {
                    var allObjects = CanvasAccess.GetCurrentObjects();
                    var fullDoc = DocumentIntrospection.GetObjectsDetails(allObjects, includeMetadata: false, includeGroups: false);
                    var edges = fullDoc.Connections.Select(c => (c.From.InstanceId, c.To.InstanceId));
                    var initialIds = resultObjects.Select(o => o.InstanceGuid);
                    var expandedIds = ConnectionGraphUtils.ExpandByDepth(edges, initialIds, connectionDepth);
                    var idMap = allObjects.ToDictionary(o => o.InstanceGuid, o => o);
                    resultObjects = expandedIds
                        .Select(g => idMap.TryGetValue(g, out var obj) ? obj : null)
                        .Where(o => o != null)
                        .ToList();
                }

                var document = DocumentIntrospection.GetObjectsDetails(resultObjects, includeMetadata, includeGroups: true);

                // only keep connections where both components are in our filtered set
                var allowed = resultObjects.Select(o => o.InstanceGuid).ToHashSet();
                document.Connections = document.Connections
                    .Where(c => allowed.Contains(c.From.InstanceId)
                            && allowed.Contains(c.To.InstanceId))
                    .ToList();

                // Get names and guids
                var names = document.Components.Select(c => c.Name).Distinct().ToList();
                var guids = document.Components.Select(c => c.InstanceGuid.ToString()).Distinct().ToList();

                // Serialize document
                var json = JsonConvert.SerializeObject(document, Formatting.None);

                // Package result with classifications
                var toolResult = new JObject
                {
                    ["names"] = JArray.FromObject(names),
                    ["guids"] = JArray.FromObject(guids),
                    ["json"] = json,
                };

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
