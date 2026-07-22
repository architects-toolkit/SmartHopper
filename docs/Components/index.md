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

Components derive from bases in `src/SmartHopper.Core/ComponentBase/`. A typical component constructs an `AIBody` and delegates to the AI runtime:

```csharp
public class MyAIComponent : AIStatefulAsyncComponentBase
{
    public override Guid ComponentGuid => new Guid("YOUR-GUID-HERE");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Prompt", "P", "Input prompt", GH_ParamAccess.item);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddTextParameter("Response", "R", "AI response", GH_ParamAccess.item);
    }

    protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
    {
        return new Worker(this, AddRuntimeMessage);
    }

    private sealed class Worker : AsyncWorkerBase
    {
        private readonly MyAIComponent _component;
        private string _prompt;
        private string _response;

        public Worker(MyAIComponent component, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
            : base(component, addRuntimeMessage)
        {
            _component = component;
        }

        public override void GatherInputData()
        {
            _prompt = _component.GetInputData("Prompt", string.Empty);
        }

        public override async Task DoWorkAsync(CancellationToken token)
        {
            var body = new AIBody { Messages = new List<AIMessage> { new AIMessage { Role = "user", Content = _prompt } } };
            var provider = _component.GetSelectedProvider();
            var result = await provider.ExecuteAsync(body, token);
            _response = result.Content;
        }

        public override void SetOutputData()
        {
            _component.SetOutputData("Response", _response);
        }
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
