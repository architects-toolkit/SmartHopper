# SmartHopper - Bringing AI to Grasshopper3D

[![Version](https://img.shields.io/badge/version-0%2E0%2E0--dev%2E250101-yellow)](https://github.com/architects-toolkit/SmartHopper/releases/tag/0.0.0-dev.250101)
[![Status](https://img.shields.io/badge/status-Unstable%20development-yellow)](https://github.com/architects-toolkit/SmartHopper/releases/tag/0.0.0-dev.250101)
[![Grasshopper](https://img.shields.io/badge/plugin_for-Grasshopper3D-darkgreen?logo=rhinoceros)](https://www.rhino3d.com/)
[![MistralAI](https://img.shields.io/badge/AI--powered-MistralAI-orange)](https://mistral.ai/)
[![OpenAI](https://img.shields.io/badge/AI--powered-OpenAI-blue?logo=openai)](https://openai.com/)
[![License](https://img.shields.io/badge/license-LGPLv3-white)](LICENSE)

SmartHopper brings the power of AI assistance directly into your Grasshopper workflow! This innovative plugin seamlessly integrates advanced AI capabilities into the Grasshopper environment, enabling architects, engineers, and designers, to enhance their parametric design process.

## 🎯 Why SmartHopper?

- 🖌️ SmartHopper is coded by designers, to designers.
- 🤖 The emerging artificial intelligence revolution is transforming the way we work. SmartHopper is a tool that empowers designers to harness the power of AI in your Grasshopper3D projects.
- ⚖️ SmartHopper is about making choices. It integrates **MistralAI** and **OpenAI** APIs. Provide your own API keys and pay for usage.
- ☁️ **SmartHopper Cloud** *[coming soon]* is a custom implementation of the MistralAI API that enhances the native AI capabilities by adding cascaded prompts and RAG, providing necessary context on the Grasshopper3D environment.

## 👥 Who Is This For?
- **Architects and engineers** looking to enhance their parametric design workflow.
- **Computational designers** seeking AI assistance in their Grasshopper projects.
- Anyone interested in exploring the intersection of AI and parametric design.

## 🛠️ Technical Details
- 🌱 Compatible with Data Tree processing in Grasshopper3D.
- ⌚ Asynchronous execution to prevent blocking the canvas.
- 🔄 Run the components with a Button (single manual run) or a Boolean Toggle (run on every input change).
- 🏗️ Optimized for parallel processing.
- 🧱 Clean, modular architecture for stability and future extensibility.

## 💻 Installation

SmartHopper is not yet available through Food4Rhino. We will be releasing it soon! In the meanwhile, you can download it directly from the [Releases](https://github.com/architects-toolkit/SmartHopper/releases) section in this repository.

## 📊 Development Status

| Component | Planned | In Progress | Testing | Released 🎉 |
|-----------|:-------:|:-----------:|:-------:|:------------------------:|
| Grasshopper Get Components (GhGet)<br><sub>Read the current Grasshopper file and convert it to GhJSON format</sub> | ⚪ | 🟡 | 🟠 | - |
| Grasshopper Put Components (GhPut)<br><sub>Place components on the canvas from a GhJSON format</sub> | ⚪ | 🟡 | 🟠 | - |
| AI Grasshopper Generate Definitions (GhGenerate)<br><sub>Automatically generate Grasshopper definitions using AI</sub> | ⚪ | - | - | - |
| AI Text Check (AiTextCheck)<br><sub>Return a boolean from a text content using AI-powered checks</sub> | ⚪ | - | - | - |
| AI Text Filter (AiTextFilter)<br><sub>Filter and process text based on AI-driven criteria</sub> | ⚪ | - | - | - |
| AI Text Generate (AiTextGenerate)<br><sub>Generate text content using AI language models</sub> | ⚪ | 🟡 | 🟠 | - |
| AI List Check (AiListCheck)<br><sub>Return a boolean from a list of elements using AI analysis</sub> | ⚪ | 🟡 | 🟠 | - |
| AI List Filter (AiListFilter)<br><sub>Filter and process items in lists based on AI-driven rules</sub> | ⚪ | 🟡 | 🟠 | - |
| AI List Generate (AiListGenerate)<br><sub>Generate lists dynamically using AI algorithms</sub> | ⚪ | - | - | - |
| AI GroupTitle (AiGroupTitle)<br><sub>Group components and set a meaningful title to the group</sub> | ⚪ | - | - | - |
| AI Chat (AiChat)<br><sub>Interactive AI-powered conversational interface</sub> | ⚪ | - | - | - |
| Deconstruct Metrics (DeconstructMetrics)<br><sub>Break down the usage metrics into individual values</sub> | ⚪ | 🟡 | 🟠 | - |

Is there something missing? Do you have a suggestion? Please open a [Feature Request](https://github.com/architects-toolkit/SmartHopper/issues/new/choose) in the Issues tab.

## 📚 Usage Examples

### 1 Generate a Title for the Current Document

https://github.com/user-attachments/assets/84439de6-91b9-4bac-b24f-ce4f2130b84d

### 2 Working with a CSV file

https://github.com/user-attachments/assets/364d3c09-a1a8-46eb-a173-77869548f33b

## ⚙️ Configuration

### Set Up the API Key and Other Settings

https://github.com/user-attachments/assets/cec6aad1-3b41-4e2d-8937-495477e2e280

### Choose the Provider for each Component

https://github.com/user-attachments/assets/457594e7-06c8-4d37-a82c-bcb9d70a57ef

## ⏩ Developing the Chat Interface (conceptual preview)

**Disclaimer:** The chat interface is a conceptual preview of future developments and is not currently implemented.

https://github.com/user-attachments/assets/9672f746-0d55-4414-b4a8-074208532458

## 🤝 Contributing

Every great innovation starts with a single contribution. Whether you're a designer, developer, or AI enthusiast, your unique perspective can help shape the future of computational design tools.

Please see our [Contributing Guidelines](CONTRIBUTING.md) for details on how to contribute to this project.

## 📝 Changelog

See [Releases](https://github.com/architects-toolkit/SmartHopper/releases) for a list of changes and updates.

## ⚖️ License

This project is licensed under the GNU Lesser General Public License v3 (LGPL) - see the [LICENSE](LICENSE) file for details.

---

<div align="center">
Made with ❤️ by designers, for designers
</div>
