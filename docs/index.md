# SmartHopper Documentation

SmartHopper is an AI-powered plugin for Grasshopper 3D. This documentation covers usage, APIs, and architecture.

---
- [Architecture overview](Architecture.md)
- [Architecture deep dives](Architecture/) — focused design docs (e.g. [MCP server](Architecture/mcp-server.md))

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `N/A` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

This is the root documentation hub for SmartHopper. It organizes all guides, references, and architecture docs so you can quickly find what you need based on your role.

**You should read this if you:**

- Are new to SmartHopper and need to find the right starting point
- Want to browse the full catalog of components, providers, or architecture topics
- Are looking for development, review, or contribution guidelines

---

## End-User Guide

### Start Here

| If you are a... | Start with |
| --- | --- |
| **Grasshopper user** wanting to use AI | [Getting Started](GETTING_STARTED/index.md) |
| **Developer** building on SmartHopper | [API Reference](API_REFERENCE/index.md) |
| **Software Architect** understanding the system | [Architecture Overview](Architecture.md) |

---

### By Topic

#### Components

- [All Components](Components/index.md) -- full catalog
- [Input Components](Components/Input/index.md) -- wrap data for AI processing
- [Output Components](Components/Output/index.md) -- call AI and extract typed results
- [AI Components](Components/AI/index.md) -- model management and legacy monolithic components
- [Knowledge Components](Components/Knowledge/index.md) -- file conversion, web scraping, forums
- [Grasshopper Components](Components/Grasshopper/index.md) -- GH definition manipulation
- [Audio Components](Components/Audio/index.md) -- audio playback and visualization
- [Misc Components](Components/Misc/index.md) -- metrics and diagnostics
- [Component Base Classes](Components/ComponentBase/index.md) -- inheritance hierarchy

#### Providers

- [Provider Overview](Providers/index.md) -- supported AI providers
- [AI Capabilities](Providers/AICapability.md) -- model capability flags
- [AI Call Pipeline](Providers/AICall/index.md) -- request/response processing

#### Architecture

- [Architecture Overview](Architecture.md) -- system design
- [AIInputPayload](Architecture/AIInputPayload.md) -- unified input format
- [AIRequestParameters](Architecture/AIRequestParameters.md) -- request customization
- [VersatileAudio](Architecture/VersatileAudio.md) -- audio type system
- [Design Decisions](DESIGN_DECISIONS/index.md) -- rationale behind key choices

#### Context & Tools

- [Context Providers](Context/index.md) -- environment, time, and file context
- [AI Tools](Tools/index.md) -- Grasshopper canvas utilities for AI

#### UI

- [User Interface](UI/index.md) -- chat UI, canvas overlays, and AI change review

#### Usage Guides

- [File to Markdown](Usage/file-to-markdown.md) -- document conversion subsystem
- [GhJSON](https://github.com/architects-toolkit/ghjson-dotnet) -- Grasshopper JSON format (external)

---

### Development

- [Authenticode Signing](Development/authenticode-signing.md) -- assembly signing for provider trust
- [Release workflow](RELEASE_WORKFLOW.md) -- branching, stabilization, and release promotion
- [Hotfix workflow](HOTFIX_WORKFLOW.md) -- shipping urgent fixes from a stable release tag

### Testing

- [Components Test Suite](Testing/index.md) -- test-only Grasshopper components for providers and utilities

### Reviews

- [Architecture Reviews](Reviews/index.md) -- analysis of SmartHopper components

### Documentation Guidelines

- [Templates](TEMPLATES/README.md) -- templates for new documentation files

---

## Developer Reference

### Creating a Custom AI Output Component

Components in SmartHopper derive from a layered base hierarchy. A minimal custom AI output component inherits from `AIOutputAdapterBase`, wires an `AIInputPayload` input, and maps the AI response to typed Grasshopper outputs:

```csharp
using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using SmartHopper.Core.ComponentBase;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;

public class MyAIOutputComponent : AIOutputAdapterBase
{
    public MyAIOutputComponent()
        : base("MyAI", "MyAI", "Calls AI and returns a custom result",
               GH_Exposure.primary)
    { }

    public override Guid ComponentGuid => new Guid("...");

    protected override IReadOnlyList<string> UsingAiTools => new[] { "text2text" };

    protected override string GetInternalSystemPrompt()
    {
        return "You are a helpful assistant.";
    }

    protected override IReadOnlyList<OutputMapping> GetOutputMappings()
    {
        return new[]
        {
            new OutputMapping
            {
                ParamName = "Result",
                NickName = "R",
                Description = "AI response",
                ParamType = typeof(Param_String),
                Access = GH_ParamAccess.tree,
                Extractor = OutputMapping.Single(aiReturn =>
                {
                    var text = aiReturn?.Body?.GetLastAssistantText();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return new GH_String(text.Trim());
                    }
                    return null;
                })
            }
        };
    }
}

```

The base class registers the `Input >` parameter for `AIInputPayload`, handles provider/model selection, and calls the AI. The output mapping extracts the final value from the returned `AIReturn`.

### Querying the Model Registry

Developers can inspect and select models programmatically via the `AIModelCapabilityRegistry`:

```csharp
// List all models for a provider that support image generation
var models = AIModelCapabilityRegistry.Instance.GetProviderModels("openai")
    .Where(m => m.HasCapability(AICapability.Text2Image))
    .OrderByDescending(m => m.Rank);

// Resolve the best model with fallback logic
var bestModel = AIModelCapabilityRegistry.Instance.SelectBestModel(
    providerName: "openai",
    userModel: null,
    requiredCapability: AICapability.Text2Text);

Console.WriteLine($"Selected model: {bestModel}");

```

---

## Architecture & Design

The SmartHopper documentation is organized into topical sections that mirror the codebase:

- **Components** covers every Grasshopper component end users interact with, grouped by category.
- **Providers** documents the AI provider plugin model, capabilities, and the call pipeline.
- **Architecture** contains deep dives into data structures, design decisions, and system behavior.
- **Context & Tools** explains how environment context is gathered and how AI tools modify the Grasshopper canvas.
- **UI** covers the WebChat interface and its bridge into Rhino/Grasshopper.
- **Development** hosts contributor-facing guides such as authenticode signing.
- **Reviews** archives architecture review analyses.

This structure ensures that users, developers, and architects each have a clear path to the information they need.
