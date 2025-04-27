# SmartHopper - AI-Powered Grasshopper3D Plugin

[![Version](https://img.shields.io/badge/version-0%2E3%2E1--dev%2E250427-brown)](https://github.com/architects-toolkit/SmartHopper/releases)
[![Status](https://img.shields.io/badge/status-Unstable%20Development-brown)](https://github.com/architects-toolkit/SmartHopper/releases)
[![Test results](https://img.shields.io/github/actions/workflow/status/architects-toolkit/SmartHopper/.github/workflows/ci-dotnet-tests.yml?label=.NET%20CI&logo=dotnet)](https://github.com/architects-toolkit/SmartHopper/actions/workflows/ci-dotnet-tests.yml)
[![Grasshopper](https://img.shields.io/badge/plugin_for-Grasshopper3D-darkgreen?logo=rhinoceros)](https://www.rhino3d.com/)
[![MistralAI](https://img.shields.io/badge/AI--powered-MistralAI-orange?logo=mistralai)](https://mistral.ai/)
[![OpenAI](https://img.shields.io/badge/AI--powered-OpenAI-blue?logo=openai)](https://openai.com/)
[![License](https://img.shields.io/badge/license-LGPLv3-white)](LICENSE)

SmartHopper is a groundbreaking plugin that enables AI to directly interact with your Grasshopper canvas! Ask for help, search on the McNeel forum, reorganize components, toggle preview on or off, and much more, just by chatting with your customizable AI assistant. Additionally, this plugins includes multiple components that correspond to individual AI tools, so that you can integrate text- or list-based operations directly into your definition.

## 🎯 Key Features

- 🔍 **Direct AI Access to Grasshopper Files**: SmartHopper allows AI to read and understand your Grasshopper definitions through GhJSON conversion, enabling intelligent analysis and manipulation of parametric models.
- 🧠 **AI-Powered Workflow Enhancement**: Leverage AI to generate text, evaluate designs, filter data, and more - all within your familiar Grasshopper environment.
- 🤖 **Multiple AI Provider Support**: Choose between [**MistralAI**](https://mistral.ai/) and [**OpenAI**](https://openai.com/) APIs. You need to [provide your own API keys](#️-Available-Providers).
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

SmartHopper is not yet available through Food4Rhino. We will be releasing it soon! In the meanwhile, you can download it directly from the [Releases](https://github.com/architects-toolkit/SmartHopper/releases) section in this repository.

## 📊 Development Status

| Component | Planned | In Progress | Testing | Released 🎉 |
|-----------|:-------:|:-----------:|:-------:|:------------------------:|
| Grasshopper Get Components (GhGet)<br><sub>Read the current Grasshopper file and convert it to GhJSON format. Optionally filter them by runtime messages: errors, warnings, remarks</sub> | ⚪ | 🟡 | 🟠 | 🟢 |
| Grasshopper Put Components (GhPut)<br><sub>Place components on the canvas from a GhJSON format</sub> | ⚪ | 🟡 | 🟠 | 🟢 |
| AI Grasshopper Generate Definitions (AIGhGenerate)<br><sub>Automatically generate Grasshopper definitions using AI</sub> | ⚪ | - | - | - |
| AI Text Evaluate (AiTextEvaluate)<br><sub>Return a boolean from a text content using AI-powered checks</sub> | ⚪ | 🟡 | 🟠 | 🟢 |
| AI Text Generate (AiTextGenerate)<br><sub>Generate text content using AI language models</sub> | ⚪ | 🟡 | 🟠 | 🟢 |
| AI List Evaluate (AiListEvaluate)<br><sub>Return a boolean from a list of elements using AI analysis</sub> | ⚪ | 🟡 | 🟠 | 🟢 |
| AI List Filter (AiListFilter)<br><sub>Process items in lists (reorder, shuffle, filter, etc.) based on AI-driven rules</sub> | ⚪ | 🟡 | 🟠 | 🟢 |
| AI List Generate (AiListGenerate)<br><sub>Generate lists dynamically using AI algorithms</sub> | ⚪ | - | - | - |
| AI GroupTitle (AiGroupTitle)<br><sub>Group components and set a meaningful title to the group</sub> | ⚪ | - | - | - |
| AI File Context (AiFileContext)<br><sub>Set a context for the current document</sub> | ⚪ | 🟡 | 🟠 | 🟢 |
| AI Chat (AiChat)<br><sub>Interactive AI-powered conversational interface</sub> | ⚪ | 🟡 | - | - |
| AI Chat Input (AiChatInput)<br><sub>Send some data from your Grasshopper to the AI Chat</sub> | ⚪ | - | - | - |
| AI Chat Output (AiChatOutput)<br><sub>Receive some data from the AI Chat to your Grasshopper</sub> | ⚪ | - | - | - |
| Context Parameters (ContextParameters)<br><sub>Set context parameters for the AI component</sub> | ⚪ | - | - | - |
| Deconstruct Metrics (DeconstructMetrics)<br><sub>Break down the usage metrics into individual values</sub> | ⚪ | 🟡 | 🟠 | 🟢 |
| Save GhJSON file (SaveGhJSON)<br><sub>Save the current Grasshopper file as a GhJSON format</sub> | ⚪ | - | - | - |
| Load GhJSON file (LoadGhJSON)<br><sub>Load a GhJSON file and convert it to a Grasshopper document</sub> | ⚪ | - | - | - |

Is there something missing? Do you have a suggestion? Please open a discussion in the [Ideas](https://github.com/architects-toolkit/SmartHopper/discussions/categories/ideas) section in the Discussions tab.

## ➡️ Available Providers

SmartHopper is currently supporting the following AI providers:

| Provider | Status | Link to API registration |
|----------|:------:|-------------------|
| [MistralAI](https://mistral.ai/) | ✅ Supported | [Le Plateforme](https://console.mistral.ai/) |
| [OpenAI](https://openai.com/) | ✅ Supported | [OpenAI Platform](https://platform.openai.com/) |
| [Anthropic](https://anthropic.com/) | 🔜 Planned | [Anthropic Console](https://console.anthropic.com/) |
| [DeepSeek](https://deepseek.com/) | 🔜 Planned | [DeepSeek Platform](https://platform.deepseek.com/) |

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

**Disclaimer:** All videos correspond to previous versions. Since version 0.2.0, the plugin made a huge step forward and the interface has been completely redesigned. It looks much better now, and its functionality has been enhanced.
### 1 Generate a Title for the Current Document

[![SmartHopper_Generate Title](https://i.vimeocdn.com/filter/overlay?src0=https%3A%2F%2Fi.vimeocdn.com%2Fvideo%2F1966651352-6cfca3b39c99d01e9bbdb1590ac7f1325d35b8dfa16e0b5ced2aad704eef2bbe-d_295x166&src1=http%3A%2F%2Ff.vimeocdn.com%2Fp%2Fimages%2Fcrawler_play.png)](https://vimeo.com/1043447175)

### 2 Working with a CSV file

[![SmartHopper_Working With CSV](https://i.vimeocdn.com/filter/overlay?src0=https%3A%2F%2Fi.vimeocdn.com%2Fvideo%2F1966651410-3ca705fd2e8fe276e9ee965339714c2de11f579ecf73d39fa92299b7b9015707-d_295x166&src1=http%3A%2F%2Ff.vimeocdn.com%2Fp%2Fimages%2Fcrawler_play.png)](https://vimeo.com/1043447217)

## ⚙️ Configuration

### Set Up the API Key and Other Settings

[![SmartHopper_Settings](https://i.vimeocdn.com/filter/overlay?src0=https%3A%2F%2Fi.vimeocdn.com%2Fvideo%2F1966651378-ee922c5452393594d6ec931f112e24c82cceadb6c5dab80ced5e362a13ef0d45-d_200x150&src1=http%3A%2F%2Ff.vimeocdn.com%2Fp%2Fimages%2Fcrawler_play.png)](https://vimeo.com/1043447205)

### Choose the Provider for each Component

[![SmartHopper_Select Provider](https://i.vimeocdn.com/filter/overlay?src0=https%3A%2F%2Fi.vimeocdn.com%2Fvideo%2F1966651347-eb497ba95d6fc8008fdb3db9b6288dbdaa4b7b4ab7a3f5f99ccd55495545a00f-d_200x150&src1=http%3A%2F%2Ff.vimeocdn.com%2Fp%2Fimages%2Fcrawler_play.png)](https://vimeo.com/1043447190)

## ⏩ Developing the Chat Interface

**Disclaimer:** The following video was a previous conceptual preview of the chat interface. From version 0.2.0, the chat interface is now available with a different design. Features will be added in future releases.

[![SmartHopper_Chat Concept](https://i.vimeocdn.com/filter/overlay?src0=https%3A%2F%2Fi.vimeocdn.com%2Fvideo%2F1966657705-a1e9c281ab11e341df94bd14ee797d816afe34413b5af057841d6eb6191595fd-d_295x166&src1=http%3A%2F%2Ff.vimeocdn.com%2Fp%2Fimages%2Fcrawler_play.png)](https://vimeo.com/1043452514)

## 🤝 Contributing

Every great innovation starts with a single contribution. Whether you're a designer, developer, or AI enthusiast, your unique perspective can help shape the future of computational design tools.

Please see our [Contributing Guidelines](CONTRIBUTING.md) for details on how to contribute to this project.

## 📝 Changelog

See [Releases](https://github.com/architects-toolkit/SmartHopper/releases) for a list of changes and updates.

## ⚖️ License

This project is licensed under the GNU Lesser General Public License v3 (LGPL) - see the [LICENSE](LICENSE) file for details.
