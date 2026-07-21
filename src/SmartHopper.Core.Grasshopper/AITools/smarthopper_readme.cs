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
    public class smarthopper_readme : IAIToolProvider
    {
        private const string ToolName = "smarthopper_readme";

        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: ToolName,
                description: "Returns detailed operational instructions for SmartHopper. REQUIRED: Pass `topic` with one of: canvas, ghjson, selected, errors, locks, visibility, discovery, scripting, python, csharp, vb, knowledge, providers, mcneel-forum, research, web. Use this to retrieve guidance instead of relying on a long system prompt.",
                category: "Instructions",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""topic"": {
                            ""type"": ""string"",
                            ""description"": ""Which instruction bundle to return."",
                            ""enum"": [""canvas"", ""discovery"", ""scripting"", ""knowledge"", ""ghjson"", ""selected"", ""errors"", ""locks"", ""visibility"", ""providers"", ""mcneel-forum"", ""ladybug-forum"", ""discourse-forum"", ""research"", ""web"", ""python"", ""csharp"", ""vb""]
                        }
                    },
                    ""required"": [""topic""]
                }",

                execute: this.ExecuteAsync,
                mutatesCanvas: false,
                tags: new[] { "instructions", "readme", "read-only" },
                outputSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""topic"": { ""type"": ""string"" },
                        ""instructions"": { ""type"": ""string"", ""description"": ""Markdown-formatted operational guidance for the agent."" }
                    },
                    ""required"": [""topic"", ""instructions""]
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
                case "selected":
                case "errors":
                case "locks":
                case "visibility":
                    return """
Canvas state reading:
- gh_report: generate a comprehensive markdown status report of the canvas (object counts, topology, groups, scribbles, viewport, errors/warnings, metadata). Optionally include an AI summary. Read-only.
- Use gh_get_selected when the user refers to "this/these/selected".
- Use gh_get_errors to locate broken definitions.
- Use gh_get_locked / gh_get_preview_off / gh_get_preview_on for quick attribute-based filters.
- Use gh_get_visible when the user refers to components currently on screen (viewport-based).
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
- gh_component_lock_selected / gh_component_unlock_selected
- gh_component_hide_preview_selected / gh_component_show_preview_selected

Modifying canvas:
- gh_group, gh_move, gh_tidy_up, gh_component_toggle_lock, gh_component_toggle_preview
- gh_put: place components from GhJSON; when instanceGuid matches existing, it replaces it (prefer user confirmation).
- gh_connect / gh_disconnect: wire or unwire existing components by GUID and parameter name.
- gh_smart_connect: AI-suggested wiring — provide component GUIDs and a purpose description; the AI proposes and executes connections.
- gh_clear: clear the canvas (optionally keep locked components); protected components are always preserved. Destructive — prefer user confirmation.
""";

                case "ghjson":
                    return """
GhJSON format documentation is maintained in the dedicated `smarthopper_ghjson_reference` tool.

Use `smarthopper_ghjson_reference` with one of these topics to get the authoritative GhJSON/GhPatch reference:
- overview
- specification
- ghpatch
- document_structure
- components
- connections
- groups
- data_types
- component_specific_formats
- validation
- examples

For canvas operations that use GhJSON (e.g., gh_put), use the `canvas` topic instead.
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

                case "providers":
                    return """
Provider and model configuration in SmartHopper:
- SmartHopper reads the default provider and model from the environment settings (SmartHopper settings in Rhino/Grasshopper).
- By default, all AI calls use the provider and model set in the environment. You do not need to set them on every component unless you want a per-component override.
- To discover the available providers and whether they are properly configured in this environment, call `get_available_providers`.
  The response includes a `configured` flag for each provider. Only those whose `configured` flag is `true` can be used to run AI calls.
- To list the models available for a specific provider, call `get_available_models` with the provider name.
- To override the provider and model on a component that supports provider selection (any `IProviderComponent`), use the `set_ai_provider_and_model` tool.
- To configure a provider, open the SmartHopper settings from the Rhino/Grasshopper menu and set the required fields:
  - Most providers require an API key.
  - Some local or custom providers may also require a base endpoint URL (e.g., `http://localhost:11434`).
""";

                case "knowledge":
                case "mcneel-forum":
                case "ladybug-forum":
                case "discourse-forum":
                case "research":
                case "web":
                    return """
Knowledge base workflow:

Discourse URL anatomy — IMPORTANT before choosing a tool:
- Topic URL: /t/{slug}/{topicId} or /t/{slug}/{topicId}/{postNumber}
  → topicId is the integer after the slug (e.g. 207407 in /t/my-topic/207407/44)
  → postNumber (e.g. 44) is the 1-based position within the topic, NOT a global post id
  → Use *_forum_topic_get with topic_id to fetch the topic; use max_posts to limit
- Global post id: the numeric "id" field in a post object returned by *_forum_post_get or *_forum_search
  → Only found in /posts/{id}.json API responses or the "id" key of search results
  → Use *_forum_post_get only when you have this global id
- Never pass a topicId or postNumber to *_forum_post_get — it will return the wrong post.

For McNeel Discourse forum (discourse.mcneel.com):
1) mcneel_forum_search: find candidate posts/topics.
2) mcneel_forum_topic_get (topic_id) to read all posts in a topic; or mcneel_forum_post_get (global post id) for a single post by its id field.
3) mcneel_forum_topic_summarize / mcneel_forum_post_summarize: summarize and extract actionable steps.

For Ladybug Tools Discourse forum (discourse.ladybug.tools):
1) ladybug_forum_search: find candidate posts/topics.
2) ladybug_forum_topic_get (topic_id) / ladybug_forum_post_get (global post id): retrieve the minimum useful content.
3) ladybug_forum_topic_summarize / ladybug_forum_post_summarize: summarize and extract actionable steps.

For any other Discourse forum (requires base_url parameter):
1) discourse_forum_search: find candidate posts/topics (requires base_url).
2) discourse_forum_topic_get (topic_id) / discourse_forum_post_get (global post id): retrieve content (requires base_url).
3) discourse_forum_topic_summarize / discourse_forum_post_summarize: summarize (requires base_url).

For general content:
- web2md: read docs/pages by URL before citing or relying on them. Note: web2md may fail or return placeholder text for pages that require JavaScript rendering (e.g. GitHub release pages served as SPAs). Prefer dedicated API tools when available.
- file2md: convert local files to Markdown given a file path.
""";

                case "scripting":
                case "python":
                case "csharp":
                case "vb":
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
- script_generate_and_place_on_canvas: generate a new script component and place it on the canvas in one call.
- script_review: review an existing script component by GUID.
- script_edit_and_replace_on_canvas: edit an existing script and replace it on the canvas in one call.

For canonical step-by-step workflows (create, edit, debug), call smarthopper_workflows with:
- workflow: create_script
- workflow: edit_script
- workflow: debug_script
""";

                default:
                    return "Unknown topic. Call the `smarthopper_readme` function again and specify the `topic` argument. Valid topics are: canvas, ghjson, selected, errors, locks, visibility, discovery, scripting, python, csharp, vb, knowledge, providers, mcneel-forum, ladybug-forum, discourse-forum, research, web. For canonical step-by-step workflows, call `smarthopper_workflows`.";
            }
        }
    }
}
