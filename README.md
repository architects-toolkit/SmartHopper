# SmartHopper - an AI-Powered Grasshopper3D Assistant (and more...)

<!-- markdownlint-disable MD033 -->

<p align="center">
  <a href="https://github.com/architects-toolkit/SmartHopper/releases">
    <img alt="Version" src="https://img.shields.io/badge/version-1.0.0--rc-purple?style=for-the-badge" />
  </a>
  <a href="https://github.com/architects-toolkit/SmartHopper/actions/workflows/ci-dotnet-tests.yml">
    <img alt=".NET CI" src="https://img.shields.io/github/actions/workflow/status/architects-toolkit/SmartHopper/.github/workflows/ci-dotnet-tests.yml?label=tests&logo=dotnet&style=for-the-badge" />
  </a>
  <a href="https://github.com/architects-toolkit/SmartHopper/releases">
    <img alt="Status" src="https://img.shields.io/badge/status-Release%20Candidate-purple?style=for-the-badge" />
  </a>
  <a href="https://smarthopper.xyz/#installation">
    <img alt="Ready to use" src="https://img.shields.io/badge/ready_to_use-YES-brightgreen?style=for-the-badge" />
  </a>
</p>

<p align="center"><b>Design with AI directly on your Grasshopper canvas — chat, generate, organize, and build smarter definitions faster.</b></p>

![SmartHopper hero placeholder](https://placehold.co/1200x400/111111/FFFFFF?text=SmartHopper+%E2%80%94+AI+for+Grasshopper+3D)

<p align="center">
  SmartHopper brings a context‑aware AI assistant and a suite of AI‑powered components into Grasshopper3D.
</p>

<div align="center">

✨ <b>Two strong points</b>

<br/>

<div style="display:inline-block; text-align:left; max-width: 900px;">

  <p>🔧 <b>AI‑powered components</b><br/>
  <span style="color:#666;">Use Grasshopper in a way that was impossible before AI — generate text, sort lists, create images, structure JSON, write scripts, and more.</span></p>

  <p>💬 <b>Canvas assistant</b><br/>
  <span style="color:#666;">An on‑canvas AI chat truly aware of your components and connected to the McNeel forum for real answers.</span></p>

</div>

<br/>

🤝 <b>Compatible providers</b>

<div>
  <span><img alt="MistralAI" src="src/SmartHopper.Providers.MistralAI/Resources/mistralai_icon.png" width="16" height="16" style="vertical-align:middle; margin-right:6px;" /> <a href="https://mistral.ai/">MistralAI</a></span> •
  <span><img alt="OpenAI" src="src/SmartHopper.Providers.OpenAI/Resources/openai_icon.png" width="16" height="16" style="vertical-align:middle; margin-right:6px;" /> <a href="https://openai.com/">OpenAI</a></span> •
  <span><img alt="DeepSeek" src="src/SmartHopper.Providers.DeepSeek/Resources/deepseek_icon.png" width="16" height="16" style="vertical-align:middle; margin-right:6px;" /> <a href="https://deepseek.com/">DeepSeek</a></span> •
  <span><img alt="Anthropic" src="src/SmartHopper.Providers.Anthropic/Resources/anthropic_icon.png" width="16" height="16" style="vertical-align:middle; margin-right:6px;" /> <a href="https://anthropic.com/">Anthropic</a></span> •
  <span><img alt="OpenRouter" src="src/SmartHopper.Providers.OpenRouter/Resources/openrouter_icon.png" width="16" height="16" style="vertical-align:middle; margin-right:6px;" /> <a href="https://openrouter.ai/">OpenRouter</a></span>
</div>

<br/>

👐 <b>Open Source</b> — and it will always be.

</div>

## 💻 Installation

Follow the official installation guide at [smarthopper.xyz/#installation](https://smarthopper.xyz/#installation).

Quick start via Rhino Package Manager (Yak):

- Open Rhino 8
- Run `PackageManager`
- Enable “Include pre-releases” (this is still alpha)
- Search for “SmartHopper” and install

## 🚀 How to use (TODO)

Use this section as a guided tour. We’ll keep it concise and visual. You can help us improve it by opening a PR.

<div align="center" style="display:grid; grid-template-columns: repeat(auto-fit, minmax(260px, 1fr)); gap: 16px; align-items: stretch;">

  <div style="border:1px solid #eee; border-radius:12px; padding:16px; text-align:left; max-width: 520px; display:inline-block;">
    <h4>⚡ Getting started in 60 seconds</h4>
    <ul>
      <li>Install, enable a provider, drop a component, run.</li>
    </ul>
    <img alt="Quickstart" src="https://placehold.co/960x540?text=Quickstart" style="width:100%; border-radius:8px;" />
  </div>

  <div style="border:1px solid #eee; border-radius:12px; padding:16px; text-align:left; max-width: 520px; display:inline-block;">
    <h4>💬 Canvas assistant (AI chat)</h4>
    <ul>
      <li>Start a chat, tidy up components, search the McNeel forum, or generate a script.</li>
    </ul>
    <img alt="Canvas chat" src="https://placehold.co/960x540?text=Canvas+Assistant" style="width:100%; border-radius:8px;" />
  </div>

  <div style="border:1px solid #eee; border-radius:12px; padding:16px; text-align:left; max-width: 520px; display:inline-block;">
    <h4>🧩 AI components</h4>
    <ul>
      <li>Examples: Text Generate, List Generate, JSON Generate, Image Generate, Script New/Review/Edit.</li>
    </ul>
    <img alt="AI Components" src="https://placehold.co/960x540?text=AI+Components" style="width:100%; border-radius:8px;" />
  </div>

  <div style="border:1px solid #eee; border-radius:12px; padding:16px; text-align:left; max-width: 520px; display:inline-block;">
    <h4>🔐 Provider setup</h4>
    <ul>
      <li>Configure an API key for your preferred provider in Settings.</li>
    </ul>
    <img alt="Providers setup" src="https://placehold.co/960x540?text=Providers+Setup" style="width:100%; border-radius:8px;" />
  </div>

</div>

More examples and recipes coming soon on the website and docs.

Developer details (AI tools, providers, data types, status) can be found in [DEV.md](DEV.md).

## 🤝 Contributing

Every great innovation starts with a single contribution. Whether you're a designer, developer, or AI enthusiast, your unique perspective can help shape the future of computational design tools.

Please see our [Contributing Guidelines](CONTRIBUTING.md) for details on how to contribute to this project.

## 📝 Changelog

See [Releases](https://github.com/architects-toolkit/SmartHopper/releases) for a list of changes and updates.

## ⚖️ License

This project is licensed under the GNU Lesser General Public License v3 (LGPL) - see the [LICENSE](LICENSE) file for details.
