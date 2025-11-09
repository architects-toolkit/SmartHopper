# Developer Reference

This document aggregates development-facing information.

- Table of contents
  - [Development Status](#development-status)
  - [AI Tools](#ai-tools)
  - [Available Providers](#available-providers)
  - [Default Models by Provider](#default-models-by-provider)
  - [Supported Data Types](#supported-data-types)

## 📊 Development Status

### Components

| Component | Planned | In Progress | Testing | Released 🎉 |
|-----------|:-------:|:-----------:|:-------:|:------------------------:|
| Grasshopper Get Components (GhGet)<br><sub>Read the current Grasshopper file and convert it to GhJSON format. Optionally filter them by runtime messages: errors, warnings, remarks</sub> | ⚪ | 🟡 | 🟠 | 🟢 |
| Grasshopper Put Components (GhPut)<br><sub>Place components on the canvas from a GhJSON format</sub> | ⚪ | 🟡 | 🟠 | 🟢 |
| Grasshopper Retrieve Components (GhRetrieveComponents)<br><sub>Retrieve all available Grasshopper components in your environment as JSON with optional category filter.</sub> | ⚪ | 🟡 | 🟠 | 🟢 |
| Grasshopper Tidy Up (GhTidyUp)<br><sub>Reorganize selected components into a clear, dependency-based grid.</sub> | ⚪ | 🟡 | 🟠 | 🟢 |
| AI Grasshopper Generate (AIGhGenerate)<br><sub>Automatically generate Grasshopper definitions using AI</sub> | ⚪ | - | - | - |
| AI Chat (AiChat)<br><sub>Interactive AI-powered conversational interface</sub> | ⚪ | 🟡 | 🟠 | 🟢 |
| AI Text Evaluate (AiTextEvaluate)<br><sub>Return a boolean from a text content using AI-powered checks</sub> | ⚪ | 🟡 | 🟠 | 🟢 |
| AI Text Generate (AiTextGenerate)<br><sub>Generate text content using AI</sub> | ⚪ | 🟡 | 🟠 | 🟢 |
| AI Text List Generate (AiTextListGenerate)<br><sub>Generate lists of text content using AI</sub> | ⚪ | 🟡 | 🟠 | 🟢 |
| AI Image Generate (AiImageGenerate)<br><sub>Generate images using AI</sub> | ⚪ | 🟡 | 🟠 | 🟢 |
| AI Script Review (AiScriptReview)<br><sub>Make a review of a script, using AI</sub> | ⚪ | - | - | - |
| AI Script Edit (AiScriptEdit)<br><sub>Modify an existing script using AI</sub> | ⚪ | - | - | - |
| AI Script New (AiScriptNew)<br><sub>Generate a script using AI</sub> | ⚪ | - | - | - |
| AI List Evaluate (AiListEvaluate)<br><sub>Return a boolean from a list of elements using AI analysis</sub> | ⚪ | 🟡 | 🟠 | 🟢 |
| AI List Filter (AiListFilter)<br><sub>Process items in lists (reorder, shuffle, filter, etc.) based on AI-driven rules</sub> | ⚪ | 🟡 | 🟠 | 🟢 |
| AI JSON Generate (AiJsonGenerate)<br><sub>Generate an AI response in strict JSON output</sub> | ⚪ | - | - | - |
| AI GroupTitle (AiGroupTitle)<br><sub>Group components and set a meaningful title to the group</sub> | ⚪ | - | - | - |
| AI File Context (AiFileContext)<br><sub>Set a context for the current document</sub> | ⚪ | 🟡 | 🟠 | 🟢 |
| AI Models (AiModels)<br><sub>Retrieve the list of available models for a specific provider</sub> | ⚪ | 🟡 | 🟠 | 🟢 |
| Image Viewer (ImageViewer)<br><sub>Display bitmap images on the canvas and save them to disk</sub> | ⚪ | 🟡 | 🟠 | 🟢 |
| JSON schema (JsonSchema)<br><sub>Set a JSON schema for the AI component</sub> | ⚪ | - | - | - |
| JSON object (JsonObject)<br><sub>Set a JSON object for the definition of the JSON Schema</sub> | ⚪ | - | - | - |
| JSON array (JsonArray)<br><sub>Set a JSON array for the definition of the JSON Schema</sub> | ⚪ | - | - | - |
| Context Parameters (ContextParameters)<br><sub>Set context parameters for the AI component</sub> | ⚪ | - | - | - |
| Deconstruct Metrics (DeconstructMetrics)<br><sub>Break down the usage metrics into individual values</sub> | ⚪ | 🟡 | 🟠 | 🟢 |
| Save GhJSON file (SaveGhJSON)<br><sub>Save the current Grasshopper file as a GhJSON format</sub> | ⚪ | - | - | - |
| Load GhJSON file (LoadGhJSON)<br><sub>Load a GhJSON file and convert it to a Grasshopper document</sub> | ⚪ | - | - | - |

### AI Tools

AI Tools are the interface between AI and Grasshopper, allowing to, for example, read your selected components, get the available Grasshopper components, or write a new script. All these tools are available to the provider to use while chatting in the AI Chat component.

| Tool Name | Description | Planned | In Progress | Testing | Released 🎉 |
|-----------|-------------|:-------:|:-----------:|:-------:|:-----------:|
| text_evaluate | Evaluates text against a true/false question | ⚪ | 🟡 | 🟠 | 🟢 |
| text_generate | Generates text based on a prompt and optional instructions | ⚪ | 🟡 | 🟠 | 🟢 |
| img_generate | Generates an image based on a prompt and optional instructions | ⚪ | 🟡 | 🟠 | 🟢 |
| list_evaluate | Evaluates a list based on a natural language question | ⚪ | 🟡 | 🟠 | 🟢 |
| list_filter | Filters a list based on natural language criteria | ⚪ | 🟡 | 🟠 | 🟢 |
| list_generate | Generates a list based on a natural language prompt | ⚪ | 🟡 | 🟠 | 🟢 |
| script_review | Review a script for potential issues using AI-powered checks | ⚪ | 🟡 | 🟠 | 🟢 |
| script_edit | Modify the script from an existing component | ⚪ | 🟡 | 🟠 | - |
| script_new | Place a new script component from a natural language prompt | ⚪ | 🟡 | 🟠 | 🟢 |
| script_parameter_add_input | Add a new input parameter to a script component | ⚪ | 🟡 | - | - |
| script_parameter_add_output | Add a new output parameter to a script component | ⚪ | 🟡 | - | - |
| script_parameter_remove_input | Remove an input parameter from a script component | ⚪ | 🟡 | - | - |
| script_parameter_remove_output | Remove an output parameter from a script component | ⚪ | 🟡 | - | - |
| script_parameter_set_type_input | Set the type hint for a script input parameter | ⚪ | 🟡 | - | - |
| script_parameter_set_type_output | Set the type hint for a script output parameter | ⚪ | 🟡 | - | - |
| script_parameter_set_access | Set how a script input parameter receives data (item/list/tree) | ⚪ | 🟡 | - | - |
| script_toggle_std_output | Show or hide the standard output parameter ('out') in a script component | ⚪ | 🟡 | - | - |
| script_set_principal_input | Set which input parameter drives the component's iteration | ⚪ | 🟡 | - | - |
| script_parameter_set_optional | Set whether a script input parameter is required or optional | ⚪ | 🟡 | - | - |
| json_generate | Generate an AI response in strict JSON output | ⚪ | - | - | - |
| web_generic_page_read | Retrieve plain text content of a webpage, excluding HTML, scripts, and images, with robots.txt compliance | ⚪ | 🟡 | 🟠 | 🟢 |
| mcneel_forum_search | Search McNeel Discourse forum with configurable limit and optional AI summaries | ⚪ | 🟡 | 🟠 | 🟢 |
| mcneel_forum_get_post | Retrieve full JSON of a McNeel Discourse forum post by ID | ⚪ | 🟡 | 🟠 | 🟢 |
| mcneel_forum_post_summarize | Generate AI-powered summary of a McNeel Discourse forum post | ⚪ | 🟡 | 🟠 | 🟢 |
| get_input | Send data from Grasshopper to AI Chat | ⚪ | - | - | - |
| get_output | Receive data from AI Chat to Grasshopper | ⚪ | - | - | - |
| gh_list_categories | List available Grasshopper categories | ⚪ | 🟡 | 🟠 | 🟢 |
| gh_list_components | List Grasshopper components (optionally filtered by category) | ⚪ | 🟡 | 🟠 | 🟢 |
| gh_get | Retrieve Grasshopper components as GhJSON with optional filters | ⚪ | 🟡 | 🟠 | 🟢 |
| gh_put | Place Grasshopper components on the canvas from GhJSON format | ⚪ | 🟡 | 🟠 | 🟢 |
| gh_toggle_preview | Toggle component preview on or off by GUID | ⚪ | 🟡 | 🟠 | 🟢 |
| gh_toggle_lock | Toggle component lock (enable/disable) by GUID | ⚪ | 🟡 | 🟠 | 🟢 |
| gh_move | Move component pivot by GUID with absolute or relative positioning | ⚪ | 🟡 | 🟠 | 🟢 |
| gh_tidy_up | Organize selected components into a tidy grid layout | ⚪ | 🟡 | 🟠 | 🟢 |
| gh_generate | Generate Grasshopper definitions using AI | ⚪ | 🟡 | - | - |
| gh_connect | Connect Grasshopper components | ⚪ | 🟡 | - | - |
| gh_group | Group components and set a meaningful title | ⚪ | 🟡 | - | - |
| gh_parameter_flatten | Flatten a parameter's data tree into a single list | ⚪ | 🟡 | - | - |
| gh_parameter_graft | Graft a parameter to add an extra branch level to the data tree | ⚪ | 🟡 | - | - |
| gh_parameter_reverse | Reverse the order of items in a parameter | ⚪ | 🟡 | - | - |
| gh_parameter_simplify | Simplify geometry in a parameter (remove redundant structure) | ⚪ | 🟡 | - | - |
| gh_parameter_bulk_inputs | Apply data settings to all input parameters of a component | ⚪ | 🟡 | - | - |
| gh_parameter_bulk_outputs | Apply data settings to all output parameters of a component | ⚪ | 🟡 | - | - |
| rhino_get_geometry | Retrieve geometry from Rhino | ⚪ | 🟡 | - | - |
| rhino_read_3dm | Read a 3dm file from disk | ⚪ | 🟡 | - | - |

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
| OpenAI | gpt-5-nano | - | ✅ | - | Text2Text | TextInput, ImageInput, TextOutput, JsonOutput, FunctionCalling, Reasoning |
| OpenAI | gpt-5-mini | ⭐ | ✅ | - | ToolChat; Text2Json; ToolReasoningChat | TextInput, ImageInput, TextOutput, JsonOutput, FunctionCalling, Reasoning |
| OpenAI | dall-e-3 | ⭐ | - | - | Text2Image | TextInput, ImageOutput |
| OpenAI | gpt-image-1 | - | - | - | Text2Image; Image2Image | TextInput, ImageInput, ImageOutput |
| MistralAI | mistral-small-latest | ⭐ | ✅ | - | Text2Text; ToolChat; Text2Json | TextInput, TextOutput, JsonOutput, FunctionCalling, ImageInput |
| MistralAI | magistral-small-latest | - | ✅ | - | ToolReasoningChat | TextInput, TextOutput, JsonOutput, FunctionCalling, Reasoning |
| DeepSeek | deepseek-reasoner | - | ✅ | - | ToolReasoningChat | TextInput, TextOutput, JsonOutput, FunctionCalling, Reasoning |
| DeepSeek | deepseek-chat | - | ✅ | - | Text2Text; ToolChat | TextInput, TextOutput, JsonOutput, FunctionCalling |
| Anthropic | claude-3-5-haiku-latest | - | ✅ | - | Text2Text; ToolChat; Text2Json | TextInput, ImageInput, TextOutput, JsonOutput, FunctionCalling |
| OpenRouter | openai/gpt-5-mini | - | ✅ | - | Text2Text | TextInput, ImageInput, TextOutput, JsonOutput, FunctionCalling, Reasoning |

## 🔢 Supported Data Types

SmartHopper registers the following data type serializers (see `src/SmartHopper.Core/Serialization/DataTypes/DataTypeRegistry.cs`). All listed types are fully supported end‑to‑end (serialization/deserialization and validation):

| Data Type | Status |
|-----------|:------:|
| Text | ✅ Supported |
| Number | ✅ Supported |
| Integer | ✅ Supported |
| Boolean | ✅ Supported |
| Colour (Color) | ✅ Supported |
| Point | ✅ Supported |
| Vector | ✅ Supported |
| Line | ✅ Supported |
| Plane | ✅ Supported |
| Circle | ✅ Supported |
| Arc | ✅ Supported |
| Box | ✅ Supported |
| Rectangle | ✅ Supported |
| Interval | ✅ Supported |
| Path | 🔜 Planned |
| File Path | 🔜 Planned |
| Unit System | 🔜 Planned |
| Time | 🔜 Planned |
| Complex | 🔜 Planned |
| Culture | 🔜 Planned |
| Domain2D | 🔜 Planned |

—

Is there something missing? Do you have a suggestion? Please open a discussion in the [Ideas](https://github.com/architects-toolkit/SmartHopper/discussions/categories/ideas) section in the Discussions tab.
