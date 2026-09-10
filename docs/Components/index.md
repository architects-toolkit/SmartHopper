# Components

Grasshopper components that form the user-facing interface for SmartHopper.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Core.Grasshopper/` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

This page explains the organization and behavior of all SmartHopper Grasshopper components. Understanding the component architecture helps you navigate the hierarchy and apply best practices when building or extending components.

**You should read this if you:**

- Want to understand the SmartHopper component architecture
- Need to find specific component categories
- Are developing new components

---

## End-User Guide

### Purpose

Expose AI capabilities (chat, list/text generation, image generation, canvas utilities) as standard GH components.

### Key locations

- `src/SmartHopper.Components/` — production components
- `src/SmartHopper.Components.Test/` — test-only components (not built in Release)
- [Component bases](./ComponentBase/index.md) in `src/SmartHopper.Core/ComponentBase/` — full hierarchy and per-class docs (AsyncComponentBase, StatefulComponentBase, AIProviderComponentBase, AIStatefulAsyncComponentBase, Selecting* bases, AIInputAdapterBase, AIOutputAdapterBase, ComponentStateManager, BatchSentinel, …).
  - AI catalog: [AI Components](./AI/index.md)
  - JSON components: [JSON](./JSON/index.md)
  - Test components: [Test](./Test/index.md)
  - [IO](./IO/index.md) — safe, versioned persistence for component outputs

### Behavior

- Components construct `AIBody`, select provider/model, and execute `AIRequestCall`.
- Metrics and errors are surfaced on outputs and runtime messages.
- Supports both button and toggle Run patterns; debounce and state transitions manage re-execution.

### Best practices

- Set `RunOnlyOnInputChanges` appropriately for your component.
- Ensure UI changes occur on Rhino's UI thread.
- Validate tool schemas and model capabilities; give clear, actionable errors.

---

## Developer Reference

Components derive from bases in `src/SmartHopper.Core/ComponentBase/`. The most common starting point for an AI output component is `AIOutputAdapterBase`, which wires an `AIInputPayload` input and maps the provider response:

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
        : base("My AI", "MYAI", "Description", GH_Exposure.primary)
    { }

    public override Guid ComponentGuid => new Guid("YOUR-GUID-HERE");

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
                ParamName = "Response",
                NickName = "R",
                Description = "AI response",
                ParamType = typeof(Param_String),
                Access = GH_ParamAccess.tree,
                Extractor = OutputMapping.Single(aiReturn =>
                {
                    var text = aiReturn?.Body?.GetLastAssistantText();
                    return string.IsNullOrWhiteSpace(text) ? null : new GH_String(text);
                })
            }
        };
    }
}

```

Configure component re-execution behavior by setting `RunOnlyOnInputChanges` in the constructor:

```csharp
public MyAIComponent()
    : base("My AI", "MYAI", "Description", "SmartHopper", "AI")
{
    this.RunOnlyOnInputChanges = false; // re-execute when provider/model changes
}

```

---

## Architecture & Design

- Components construct `AIBody`, select provider/model, and execute `AIRequestCall`.
- Metrics and errors are surfaced on outputs and runtime messages.
- Supports both button and toggle Run patterns; debounce and state transitions manage re-execution.
