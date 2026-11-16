# Getting Started with SmartHopper

SmartHopper is an open-source AI plugin for Grasshopper 3D (Rhino 8) that connects language, vision, audio, and knowledge models to your parametric workflows.

> **Full user and developer documentation:** Visit **[docs.smarthopper.xyz](https://docs.smarthopper.xyz)** for step-by-step installation guides, provider setup walkthroughs, the visual Component Library, the AI Tools Library, and video tutorials.

## Two ways to work in SmartHopper

### 1. The Canvas Assistant & AI Chat

Click the **SmartHopper** button on the Grasshopper canvas toolbar (or drop an **AI Chat** component from `SmartHopper → A. AI`) to open an interactive chat assistant. It can read your selected components, explain or debug errors, search the McNeel and Ladybug forums, write Python or C# script components, and propose canvas edits through a review dialog with full `Ctrl+Z` undo.

### 2. Composable Input → Output components

When you want AI inside a parametric data flow, connect any **Input** component (`SmartHopper → B. Input`) into any **Output** component (`SmartHopper → C. Output`):

```text
[Input component]  ──(Input >)──►  [Output component]  ──►  [Typed Grasshopper output]
  (Text to AI,                       (AI to Text,
   Image to AI,                       AI to Number,
   File to AI…)                       AI to Boolean…)
```

- **Input components** package Grasshopper values, images, audio, local files, web pages, forum posts, or system instructions into a single `Input >` wire. Multiple inputs wired into the same branch are combined into one AI request.
- **Output components** call the selected AI provider and extract a typed Grasshopper result (text, list, number, integer, boolean, JSON, GhJSON, image, speech audio, or script code), plus per-branch JSON usage `Metrics`.

For simple single-step tasks, SmartHopper also provides one-step convenience components in `I. Text & Lists`, `G. Media`, and `J. Script`.

## Common component workflows

### Text generation

```text
[Panel] ──► [Text to AI] ──► [AI to Text] ──► [Panel]
```

### Vision (image analysis)

```text
[Viewport To Image] ──► [Image to AI] ─┐
[Panel: "Describe the massing"] ──► [Text to AI] ─┴──► [AI to Text] ──► [Panel]
```

### Extracting numbers or booleans for downstream components

```text
[Text to AI] ──► [AI to Number] ──► [Addition / Series / Domain]
```

### Summarizing a local PDF or Office document

```text
[File Path] ──► [File to AI] ──► [AI to Text] ──► [Panel]
```

## In-repo reference links

- [Components overview](../Components/index.md) — ribbon panels (`A. AI` through `K. Library`) and base class architecture
  - [Input components (`B. Input`)](../Components/Input/index.md)
  - [Output components (`C. Output`)](../Components/Output/index.md)
  - [AI, Settings, Metrics & Convenience components](../Components/AI/index.md)
  - [Canvas & GhJSON components (`D. GhJSON`)](../Components/Grasshopper/index.md)
  - [Forums & Documents components (`E. Forums`, `F. Documents`)](../Components/Knowledge/index.md)
  - [JSON components (`H. JSON`)](../Components/JSON/index.md)
  - [Metrics components (`A. AI`)](../Components/Misc/index.md)
- [Providers overview](../Providers/index.md) and [Model capabilities](../Providers/AICapability.md)
- [Architecture overview](../Architecture.md) and [AICall pipeline](../Providers/AICall/index.md)
