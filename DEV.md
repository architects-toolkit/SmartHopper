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
|-----------|----------|:-------:|:-----------:|:-------:|:------------------------:|
| Get GhJSON (GhGet)<br><sub>Read the current Grasshopper file and convert it to GhJSON format. Filter by runtime messages, component state, preview, type, category, and more.</sub> | Grasshopper | ⚪ | 🟡 | 🟠 | 🟢 |
| Place GhJSON (GhPut)<br><sub>Place components on the canvas from a GhJSON format</sub> | Grasshopper | ⚪ | 🟡 | 🟠 | 🟢 |
| Merge GhJSON (GhMerge)<br><sub>Merge two GhJSON documents into one, with the target document taking priority on conflicts.</sub> | Grasshopper | ⚪ | 🟡 | 🟠 | 🟢 |
| Retrieve Components (GhRetrieveComponents)<br><sub>Retrieve all available Grasshopper components in your environment as JSON with optional category filter.</sub> | Grasshopper | ⚪ | 🟡 | 🟠 | 🟢 |
| Tidy Up (GhTidyUp)<br><sub>Organize selected components into a tidy grid layout based on dependencies.</sub> | Grasshopper | ⚪ | 🟡 | 🟠 | 🟢 |
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
| Web To Markdown (WebToMd)<br><sub>Convert web pages (URLs) to Markdown with specialized handlers for Wikipedia, GitHub, Discourse, Stack Exchange</sub> | Knowledge | ⚪ | 🟡 | 🟠 | 🟢 |
| File To Markdown (FileToMd)<br><sub>Convert local files (PDF, DOCX, XLSX, PPTX, HTML, CSV, JSON, XML, TXT, EML, EPUB, RTF) to Markdown</sub> | Knowledge | ⚪ | 🟡 | 🟠 | 🟢 |
| McNeel Forum Search (McNeelForumSearch)<br><sub>Search McNeel Discourse forum with configurable limit</sub> | Knowledge | ⚪ | 🟡 | 🟠 | 🟢 |
| McNeel Forum Post Get (McNeelForumPostGet)<br><sub>Retrieve a McNeel Discourse forum post by ID</sub> | Knowledge | ⚪ | 🟡 | 🟠 | 🟢 |
| McNeel Forum Post Open (McNeelForumPostOpen)<br><sub>Open a McNeel forum post URL in the default browser</sub> | Knowledge | ⚪ | 🟡 | 🟠 | 🟢 |
| McNeel Forum Deconstruct Post (McNeelForumDeconstructPost)<br><sub>Deconstruct forum post JSON into individual fields</sub> | Knowledge | ⚪ | 🟡 | 🟠 | 🟢 |
| AI McNeel Forum Post Summarize (AIMcNeelForumPostSummarize)<br><sub>Generate AI summary of a McNeel Discourse forum post</sub> | Knowledge | ⚪ | 🟡 | 🟠 | 🟢 |
| AI McNeel Forum Topic Summarize (AIMcNeelForumTopicSummarize)<br><sub>Generate AI summary of a McNeel Discourse forum topic</sub> | Knowledge | ⚪ | 🟡 | 🟠 | 🟢 |
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

AI Tools are the interface between AI and Grasshopper, allowing to, for example, read your selected components, get the available Grasshopper components, or write a new script. All these tools are available to the provider to use while chatting in the AI Chat component.

