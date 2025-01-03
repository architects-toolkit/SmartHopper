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
| AI Text Alter (AiTextAlter)<br><sub>Modify and process text based on AI-driven criteria</sub> | ⚪ | - | - | - |
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

[![SmartHopper_Generate Title](https://i.vimeocdn.com/filter/overlay?src0=https%3A%2F%2Fi.vimeocdn.com%2Fvideo%2F1966651352-6cfca3b39c99d01e9bbdb1590ac7f1325d35b8dfa16e0b5ced2aad704eef2bbe-d_295x166&src1=http%3A%2F%2Ff.vimeocdn.com%2Fp%2Fimages%2Fcrawler_play.png)](https://vimeo.com/1043447175)

### 2 Working with a CSV file

[![SmartHopper_Working With CSV](https://i.vimeocdn.com/filter/overlay?src0=https%3A%2F%2Fi.vimeocdn.com%2Fvideo%2F1966651410-3ca705fd2e8fe276e9ee965339714c2de11f579ecf73d39fa92299b7b9015707-d_295x166&src1=http%3A%2F%2Ff.vimeocdn.com%2Fp%2Fimages%2Fcrawler_play.png)](https://vimeo.com/1043447217)

## ⚙️ Configuration

### Set Up the API Key and Other Settings

[![SmartHopper_Settings](https://i.vimeocdn.com/filter/overlay?src0=https%3A%2F%2Fi.vimeocdn.com%2Fvideo%2F1966651378-ee922c5452393594d6ec931f112e24c82cceadb6c5dab80ced5e362a13ef0d45-d_200x150&src1=http%3A%2F%2Ff.vimeocdn.com%2Fp%2Fimages%2Fcrawler_play.png)](https://vimeo.com/1043447205)

### Choose the Provider for each Component

[![SmartHopper_Select Provider](https://i.vimeocdn.com/filter/overlay?src0=https%3A%2F%2Fi.vimeocdn.com%2Fvideo%2F1966651347-eb497ba95d6fc8008fdb3db9b6288dbdaa4b7b4ab7a3f5f99ccd55495545a00f-d_200x150&src1=http%3A%2F%2Ff.vimeocdn.com%2Fp%2Fimages%2Fcrawler_play.png)](https://vimeo.com/1043447190)

## ⏩ Developing the Chat Interface (conceptual preview)

**Disclaimer:** The chat interface is a conceptual preview of future developments and is not currently implemented.

[![SmartHopper_Chat Concept](https://i.vimeocdn.com/filter/overlay?src0=https%3A%2F%2Fi.vimeocdn.com%2Fvideo%2F1966657705-a1e9c281ab11e341df94bd14ee797d816afe34413b5af057841d6eb6191595fd-d_295x166&src1=http%3A%2F%2Ff.vimeocdn.com%2Fp%2Fimages%2Fcrawler_play.png)](https://vimeo.com/1043452514)

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
