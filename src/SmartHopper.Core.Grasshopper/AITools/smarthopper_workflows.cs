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
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Returns canonical SmartHopper workflows so an MCP client can discover how to chain tools.
    /// </summary>
    public class smarthopper_workflows : IAIToolProvider
    {
        private const string ToolName = "smarthopper_workflows";

        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: ToolName,
                description: "Returns canonical SmartHopper tool workflows. Pass `workflow` to get detailed steps for a specific workflow, or omit it to list available workflows. Use this to understand how to chain tools without reading source code.",
                category: "Instructions",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""workflow"": {
                            ""type"": ""string"",
                            ""description"": ""Optional workflow name to retrieve. Omit to list all workflows."",
                            ""enum"": [""inspect_canvas"", ""edit_script"", ""create_script"", ""debug_script"", ""organize_canvas"", ""place_components"", ""search_knowledge"", ""compare_definitions"", ""apply_patch""]
                        }
                    }
                }",
                execute: this.ExecuteAsync,
                mutatesCanvas: false,
                tags: new[] { "instructions", "workflow", "read-only" },
                outputSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""workflows"": {
                            ""type"": ""array"",
                            ""items"": {
                                ""type"": ""object"",
                                ""properties"": {
                                    ""name"": { ""type"": ""string"" },
                                    ""description"": { ""type"": ""string"" },
                                    ""steps"": {
                                        ""type"": ""array"",
                                        ""items"": { ""type"": ""string"" }
                                    }
                                }
                            }
                        }
                    }
                }");
        }

        private Task<AIReturn> ExecuteAsync(AIToolCall toolCall)
        {
            var output = new AIReturn()
            {
                Request = toolCall,
            };

            try
            {
                toolCall.SkipMetricsValidation = true;

                var toolInfo = toolCall.GetToolCall();
                var args = toolInfo.GetArgumentsOrEmpty();
                var workflow = args["workflow"]?.ToString() ?? string.Empty;

                var workflows = this.GetWorkflows(workflow);
                var toolResult = new JObject
                {
                    ["workflows"] = new JArray(workflows),
                };

                var toolBody = AIBodyBuilder.Create()
                    .AddToolResult(toolResult, id: toolInfo?.Id, name: ToolName)
                    .Build();

                output.CreateSuccess(toolBody);
                return Task.FromResult(output);
            }
            catch (Exception ex)
            {
                output.CreateError($"Error: {ex.Message}");
                return Task.FromResult(output);
            }
        }

        private List<JObject> GetWorkflows(string filter)
        {
            var all = new List<(string name, string description, string[] steps)>
            {
                (
                    "inspect_canvas",
                    "Read the current state of the Grasshopper canvas with the right level of detail.",
                    new[]
                    {
                        "Call gh_get_selected when the user refers to 'selected', 'this', or 'these'.",
                        "Call gh_get_errors to locate broken definitions.",
                        "Call *_with_data tools (e.g. gh_get_start_with_data, gh_get_end_with_data) when computed values are needed.",
                        "Call gh_get_start to find data sources (parameters, sliders, first components in the flow, etc.) without runtime data.",
                        "Call gh_get_end to find output sinks (panels, previews, last component in the flow, etc.) without runtime data.",
                        "Call gh_get_by_guid when GUIDs are already known.",
                        "Use gh_get (generic) as a fallback only when no specialized gh_get_* variant fits.",
                    }),
                (
                    "edit_script",
                    "Modify an existing Grasshopper script component.",
                    new[]
                    {
                        "Identify the script component via gh_get_selected or gh_get_by_guid.",
                        "Call script_edit_and_replace_on_canvas with the instanceGuid and instructions.",
                        "Alternatively, call script_edit on the GhJSON, then gh_put with editMode=true.",
                    }),
                (
                    "create_script",
                    "Create a new Grasshopper script component from natural language.",
                    new[]
                    {
                        "Call script_generate with the instructions and preferred language.",
                        "Call smarthopper_ghjson_reference with topic 'specification' or 'components' when reviewing or adjusting the generated GhJSON.",
                        "Call gh_put with the returned GhJSON and editMode=false.",
                    }),
                (
                    "debug_script",
                    "Find and fix problems in a Grasshopper script component.",
                    new[]
                    {
                        "Call gh_get_errors or gh_get with categoryFilter=['+Script'] to locate broken script components.",
                        "Call script_review on the broken component's GUID.",
                        "Call script_edit_and_replace_on_canvas to apply the fix.",
                    }),
                (
                    "organize_canvas",
                    "Tidy up selected components on the Grasshopper canvas.",
                    new[]
                    {
                        "Call gh_get_selected if the user already selected the components; otherwise use gh_get (or a specialized gh_get variant) to obtain the component GUIDs.",
                        "Call gh_tidy_up with the GUIDs, or gh_tidy_up_selected if the components are already selected.",
                        "Optionally call gh_group with the GUIDs to visually highlight the changed area.",
                    }),
                (
                    "place_components",
                    "Add new components to the canvas from a description or GhJSON.",
                    new[]
                    {
                        "To generate a new network from a description, call gh_generate with instructions. For complex networks, call smarthopper_ghjson_reference with topic 'specification' or 'components' first.",
                        "To generate and place in one step, use gh_generate_and_place_on_canvas.",
                        "Once the GhJSON is ready, call gh_put with editMode=false to place components on the canvas.",
                        "Use gh_connect to wire the new components to existing ones.",
                    }),
                (
                    "search_knowledge",
                    "Search McNeel or Ladybug Discourse forums for answers.",
                    new[]
                    {
                        "Call mcneel_forum_search or ladybug_forum_search with the query.",
                        "Retrieve promising topics/posts with mcneel_forum_topic_get / mcneel_forum_post_get.",
                        "Summarize with mcneel_forum_topic_summarize / mcneel_forum_post_summarize.",
                        "For general web pages, use web2md.",
                    }),
                (
                    "compare_definitions",
                    "Compare two Grasshopper definitions and describe the differences.",
                    new[]
                    {
                        "Get the two GhJSON documents with gh_get or gh_get_by_guid.",
                        "Call gh_diff with the two documents.",
                        "Review the returned .ghpatch or summary.",
                    }),
                (
                    "apply_patch",
                    "Apply a structured .ghpatch change to a Grasshopper definition.",
                    new[]
                    {
                        "Obtain the base GhJSON document and a .ghpatch document (e.g., from gh_diff).",
                        "Call smarthopper_ghjson_reference with topic 'ghpatch' or 'validation' when inspecting or editing the patch by hand.",
                        "Call gh_patch_validate on the patch first.",
                        "Call gh_patch_apply with the base GhJSON and the patch.",
                        "Review any conflicts reported by gh_patch_apply.",
                        "Call gh_put with the resulting GhJSON and editMode=true to update the canvas.",
                    }),
            };

            var result = new List<JObject>();
            foreach (var (name, description, steps) in all)
            {
                if (string.IsNullOrWhiteSpace(filter) || string.Equals(name, filter, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(new JObject
                    {
                        ["name"] = name,
                        ["description"] = description,
                        ["steps"] = new JArray(steps),
                    });
                }
            }

            return result;
        }
    }
}