| Tool Name | Category | Description | Planned | In Progress | Testing | Released 🎉 |
|-----------|----------|-------------|:-------:|:-----------:|:-------:|:-----------:|
| `text2boolean` | DataProcessing | Evaluates text against a true/false question | ⚪ | 🟡 | 🟠 | 🟢 |
| `text2text` | DataProcessing | Generates text based on a prompt and optional instructions | ⚪ | 🟡 | 🟠 | 🟢 |
| `text2img` | DataProcessing | Generates an image based on a prompt and optional instructions | ⚪ | 🟡 | 🟠 | 🟢 |
| `textlist2boolean` | DataProcessing | Evaluates a list based on natural language question | ⚪ | 🟡 | 🟠 | 🟢 |
| `list_filter` | DataProcessing | Filters a list based on natural language criteria | ⚪ | 🟡 | 🟠 | 🟢 |
| `text2textlist` | DataProcessing | Generates a list based on a natural language prompt | ⚪ | 🟡 | 🟠 | 🟢 |
| `img2text` | ImageProcessing | Describes or analyzes an image using a vision model | ⚪ | 🟡 | 🟠 | 🟢 |
| `text2json` | DataProcessing | Generates structured JSON from a prompt conforming to a provided JSON Schema | ⚪ | 🟡 | - | - |
| `get_input` | DataProcessing | Send data from Grasshopper to AI Chat | ⚪ | - | - | - |
| `get_output` | DataProcessing | Receive data from AI Chat to Grasshopper | ⚪ | - | - | - |
| `script_review` | Script | Review a script for potential issues using AI-powered checks | ⚪ | 🟡 | 🟠 | 🟢 |
| `script_generate` | Script | Create Grasshopper script components based on instructions (hidden from chat) | ⚪ | 🟡 | 🟠 | 🟢 |
| `script_generate_and_place_on_canvas` | Script | Generate a new script component and place it on canvas in one call | ⚪ | 🟡 | 🟠 | 🟢 |
| `script_edit` | Script | Edit Grasshopper script components based on instructions (hidden from chat) | ⚪ | 🟡 | 🟠 | 🟢 |
| `script_edit_and_replace_on_canvas` | Script | Edit a script component by GUID and replace it on canvas in one call | ⚪ | 🟡 | 🟠 | 🟢 |
| `instruction_get` | Instructions | Returns operational instructions for SmartHopper by topic (canvas, ghjson, scripting, etc.) | ⚪ | 🟡 | 🟠 | 🟢 |
| `web2md` | Knowledge | Convert web pages (URLs) to Markdown with metadata and warnings | ⚪ | 🟡 | 🟠 | 🟢 |
| `file2md` | Knowledge | Convert local files to Markdown (PDF, DOCX, XLSX, PPTX, HTML, CSV, JSON, XML, TXT, EML, EPUB, RTF) | ⚪ | 🟡 | 🟠 | 🟢 |
| `mcneel_forum_search` | Knowledge | Search McNeel Discourse forum with configurable limit | ⚪ | 🟡 | 🟠 | 🟢 |
| `mcneel_forum_post_get` | Knowledge | Retrieve filtered McNeel Discourse forum post by ID | ⚪ | 🟡 | 🟠 | 🟢 |
| `mcneel_forum_post_summarize` | Knowledge | Generate AI-powered summary of a McNeel Discourse forum post | ⚪ | 🟡 | 🟠 | 🟢 |
| `mcneel_forum_topic_get` | Knowledge | Retrieve all posts in a McNeel Discourse forum topic by ID | ⚪ | 🟡 | 🟠 | 🟢 |
| `mcneel_forum_topic_summarize` | Knowledge | Generate AI-powered summary of a McNeel Discourse forum topic | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_list_categories` | Components | List available Grasshopper categories | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_list_components` | Components | List Grasshopper components (optionally filtered by category) | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_get` | Components | Retrieve Grasshopper components as GhJSON with optional filters (attr, category, type, guid, connectionDepth, metadata, runtimeData) | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_get_selected` | Components | Retrieve only the selected components from the Grasshopper canvas as GhJSON | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_get_selected_with_data` | Components | Retrieve selected components as GhJSON with runtime data snapshot | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_get_by_guid` | Components | Retrieve specific components by GUID as GhJSON | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_get_by_guid_with_data` | Components | Retrieve specific components by GUID as GhJSON with runtime data | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_get_errors` | Components | Retrieve only components that have error messages as GhJSON | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_get_errors_with_data` | Components | Retrieve errored components as GhJSON with runtime data | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_get_locked` | Components | Retrieve only locked (disabled) components as GhJSON | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_get_hidden` | Components | Retrieve only components with preview turned off as GhJSON | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_get_visible` | Components | Retrieve only components with preview turned on as GhJSON | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_get_start` | Components | Retrieve start nodes (data sources with no incoming connections) as GhJSON | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_get_start_with_data` | Components | Retrieve start nodes as GhJSON with runtime data | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_get_end` | Components | Retrieve end nodes (data sinks with no outgoing connections) as GhJSON | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_get_end_with_data` | Components | Retrieve end nodes as GhJSON with runtime data | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_put` | Components | Place Grasshopper components on the canvas from GhJSON format | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_merge` | Components | Merge two GhJSON documents into one (target takes priority on conflicts) | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_component_toggle_preview` | Components | Show or hide component geometry preview by GUID | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_component_hide_preview_selected` | Components | Hide geometry preview for currently selected components | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_component_show_preview_selected` | Components | Show geometry preview for currently selected components | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_component_toggle_lock` | Components | Lock (disable) or unlock (enable) components by GUID | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_component_lock_selected` | Components | Lock currently selected components | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_component_unlock_selected` | Components | Unlock currently selected components | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_move` | Components | Move component pivot by GUID with absolute or relative positioning | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_tidy_up` | Components | Organize selected components into a tidy grid layout | ⚪ | 🟡 | 🟠 | 🟢 |
| `gh_generate` | Components | Generate Grasshopper definitions using AI | ⚪ | 🟡 | - | - |
| `gh_connect` | Components | Connect Grasshopper components by creating wires between outputs and inputs | ⚪ | 🟡 | - | - |
| `gh_group` | Components | Group components and set a meaningful title | ⚪ | 🟡 | - | - |
| `gh_parameter_data_mapping_none` | Parameters | Set a parameter's data mapping to None | ⚪ | - | - | - |
| `gh_parameter_data_mapping_flatten` | Parameters | Set a parameter's data mapping to Flatten | ⚪ | 🟡 | - | - |
| `gh_parameter_data_mapping_graft` | Parameters | Set a parameter's data mapping to Graft | ⚪ | 🟡 | - | - |
| `gh_parameter_reverse` | Parameters | Reverse the order of items in a parameter | ⚪ | 🟡 | - | - |
| `gh_parameter_simplify` | Parameters | Simplify geometry in a parameter (remove redundant control points) | ⚪ | 🟡 | - | - |
| `rhino_get_geometry` | Rhino | Retrieve geometry from the active Rhino document (by selection, layer, or type) | ⚪ | 🟡 | - | - |
| `rhino_read_3dm` | Rhino | Analyze a Rhino .3dm file and extract information about objects, layers, and metadata | ⚪ | 🟡 | - | - |
| `script_parameter_add_input` | NotTested | Add a new input parameter to a script component | ⚪ | 🟡 | - | - |
| `script_parameter_add_output` | NotTested | Add a new output parameter to a script component | ⚪ | 🟡 | - | - |
| `script_parameter_remove_input` | NotTested | Remove an input parameter from a script component | ⚪ | 🟡 | - | - |
| `script_parameter_remove_output` | NotTested | Remove an output parameter from a script component | ⚪ | 🟡 | - | - |
| `script_parameter_set_type_input` | NotTested | Set the type hint for a script input parameter | ⚪ | 🟡 | - | - |
| `script_parameter_set_type_output` | NotTested | Set the type hint for a script output parameter | ⚪ | 🟡 | - | - |
| `script_parameter_set_access` | NotTested | Set how a script input parameter receives data (item/list/tree) | ⚪ | 🟡 | - | - |
| `script_toggle_std_output` | NotTested | Show or hide the standard output parameter ('out') in a script component | ⚪ | 🟡 | - | - |
| `script_set_principal_input` | NotTested | Set which input parameter drives the component's iteration | ⚪ | 🟡 | - | - |
| `script_parameter_set_optional` | NotTested | Set whether a script input parameter is required or optional | ⚪ | 🟡 | - | - |

