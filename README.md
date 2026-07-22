# SmartHopper - AI-Powered Tools and Assistant for Grasshopper3D

[![Version](https://img.shields.io/badge/version-2.0.0--dev.260513-brown?style=for-the-badge)](https://github.com/architects-toolkit/SmartHopper/releases)
[![Status](https://img.shields.io/badge/status-Unstable%20Development-brown?style=for-the-badge)](https://github.com/architects-toolkit/SmartHopper/releases)
[![.NET CI](https://img.shields.io/github/actions/workflow/status/architects-toolkit/SmartHopper/.github/workflows/ci-dotnet-tests.yml?label=tests&logo=dotnet&style=for-the-badge)](https://github.com/architects-toolkit/SmartHopper/actions/workflows/ci-dotnet-tests.yml)
[![Ready to use](https://img.shields.io/badge/ready_to_use-NO-brown?style=for-the-badge)](https://smarthopper.xyz/#installation)
[![License](https://img.shields.io/badge/license-LGPL%20v3-white?style=for-the-badge)](https://github.com/architects-toolkit/SmartHopper/blob/main/LICENSE)
[![Ask DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/architects-toolkit/SmartHopper)

<div align="center">

[![SmartHopper](./img/smarthopper.png)](https://smarthopper.xyz)

**Design with AI directly on your Grasshopper canvas — chat, code, organize, and build smarter definitions faster.**

</div>

---

SmartHopper brings a context‑aware AI assistant and a suite of AI‑powered components into Grasshopper3D.

- 💬 **Canvas assistant**
  An on‑canvas AI chat truly aware of your components and connected to the McNeel forum for real answers.

- 📝 **Script generator**
  Get help creating, editing and fixing Script components in Python, IronPython, C#, and VB<span>.NET</span>.

- 🔧 **AI‑powered components**
  Use Grasshopper in a way that was impossible before AI — generate text, generate and sort lists based on prompt, create images, convert files to Markdown, and more.

- 🤝 Multiple compatible providers (check the [full provider feature matrix](DEV.md#➡️-available-providers) for details)

  - ![MistralAI](src/SmartHopper.Providers.MistralAI/Resources/mistralai_icon.png) [MistralAI](https://mistral.ai/)
  - ![OpenAI](src/SmartHopper.Providers.OpenAI/Resources/openai_icon.png) [OpenAI](https://openai.com/)
  - ![DeepSeek](src/SmartHopper.Providers.DeepSeek/Resources/deepseek_icon.png) [DeepSeek](https://deepseek.com/)
  - ![Anthropic](src/SmartHopper.Providers.Anthropic/Resources/anthropic_icon.png) [Anthropic](https://anthropic.com/)
  - ![OpenRouter](src/SmartHopper.Providers.OpenRouter/Resources/openrouter_icon.png) [OpenRouter](https://openrouter.ai/)
  - ![Gemini](src/SmartHopper.Providers.Gemini/Resources/gemini_icon.png) [Google Gemini](https://ai.google.dev/)
  - ![LocalAI](src/SmartHopper.Providers.LocalAI/Resources/localai_icon.png) [LocalAI](https://localai.io/) — self-hosted, OpenAI-compatible
  - ![Ollama](src/SmartHopper.Providers.Ollama/Resources/ollama_icon.png) [Ollama](https://ollama.com/) — local, OpenAI-compatible

- Open Source — and it will always be.

## 💻 Installation

Install, enable a provider, and set up an API key.

**System requirements:**

- Rhino 8.0 or newer on Windows or macOS
- Distributed through the Rhino Package Manager
- You need to have a provider API key to use most of SmartHopper features

[![Quickstart ▶](./img/video-installation.jpg)](https://vimeo.com/1126454690 "Quickstart ▶ — click to watch on Vimeo")

[View the video on Vimeo](https://vimeo.com/1126454690)

## 🚀 How to use

### Canvas assistant (AI chat)

Start a chat, ask for help, search the McNeel forum, or talk about life.

[![Canvas Assistant ▶](./img/video-chat.png)](https://vimeo.com/1126454713 "Canvas Assistant ▶ — click to watch on Vimeo")

[View the video on Vimeo](https://vimeo.com/1126454713)

### Generate and Edit Script Components

Create powerful scripts in seconds. Let AI write, review, and refine your code following your instructions.

[![Generate and Edit Script Components ▶](./img/video-scripting.jpg)](https://vimeo.com/1144166204 "Generate and Edit Script Components ▶ — click to watch on Vimeo")

[View the video on Vimeo](https://vimeo.com/1144166204)

### AI-powered components

Do things that were impossible before.

[![AI Components ▶](./img/video-components.png)](https://vimeo.com/1126454744 "AI Components ▶ — click to watch on Vimeo")

[View the video on Vimeo](https://vimeo.com/1126454744)

Choose a default provider, or specify a provider for each component.

[![Select AI provider ▶](./img/video-select-provider.jpg)](https://vimeo.com/1126547055 "Select AI provider ▶ — click to watch on Vimeo")

[View the video on Vimeo](https://vimeo.com/1126547055)

More examples and recipes coming soon on the website and docs.

Developer details (AI tools, providers, data types, status) can be found in [DEV.md](DEV.md).

## 🤝 Contributing

Every great innovation starts with a single contribution. Whether you're a designer, developer, or AI enthusiast, your unique perspective can help shape the future of computational design tools.

Please see our [Contributing Guidelines](CONTRIBUTING.md) for details on how to contribute to this project.

## 📝 Changelog

See [Releases](https://github.com/architects-toolkit/SmartHopper/releases) for a list of changes and updates.

## ⚖️ License

This project is licensed under the GNU Lesser General Public License v3 (LGPL) - see the [LICENSE](LICENSE) file for details.

## ™️ Trademark and Logo Usage Policy

The SmartHopper name and logo are the property of the SmartHopper / architects-toolkit maintainers. The LGPL v3 license under which the source code is distributed does **not** grant rights in the SmartHopper name or logo.

We allow use of the SmartHopper name and logo in the following contexts:

- In blog posts, articles, tutorials, talks, or reviews that discuss or promote SmartHopper, provided the usage is accurate, fair, and does not imply endorsement by the SmartHopper maintainers.
- In educational materials, courses, or presentations that accurately represent the project.
- When referring to the unmodified, official SmartHopper plug-in as installed from the Rhino Package Manager.

Please refrain from using the SmartHopper name and logo:

- In promotional materials for paid services or commercial products that bundle, redistribute, or extend SmartHopper, without prior written permission.
- In a manner that may cause confusion about the origin of a product or imply endorsement.
- On forks, derivative works, or paid offerings derived from the SmartHopper source code — please choose a distinct name and logo for your fork, as is common open-source trademark practice.

If you have any questions or wish to seek permission for other uses, please open an issue on this repository or contact the maintainers via [smarthopper.xyz](https://smarthopper.xyz).

Thank you for your understanding and cooperation.

---

<div align="center">
Started in Barcelona — spread worldwide    •    <a href="https://smarthopper.xyz">smarthopper.xyz</a>
</div>
