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
 * along with this library; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
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
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
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
                            ""description"": ""Whether to include document metadata (timestamps, Rhino/Grasshopper versions, plugin dependencies). Default is false.""
                        },
                        ""includeRuntimeData"": {
                            ""type"": ""boolean"",
                            ""default"": false,
                            ""description"": ""Whether to include runtime/volatile data (actual values currently flowing through component outputs). Useful for inspecting computed results. Default is false. This is token-expansive!""
                        }
                    }
                }",
                execute: (toolCall) => this.GhGetToolAsync(toolCall, null, null, false));

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
                execute: (toolCall) => this.GhGetToolAsync(toolCall, new[] { "+selected" }, null, false));

            // Specialized wrapper: gh_get_selected_with_data
            yield return new AITool(
                name: "gh_get_selected_with_data",
                description: "Read selected components WITH their runtime data (volatile data - actual values flowing through outputs). Use this when you need to inspect computed results, count items, or check actual output values. Returns GhJSON with an additional 'runtimeData' object. This is token-expansive!",
                category: "Components",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""connectionDepth"": {
                            ""type"": ""integer"",
                            ""default"": 0,
                            ""description"": ""Depth of connections to include: 0 (default) only selected components; 1 includes directly connected components, etc.""
                        }
                    }
                }",
                execute: (toolCall) => this.GhGetToolAsync(toolCall, new[] { "+selected" }, null, true));

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
                execute: (toolCall) => this.GhGetToolAsync(toolCall, null, null, false));

            // Specialized wrapper: gh_get_by_guid_with_data
            yield return new AITool(
                name: "gh_get_by_guid_with_data",
                description: "Read specific components by GUID WITH their runtime data (volatile data - actual values flowing through outputs). Use this when you need to inspect computed results from known components. Returns GhJSON with an additional 'runtimeData' object. This is token-expansive!",
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
                            ""description"": ""Depth of connections to include: 0 (default) only specified components; 1 includes directly connected components, etc.""
                        }
                    },
                    ""required"": [""guidFilter""]
                }",
                execute: (toolCall) => this.GhGetToolAsync(toolCall, null, null, true));

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
                execute: (toolCall) => this.GhGetToolAsync(toolCall, new[] { "+error" }, null, false));

            // Specialized wrapper: gh_get_errors_with_data
            yield return new AITool(
                name: "gh_get_errors_with_data",
                description: "Read only components that have error messages WITH their runtime data (volatile data - actual values flowing through outputs). Use this when debugging broken components and you also need to inspect their computed results. Returns GhJSON plus a 'runtimeData' object. This is token-expansive!",
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
                execute: (toolCall) => this.GhGetToolAsync(toolCall, new[] { "+error" }, null, true));

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
                execute: (toolCall) => this.GhGetToolAsync(toolCall, new[] { "+disabled" }, null, false));

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
                execute: (toolCall) => this.GhGetToolAsync(toolCall, new[] { "+previewoff" }, null, false));

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
                execute: (toolCall) => this.GhGetToolAsync(toolCall, new[] { "+previewon" }, null, false));

            // Specialized wrapper: gh_get_start
            yield return new AITool(
                name: "gh_get_start",
                description: "Read only start nodes (components with no incoming connections - data sources like parameters, sliders, panels with internalized data). Use this to get a wide view of where data originates in the definition. Returns a GhJSON structure.",
                category: "Components",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""connectionDepth"": {
                            ""type"": ""integer"",
                            ""default"": 0,
                            ""description"": ""Depth of connections to include: 0 (default) only start nodes; 1 includes directly connected components; 2 includes two-level connected components, etc.""
                        }
                    }
                }",
                execute: (toolCall) => this.GhGetToolAsync(toolCall, null, new[] { "+startnodes" }, false));

            // Specialized wrapper: gh_get_start_with_data
            yield return new AITool(
                name: "gh_get_start_with_data",
                description: "Read start nodes (data sources) WITH their runtime data. Use this to inspect what initial values are feeding into the definition. Returns GhJSON with 'runtimeData'. This is token-expansive!",
                category: "Components",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""connectionDepth"": {
                            ""type"": ""integer"",
                            ""default"": 0,
                            ""description"": ""Depth of connections to include: 0 (default) only start nodes; 1 includes directly connected components, etc.""
                        }
                    }
                }",
                execute: (toolCall) => this.GhGetToolAsync(toolCall, null, new[] { "+startnodes" }, true));

            // Specialized wrapper: gh_get_end
            yield return new AITool(
                name: "gh_get_end",
                description: "Read only end nodes (components with no outgoing connections - data sinks like panels, preview components, bake components). Use this to get a wide view of the definition's outputs. Returns a GhJSON structure.",
                category: "Components",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""connectionDepth"": {
                            ""type"": ""integer"",
                            ""default"": 0,
                            ""description"": ""Depth of connections to include: 0 (default) only end nodes; 1 includes directly connected components; 2 includes two-level connected components, etc.""
                        }
                    }
                }",
                execute: (toolCall) => this.GhGetToolAsync(toolCall, null, new[] { "+endnodes" }, false));

            // Specialized wrapper: gh_get_end_with_data
            yield return new AITool(
                name: "gh_get_end_with_data",
                description: "Read end nodes (data sinks) WITH their runtime data. Use this to inspect the final computed outputs of the definition. Returns GhJSON with 'runtimeData'. This is token-expansive!",
                category: "Components",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""connectionDepth"": {
                            ""type"": ""integer"",
                            ""default"": 0,
                            ""description"": ""Depth of connections to include: 0 (default) only end nodes; 1 includes directly connected components, etc.""
                        }
                    }
                }",
                execute: (toolCall) => this.GhGetToolAsync(toolCall, null, new[] { "+endnodes" }, true));
        }

        /// <summary>
        /// Executes the Grasshopper get components tool with optional predefined filters.
        /// </summary>
        /// <param name="toolCall">The tool call containing parameters.</param>
        /// <param name="predefinedAttrFilters">Predefined attribute filters to apply (used by wrapper tools).</param>
        /// <param name="predefinedTypeFilters">Predefined type filters to apply (used by wrapper tools).</param>
        /// <param name="forceIncludeRuntimeData">When true, forces inclusion of runtime data regardless of parameter value.</param>
        /// <returns>Task that returns the result of the operation.</returns>
        private Task<AIReturn> GhGetToolAsync(AIToolCall toolCall, string[] predefinedAttrFilters = null, string[] predefinedTypeFilters = null, bool forceIncludeRuntimeData = false)
        {
            var output = new AIReturn() { Request = toolCall };

            try
            {
                // Local tool: we don't need provider/model/finish_reason metrics for validation
                toolCall.SkipMetricsValidation = true;

                AIInteractionToolCall toolInfo = toolCall.GetToolCall();
                var args = toolInfo.Arguments ?? new JObject();

                // Parse parameters
                var connectionDepth = args["connectionDepth"]?.ToObject<int>() ?? 0;
                var includeRuntimeData = forceIncludeRuntimeData || (args["includeRuntimeData"]?.ToObject<bool>() ?? false);
                Debug.WriteLine($"[gh_get] includeRuntimeData: {includeRuntimeData}, connectionDepth: {connectionDepth}");

                // Build the query using CanvasSelector
                var selector = CanvasSelector.FromActiveCanvas();

                // GUID restriction
                var guidStrings = args["guidFilter"]?.ToObject<List<string>>();
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

                // Type filters
                var typeTokens = predefinedTypeFilters
                    ?? args["typeFilter"]?.ToObject<string[]>();
                if (typeTokens != null)
                {
                    selector.WithTypes(typeTokens);
                }

                // Category filters
                var categoryTokens = args["categoryFilter"]?.ToObject<string[]>();
                if (categoryTokens != null)
                {
                    selector.WithCategories(categoryTokens);
                }

                // Attribute filters
                var attrTokens = predefinedAttrFilters
                    ?? args["attrFilters"]?.ToObject<string[]>();
                if (attrTokens != null)
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
                Debug.WriteLine($"[gh_get] Query returned {resultObjects.Count} objects");

                // Serialize the result
                var serOptions = new SerializationOptions
                {
                    IncludeConnections = true,
                    IncludeGroups = true,
                    IncludeInternalizedData = includeRuntimeData,
                    IncludeRuntimeMessages = false,
                    IncludeSelectedState = false,
                    AssignSequentialIds = true,
                };

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

                var json = GhJson.ToJson(document, new WriteOptions { Indented = false });

                var toolResult = new JObject
                {
                    ["names"] = JArray.FromObject(names),
                    ["guids"] = JArray.FromObject(guidList),
                    ["ghjson"] = json,
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