Notes:

- **`web2md`** supports dedicated flows for Wikipedia/Wikimedia APIs, Discourse raw markdown (`/posts/{id}.json`), GitHub/GitLab raw files, and Stack Exchange questions via the public API. Use it for AI-friendly text without extra HTML cleanup.
- **`instruction_get`** is an internal tool that provides operational instructions to the AI agent by topic. It is always available.

Is there something missing? Do you have a suggestion? Please open a discussion in the [Ideas](https://github.com/architects-toolkit/SmartHopper/discussions/categories/ideas) section in the Discussions tab.

## ➡️ Available Providers

SmartHopper currently supports the following AI providers and features:

| Provider | Status | API Registration | Streaming | Reasoning exposed by API | Live reasoning streaming in UI | Temperature config | Tool calling | JSON output | Image generation |
|----------|:------:|------------------|:--------:|:------------------------:|:-------------------------------:|:------------------:|:-----------:|:-----------:|:----------------:|
| OpenAI | ✅ Supported | [OpenAI Platform](https://platform.openai.com/) | Yes | Yes (o‑series & gpt‑5 structured content) | Yes | Yes (non o‑series & non gpt‑5) | Yes | Yes | Yes (DALL‑E) |
| MistralAI | ✅ Supported | [Le Plateforme](https://console.mistral.ai/) | Yes | Yes (thinking blocks) | Yes | Yes | Yes | Yes | No |
| DeepSeek | ✅ Supported | [DeepSeek Platform](https://platform.deepseek.com/) | Yes | Yes (reasoning_content) | Yes | Yes | Yes | Yes | No |
| Anthropic | ✅ Supported | [Claude Console](https://platform.claude.com/) | Yes | No | No | Yes | Yes | Yes | No |
| OpenRouter | ✅ Supported | [OpenRouter](https://openrouter.ai/) | No | No (varies by routed model) | No | Varies | Varies | Varies | Varies |

Notes:
- “Temperature config” indicates whether the provider/model family supports a temperature parameter in SmartHopper. For OpenAI o‑series and gpt‑5, temperature is omitted by design; other OpenAI models support it.
- “Live reasoning streaming in UI” depends on the provider exposing a distinct reasoning/thinking channel and SmartHopper adapter support.
- OpenRouter capabilities vary by the routed underlying model; current SmartHopper adapter does not enable streaming/reasoning there.

Do you want more providers? Please open a discussion in the [Ideas](https://github.com/architects-toolkit/SmartHopper/discussions/categories/ideas) section in the Discussions tab.

## 🧠 Default Models by Provider

The following table summarizes the models explicitly registered as defaults in each provider’s model registry. Source files:

- `src/SmartHopper.Providers.OpenAI/OpenAIProviderModels.cs`
- `src/SmartHopper.Providers.MistralAI/MistralAIProviderModels.cs`
- `src/SmartHopper.Providers.DeepSeek/DeepSeekProviderModels.cs`
- `src/SmartHopper.Providers.Anthropic/AnthropicProviderModels.cs`
- `src/SmartHopper.Providers.OpenRouter/OpenRouterProviderModels.cs`

Notes:
- “Default For” lists the feature areas the model is set as default for (e.g., `Text2Text`, `ToolChat`).
- “Capabilities” lists the core capability flags registered for the model.
- “Verified” reflects the `Verified` flag in the registry; “Deprecated” reflects the `Deprecated` flag (none of the current defaults are flagged deprecated).

| Provider | Model | Verified | Streaming | Deprecated | Default For | Capabilities |
|---|---|:---:|:---:|:---:|---|---|
| Anthropic | `claude-haiku-4-5` | ⭐ | ✅ | - | Text2Text, ReasoningChat, ToolReasoningChat | TextInput, ImageInput, TextOutput, FunctionCalling, Reasoning |
| Anthropic | `claude-sonnet-4-5` | ⭐ | ✅ | - | Text2Text, Text2Json, ReasoningChat, ToolReasoningChat | TextInput, ImageInput, TextOutput, JsonOutput, FunctionCalling, Reasoning |
| Anthropic | `claude-opus-4-5` | - | ✅ | - | - | TextInput, TextOutput, JsonOutput, FunctionCalling, ImageInput, Reasoning |
| DeepSeek | `deepseek-chat` | - | ✅ | - | Text2Text, ToolChat | TextInput, TextOutput, JsonOutput, FunctionCalling |
| DeepSeek | `deepseek-reasoner` | - | ✅ | - | ToolReasoningChat | TextInput, TextOutput, JsonOutput, FunctionCalling, Reasoning |
| MistralAI | `mistral-small` | ⭐ | ✅ | - | Text2Text, ToolChat, Text2Json | TextInput, ImageInput, TextOutput, JsonOutput, FunctionCalling |
| MistralAI | `mistral-medium` | ⭐ | ✅ | - | - | TextInput, ImageInput, TextOutput, JsonOutput, FunctionCalling |
| MistralAI | `mistral-large-latest` | - | ✅ | - | - | TextInput, ImageInput, TextOutput, JsonOutput, FunctionCalling |
| MistralAI | `magistral-small-latest` | - | ✅ | - | ToolReasoningChat | TextInput, ImageInput, TextOutput, JsonOutput, FunctionCalling, Reasoning |
| MistralAI | `magistral-medium-latest` | - | ✅ | - | - | TextInput, ImageInput, TextOutput, JsonOutput, FunctionCalling, Reasoning |
| OpenAI | `gpt-5-nano` | - | ✅ | - | Text2Text | TextInput, ImageInput, TextOutput, JsonOutput, FunctionCalling, Reasoning |
| OpenAI | `gpt-5-mini` | ⭐ | ✅ | - | Text2Text, ToolChat, Text2Json, ToolReasoningChat | TextInput, ImageInput, TextOutput, JsonOutput, FunctionCalling, Reasoning |
| OpenAI | `gpt-5.1` | - | ✅ | - | - | TextInput, ImageInput, TextOutput, JsonOutput, FunctionCalling, Reasoning |
| OpenAI | `dall-e-3` | ⭐ | - | - | Text2Image | TextInput, ImageOutput |
| OpenAI | `gpt-image-1-mini` | - | - | - | Text2Image, Image2Image | TextInput, ImageInput, ImageOutput |
| OpenRouter | `openai/gpt-5-mini` | - | ✅ | - | Text2Text | TextInput, ImageInput, TextOutput, JsonOutput, FunctionCalling, Reasoning |

### Discouraged models for script tools

Some models are still supported but **not recommended** for script‑oriented tools due to quality and stability trade‑offs. These models are marked with `DiscouragedForTools` in the provider registries and surface in the UI as a "Not Recommended" badge when used with those tools.

- **MistralAI**
  - `mistral-small-latest`/`mistral-small` → discouraged for: `script_generate`, `script_edit`
- **Anthropic**
  - `claude-haiku-4-5`/`claude-haiku-4-5-20251001`/`claude-3-5-haiku-latest`/`claude-3-5-haiku-20241022`/`claude-3-haiku-20240307` → discouraged for: `script_generate`, `script_edit`

## 🔢 Supported Data Types

Data type serialization is handled by the [ghjson-dotnet](https://github.com/architects-toolkit/ghjson-dotnet) library. See its documentation for the full list of supported data types, serialization formats, and extensibility patterns.

—

Is there something missing? Do you have a suggestion? Please open a discussion in the [Ideas](https://github.com/architects-toolkit/SmartHopper/discussions/categories/ideas) section in the Discussions tab.
