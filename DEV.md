# Developer Reference

This document aggregates development-facing information.

- Table of contents
  - [Development Status](#-development-status)
  - [AI Tools](#ai-tools)
  - [Available Providers](#️-available-providers)
  - [Default Models by Provider](#-default-models-by-provider)
  - [Supported Data Types](#-supported-data-types)

## 📊 Development Status

### Components

| Component | Category | Planned | In Progress | Testing | Released 🎉 |
| --------- | -------- | :-------: | :-----------: | :-------: | :------------------------: |
| Get GhJSON (GhGet)<br><sub>Read the current Grasshopper file and convert it to GhJSON format. Filter by runtime messages, component state, preview, type, category, and more.</sub> | Grasshopper | ⚪ | 🟡 | 🟠 | 🟢 |
| Place GhJSON (GhPut)<br><sub>Place components on the canvas from a GhJSON format</sub> | Grasshopper | ⚪ | 🟡 | 🟠 | 🟢 |
| Merge GhJSON (GhMerge)<br><sub>Merge two GhJSON documents into one, with the target document taking priority on conflicts.</sub> | Grasshopper | ⚪ | 🟡 | 🟠 | 🟢 |
| Retrieve Components (GhRetrieveComponents)<br><sub>Retrieve all available Grasshopper components in your environment as JSON with optional category filter.</sub> | Grasshopper | ⚪ | 🟡 | 🟠 | 🟢 |
| Tidy Up (GhTidyUp)<br><sub>Organize selected components into a tidy grid layout based on dependencies.</sub> | Grasshopper | ⚪ | 🟡 | 🟠 | 🟢 |
| AI Canvas Report (AIGhReport)<br><sub>Generate a comprehensive canvas status report including object counts, topology, groups, scribbles, viewport contents, metadata, and runtime messages. Optionally includes an AI-generated summary.</sub> | Grasshopper | ⚪ | 🟡 | 🟠 | 🟢 |
| AI Smart Connect (AIGhConnect)<br><sub>Use AI to intelligently connect selected Grasshopper components based on a described purpose. Select components, describe the wiring goal, and let AI suggest and create connections.</sub> | Grasshopper | ⚪ | 🟡 | 🟠 | 🟢 |
| AI GroupTitle (AIGroupTitle)<br><sub>Group components and set a meaningful title to the group</sub> | Grasshopper | ⚪ | - | - | - |
| AI Grasshopper Generate (AIGhGenerate)<br><sub>Automatically generate Grasshopper definitions using AI</sub> | Grasshopper | ⚪ | - | - | - |
| Save GhJSON file (SaveGhJSON)<br><sub>Save the current Grasshopper file as a GhJSON format</sub> | Grasshopper | ⚪ | - | - | - |
| Load GhJSON file (LoadGhJSON)<br><sub>Load a GhJSON file and convert it to a Grasshopper document</sub> | Grasshopper | ⚪ | - | - | - |
| AI Chat (AIChat)<br><sub>Interactive AI-powered conversational interface with tool calling</sub> | AI | ⚪ | 🟡 | 🟠 | 🟢 |
| AI File Context (AIFileContext)<br><sub>Set a context for the current document</sub> | AI | ⚪ | 🟡 | 🟠 | 🟢 |
| AI Models (AIModels)<br><sub>Retrieve the list of available models from the selected AI provider</sub> | AI | ⚪ | 🟡 | 🟠 | 🟢 |
| Context Parameters (ContextParameters)<br><sub>Set context parameters for the AI component</sub> | AI | ⚪ | - | - | - |
| AI Text To Boolean (AIText2Boolean)<br><sub>Return a boolean from a text content using AI-powered checks</sub> | Text | ⚪ | 🟡 | 🟠 | 🟢 |
| AI Text To Text (AIText2Text)<br><sub>Generate text content using AI</sub> | Text | ⚪ | 🟡 | 🟠 | 🟢 |
| AI Text To Text List (AIText2TextList)<br><sub>Generate lists of text content using AI</sub> | Text | ⚪ | 🟡 | 🟠 | 🟢 |
| AI Text To Image (AIText2Img)<br><sub>Generate images using AI</sub> | Img | ⚪ | 🟡 | 🟠 | 🟢 |
| Image Viewer (ImageViewer)<br><sub>Display bitmap images on the canvas and save them to disk</sub> | Img | ⚪ | 🟡 | 🟠 | 🟢 |
| AI Script Review (AIScriptReview)<br><sub>Review script components using AI-based static analysis</sub> | Script | ⚪ | 🟡 | 🟠 | 🟢 |
| AI Script Generate (AIScriptGenerate)<br><sub>Create or edit Grasshopper script components using AI. Supports create mode (from prompts) and edit mode (from selected components).</sub> | Script | ⚪ | 🟡 | 🟠 | 🟢 |
| AI List To Boolean (AIList2Boolean)<br><sub>Return a boolean from a list of elements using AI analysis</sub> | List | ⚪ | 🟡 | 🟠 | 🟢 |
| AI List Filter (AIListFilter)<br><sub>Process items in lists (reorder, shuffle, filter, etc.) based on AI-driven rules</sub> | List | ⚪ | 🟡 | 🟠 | 🟢 |
| Web To Markdown (Web2Md)<br><sub>Convert web pages (URLs) to Markdown with specialized handlers for Wikipedia, GitHub, Discourse, Stack Exchange</sub> | Knowledge | ⚪ | 🟡 | 🟠 | 🟢 |
| File To Markdown (File2Md)<br><sub>Convert local files (PDF, DOCX, XLSX, PPTX, HTML, CSV, JSON, XML, TXT, EML, EPUB, RTF) to Markdown</sub> | Knowledge | ⚪ | 🟡 | 🟠 | 🟢 |
| Discourse Search (DiscourseSearch)<br><sub>Search any Discourse forum with configurable limit</sub> | Knowledge | ⚪ | 🟡 | 🟠 | 🟢 |
| Discourse Post Get (DiscoursePostGet)<br><sub>Retrieve a Discourse forum post by ID</sub> | Knowledge | ⚪ | 🟡 | 🟠 | 🟢 |
| Discourse Post Open (DiscoursePostOpen)<br><sub>Open a Discourse forum post URL in the default browser</sub> | Knowledge | ⚪ | 🟡 | 🟠 | 🟢 |
| Discourse Post Deconstruct (DiscoursePostDeconstruct)<br><sub>Deconstruct Discourse forum post JSON into individual fields</sub> | Knowledge | ⚪ | 🟡 | 🟠 | 🟢 |
| AI Discourse Post Summarize (AIDiscoursePostSummarize)<br><sub>Generate AI summary of a Discourse forum post</sub> | Knowledge | ⚪ | 🟡 | 🟠 | 🟢 |
| AI Discourse Topic Summarize (AIDiscourseTopicSummarize)<br><sub>Generate AI summary of a Discourse forum topic</sub> | Knowledge | ⚪ | 🟡 | 🟠 | 🟢 |
| McNeel Forum Search (McNeelForumSearch)<br><sub>Search McNeel Discourse forum with configurable limit</sub> | Knowledge | ⚪ | 🟡 | 🟠 | 🟢 |
| McNeel Forum Post Get (McNeelForumPostGet)<br><sub>Retrieve a McNeel Discourse forum post by ID</sub> | Knowledge | ⚪ | 🟡 | 🟠 | 🟢 |
| McNeel Forum Post Open (McNeelForumPostOpen)<br><sub>Open a McNeel forum post URL in the default browser</sub> | Knowledge | ⚪ | 🟡 | 🟠 | 🟢 |
| AI McNeel Forum Post Summarize (AIMcNeelForumPostSummarize)<br><sub>Generate AI summary of a McNeel Discourse forum post</sub> | Knowledge | ⚪ | 🟡 | 🟠 | 🟢 |
| AI McNeel Forum Topic Summarize (AIMcNeelForumTopicSummarize)<br><sub>Generate AI summary of a McNeel Discourse forum topic</sub> | Knowledge | ⚪ | 🟡 | 🟠 | 🟢 |
| Ladybug Forum Search (LadybugForumSearch)<br><sub>Search Ladybug Tools Discourse forum with configurable limit</sub> | Knowledge | ⚪ | 🟡 | 🟠 | 🟢 |
| Ladybug Forum Post Get (LadybugForumPostGet)<br><sub>Retrieve a Ladybug forum post by ID</sub> | Knowledge | ⚪ | 🟡 | 🟠 | 🟢 |
| Ladybug Forum Post Open (LadybugForumPostOpen)<br><sub>Open a Ladybug forum post URL in the default browser</sub> | Knowledge | ⚪ | 🟡 | 🟠 | 🟢 |
| AI Ladybug Forum Post Summarize (AILadybugForumPostSummarize)<br><sub>Generate AI summary of a Ladybug forum post</sub> | Knowledge | ⚪ | 🟡 | 🟠 | 🟢 |
| AI Ladybug Forum Topic Summarize (AILadybugForumTopicSummarize)<br><sub>Generate AI summary of a Ladybug forum topic</sub> | Knowledge | ⚪ | 🟡 | 🟠 | 🟢 |
| Deconstruct Metrics (DeconstructMetrics)<br><sub>Break down the usage metrics into individual values</sub> | Misc | ⚪ | 🟡 | 🟠 | 🟢 |
| AI Text To JSON (AIText2Json)<br><sub>Generate structured JSON from a prompt using AI with JSON Schema validation</sub> | JSON | ⚪ | 🟡 | - | - |
| JSON Schema (JsonSchema)<br><sub>Build a JSON Schema from property definitions with nested object/array support via dot-notation</sub> | JSON | ⚪ | 🟡 | - | - |
| JSON Schema Property (JsonSchemaProp)<br><sub>Build scalar property definitions for JSON Schema using individual inputs</sub> | JSON | ⚪ | 🟡 | - | - |
| JSON Schema Property Object (JsonSchemaPropObj)<br><sub>Build object property definitions with sub-properties for JSON Schema</sub> | JSON | ⚪ | 🟡 | - | - |
| JSON Schema Property Array (JsonSchemaPropArr)<br><sub>Build array property definitions with configurable item type for JSON Schema</sub> | JSON | ⚪ | 🟡 | - | - |
| JSON Object (JsonObject)<br><sub>Create a JSON object from key-value pairs with auto-coerced values</sub> | JSON | ⚪ | 🟡 | - | - |
| JSON Array (JsonArray)<br><sub>Create a JSON array from a list of items with auto-coerced values</sub> | JSON | ⚪ | 🟡 | - | - |
| JSON Array To Text List (JsonArray2Text)<br><sub>Parse a JSON array string into a Grasshopper text list</sub> | JSON | ⚪ | 🟡 | - | - |
| JSON To Text (Json2Text)<br><sub>Serialize a JSON value to a string with optional pretty-print</sub> | JSON | ⚪ | 🟡 | - | - |
| JSON Get Value (JsonGetValue)<br><sub>Extract a nested value from JSON using dot-notation path</sub> | JSON | ⚪ | 🟡 | - | - |
| JSON Merge (JsonMerge)<br><sub>Merge multiple JSON objects via shallow merge (last-wins)</sub> | JSON | ⚪ | 🟡 | - | - |

### AI Tools

AI Tools are the interface between AI and Grasshopper, allowing to, for example,
read your selected components, get the available Grasshopper components, or write
a new script. All these tools are available to the provider to use while chatting
in the AI Chat component.

| Tool Name | Category | Description | Planned | In Progress | Testing | Released 🎉 |
| Tool Name | Category | Description | Planned | In Progress | Testing | Released |
|-----------|----------|-------------|:-------:|:-----------:|:-------:|:--------:|
| `text2boolean` | DataProcessing | Evaluates a text against a true/false question with optional fallback value | ⚪ | 🟡 | 🟠 | 🟢 |
| `text2text` | DataProcessing | Generates text based on a prompt and optional instructions | ⚪ | 🟡 | 🟠 | 🟢 |
| `text2img` | ImageProcessing | Generates an image based on a text prompt using AI image generation models | ⚪ | 🟡 | 🟠 | 🟢 |
| `textlist2boolean` | DataProcessing | Evaluates a list based on a natural language question with optional fallback value | ⚪ | 🟡 | 🟠 | 🟢 |
| `list_filter` | DataProcessing | Manipulates a list based on natural language criteria: filter, sort, reorder, select, shuffle, expand, or rearrange items | ⚪ | 🟡 | 🟠 | 🟢 |
| `text2textlist` | DataProcessing | Generates a list of items based on a prompt, count and type | ⚪ | 🟡 | 🟠 | 🟢 |
| `img2text` | Img | Describes or analyzes an image using a vision AI model. Provide either an image URL or base64-encoded image data. Returns a text description of the image content. | ⚪ | 🟡 | 🟠 | 🟢 |
| `text2json` | DataProcessing | Generates a JSON object from a prompt, conforming strictly to a provided JSON Schema | ⚪ | 🟡 | - | - |
| `get_input` | DataProcessing | Send data from Grasshopper to AI Chat | ⚪ | - | - | - |
| `get_output` | DataProcessing | Receive data from AI Chat to Grasshopper | ⚪ | - | - | - |
| `get_available_providers` | Providers | Retrieve the list of enabled AI providers registered in SmartHopper, including a `configured` flag that reflects whether the provider has the required settings in the current environment. | ⚪ | 🟡 | 🟠 | - |
| `get_available_models` | Providers | Retrieve the list of available models for a given AI provider. Uses live provider APIs when possible and falls back to the static model list. | ⚪ | 🟡 | 🟠 | - |
| `script_review` | Scripting | Return a code review for the script component specified by its GUID. | ⚪ | 🟡 | 🟠 | 🟢 |
| `script_generate` | Hidden | Generate a new Grasshopper script component from natural language instructions. Returns GhJSON representing the script component (does not place it on canvas). | ⚪ | 🟡 | 🟠 | 🟢 |
| `script_generate_and_place_on_canvas` | Scripting | Generate a new Grasshopper script component from natural language instructions and place it on the canvas. This wrapper combines script_generate and gh_put into a single operation. | ⚪ | 🟡 | 🟠 | 🟢 |
| `script_edit` | Hidden | Edit an existing Grasshopper script component based on instructions. Takes GhJSON input and returns updated GhJSON (does not modify canvas). | ⚪ | 🟡 | 🟠 | 🟢 |
| `script_edit_and_replace_on_canvas` | Scripting | Edit an existing Grasshopper script component by instance GUID and replace it on the canvas. This wrapper automatically retrieves the component GhJSON (gh_get_by_guid), calls script_edit, and then gh_put with editMode=true. | ⚪ | 🟡 | 🟠 | 🟢 |
| `smarthopper_readme` | Instructions | Returns detailed operational instructions for SmartHopper. REQUIRED: Pass `topic` with one of: canvas, ghjson, selected, errors, locks, visibility, discovery, scripting, python, csharp, vb, knowledge, mcneel-forum, research, web. Use this to retrieve guidance instead of relying on a long system prompt. | ⚪ | 🟡 | 🟠 | 🟢 |
| `smarthopper_workflows` | Instructions | Documents common SmartHopper tool sequences and workflows for the AI assistant. Use this to discover recommended tool call patterns for tasks like auditing the canvas, editing scripts, or retrieving web knowledge. | ⚪ | 🟡 | 🟠 | 🟢 |
| `smarthopper_tool_help` | Instructions | Provides detailed usage help for other SmartHopper tools, including parameter descriptions, output shape, and hints. Use this to understand how to call a specific tool correctly. | ⚪ | 🟡 | 🟠 | 🟢 |
| `smarthopper_ghjson_reference` | Instructions | Returns GhJSON and GhPatch format reference documentation. Pass `topic` to retrieve the full specification or a focused section. Use this whenever you need to generate, edit, or validate GhJSON/GhPatch documents instead of relying on internalized format knowledge. | ⚪ | 🟡 | 🟠 | 🟢 |
| `web2md` | Knowledge | Convert a web page (URL) to Markdown text. Supports Wikipedia/Wikimedia, Discourse forums, GitHub/GitLab files, Stack Exchange questions, and generic webpages. Respects robots.txt. Use this when you need to read the contents of a web page. | ⚪ | 🟡 | 🟠 | 🟢 |
| `file2md` | Knowledge | Convert a local file (PDF, DOCX, XLSX, PPTX, HTML, CSV, JSON, XML, TXT, EML, EPUB, RTF, etc.) to Markdown text. Use this when you need to read the contents of a file that the user has mentioned or referenced. | ⚪ | 🟡 | 🟠 | 🟢 |
| `discourse_forum_search` | Knowledge | Search Discourse forum posts by query and return matching results. | ⚪ | 🟡 | 🟠 | 🟢 |
| `discourse_forum_post_get` | Knowledge | Retrieve a filtered Discourse forum post by ID (username, date, title, raw markdown). | ⚪ | 🟡 | 🟠 | 🟢 |
| `discourse_forum_topic_get` | Knowledge | Retrieve all posts in a Discourse forum topic by topic ID (title, URL, posts array). | ⚪ | 🟡 | 🟠 | 🟢 |
| `discourse_forum_post_summarize` | Knowledge | Generate a concise summary of one or more Discourse forum posts by ID. | ⚪ | 🟡 | 🟠 | 🟢 |
| `discourse_forum_topic_summarize` | Knowledge | Generate a concise summary of a Discourse forum topic by ID, based on its posts. | ⚪ | 🟡 | 🟠 | 🟢 |
| `mcneel_forum_search` | Knowledge | Search McNeel forum posts by query and return matching results. | ⚪ | 🟡 | 🟠 | 🟢 |
| `mcneel_forum_post_get` | Knowledge | Retrieve a filtered McNeel forum post by ID (username, date, title, raw markdown). | ⚪ | 🟡 | 🟠 | 🟢 |
| `mcneel_forum_topic_get` | Knowledge | Retrieve all posts in a McNeel forum topic by topic ID (title, URL, posts array). | ⚪ | 🟡 | 🟠 | 🟢 |
| `mcneel_forum_post_summarize` | Knowledge | Generate a concise summary of one or more McNeel forum posts by ID. | ⚪ | 🟡 | 🟠 | 🟢 |
| `mcneel_forum_topic_summarize` | Knowledge | Generate a concise summary of a McNeel forum topic by ID, based on its posts. | ⚪ | 🟡 | 🟠 | 🟢 |
| `ladybug_forum_search` | Knowledge | Search Ladybug Tools forum posts by query and return matching results. | ⚪ | 🟡 | 🟠 | 🟢 |
| `ladybug_forum_post_get` | Knowledge | Retrieve a filtered Ladybug Tools forum post by ID (username, date, title, raw markdown). | ⚪ | 🟡 | 🟠 | 🟢 |
| `ladybug_forum_topic_get` | Knowledge | Retrieve all posts in a Ladybug Tools forum topic by topic ID (title, URL, posts array). | ⚪ | 🟡 | 🟠 | 🟢 |
| `ladybug_forum_post_summarize` | Knowledge | Generate a concise summary of one or more Ladybug Tools forum posts by ID. | ⚪ | 🟡 | 🟠 | 🟢 |
| `ladybug_forum_topic_summarize` | Knowledge | Generate a concise summary of a Ladybug Tools forum topic by ID, based on its posts. | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_list_categories` | ComponentsRetrieval | Discover what component categories are available in the user's Grasshopper installation (e.g., 'Maths', 'Curve', 'Surface'). Use this before gh_list_components to narrow your search. Apply filters to find specific categories and save tokens. | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_list_components` | ComponentsRetrieval | Search for available Grasshopper components by category or name to find what the user can use. Returns component details including inputs/outputs. IMPORTANT: Use includeDetails parameter to request only needed fields (e.g., ['name','description','inputs','outputs']) to avoid token waste. Use maxResults to limit output. | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_get` | Components | Read the current Grasshopper file with optional filters. By default, it returns all components. Returns a GhJSON structure of the file. | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_get_selected` | Components | Read only the selected components from the Grasshopper canvas. Use this when the user asks about 'selected', 'this', or 'these' components. Returns a GhJSON structure. | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_get_selected_with_data` | Components | Read selected components WITH their runtime data (volatile data - actual values flowing through outputs). Use this when you need to inspect computed results, count items, or check actual output values. Returns GhJSON with an additional 'runtimeData' object. This is token-expansive! | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_get_by_guid` | Components | Read specific components by their GUIDs. Use this when you have component GUIDs from a previous query. Returns a GhJSON structure. | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_get_by_guid_with_data` | Components | Read specific components by GUID WITH their runtime data (volatile data - actual values flowing through outputs). Use this when you need to inspect computed results from known components. Returns GhJSON with an additional 'runtimeData' object. This is token-expansive! | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_get_visible` | Components | Read only components currently visible in the canvas viewport. Use this when the user refers to 'on screen', 'visible', or 'what I can see'. Returns a GhJSON structure. | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_get_errors` | Components | Read only components that have error messages. Use this when debugging or when the user asks about errors or broken components. Returns a GhJSON structure. | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_get_errors_with_data` | Components | Read only components that have error messages WITH their runtime data (volatile data - actual values flowing through outputs). Use this when debugging broken components and you also need to inspect their computed results. Returns GhJSON plus a 'runtimeData' object. This is token-expansive! | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_get_locked` | Components | Read only locked (disabled) components from the Grasshopper canvas. Use this when the user asks about locked or disabled components. Returns a GhJSON structure. | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_get_preview_off` | Components | Read only components with preview turned off (hidden geometry). Use this when the user asks about hidden components or components with disabled preview. Returns a GhJSON structure. | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_get_preview_on` | Components | Read only components with preview turned on (visible geometry). Use this when the user asks about visible components or components with enabled preview. Returns a GhJSON structure. | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_get_start` | Components | Read only start nodes (components with no incoming connections - data sources like parameters, sliders, panels with internalized data). Use this to get a wide view of where data originates in the definition. Returns a GhJSON structure. | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_get_start_with_data` | Components | Read start nodes (data sources) WITH their runtime data. Use this to inspect what initial values are feeding into the definition. Returns GhJSON with 'runtimeData'. This is token-expansive! | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_get_end` | Components | Read only end nodes (components with no outgoing connections - data sinks like panels, preview components, bake components). Use this to get a wide view of the definition's outputs. Returns a GhJSON structure. | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_get_end_with_data` | Components | Read end nodes (data sinks) WITH their runtime data. Use this to inspect the final computed outputs of the definition. Returns GhJSON with 'runtimeData'. This is token-expansive! | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_put` | Components | Add new components to the canvas from GhJSON format. Use this to create component networks, add missing components, or build parametric definitions. The GhJSON must include component types, positions, and connections. | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_merge` | Components | Merge two GhJSON documents into one. The target document takes priority on conflicts (duplicate components by GUID are skipped from source). Connections and groups from both documents are combined with proper ID remapping and deduplication. | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_diff` | Components | Diff two GhJSON documents and produce a structured `.ghpatch` document describing the differences (added/removed/modified components, connections, groups, and metadata). Components are matched by instanceGuid, then id, then structural fingerprint (componentGuid + name + optional pivot). Connections are matched by their endpoints (paramName preferred, paramIndex fallback). By default, runtime messages, metadata counters and metadata timestamps are ignored. | ⚪ | 🟡 | - | - |
| `gh_patch_apply` | Components | Apply a `.ghpatch` patch document to a base GhJSON document. Components are matched by instanceGuid, then id, then structural fingerprint. By default, the patch's recorded base checksum is verified against the supplied base document — on mismatch, the apply is refused (no partial application). Conflicts (match not found, connection already present, dangling group members, ...) are recorded in the result. | - | - | - | - |
| `gh_patch_validate` | Components | Structurally validate a `.ghpatch` document. Checks the patch kind, that components/groups in remove/modify ops carry at least one identity field, and that connections have valid endpoints. | ⚪ | 🟡 | - | - |
| `gh_component_toggle_preview` | Components | Show or hide component geometry preview in the Rhino viewport. Hiding preview improves performance for complex definitions. Only affects components that generate geometry. Requires component GUIDs from gh_get. | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_component_hide_preview_selected` | Components | Hide geometry preview for currently selected components. Quick way to hide preview for selected items without needing to specify GUIDs manually. Improves performance for complex definitions. | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_component_show_preview_selected` | Components | Show geometry preview for currently selected components. Quick way to enable preview for selected items without needing to specify GUIDs manually. | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_component_toggle_lock` | Components | Lock (disable) or unlock (enable) components. Locked components don't execute and show as grayed out. Use this to temporarily disable parts of a definition without deleting them. Requires component GUIDs from gh_get. | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_component_lock_selected` | Components | Lock (disable) currently selected components. Quick way to disable selected items without needing to specify GUIDs manually. Locked components don't execute and show as grayed out. | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_component_unlock_selected` | Components | Unlock (enable) currently selected components. Quick way to enable selected items without needing to specify GUIDs manually. Unlocked components will execute normally. | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_move` | Components | Reposition components on the canvas by specifying target coordinates. Use absolute coordinates (canvas position) or relative offsets (move by delta). Useful for organizing layouts or separating component groups. Requires component GUIDs from gh_get. | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_tidy_up` | Components | Automatically arrange components into a clean grid layout respecting data flow direction. Organizes components left-to-right based on their connections. Use this to clean up messy definitions. Requires component GUIDs from gh_get. | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_tidy_up_selected` | Components | Organize currently selected components into a tidy grid layout. Quick way to clean up selected items without needing to specify GUIDs manually. Arranges components left-to-right based on connections. | - | - | - | - |
| `gh_generate` | NotTested | Generate GhJSON for creating a set of Grasshopper components by name and parameters. Returns a valid GhJSON structure that can be passed to gh_put to place components on canvas. Use this to create individual components or small networks when you know the exact component names. For complex networks, consider using the full gh_put workflow with AI-generated GhJSON. | ⚪ | 🟡 | 🟠 | - |
| `gh_generate_and_place_on_canvas` | Components | Generate a GhJSON document from instructions and immediately place it on the canvas. This wraps gh_generate followed by gh_put with editMode=false. Example: gh_generate_and_place_on_canvas({ instructions: 'Create a number slider connected to a panel' }). | ⚪ | 🟡 | 🟠 | - |
| `gh_connect` | NotTested | Connect Grasshopper components together by creating wires between outputs and inputs. Use this to establish data flow between existing components on the canvas. Requires component GUIDs (use gh_get_selected or gh_get to find them first). | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_disconnect` | NotTested | Disconnect Grasshopper components by removing wires between outputs and inputs. Use this to break data flow between existing components on the canvas. Requires component GUIDs (use gh_get_selected or gh_get to find them first). | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_group` | Components | Create a visual group container around components to organize and annotate them. Use this to highlight related components, mark areas of interest, or add notes to the canvas. Requires component GUIDs from gh_get. | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_group_selected` | Components | Create a group around currently selected components. Quick way to organize selected items without needing to specify GUIDs manually. | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_parameter_data_mapping_none` | Parameters | Set a parameter's data mapping to None | ⚪ | 🟡 | - | - |
| `gh_parameter_data_mapping_flatten` | NotTested | Set a parameter's data mapping to Flatten | ⚪ | 🟡 | - | - |
| `gh_parameter_data_mapping_graft` | NotTested | Set a parameter's data mapping to Graft | ⚪ | 🟡 | - | - |
| `gh_parameter_reverse` | NotTested | Reverse the order of items in a parameter | ⚪ | 🟡 | - | - |
| `gh_parameter_simplify` | NotTested | Simplify geometry in a parameter (removes redundant control points) | ⚪ | 🟡 | - | - |
| `rhino_get_geometry` | NotTested | Extract detailed geometry information from objects in the active Rhino document. Can retrieve selected objects, objects by layer, or objects by type. Returns geometry properties, coordinates, and metadata. | ⚪ | 🟡 | - | - |
| `rhino_read_3dm` | NotTested | Analyze a Rhino .3dm file and extract information about objects, layers, and file metadata. Returns summary statistics and object details. Use this to understand the contents of a 3DM file before processing. | ⚪ | 🟡 | - | - |
| `script_parameter_add_input` | NotTested | Add a new input parameter to a script component | ⚪ | 🟡 | - | - |
| `script_parameter_add_output` | NotTested | Add a new output parameter to a script component | ⚪ | 🟡 | - | - |
| `script_parameter_remove_input` | NotTested | Remove an input parameter from a script component | ⚪ | 🟡 | - | - |
| `script_parameter_remove_output` | NotTested | Remove an output parameter from a script component | ⚪ | 🟡 | - | - |
| `script_parameter_set_type_input` | NotTested | Set the type hint for a script component input parameter | ⚪ | 🟡 | - | - |
| `script_parameter_set_type_output` | NotTested | Set the type hint for a script component output parameter | ⚪ | 🟡 | - | - |
| `script_parameter_set_access` | NotTested | Set how an input parameter receives data (item/list/tree) | ⚪ | 🟡 | - | - |
| `script_toggle_std_output` | NotTested | Show or hide the standard output parameter ('out') in a script component | ⚪ | 🟡 | - | - |
| `script_set_principal_input` | NotTested | Set which input parameter drives the component's iteration | ⚪ | 🟡 | - | - |
| `script_parameter_set_optional` | NotTested | Set whether a script input parameter is required or optional | ⚪ | 🟡 | - | - |
| `speech_generate` | Speech | Generates speech audio from text input | - | - | - | - |
| `button_click` | Components | Simulate a momentary click on Grasshopper Buttons (not Boolean Toggles). The button is pressed for 100 ms, then released. Provide the instance GUIDs of the buttons. | ⚪ | 🟡 | 🟠 | - |
| `gh_document_save` | Document | Save the current Grasshopper document. If no filePath is provided, the document is saved to its existing location. Provide a full file path to save a copy or unnamed document. | ⚪ | 🟡 | 🟠 | - |
| `gh_remove` | Components | Remove components from the Grasshopper canvas by their instance GUIDs. The operation records an undo event so the user can reverse it with Ctrl+Z. Use GUIDs from gh_get or similar tools. | ⚪ | 🟡 | 🟠 | - |
| `gh_clear` | Components | Clear all components from the Grasshopper canvas. Optionally keep locked (disabled) components. Protected components (and their direct neighbors) are always preserved. This is a destructive operation - use with caution. Supports undo (Ctrl+Z). | ⚪ | 🟡 | 🟠 | - |
| `gh_report` | Components | Generate a comprehensive status report of the current Grasshopper canvas. Returns a structured markdown summary including object counts by type/topology, unique component names, group titles, scribble texts, viewport contents, file metadata, and all errors/warnings. Optionally includes an AI-generated summary of the file purpose. | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_smart_connect` | Components | AI-powered smart connection tool. Given a set of component GUIDs and a purpose description, retrieves their structure via gh_get, asks an AI model to suggest optimal connections, and executes them via gh_connect. Returns the connection results and the AI reasoning. | ⚪ | 🟡 | 🟠 | 🟢 |
| `set_ai_provider_and_model` | Components | Configure an `IProviderComponent` by setting its selected AI provider and wiring a new Panel with the model name into its Settings input. Supports undo and respects `CanvasProtection`. | ⚪ | 🟡 | 🟠 | - |
| `gh_generate_and_place_on_canvas` | Components | Generate a GhJSON document from instructions and immediately place it on the canvas. This wraps gh_generate followed by gh_put with editMode=false. Example: gh_generate_and_place_on_canvas({ instructions: 'Create a number slider connected to a panel' }). | - | - | - | - |
| `smarthopper_ghjson_reference` | Instructions | Returns GhJSON and GhPatch format reference documentation. Pass `topic` to retrieve the full specification or a focused section. Use this whenever you need to generate, edit, or validate GhJSON/GhPatch documents instead of relying on internalized format knowledge. | - | - | - | - |

Notes:

- **`web2md`** supports dedicated flows for Wikipedia/Wikimedia APIs, Discourse raw markdown (`/posts/{id}.json`), GitHub/GitLab raw files, and Stack Exchange questions via the public API. Use it for AI-friendly text without extra HTML cleanup.
- **`smarthopper_readme`** is an internal tool that provides operational instructions to the AI agent by topic. It is always available.

Is there something missing? Do you have a suggestion? Please open a discussion in the [Ideas](https://github.com/architects-toolkit/SmartHopper/discussions/categories/ideas) section in the Discussions tab.

## ➡️ Available Providers

SmartHopper currently supports the following AI providers and features:

| Provider | Status | API Registration | Streaming | Reasoning exposed by API | Live reasoning streaming in UI | Temperature config | Tool calling | JSON output | Image generation | Batch processing |
|---|:---:|---|:---:|---|:---:|---|:---:|:---:|:---:|:---:|
| OpenAI | ✅ Supported | OpenAI Platform | Yes | Yes (o-series & gpt-5 structured content) | Yes | Yes (non o-series & non gpt-5) | Yes | Yes | Yes (DALL-E) | ✅ Yes |
| MistralAI | ✅ Supported | Le Plateforme | Yes | Yes (thinking blocks) | Yes | Yes | Yes | Yes | No | ✅ Yes |
| DeepSeek | ✅ Supported | DeepSeek Platform | Yes | Yes (reasoning_content) | Yes | Yes | Yes | Yes | No | ❌ No |
| Anthropic | ✅ Supported | Claude Console | Yes | Yes (thinking blocks) | No | Yes | Yes | Yes | No | ✅ Yes |
| OpenRouter | ✅ Supported | OpenRouter | Yes | No (varies by routed model) | No | Varies | Varies | Varies | Varies | ❌ No |
| Gemini | 🟠 Testing | Google AI Studio | Yes | Yes (thinking_level) | Yes | Yes | Yes | Yes | ✅ Yes | ✅ Yes |
| Ollama | ⚪ Planned | Local Ollama server | Planned | Planned | Planned | Planned | Planned | Planned | No | Planned |
| LocalAI | ⚪ Planned | LocalAI server | Planned | Planned | Planned | Planned | Planned | Planned | Planned | Planned |
| Black Forest Labs | ⚪ Planned | Black Forest Labs API | Planned | No | No | Planned | No | No | Planned | Planned |
| Stable Diffusion | ⚪ Planned | Local/API Stable Diffusion endpoint | Planned | No | No | Planned | No | No | Planned | Planned |

Notes:
- “Temperature config” indicates whether the provider/model family supports a temperature parameter in SmartHopper. For OpenAI o‑series and gpt‑5, temperature is omitted by design; other OpenAI models support it.
- “Live reasoning streaming in UI” depends on the provider exposing a distinct reasoning/thinking channel and SmartHopper adapter support.
- OpenRouter capabilities vary by the routed underlying model; the SmartHopper adapter enables streaming, but reasoning support depends on the routed model.

Do you want more providers? Please open a discussion in the [Ideas](https://github.com/architects-toolkit/SmartHopper/discussions/categories/ideas) section in the Discussions tab.

## 🧠 Default Models by Provider

The following table summarizes the models explicitly registered as defaults or verified in each provider model registry. Source files:

- `src/SmartHopper.Providers.Anthropic/AnthropicProviderModels.cs`
- `src/SmartHopper.Providers.DeepSeek/DeepSeekProviderModels.cs`
- `src/SmartHopper.Providers.Gemini/GeminiProviderModels.cs`
- `src/SmartHopper.Providers.MistralAI/MistralAIProviderModels.cs`
- `src/SmartHopper.Providers.OpenAI/OpenAIProviderModels.cs`
- `src/SmartHopper.Providers.OpenRouter/OpenRouterProviderModels.cs`

Notes:
- "Default For" lists the feature areas the model is set as default for (e.g., `Text2Text`, `ToolChat`).
- "Capabilities" lists the core capability flags registered for the model.
- "Verified" reflects the `Verified` flag in the registry; "Deprecated" reflects the `Deprecated` flag (none of the current documented models are flagged deprecated).

| Provider | Model | Verified | Streaming | Deprecated | Default For | Capabilities |
|---|---|:---:|:---:|:---:|---|---|
| Anthropic | `claude-sonnet-4-6` | - | ✅ | - | Text2Json | TextInput, ImageInput, TextOutput, FunctionCalling, JsonOutput, Reasoning |
| Anthropic | `claude-haiku-4-5-20251001` | ⭐ | ✅ | - | Text2Text, ReasoningChat, ToolReasoningChat, ToolChat, Image2Text | TextInput, ImageInput, TextOutput, FunctionCalling, JsonOutput, Reasoning |
| Anthropic | `claude-sonnet-4-5-20250929` | ⭐ | ✅ | - | - | TextInput, TextOutput, JsonOutput, FunctionCalling, ImageInput, Reasoning |
| DeepSeek | `deepseek-v4-flash` | - | ✅ | - | Text2Text, ToolChat, ReasoningChat, ToolReasoningChat, Text2Json | TextInput, TextOutput, FunctionCalling, JsonOutput, Reasoning |
| Gemini | `gemini-3.1-flash-image-preview` | - | ✅ | - | Text2Image | TextInput, ImageInput, TextOutput, ImageOutput, JsonOutput, Reasoning |
| Gemini | `gemini-3-pro-image-preview` | - | ✅ | - | Text2Image, Image2Image | TextInput, ImageInput, TextOutput, ImageOutput, JsonOutput, Reasoning |
| Gemini | `gemini-2.5-flash-lite` | ⭐ | ✅ | - | - | TextInput, ImageInput, AudioInput, VideoInput, TextOutput, FunctionCalling, JsonOutput, Reasoning |
| Gemini | `gemini-2.5-flash-image` | ⭐ | ✅ | - | Text2Image | TextInput, ImageInput, TextOutput, ImageOutput, JsonOutput |
| Gemini | `gemini-2.5-flash` | ⭐ | ✅ | - | Text2Text, Text2Json, ReasoningChat, ToolReasoningChat | TextInput, ImageInput, AudioInput, VideoInput, TextOutput, FunctionCalling, JsonOutput, Reasoning |
| Gemini | `gemini-2.5-pro` | ⭐ | ✅ | - | - | TextInput, ImageInput, AudioInput, VideoInput, TextOutput, FunctionCalling, JsonOutput, Reasoning |
| MistralAI | `mistral-small-2603` | ⭐ | ✅ | - | Text2Text, ToolChat, Text2Json, Image2Text | TextInput, ImageInput, TextOutput, JsonOutput, FunctionCalling, Reasoning |
| MistralAI | `voxtral-mini-2602` | - | - | - | Speech2Text | AudioInput, TextOutput |
| MistralAI | `voxtral-mini-tts-2603` | - | - | - | Text2Speech | TextInput, AudioInput, AudioOutput |
| OpenAI | `gpt-audio-mini-2025-12-15` | - | - | - | Text2Speech, Speech2Text | TextInput, AudioInput, TextOutput, AudioOutput, FunctionCalling |
| OpenAI | `gpt-5.4-mini-2026-03-17` | - | ✅ | - | Text2Text, ToolChat, ReasoningChat, ToolReasoningChat, Text2Json, Image2Text | TextInput, ImageInput, TextOutput, JsonOutput, FunctionCalling, Reasoning |
| OpenAI | `gpt-5-mini-2025-08-07` | ⭐ | ✅ | - | Text2Text, ToolChat, ReasoningChat, ToolReasoningChat, Text2Json, Image2Text | TextInput, ImageInput, TextOutput, JsonOutput, FunctionCalling, Reasoning |
| OpenAI | `gpt-image-2-2026-04-21` | - | - | - | Image2Image | TextInput, ImageInput, ImageOutput |
| OpenAI | `whisper-1` | - | - | - | Speech2Text | SpeechInput, TextOutput |
| OpenRouter | `google/gemini-3.1-flash-lite-image` | - | - | - | Text2Image, Image2Image, Image2Text | TextInput, ImageInput, TextOutput, ImageOutput, JsonOutput, Reasoning |
| OpenRouter | `google/gemini-3.1-flash-lite` | - | - | - | Speech2Text | TextInput, ImageInput, AudioInput, VideoInput, TextOutput, FunctionCalling, JsonOutput, Reasoning |
| OpenRouter | `openai/gpt-5.6-luna` | - | - | - | ToolChat, ReasoningChat, ToolReasoningChat | TextInput, ImageInput, TextOutput, FunctionCalling, JsonOutput, Reasoning |
| OpenRouter | `google/lyria-3-pro-preview` | - | ✅ | - | Text2Speech | TextInput, ImageInput, TextOutput, AudioOutput, JsonOutput |
| OpenRouter | `openai/gpt-5-mini` | - | ✅ | - | Text2Text, Text2Json | TextInput, ImageInput, TextOutput, FunctionCalling, JsonOutput, Reasoning |

### Discouraged models for script tools

Some models are still supported but **not recommended** for script-oriented tools due to quality and stability trade-offs. These models are marked with `DiscouragedForTools` in the provider registries and surface in the UI as a "Not Recommended" badge when used with those tools.

- **Anthropic**
  - `claude-haiku-4-5-20251001`/`claude-haiku-4-5`/`claude-haiku-4-5-latest`/`claude-haiku-4.5`/`claude-haiku-4.5-latest` -> discouraged for: `script_generate`, `script_edit`
  - `claude-3-haiku-20240307`/`claude-3-haiku`/`claude-3-haiku-latest` -> discouraged for: `script_generate`, `script_edit`
  - `claude-3-5-haiku-20241022`/`claude-3-5-haiku`/`claude-3-5-haiku-latest` -> discouraged for: `script_generate`, `script_edit`
- **MistralAI**
  - `mistral-small-2603`/`mistral-small`/`mistral-small-latest`/`magistral-small-latest`/`mistral-vibe-cli-fast` -> discouraged for: `script_generate`, `script_edit`
  - `mistral-ocr-2512`/`mistral-ocr-3-0`/`mistral-ocr-3` -> discouraged for: any tool
  - `mistral-ocr-2505` -> discouraged for: any tool
- **OpenAI**
  - `omni-moderation-2024-09-26`/`omni-moderation-latest`/`omni-moderation` -> discouraged for: any tool

## 🔢 Supported Data Types

Data type serialization is handled by the [ghjson-dotnet](https://github.com/architects-toolkit/ghjson-dotnet) library. See its documentation for the full list of supported data types, serialization formats, and extensibility patterns.

### SmartHopper-specific types

The following custom goo types are persisted via `SafeGooCodec` when components save to `.gh` files:

- `GH_VersatileImage` — Wraps `Bitmap`, file paths, URLs, base64, data-URIs, and document-extracted images with metadata. Persisted as a compact JSON payload with base64 PNG encoding for in-memory bitmaps.
- `GH_VersatileAudio` — Wraps file paths, URLs, base64, data-URIs, and document-extracted audio with metadata. Persisted as a compact JSON payload.

See [`docs/Components/IO/Persistence.md`](./docs/Components/IO/Persistence.md) for the full persistence format.

—

Is there something missing? Do you have a suggestion? Please open a discussion in the [Ideas](https://github.com/architects-toolkit/SmartHopper/discussions/categories/ideas) section in the Discussions tab.
