# SmartHopper - AI-Powered Grasshopper3D Assistant and Tools

[![Version](https://img.shields.io/badge/version-1.0.0--dev.250927-brown?style=for-the-badge)](https://github.com/architects-toolkit/SmartHopper/releases)
[![Status](https://img.shields.io/badge/status-Unstable%20Development-brown?style=for-the-badge)](https://github.com/architects-toolkit/SmartHopper/releases)
[![.NET CI](https://img.shields.io/github/actions/workflow/status/architects-toolkit/SmartHopper/.github/workflows/ci-dotnet-tests.yml?label=tests&logo=dotnet&style=for-the-badge)](https://github.com/architects-toolkit/SmartHopper/actions/workflows/ci-dotnet-tests.yml)
[![Ready to use](https://img.shields.io/badge/ready_to_use-YES-brightgreen?style=for-the-badge)](https://smarthopper.xyz/#installation)
[![License](https://img.shields.io/badge/license-LGPL%20v3-white?style=for-the-badge)](https://github.com/architects-toolkit/SmartHopper/blob/main/LICENSE)

**Design with AI directly on your Grasshopper canvas — chat, generate, organize, and build smarter definitions faster.**

![SmartHopper hero placeholder](https://placehold.co/1200x400/111111/FFFFFF?text=SmartHopper+%E2%80%94+AI+for+Grasshopper+3D)

SmartHopper brings a context‑aware AI assistant and a suite of AI‑powered components into Grasshopper3D.

- 🔧 **AI‑powered components**
  Use Grasshopper in a way that was impossible before AI — generate text, generate and sort lists based on prompt, create images, and more.

- 💬 **Canvas assistant**
  An on‑canvas AI chat truly aware of your components and connected to the McNeel forum for real answers.

- 🤝 Multiple compatible providers (check the [full provider feature matrix](DEV.md#➡️-available-providers) for details)

  - ![MistralAI](src/SmartHopper.Providers.MistralAI/Resources/mistralai_icon.png) [MistralAI](https://mistral.ai/)
  - ![OpenAI](src/SmartHopper.Providers.OpenAI/Resources/openai_icon.png) [OpenAI](https://openai.com/)
  - ![DeepSeek](src/SmartHopper.Providers.DeepSeek/Resources/deepseek_icon.png) [DeepSeek](https://deepseek.com/)
  - ![Anthropic](src/SmartHopper.Providers.Anthropic/Resources/anthropic_icon.png) [Anthropic](https://anthropic.com/)
  - ![OpenRouter](src/SmartHopper.Providers.OpenRouter/Resources/openrouter_icon.png) [OpenRouter](https://openrouter.ai/)

- Open Source — and it will always be.

## 💻 Installation

Follow the official installation guide at [smarthopper.xyz/#installation](https://smarthopper.xyz/#installation).

Quick start via Rhino Package Manager (Yak):

- Open Rhino 8
- Run `PackageManager`
- Enable “Include pre-releases” (this is still alpha)
- Search for “SmartHopper” and install

## 🚀 How to use

### Getting started

Install, enable a provider, and set up an API key.

<a href="https://vimeo.com/1126454690" title="Quickstart — click to watch on Vimeo">
<div style="position: relative; display: inline-block;">
  <img src="./img/video-installation.png" alt="Quickstart ▶" style="width: 100%; max-width: 960px;">
  <div style="position: absolute; top: 50%; left: 50%; transform: translate(-50%, -50%); font-size: 4em; color: white; text-shadow: 2px 2px 4px rgba(0,0,0,0.7);">▶</div>
</div>
</a>

### Canvas assistant (AI chat)

Start a chat, ask for help, search the McNeel forum, or talk about life.

<a href="https://vimeo.com/1126454713" title="Canvas Assistant — click to watch on Vimeo">
<div style="position: relative; display: inline-block;">
  <img src="./img/video-chat.png" alt="Canvas Assistant ▶" style="width: 100%; max-width: 960px;">
  <div style="position: absolute; top: 50%; left: 50%; transform: translate(-50%, -50%); font-size: 4em; color: white; text-shadow: 2px 2px 4px rgba(0,0,0,0.7);">▶</div>
</div>
</a>

### AI-powered components

Do things that were impossible before.

<a href="https://vimeo.com/1126454744" title="AI Components — click to watch on Vimeo">
<div style="position: relative; display: inline-block;">
  <img src="./img/video-components.png" alt="AI Components ▶" style="width: 100%; max-width: 960px;">
  <div style="position: absolute; top: 50%; left: 50%; transform: translate(-50%, -50%); font-size: 4em; color: white; text-shadow: 2px 2px 4px rgba(0,0,0,0.7);">▶</div>
</div>
</a>

More examples and recipes coming soon on the website and docs.

Developer details (AI tools, providers, data types, status) can be found in [DEV.md](DEV.md).

## 🤝 Contributing

Every great innovation starts with a single contribution. Whether you're a designer, developer, or AI enthusiast, your unique perspective can help shape the future of computational design tools.

Please see our [Contributing Guidelines](CONTRIBUTING.md) for details on how to contribute to this project.

## 📝 Changelog

See [Releases](https://github.com/architects-toolkit/SmartHopper/releases) for a list of changes and updates.

## ⚖️ License

This project is licensed under the GNU Lesser General Public License v3 (LGPL) - see the [LICENSE](LICENSE) file for details.
