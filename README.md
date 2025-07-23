# SmartHopper - AI-Powered Grasshopper3D Plugin

[![Version](https://img.shields.io/badge/version-0%2E5%2E0-dev-brown)](https://github.com/architects-toolkit/SmartHopper/releases)
[![Status](https://img.shields.io/badge/status-Unstable%20Development-brown)](https://github.com/architects-toolkit/SmartHopper/releases)
[![Test results](https://img.shields.io/github/actions/workflow/status/architects-toolkit/SmartHopper/.github/workflows/ci-dotnet-tests.yml?label=.NET%20CI&logo=dotnet)](https://github.com/architects-toolkit/SmartHopper/actions/workflows/ci-dotnet-tests.yml)
[![Grasshopper](https://img.shields.io/badge/plugin_for-Grasshopper3D-darkgreen?logo=rhinoceros)](https://www.rhino3d.com/)
[![MistralAI](https://img.shields.io/badge/AI--powered-MistralAI-orange?logo=mistralai)](https://mistral.ai/)
[![OpenAI](https://img.shields.io/badge/AI--powered-OpenAI-lightgrey?logo=openai)](https://openai.com/)
[![DeepSeek](https://img.shields.io/badge/AI--powered-DeepSeek-blue?logo=deepseek)](https://deepseek.com/)
[![License](https://img.shields.io/badge/license-LGPLv3-white)](LICENSE)

SmartHopper is a groundbreaking plugin that enables AI to directly interact with your Grasshopper canvas! Ask for help, search on the McNeel forum, reorganize components, toggle preview on or off, and much more, just by chatting with your customizable AI assistant. Additionally, this plugins includes multiple components that correspond to individual AI tools, so that you can integrate text- or list-based operations directly into your definition.

## 🎯 Key Features

- 🔍 **Direct AI Access to Grasshopper Files**: SmartHopper allows AI to read and understand your Grasshopper definitions through GhJSON conversion, enabling intelligent analysis and manipulation of parametric models.
- 🧠 **AI-Powered Workflow Enhancement**: Leverage AI to generate text, evaluate designs, filter data, and more - all within your familiar Grasshopper environment.
- 🤖 **Multiple AI Provider Support**: Choose between [**MistralAI**](https://mistral.ai/), [**OpenAI**](https://openai.com/) and [**DeepSeek**](https://deepseek.com/) APIs. You need to [provide your own API keys](#️-Available-Providers).
- 🔄 **Bidirectional Integration**: Not only can AI read your Grasshopper, but it can also generate and place definitions directly on your canvas *[coming soon]*.

## 👥 Who Is This For?

- **Architects and engineers** looking to enhance their parametric design workflow with AI assistance.
- **Computational designers** seeking to automate repetitive tasks and generate creative solutions.
- **Researchers** exploring the intersection of AI and parametric design methodologies.

## 🛠️ Technical Capabilities

- **Seamless integration** with Grasshopper's Data Tree structure.
- **Asynchronous** execution to maintain a responsive design environment.
- **Flexible** triggering options: run components manually or automatically when inputs change.
- **Modular** architecture designed for stability and future extensibility.

## 💻 Installation

You can install SmartHopper through multiple methods:

1. **Rhino Package Manager** (Recommended):
   - Open Rhino 8
   - Type `PackageManager` in the command line
   - In the Package Manager, select "include pre-releases"
   - Search for "SmartHopper"
   - Click "Install"

2. **Food4Rhino**:
   - Go to [Food4Rhino](https://www.food4rhino.com/en/app/smarthopper)
   - Click "Install"

3. **GitHub Releases**:
   - Download the latest release from our [GitHub Releases](https://github.com/architects-toolkit/SmartHopper/releases) page
   - Extract the downloaded ZIP file
   - Copy the contents to your Grasshopper plugins folder
   - Restart Rhino

After installation, all SmartHopper components will be available in the Grasshopper palette.

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
| AI Chat Input (AiChatInput)<br><sub>Send some data from your Grasshopper to the AI Chat</sub> | ⚪ | - | - | - |
| AI Chat Output (AiChatOutput)<br><sub>Receive some data from the AI Chat to your Grasshopper</sub> | ⚪ | - | - | - |
| AI Text Evaluate (AiTextEvaluate)<br><sub>Return a boolean from a text content using AI-powered checks</sub> | ⚪ | 🟡 | 🟠 | 🟢 |
| AI Text Generate (AiTextGenerate)<br><sub>Generate text content using AI</sub> | ⚪ | 🟡 | 🟠 | 🟢 |
| AI Text List Generate (AiTextListGenerate)<br><sub>Generate lists of text content using AI</sub> | ⚪ | 🟡 | 🟠 | 🟢 |
| AI Script Review (AiScriptReview)<br><sub>Make a review of a script, using AI</sub> | ⚪ | - | - | - |
| AI Script Edit (AiScriptEdit)<br><sub>Modify an existing script using AI</sub> | ⚪ | - | - | - |
| AI Script New (AiScriptNew)<br><sub>Generate a script using AI</sub> | ⚪ | - | - | - |
| AI List Evaluate (AiListEvaluate)<br><sub>Return a boolean from a list of elements using AI analysis</sub> | ⚪ | 🟡 | 🟠 | 🟢 |
| AI List Filter (AiListFilter)<br><sub>Process items in lists (reorder, shuffle, filter, etc.) based on AI-driven rules</sub> | ⚪ | 🟡 | 🟠 | 🟢 |
| AI JSON Generate (AiJsonGenerate)<br><sub>Generate an AI response in strict JSON output</sub> | ⚪ | - | - | - |
| AI GroupTitle (AiGroupTitle)<br><sub>Group components and set a meaningful title to the group</sub> | ⚪ | - | - | - |
| AI File Context (AiFileContext)<br><sub>Set a context for the current document</sub> | ⚪ | 🟡 | 🟠 | 🟢 |
| AI Models (AiModels)<br><sub>Retrieve the list of available models for a specific provider</sub> | ⚪ | 🟡 | 🟠 | 🟢 |
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
| list_evaluate | Evaluates a list based on a natural language question | ⚪ | 🟡 | 🟠 | 🟢 |
| list_filter | Filters a list based on natural language criteria | ⚪ | 🟡 | 🟠 | 🟢 |
| list_generate | Generates a list based on a natural language prompt | ⚪ | 🟡 | 🟠 | 🟢 |
| script_review | Review a script for potential issues using AI-powered checks | ⚪ | 🟡 | 🟠 | 🟢 |
| script_edit | Modify the script from an existing component | ⚪ | 🟡 | - | - |
| script_new | Place a new script component from a natural language prompt | ⚪ | 🟡 | 🟠 | 🟢 |
| json_generate | Generate an AI response in strict JSON output | ⚪ | - | - | - |
| web_fetch_page_text | Retrieve plain text content of a webpage, excluding HTML, scripts, and images, with robots.txt compliance | ⚪ | 🟡 | 🟠 | 🟢 |
| web_search_rhino_forum | Search Rhino Discourse forum posts by query and return matching results | ⚪ | 🟡 | 🟠 | 🟢 |
| web_get_rhino_forum_post | Retrieve full JSON of a Rhino Discourse forum post by ID | ⚪ | 🟡 | 🟠 | 🟢 |
| get_input | Send data from Grasshopper to AI Chat | ⚪ | - | - | - |
| get_output | Receive data from AI Chat to Grasshopper | ⚪ | - | - | - |
| gh_get | Retrieve Grasshopper components as GhJSON with optional filters | ⚪ | 🟡 | 🟠 | 🟢 |
| gh_put | Place Grasshopper components on the canvas from GhJSON format | ⚪ | 🟡 | 🟠 | 🟢 |
| gh_toggle_preview | Toggle component preview on or off by GUID | ⚪ | 🟡 | 🟠 | 🟢 |
| gh_toggle_lock | Toggle component lock (enable/disable) by GUID | ⚪ | 🟡 | 🟠 | 🟢 |
| gh_move_obj | Move component pivot by GUID with absolute or relative positioning | ⚪ | 🟡 | 🟠 | 🟢 |
| gh_tidy_up | Organize selected components into a tidy grid layout | ⚪ | 🟡 | 🟠 | 🟢 |
| gh_generate | Generate Grasshopper definitions using AI | ⚪ | - | - | - |
| gh_group | Group components and set a meaningful title | ⚪ | - | - | - |

Is there something missing? Do you have a suggestion? Please open a discussion in the [Ideas](https://github.com/architects-toolkit/SmartHopper/discussions/categories/ideas) section in the Discussions tab.

## ➡️ Available Providers

SmartHopper is currently supporting the following AI providers:

| Provider | Status | Link to API registration |
|----------|:------:|-------------------|
| [MistralAI](https://mistral.ai/) | ✅ Supported | [Le Plateforme](https://console.mistral.ai/) |
| [OpenAI](https://openai.com/) | ✅ Supported | [OpenAI Platform](https://platform.openai.com/) |
| [DeepSeek](https://deepseek.com/) | ✅ Supported | [DeepSeek Platform](https://platform.deepseek.com/) |
| [Anthropic](https://anthropic.com/) | 🔜 Planned | [Anthropic Console](https://console.anthropic.com/) |

## 🔢 Supported Data Types

SmartHopper is designed to work with various Grasshopper-native data types. Additional geometric and complex data types will be added in future releases. Stay tuned for updates!

| Data Type | Status |
|-----------|:------:|
| Text | ✅ Supported |
| Number | ✅ Supported |
| Integer | ✅ Supported |
| Boolean | ✅ Supported |
| Point | 🔜 Planned |
| Plane | 🔜 Planned |
| Line | 🔜 Planned |
| Circle | 🔜 Planned |

## 📚 Usage Examples

No usage examples available at the moment.

## 🤝 Contributing

Every great innovation starts with a single contribution. Whether you're a designer, developer, or AI enthusiast, your unique perspective can help shape the future of computational design tools.

Please see our [Contributing Guidelines](CONTRIBUTING.md) for details on how to contribute to this project.

## 📝 Changelog

See [Releases](https://github.com/architects-toolkit/SmartHopper/releases) for a list of changes and updates.

## ⚖️ License

This project is licensed under the GNU Lesser General Public License v3 (LGPL) - see the [LICENSE](LICENSE) file for details.
