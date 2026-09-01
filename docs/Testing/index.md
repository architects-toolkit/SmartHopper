# SmartHopper Components Test Suite

Test-only Grasshopper components that validate SmartHopper provider and utility behavior inside Rhino/Grasshopper.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Components.Test/` |
| **Since Version** | ? |
| **Last Updated** | 2026-09-01 |
| **Documentation Maintainer** | Devin AI |

---

## Why Read This?

This document describes the test-only Grasshopper components used to validate providers, AI tools, and shared utilities. It explains how the test matrix is organized and why each per-provider component is kept as an independent runner.

**You should read this if you:**

- Want to run or extend the SmartHopper test components in Grasshopper.
- Are adding a new provider and need to create a corresponding test component.
- Need to understand the shared base that centralizes common test wiring.

---

## End-User Guide

### What Are the Test Components?

The `src/SmartHopper.Components.Test/` project contains Grasshopper components that are only built in Debug configuration. They are not included in Release builds and are not meant for end-user design workflows. Instead, they exercise provider features such as encoding, decoding, standard calls, batch calls, tools, vision, streaming, and cancellation.

### Running the Tests

1. Build SmartHopper in **Debug**.
2. Drop the relevant test component onto the Grasshopper canvas.
3. Provide any required inputs (API key, model, prompt, etc.).
4. Trigger the component.
5. Read the `Success` boolean and `Messages` output for diagnostic output.

### Test Component Catalog

| Category | Description | Location |
| --- | --- | --- |
| **Providers** | One component per provider and feature (~43 components) | `src/SmartHopper.Components.Test/Providers/` |
| **AI Tools** | Tests for canvas tools such as `gh_get` and `gh_put` | `src/SmartHopper.Components.Test/AiTools/` |
| **Badges** | Tests for component badge rendering | `src/SmartHopper.Components.Test/Badges/` |
| **DataProcessor** | Tests for data-tree matching, broadcasting, and grafting | `src/SmartHopper.Components.Test/DataProcessor/` |
| **Misc** | Tests for state managers, async workers, and dialogs | `src/SmartHopper.Components.Test/Misc/` |

### Typical Workflow

1. Choose the test component for the provider and feature you want to validate.
2. Wire the required inputs and a button or toggle to trigger computation.
3. Inspect the `Success` and `Messages` outputs.
4. If a test fails, read the structured messages for the cause.

### Common Questions

**Q: Why are there so many provider test components?**
A: Each component is a focused, independent runner. Keeping them separate makes it easy to see which provider/feature combination fails without running a large, all-in-one test panel.

**Q: Can I use these components in a production Grasshopper definition?**
A: No. They are built only in Debug and are intended for development and CI validation.

---

## Developer Reference

### Shared Setup Base

`ProviderTestComponentBase` lives in `src/SmartHopper.Components.Test/Providers/ProviderTestComponentBase.cs` and centralizes the boilerplate that every provider test component used to repeat:

```csharp
public abstract class ProviderTestComponentBase : AIStatefulAsyncComponentBase
{
    protected abstract string TestProviderName { get; }

    protected ProviderTestComponentBase(
        string name,
        string nickname,
        string description,
        string category = "SmartHopper Tests",
        string subCategory = "Testing Providers")
        : base(name, nickname, description, category, subCategory)
    {
        this.RunOnlyOnInputChanges = false;
        this.SetSelectedProviderName(this.TestProviderName);
    }

    public override GH_Exposure Exposure => GH_Exposure.secondary;
}
```

Derived components only override `TestProviderName`, pass their name/nickname/description, and implement their test worker:

```csharp
public class TestOpenAIEncodeComponent : ProviderTestComponentBase
{
    public override Guid ComponentGuid => new Guid("AD538781-65B9-4123-B4EE-874D03BD6FC3");
    protected override string TestProviderName => "OpenAI";

    public TestOpenAIEncodeComponent()
        : base("Test OpenAI Encode", "TEST-OPENAI-ENC", "Tests OpenAI message encoding.")
    {
    }

    protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
    {
        return new Worker(this, this.AddRuntimeMessage);
    }

    private sealed class Worker : AsyncWorkerBase
    {
        // provider-specific test logic
    }
}
```

### Per-Provider Test Runners

The `src/SmartHopper.Components.Test/Providers/` directory contains one component per provider and feature (e.g., `TestOpenAIEncodeComponent`, `TestAnthropicStreamingComponent`). These ~43 components are **intentional per-provider test runners**, not accidental duplication. Each one:

- Inherits from `ProviderTestComponentBase` for shared setup/teardown.
- Keeps its own `ComponentGuid`, display name, nickname, description, outputs, and test-specific `Worker` logic.
- Calls the real provider API (`Encode`, `Decode`, `Call`, `Batch`, `Streaming`, `Tools`, `Vision`) and reports boolean results plus diagnostic messages.

### Extension Points

To add a test component for a new provider:

1. Create a class in `src/SmartHopper.Components.Test/Providers/` that derives from `ProviderTestComponentBase`.
2. Override `TestProviderName` to return the provider key.
3. Implement an `AsyncWorkerBase` that exercises the relevant API surface.
4. Add the new component to the `SmartHopper.Components.Test` project file if it is not auto-included.

---

## Architecture & Design

### Design Rationale

**Problem**: Validating provider behavior requires exercising many feature/provider combinations. A single monolithic test component would be hard to read, slow to run, and difficult to maintain.

**Decision**: Provide a lightweight base class that centralizes wiring, and keep one component per provider/feature pair. This makes each test independent and easy to invoke from the Grasshopper canvas.

### Trade-offs

- **More files and components** than a single test runner, but each failure is isolated and self-describing.
- **Debug-only build** keeps Release artifacts small while still giving developers a concrete in-application test harness.

### Data Flow

```text
Grasshopper canvas input
        │
        ▼
Test component worker
        │
        ▼
Provider API call (Encode / Decode / Call / Batch / ...)
        │
        ▼
Result and messages surfaced on the component outputs
```

### Related Documentation

- [Grasshopper Test Components](../Components/GrasshopperTestComponents.md)
- [Async Worker Base](../Components/ComponentBase/AsyncWorkerBase.md)
- [AI Stateful Async Component Base](../Components/ComponentBase/AIStatefulAsyncComponentBase.md)
