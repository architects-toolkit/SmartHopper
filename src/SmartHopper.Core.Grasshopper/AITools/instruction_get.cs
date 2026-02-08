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
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Provides instruction bundles to the agent so the system prompt can remain short.
    /// </summary>
    public class instruction_get : IAIToolProvider
    {
        private const string ToolName = "instruction_get";

        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: ToolName,
                description: "Returns detailed operational instructions for SmartHopper. REQUIRED: Pass `topic` with one of: canvas, viewport, ghjson, selected, errors, locks, visibility, discovery, scripting, python, csharp, vb, ghpython, knowledge, mcneel-forum, research, web. Use this to retrieve guidance instead of relying on a long system prompt.",
                category: "Instructions",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""topic"": {
                            ""type"": ""string"",
                            ""description"": ""Which instruction bundle to return."",
                            ""enum"": [""canvas"", ""discovery"", ""scripting"", ""knowledge"", ""ghjson"", ""selected"", ""errors"", ""locks"", ""visibility""]
                        }
                    },
                    ""required"": [""topic""]
                }",
                execute: this.ExecuteAsync);
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
                var args = toolInfo.Arguments ?? new JObject();
                var topic = args["topic"]?.ToString() ?? string.Empty;

                var instructions = this.GetInstructions(topic);

                var toolResult = new JObject();
                toolResult.Add("topic", topic);
                toolResult.Add("instructions", instructions);

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

        private string GetInstructions(string topic)
        {
            switch (topic.Trim().ToLowerInvariant())
            {
                case "canvas":
                case "ghjson":
                case "selected":
                case "errors":
                case "locks":
                case "visibility":
                case "viewport":
                    return """
Canvas state reading:
- Use gh_report for a quick overview: object counts, topology, groups, scribbles, errors/warnings, viewport, and optional AI summary.
- Use gh_get_selected when the user refers to "this/these/selected".
- Use gh_get_in_view to see what is currently visible in the canvas viewport.
- Use gh_get_errors to locate broken definitions.
- Use gh_get_locked / gh_get_hidden / gh_get_visible for quick filters.
- Use gh_get_start / gh_get_end to get a wide view of data sources (startnodes) or outputs (endnodes).
- Use gh_get_start_with_data / gh_get_end_with_data to inspect initial values or final outputs with runtime data.
- Use gh_get_by_guid only when you already have GUIDs from prior steps.
- Use gh_get (generic) only when a specialized tool does not fit.

Node types terminology:
- startnodes: components with no incoming connections (data sources like parameters, sliders)
- endnodes: components with no outgoing connections (data sinks like panels, preview)
- middlenodes: components with both incoming and outgoing connections (processors)
- isolatednodes: components with neither incoming nor outgoing connections

Quick actions on selected components (no GUIDs needed):
- gh_group_selected
- gh_tidy_up_selected
- gh_lock_selected / gh_unlock_selected
- gh_hide_preview_selected / gh_show_preview_selected

Modifying canvas:
- gh_group, gh_move, gh_tidy_up, gh_component_toggle_lock, gh_component_toggle_preview
- gh_put: place components from GhJSON; when instanceGuid matches existing, it replaces it (prefer user confirmation).
""";

                case "discovery":
                    return """
Discovering available components:
1) gh_list_categories: discover available categories first (saves tokens).
2) gh_list_components: then search within categories.
   - Always pass includeDetails=['name','description','inputs','outputs'] unless you truly need more.
   - Always pass maxResults to prevent token overload.
   - Use categoryFilter with +/- tokens to narrow scope.
""";

                case "knowledge":
                case "mcneel-forum":
                case "research":
                case "web":
                    return """
Knowledge base workflow:
1) mcneel_forum_search: find candidate posts/topics.
2) mcneel_forum_topic_get / mcneel_forum_post_get: retrieve the minimum useful content.
3) mcneel_forum_topic_summarize / mcneel_forum_post_summarize: summarize and extract actionable steps.
4) web_generic_page_read: read docs/pages by URL before citing or relying on them.
""";

                case "scripting":
                case "python":
                case "python3":
                case "csharp":
                case "vb":
                case "ghpython":
                    return """
Scripting rules:
- When the user asks to CREATE or MODIFY a Grasshopper script component, use the scripting tools (do not only reply in natural language).
- All scripting happens inside Grasshopper script components, not an external environment.
- Do not propose or rely on traditional unit tests or external test projects.
- For manual inspection, instruct the user to open the script component editor (double-click in Grasshopper).
- Avoid copying full scripts from the canvas into chat (keep context small).
- Use fenced code blocks only when discussing a specific snippet or when an operation fails and the user must manually apply code.

Tools:
- script_generate: generate a new script component as GhJSON (not placed).
- script_review: review an existing script component by GUID.
- script_edit_and_replace_on_canvas: edit an existing script and replace it on the canvas in one call.

Required workflows:
- Create NEW script:
  1) script_generate
  2) gh_put (editMode=false)

- Edit EXISTING script:
  1) gh_get_selected (preferred) or gh_get_by_guid
  2) script_edit_and_replace_on_canvas

- Fix BUGS in script:
  1) gh_get_errors (or gh_get with categoryFilter=['+Script'])
  2) script_review
  3) script_edit_and_replace_on_canvas
""";

                default:
                    return "Unknown topic. Call the `instruction_get` function again and specify the `topic` argument. Valid topics are canvas, viewport, ghjson, selected, errors, locks, visibility, discovery, scripting, python, python3, csharp, vb, ghpython, knowledge, mcneel-forum, research, web.";
            }
        }
    }
}
