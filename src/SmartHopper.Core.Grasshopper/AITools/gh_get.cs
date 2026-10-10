/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024-2026 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this library; if not, see <https://www.gnu.org/licenses/lgpl-3.0.html>.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using GhJSON.Core;
using GhJSON.Core.Serialization;
using GhJSON.Grasshopper;
using GhJSON.Grasshopper.Query;
using GhJSON.Grasshopper.Serialization;
using Grasshopper;
using Grasshopper.Kernel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AIContext;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.Diagnostics;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Tool provider for Grasshopper component retrieval via AI Tool Manager.
    /// Provides a generic <c>gh_get</c> tool plus a small set of specialized
    /// wrappers (<c>gh_get_selected</c>, <c>gh_get_by_guid</c>, <c>gh_get_errors</c>)
    /// that share the same parameter surface and only inject a predefined filter.
    /// </summary>
    public class gh_get : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private readonly string toolName = "gh_get";

        /// <summary>
        /// Fields allowed in the <c>fields</c> parameter for the summary projection.
        /// </summary>
        private static readonly string[] AllowedFields =
        {
            "instanceGuid", "name", "nickName", "pivot", "bounds",
            "selected", "locked", "previewOn", "category", "subcategory",
            "messages", "runtimeData", "internalizedData",
        };

        /// <summary>
        /// Fields emitted when <c>fields</c> is not provided.
        /// </summary>
        private static readonly string[] DefaultFields =
        {
            "instanceGuid", "name", "nickName", "pivot", "bounds",
        };

        /// <summary>
        /// Attribute filter tokens (without the '+') that denote a runtime-message
        /// level. An include token of this kind automatically enables
        /// <c>includeRuntimeMessages</c>.
        /// </summary>
        private static readonly HashSet<string> MessageLevelTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "error", "errors", "warning", "warnings", "warn", "remark", "remarks", "info",
        };

        /// <summary>
        /// Common metadata for all gh_get variants.
        /// </summary>
        private AITool CreateGhGetTool(
            string name,
            string description,
            bool requireGuidFilter,
            Func<AIToolCall, Task<AIReturn>> execute)
        {
            var tags = new List<string> { "canvas", "components", "read-only", "ghjson" };

            var outputSchema = @"{ ""type"": ""object"", ""properties"": { ""detail"": { ""type"": ""string"" }, ""ghjson"": { ""type"": ""string"", ""description"": ""Serialized Grasshopper document in GhJSON format. Omitted when detail=summary."" }, ""components"": { ""type"": ""array"", ""description"": ""Per-component projection. Only present when detail=summary; controlled by the 'fields' parameter."" }, ""pagination"": { ""type"": ""object"", ""description"": ""Pagination metadata."" }, ""serializationQuality"": { ""type"": ""object"", ""description"": ""Omitted when detail=summary."" } } }";

            var schema = AddDetailToSchema(AddPaginationToSchema(BuildParametersSchema(requireGuidFilter)));

            return new AITool(
                name: name,
                description: description,
                category: "Components",
                parametersSchema: schema,
                execute: execute,
                mutatesCanvas: false,
                tags: tags,
                outputSchema: outputSchema,
                annotations: new AIToolAnnotations(readOnlyHint: true));
        }

        /// <summary>
        /// Builds the shared parameter schema used by every gh_get variant.
        /// </summary>
        private static string BuildParametersSchema(bool requireGuidFilter)
        {
            var schema = JObject.Parse(@"{
                ""type"": ""object"",
                ""properties"": {
                    ""attrFilters"": {
                        ""type"": ""array"",
                        ""items"": { ""type"": ""string"" },
                        ""description"": ""Optional array of attribute filter tokens. '+' includes, '-' excludes. Defaults to all components. Available tags:\n  selected/unselected: component selection state on canvas;\n  enabled/disabled: whether the component can run (enabled = unlocked);\n  error/warning/remark: runtime message levels;\n  previewcapable/notpreviewcapable: supports geometry preview;\n  previewon/previewoff: current preview toggle.\nSynonyms: locked→disabled, unlocked→enabled, remarks/info→remark, warn/warnings→warning, errors→error, visible→previewon, hidden→previewoff. Examples: '+error' → only components with errors; '+error +warning' → errors OR warnings; '+error -warning' → errors excluding warnings; '+error -previewoff' → errors with preview on; no filter → all components.""
                    },
                    ""categoryFilter"": {
                        ""type"": ""array"",
                        ""items"": { ""type"": ""string"" },
                        ""description"": ""Optionally filter components by Grasshopper category or subcategory. '+' includes, '-' excludes. Most common categories: Params, Maths, Vector, Curve, Surface, Mesh, Intersect, Transform, Sets, Display, Rhino, Kangaroo, Script. E.g. ['+Vector','-Curve','+Script'].""
                    },
                    ""typeFilter"": {
                        ""type"": ""array"",
                        ""items"": { ""type"": ""string"" },
                        ""description"": ""Optional array of type tokens with include/exclude syntax. Defaults to all types. Available tokens:\n  params: only parameter objects;\n  components: only component objects;\n  startnodes: components with no incoming connections (data sources);\n  endnodes: components with no outgoing connections (data sinks);\n  middlenodes: components with both incoming and outgoing connections (processors);\n  isolatednodes: components with neither incoming nor outgoing connections.\nExamples: ['+params', '-components'] to include parameters and exclude components.""
                    },
                    ""instanceGuids"": {
                        ""type"": ""array"",
                        ""items"": { ""type"": ""string"" },
                        ""description"": ""Optional list of object instance GUIDs for initial filtering. When provided, only objects with these instance GUIDs are processed. If not provided, all objects are processed.""
                    },
                    ""nameFilter"": {
                        ""type"": ""array"",
                        ""items"": { ""type"": ""string"" },
                        ""description"": ""Optional list of name fragments. Only components whose Name OR NickName contains any fragment (case-insensitive) are returned. Examples: ['panel'] finds all panels; ['slider','script'] finds sliders and scripts.""
                    },
                    ""connectionDepth"": {
                        ""type"": ""integer"",
                        ""default"": 0,
                        ""description"": ""Depth of connections to include: 0 (default) only matching components; 1 includes directly connected components; 2 includes two-level connected components, etc. Note: when used with viewportOnly, values > 0 may include off-screen neighbors of visible components.""
                    },
                    ""viewportOnly"": {
                        ""type"": ""boolean"",
                        ""default"": false,
                        ""description"": ""When true, only returns components currently visible in the canvas viewport. Useful for large definitions where off-screen components should be ignored.""
                    },
                    ""includeMetadata"": {
                        ""type"": ""boolean"",
                        ""default"": false,
                        ""description"": ""Whether to include document metadata (timestamps, Rhino/Grasshopper versions, plugin dependencies). Default is false.""
                    },
                    ""includeInternalizedData"": {
                        ""type"": ""boolean"",
                        ""default"": false,
                        ""description"": ""Whether to include internalized (persistent) data stored in parameters, such as panel text or slider values. Default is false. This is token-expansive!""
                    },
                    ""includeRuntimeData"": {
                        ""type"": ""boolean"",
                        ""default"": false,
                        ""description"": ""Whether to include runtime (volatile) data - actual values currently flowing through component outputs. Useful for inspecting computed results. Default is false. This is token-expansive!""
                    },
                    ""includeRuntimeMessages"": {
                        ""type"": ""boolean"",
                        ""default"": false,
                        ""description"": ""Whether to include runtime messages (errors, warnings, remarks) per component in the GhJSON output. Automatically enabled when an error/warning/remark include filter is used (e.g. attrFilters:['+error']). Default is false.""
                    },
                    ""fields"": {
                        ""type"": ""array"",
                        ""items"": { ""type"": ""string"" },
                        ""description"": ""Optional list of fields to emit per component when detail='summary'. Allowed: instanceGuid, name, nickName, pivot, bounds, selected, locked, previewOn, category, subcategory, messages, runtimeData, internalizedData. 'instanceGuid' is always included. 'messages' returns the component's errors/warnings/remarks; 'runtimeData'/'internalizedData' return the GhJSON-serialized volatile/persistent data per output parameter. Default: instanceGuid, name, nickName, pivot, bounds. The includeRuntimeData/includeInternalizedData/includeRuntimeMessages flags also enable their corresponding fields in summary mode. Ignored when detail='full'.""
                    }
                }
            }");

            if (requireGuidFilter)
            {
                schema["required"] = new JArray("instanceGuids");
                var guidDesc = schema["properties"]?["instanceGuids"]?["description"];
                if (guidDesc != null)
                {
                    schema["properties"]!["instanceGuids"]!["description"] =
                        "Required list of object instance GUIDs to retrieve.";
                }
            }

            return schema.ToString(Formatting.None);
        }

        /// <summary>
        /// Adds pagination parameters to a JSON schema when they are not already present.
        /// </summary>
        private static string AddPaginationToSchema(string parametersSchema)
        {
            var obj = JObject.Parse(parametersSchema);
            var properties = obj["properties"] as JObject;
            if (properties == null)
            {
                return parametersSchema;
            }

            if (!properties.ContainsKey("page"))
            {
                properties["page"] = JObject.Parse(@"{ ""type"": ""integer"", ""default"": 1, ""minimum"": 1, ""description"": ""One-based page index for paginated results. Default is 1."" }");
            }

            if (!properties.ContainsKey("pageSize"))
            {
                properties["pageSize"] = JObject.Parse(@"{ ""type"": ""integer"", ""default"": 25, ""minimum"": 1, ""description"": ""Number of components per page. Default is 25."" }");
            }

            return obj.ToString(Formatting.None);
        }

        /// <summary>
        /// Adds the optional <c>detail</c> parameter (summary|full) to a JSON schema when it
        /// is not already present. Defaults to full for backwards compatibility.
        /// </summary>
        private static string AddDetailToSchema(string parametersSchema)
        {
            var obj = JObject.Parse(parametersSchema);
            var properties = obj["properties"] as JObject;
            if (properties == null || properties.ContainsKey("detail"))
            {
                return parametersSchema;
            }

            properties["detail"] = JObject.Parse(@"{ ""type"": ""string"", ""enum"": [""summary"", ""full""], ""default"": ""full"", ""description"": ""Response detail level. 'full' (default) returns the complete GhJSON serialization. 'summary' omits 'ghjson' and 'serializationQuality' and returns a compact per-component projection controlled by the 'fields' parameter."" }");
            return obj.ToString(Formatting.None);
        }

        /// <summary>
        /// Returns a list of AI tools provided by this plugin.
        /// </summary>
        /// <returns>Collection of AI tools.</returns>
        public IEnumerable<AITool> GetTools()
        {
            // Generic gh_get tool with all options
            yield return this.CreateGhGetTool(
                name: this.toolName,
                description: "Read the current Grasshopper file with optional filters. By default, it returns all components. Returns a GhJSON structure of the file. Examples: selected components → gh_get({ attrFilters: ['+selected'] }); errors only → gh_get({ attrFilters: ['+error'], detail: 'summary', fields: ['name','messages'] }); viewport only → gh_get({ viewportOnly: true }); by name → gh_get({ nameFilter: ['panel'] }); data sources → gh_get({ typeFilter: ['+startnodes'] }). See also: gh_get_selected, gh_get_errors, gh_get_by_guid.",
                requireGuidFilter: false,
                execute: (toolCall) => this.GhGetToolAsync(toolCall));

            // Specialized wrapper: gh_get_selected
            yield return this.CreateGhGetTool(
                name: "gh_get_selected",
                description: "Read only the selected components from the Grasshopper canvas. Use this when the user asks about 'selected', 'this', or 'these' components. Accepts the same filters as gh_get (e.g. connectionDepth: 1 to include connected neighbors, includeRuntimeData for computed values). Returns a GhJSON structure.",
                requireGuidFilter: false,
                execute: (toolCall) => this.GhGetToolAsync(toolCall, predefinedAttrFilters: new[] { "+selected" }));

            // Specialized wrapper: gh_get_by_guid
            yield return this.CreateGhGetTool(
                name: "gh_get_by_guid",
                description: "Read specific components by their GUIDs. Use this when you have component GUIDs from a previous query. Accepts the same filters as gh_get (e.g. includeRuntimeData for computed values, detail: 'summary' for a compact projection). Returns a GhJSON structure. Example: gh_get_by_guid({ instanceGuids: ['...'], connectionDepth: 1 }).",
                requireGuidFilter: true,
                execute: (toolCall) => this.GhGetToolAsync(toolCall));

            // Specialized wrapper: gh_get_errors
            yield return this.CreateGhGetTool(
                name: "gh_get_errors",
                description: "Read only components that have error messages. Use this when debugging or when the user asks about errors or broken components. Runtime messages are always included. Accepts the same filters as gh_get (use detail: 'summary' + fields: ['name','messages'] for a compact report). Returns a GhJSON structure. See also: gh_get, gh_report, script_review.",
                requireGuidFilter: false,
                execute: (toolCall) => this.GhGetToolAsync(toolCall, predefinedAttrFilters: new[] { "+error" }, forceIncludeMessages: true));
        }

        /// <summary>
        /// Executes the Grasshopper get components tool with optional predefined filters.
        /// </summary>
        /// <param name="toolCall">The tool call containing parameters.</param>
        /// <param name="predefinedAttrFilters">Attribute filters injected by wrapper tools. Merged with user-provided attrFilters.</param>
        /// <param name="predefinedTypeFilters">Type filters injected by wrapper tools. Merged with user-provided typeFilter.</param>
        /// <param name="forceViewportOnly">When true, restricts results to components visible in the canvas viewport regardless of parameter value.</param>
        /// <param name="forceIncludeMessages">When true, forces inclusion of runtime messages (errors/warnings/remarks) regardless of parameter value.</param>
        /// <returns>Task that returns the result of the operation.</returns>
        private Task<AIReturn> GhGetToolAsync(AIToolCall toolCall, string[]? predefinedAttrFilters = null, string[]? predefinedTypeFilters = null, bool forceViewportOnly = false, bool forceIncludeMessages = false)
        {
            var output = new AIReturn() { Request = toolCall };

            try
            {
                // Local tool: we don't need provider/model/finish_reason metrics for validation
                toolCall.SkipMetricsValidation = true;

                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.GetArgumentsOrEmpty();

                // Parse parameters
                var connectionDepth = args["connectionDepth"]?.ToObject<int>() ?? 0;
                var includeInternalizedData = args["includeInternalizedData"]?.ToObject<bool>() ?? false;
                var includeRuntimeData = args["includeRuntimeData"]?.ToObject<bool>() ?? false;

                // Attribute filters (predefined tokens merged with user tokens)
                var attrTokens = MergeFilterTokens(predefinedAttrFilters, args["attrFilters"]?.ToObject<string[]>(), "attrFilters", output);

                // An error/warning/remark include filter implies runtime messages:
                // an errors report without message contents would be useless.
                var includeMessages = forceIncludeMessages
                    || (args["includeRuntimeMessages"]?.ToObject<bool>() ?? false)
                    || attrTokens.Any(t => t.StartsWith("+", StringComparison.Ordinal)
                        && MessageLevelTokens.Contains(t.Substring(1).Trim()));
                var includeMetadata = args["includeMetadata"]?.ToObject<bool>() ?? false;
                var viewportOnly = forceViewportOnly || (args["viewportOnly"]?.ToObject<bool>() ?? false);
                var page = args["page"]?.ToObject<int>() ?? 1;
                var pageSize = args["pageSize"]?.ToObject<int>() ?? 25;
                var detail = args["detail"]?.ToString()?.ToLowerInvariant() ?? "full";
                if (detail != "summary" && detail != "full")
                {
                    output.CreateError($"Invalid 'detail' value '{detail}'. Expected 'summary' or 'full'.");
                    return Task.FromResult(output);
                }

                // Resolve requested summary fields
                var fieldTokens = args["fields"]?.ToObject<List<string>>();
                var requestedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (fieldTokens != null && fieldTokens.Count > 0)
                {
                    var invalid = fieldTokens
                        .Where(f => !AllowedFields.Contains(f, StringComparer.OrdinalIgnoreCase))
                        .ToList();
                    if (invalid.Count > 0)
                    {
                        output.AddRuntimeMessage(
                            SHRuntimeMessageSeverity.Warning,
                            SHRuntimeMessageOrigin.Tool,
                            $"Unknown 'fields' entries ignored: {string.Join(", ", invalid)}. Allowed: {string.Join(", ", AllowedFields)}.");
                    }

                    foreach (var f in fieldTokens.Where(f => AllowedFields.Contains(f, StringComparer.OrdinalIgnoreCase)))
                    {
                        requestedFields.Add(f);
                    }

                    if (detail == "full")
                    {
                        output.AddRuntimeMessage(
                            SHRuntimeMessageSeverity.Warning,
                            SHRuntimeMessageOrigin.Tool,
                            "'fields' only applies when detail='summary'; it is ignored in the full GhJSON response.");
                    }
                }
                else
                {
                    foreach (var f in DefaultFields)
                    {
                        requestedFields.Add(f);
                    }
                }

                // In summary mode the include* flags map to their projection fields,
                // so e.g. gh_get_errors with detail:'summary' still reports messages.
                if (detail == "summary")
                {
                    if (includeMessages)
                    {
                        requestedFields.Add("messages");
                    }

                    if (includeRuntimeData)
                    {
                        requestedFields.Add("runtimeData");
                    }

                    if (includeInternalizedData)
                    {
                        requestedFields.Add("internalizedData");
                    }
                }

                Debug.WriteLine($"[gh_get] internalized: {includeInternalizedData}, runtime: {includeRuntimeData}, messages: {includeMessages}, depth: {connectionDepth}, metadata: {includeMetadata}, viewportOnly: {viewportOnly}, page: {page}, pageSize: {pageSize}, detail: {detail}");

                // Build the query using CanvasSelector
                var selector = CanvasSelector.FromActiveCanvas();

                // Viewport restriction — only include components whose bounds intersect the visible canvas area
                if (viewportOnly)
                {
                    var canvas = Instances.ActiveCanvas;
                    if (canvas?.Viewport != null)
                    {
                        selector.WithViewport(canvas.Viewport.VisibleRegion);
                    }
                    else
                    {
                        Debug.WriteLine("[gh_get] viewportOnly requested but no active canvas viewport available.");
                        output.AddRuntimeMessage(SHRuntimeMessageSeverity.Warning, SHRuntimeMessageOrigin.Tool, "viewportOnly was requested but no active canvas viewport was available. Returning all components.");
                    }
                }

                // GUID restriction
                // Instance GUID restriction ("guidFilter" kept as a silent legacy alias)
                var guidStrings = (args["instanceGuids"] ?? args["guidFilter"])?.ToObject<List<string>>();
                if (guidStrings != null)
                {
                    var guids = new List<Guid>();
                    foreach (var s in guidStrings)
                    {
                        if (Guid.TryParse(s, out var g))
                        {
                            guids.Add(g);
                        }
                    }

                    if (guids.Count > 0)
                    {
                        selector.WithGuids(guids);
                    }
                }

                // Type filters (predefined tokens merged with user tokens)
                var typeTokens = MergeFilterTokens(predefinedTypeFilters, args["typeFilter"]?.ToObject<string[]>(), "typeFilter", output);
                if (typeTokens.Length > 0)
                {
                    selector.WithTypes(typeTokens);
                }

                // Category filters
                var categoryTokens = args["categoryFilter"]?.ToObject<string[]>();
                if (categoryTokens != null)
                {
                    selector.WithCategories(categoryTokens);
                }

                // Attribute filters were already merged above (needed for includeMessages detection)
                if (attrTokens.Length > 0)
                {
                    selector.WithAttributes(attrTokens);
                }

                // Connection depth
                if (connectionDepth > 0)
                {
                    selector.WithConnected(connectionDepth);
                }

                // Execute the query
                var resultObjects = selector.Execute();

                // Name/nickname substring restriction (post-filter on live objects)
                var nameTokens = args["nameFilter"]?.ToObject<List<string>>()
                    ?.Where(t => !string.IsNullOrWhiteSpace(t))
                    .Select(t => t.Trim())
                    .ToList();
                if (nameTokens != null && nameTokens.Count > 0)
                {
                    resultObjects = resultObjects
                        .Where(o => nameTokens.Any(t =>
                            (o.Name ?? string.Empty).IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            (o.NickName ?? string.Empty).IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0))
                        .ToList();
                }

                Debug.WriteLine($"[gh_get] Query returned {resultObjects.Count} objects");

                // Serialize the result
                var serOptions = new SerializationOptions
                {
                    IncludeConnections = true,
                    IncludeGroups = true,
                    IncludeInternalizedData = includeInternalizedData,
                    IncludeRuntimeData = includeRuntimeData,
                    IncludeRuntimeMessages = includeMessages,
                    IncludeSelectedState = true,
                    AssignSequentialIds = true,
                    IncludeMetadata = includeMetadata,
                    Page = page,
                    PageSize = pageSize,
                };

                // Apply user-defined metadata overrides from the file context provider
                if (includeMetadata)
                {
                    var metadataProvider = AIContextManager.GetProvider("file-metadata");
                    if (metadataProvider != null)
                    {
                        try
                        {
                            var metadataContext = metadataProvider.GetContext();
                            if (metadataContext.TryGetValue("title", out var metaTitle))
                            {
                                serOptions.MetadataTitle = metaTitle;
                            }

                            if (metadataContext.TryGetValue("description", out var metaDescription))
                            {
                                serOptions.MetadataDescription = metaDescription;
                            }

                            if (metadataContext.TryGetValue("version", out var metaVersion))
                            {
                                serOptions.MetadataVersion = metaVersion;
                            }

                            if (metadataContext.TryGetValue("author", out var metaAuthor))
                            {
                                serOptions.MetadataAuthor = metaAuthor;
                            }

                            if (metadataContext.TryGetValue("tags", out var metaTags))
                            {
                                serOptions.MetadataTags = metaTags
                                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                    .Select(t => t.Trim())
                                    .Where(t => !string.IsNullOrWhiteSpace(t))
                                    .ToList();
                            }

                            Debug.WriteLine("[gh_get] Applied metadata overrides from file context provider");
                        }
                        catch (Exception metaEx)
                        {
                            Debug.WriteLine($"[gh_get] Error applying file-metadata overrides: {metaEx.Message}");
                        }
                    }
                }

                var totalComponents = resultObjects.Count;
                var pageCount = pageSize > 0 ? (int)Math.Ceiling((double)totalComponents / pageSize) : 1;
                var pagedObjects = pageSize > 0
                    ? resultObjects.Skip((page - 1) * pageSize).Take(pageSize).ToList()
                    : resultObjects;

                JObject toolResult;
                if (detail == "summary")
                {
                    var names = pagedObjects
                        .Where(o => !string.IsNullOrWhiteSpace(o?.Name))
                        .Select(o => o.Name)
                        .Distinct()
                        .ToList();

                    // Runtime and internalized data are projected through the GhJSON
                    // serializer itself (structure-aware, compact) rather than dumping
                    // raw data-tree items. Scalar fields are read off live objects.
                    var wantRuntimeData = requestedFields.Contains("runtimeData");
                    var wantInternalizedData = requestedFields.Contains("internalizedData");
                    Dictionary<Guid, JObject> fragmentMap = new Dictionary<Guid, JObject>();
                    if (wantRuntimeData || wantInternalizedData)
                    {
                        var fragmentDoc = GhJsonGrasshopper.Serialize(pagedObjects, new SerializationOptions
                        {
                            IncludeConnections = false,
                            IncludeGroups = false,
                            IncludeInternalizedData = wantInternalizedData,
                            IncludeRuntimeData = wantRuntimeData,
                            IncludeRuntimeMessages = false,
                            IncludeSelectedState = false,
                            IncludeMetadata = false,
                        });

                        fragmentMap = fragmentDoc.Components
                            .Where(c => c.InstanceGuid.HasValue)
                            .GroupBy(c => c.InstanceGuid!.Value)
                            .ToDictionary(g => g.Key, g => JObject.FromObject(g.First()));
                    }

                    var components = pagedObjects
                        .Where(o => o != null)
                        .Select(o => BuildSummaryItem(o, requestedFields, fragmentMap.GetValueOrDefault(o.InstanceGuid)))
                        .Where(item => item != null)
                        .ToList();

                    if (components.Count == 0)
                    {
                        output.AddRuntimeMessage(SHRuntimeMessageSeverity.Warning, SHRuntimeMessageOrigin.Tool, "No components matched the requested filters. Try relaxing filters or adjusting pagination.");
                    }

                    toolResult = new JObject
                    {
                        ["detail"] = "summary",
                        ["names"] = JArray.FromObject(names),
                        ["guids"] = JArray.FromObject(pagedObjects.Select(o => o.InstanceGuid.ToString()).Distinct()),
                        ["components"] = JArray.FromObject(components),
                        ["pagination"] = new JObject
                        {
                            ["page"] = page,
                            ["pageSize"] = pageSize,
                            ["totalComponents"] = totalComponents,
                            ["pageCount"] = pageCount,
                            ["returnedComponents"] = components.Count,
                        },
                    };
                }
                else
                {
                    var document = GhJsonGrasshopper.Serialize(resultObjects, serOptions);

                    var names = document.Components
                        .Where(c => !string.IsNullOrWhiteSpace(c.Name))
                        .Select(c => c.Name)
                        .Distinct()
                        .ToList();

                    var guidList = document.Components
                        .Where(c => c.InstanceGuid.HasValue)
                        .Select(c => c.InstanceGuid!.Value.ToString())
                        .Distinct()
                        .ToList();

                    if (document.Components.Count == 0)
                    {
                        output.AddRuntimeMessage(SHRuntimeMessageSeverity.Warning, SHRuntimeMessageOrigin.Tool, "No components matched the requested filters. Try relaxing filters or adjusting pagination.");
                    }

                    var json = GhJson.ToJson(document, new WriteOptions { Indented = false });

                    var thinComponents = document.Components
                        .Where(c => c.Warnings?.Any(w => w.Contains("without a specialized handler")) == true)
                        .Select(c => new { c.Name, c.Library })
                        .ToList();

                    var missingPlugins = document.Components
                        .Select(c => c.Library)
                        .Where(l => !string.IsNullOrEmpty(l))
                        .Distinct()
                        .ToList();

                    toolResult = new JObject
                    {
                        ["detail"] = "full",
                        ["names"] = JArray.FromObject(names),
                        ["guids"] = JArray.FromObject(guidList),
                        ["ghjson"] = json,
                        ["pagination"] = new JObject
                        {
                            ["page"] = page,
                            ["pageSize"] = pageSize,
                            ["totalComponents"] = totalComponents,
                            ["pageCount"] = pageCount,
                            ["returnedComponents"] = document.Components.Count,
                        },
                        ["serializationQuality"] = new JObject
                        {
                            ["totalComponents"] = totalComponents,
                            ["thinComponents"] = JArray.FromObject(thinComponents),
                            ["referencedPlugins"] = JArray.FromObject(missingPlugins),
                        },
                    };
                }

                var body = AIBodyBuilder.Create()
                    .AddToolResult(toolResult, toolInfo.Id, toolInfo.Name ?? this.toolName)
                    .Build();

                output.CreateSuccess(body, toolCall);
                return Task.FromResult(output);
            }
            catch (Exception ex)
            {
                output.CreateError($"Error executing {this.toolName}: {ex.Message}");
                return Task.FromResult(output);
            }
        }

        /// <summary>
        /// Builds a per-component summary item from the live document object,
        /// emitting only the requested fields. <paramref name="serializedFragment"/>
        /// is the GhJSON-serialized form of the same object, used for the
        /// <c>runtimeData</c> and <c>internalizedData</c> projections.
        /// </summary>
        private static JObject? BuildSummaryItem(IGH_DocumentObject? o, HashSet<string> fields, JObject? serializedFragment)
        {
            if (o == null)
            {
                return null;
            }

            var item = new JObject();

            // instanceGuid is always emitted: it is the join key for follow-up queries.
            item["instanceGuid"] = o.InstanceGuid.ToString();

            if (fields.Contains("name"))
            {
                item["name"] = o.Name;
            }

            if (fields.Contains("nickName"))
            {
                item["nickName"] = o.NickName;
            }

            if (fields.Contains("pivot"))
            {
                item["pivot"] = o.Attributes != null
                    ? new JObject { ["x"] = o.Attributes.Pivot.X, ["y"] = o.Attributes.Pivot.Y }
                    : null;
            }

            if (fields.Contains("bounds"))
            {
                var bounds = o.Attributes?.Bounds;
                if (bounds.HasValue)
                {
                    item["bounds"] = new JObject
                    {
                        ["x"] = bounds.Value.X,
                        ["y"] = bounds.Value.Y,
                        ["width"] = bounds.Value.Width,
                        ["height"] = bounds.Value.Height,
                    };
                }
            }

            if (fields.Contains("selected"))
            {
                item["selected"] = o.Attributes?.Selected ?? false;
            }

            if (fields.Contains("locked"))
            {
                item["locked"] = (o as IGH_ActiveObject)?.Locked;
            }

            if (fields.Contains("previewOn"))
            {
                item["previewOn"] = o is IGH_PreviewObject p && p.IsPreviewCapable
                    ? !p.Hidden
                    : (bool?)null;
            }

            if (fields.Contains("category"))
            {
                item["category"] = (o as GH_DocumentObject)?.Category;
            }

            if (fields.Contains("subcategory"))
            {
                item["subcategory"] = (o as GH_DocumentObject)?.SubCategory;
            }

            if (fields.Contains("messages"))
            {
                var active = o as IGH_ActiveObject;
                if (active != null)
                {
                    item["messages"] = new JObject
                    {
                        ["errors"] = JArray.FromObject(active.RuntimeMessages(GH_RuntimeMessageLevel.Error)),
                        ["warnings"] = JArray.FromObject(active.RuntimeMessages(GH_RuntimeMessageLevel.Warning)),
                        ["remarks"] = JArray.FromObject(active.RuntimeMessages(GH_RuntimeMessageLevel.Remark)),
                    };
                }
                else
                {
                    item["messages"] = null;
                }
            }

            if (fields.Contains("runtimeData"))
            {
                item["runtimeData"] = serializedFragment != null
                    ? ExtractParamData(serializedFragment, "runtimeData")
                    : null;
            }

            if (fields.Contains("internalizedData"))
            {
                item["internalizedData"] = serializedFragment != null
                    ? ExtractParamData(serializedFragment, "internalizedData")
                    : null;
            }

            return item;
        }

        /// <summary>
        /// Extracts per-parameter data (<c>runtimeData</c> or <c>internalizedData</c>)
        /// from a GhJSON-serialized component, keyed by parameter nickname.
        /// Only parameters that actually carry the requested data are included.
        /// </summary>
        private static JObject ExtractParamData(JObject compJson, string dataKey)
        {
            var data = new JObject();
            foreach (var side in new[] { "inputSettings", "outputSettings" })
            {
                if (compJson[side] is not JArray settings)
                {
                    continue;
                }

                foreach (var p in settings.OfType<JObject>())
                {
                    var value = p[dataKey];
                    if (value == null || value.Type == JTokenType.Null)
                    {
                        continue;
                    }

                    var key = p["nickName"]?.ToString()
                        ?? p["parameterName"]?.ToString()
                        ?? p["variableName"]?.ToString()
                        ?? side;

                    // Disambiguate input/output params that share a nickname.
                    if (data[key] != null)
                    {
                        key = $"{key} ({side})";
                    }

                    data[key] = value.DeepClone();
                }
            }

            return data;
        }

        /// <summary>
        /// Merges wrapper-injected (predefined) filter tokens with user-supplied ones.
        /// Empty entries are dropped, and a user token that directly negates a
        /// predefined token (e.g. '-error' against an injected '+error') is dropped
        /// with a warning so the wrapper's core guarantee cannot be silently broken.
        /// </summary>
        private static string[] MergeFilterTokens(string[]? predefined, string[]? user, string parameterName, AIReturn output)
        {
            var merged = new List<string>();
            var dropped = new List<string>();

            if (predefined != null)
            {
                merged.AddRange(predefined.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()));
            }

            if (user != null)
            {
                foreach (var raw in user)
                {
                    if (string.IsNullOrWhiteSpace(raw))
                    {
                        continue;
                    }

                    var token = raw.Trim();
                    var contradicts = token.StartsWith("-", StringComparison.Ordinal)
                        && merged.Any(p =>
                            p.StartsWith("+", StringComparison.Ordinal)
                            && string.Equals(p.Substring(1).Trim(), token.Substring(1).Trim(), StringComparison.OrdinalIgnoreCase));

                    if (contradicts)
                    {
                        dropped.Add(token);
                    }
                    else
                    {
                        merged.Add(token);
                    }
                }
            }

            if (dropped.Count > 0)
            {
                output.AddRuntimeMessage(
                    SHRuntimeMessageSeverity.Warning,
                    SHRuntimeMessageOrigin.Tool,
                    $"Dropped {parameterName} tokens that contradict this tool's predefined filter: {string.Join(", ", dropped)}.");
            }

            return merged.ToArray();
        }
    }
}
