# ComponentState

Defines the state enum used by [StatefulComponentBase](./StatefulComponentBase.md) and [ComponentStateManager](./ComponentStateManager.md), plus a `ToMessageString` extension used to render the component message.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Core.Grasshopper/ComponentBase/StateManager.cs` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

ComponentState is the core enum that drives every stateful SmartHopper component. Understanding the available states and how they translate to user-facing messages helps you interpret component behavior and debug stuck transitions.

**You should read this if you:**

- Want to understand what each state indicator (Ready, Processing, NeedsRun...) means
- Are building a custom stateful component and need to react to state changes
- Need to render or log component state in your own code

---

## End-User Guide

### States

| State | Meaning |
| --- | --- |
| `Completed` | Initial / idle. All workers finished; previous outputs are emitted. |
| `Waiting` | Toggle Run is true; idle until inputs change. |
| `NeedsRun` | Inputs changed while Run is false. Awaits Run = true. |
| `Processing` | A worker is running. |
| `Cancelled` | Manually cancelled. |
| `Error` | A persistent error was reported. |

`ComponentStateExtensions.ToMessageString(state, progressInfo?)` produces the friendly message shown on the component (`"Run me!"`, `"Process N/M..."`, `"Done"`, etc.). Pass a [ProgressInfo](./ProgressInfo.md) to render counters during `Processing`.

> Note: `StateManager.cs` only defines the enum and the extension. The runtime state machine (transitions, debounce, hash tracking) lives in [ComponentStateManager](./ComponentStateManager.md).

---

## Developer Reference

### Example: Checking State Programmatically

```csharp
public void OnStateChanged(ComponentState newState)
{
    if (newState == ComponentState.NeedsRun)
    {
        AddRuntimeMessage(
            GH_RuntimeMessageLevel.Warning,
            "Inputs changed. Toggle Run to process.");
    }
    else if (newState == ComponentState.Error)
    {
        AddRuntimeMessage(
            GH_RuntimeMessageLevel.Error,
            "A persistent error was reported.");
    }
}

```

### Example: Rendering the Component Message

```csharp
// Simple message without progress
string msg = currentState.ToMessageString();

// Message with progress counters during Processing
var progress = new ProgressInfo { Current = 3, Total = 10 };
string msgWithProgress = currentState.ToMessageString(progress);
// Result: "Process 3/10..."

```

---

## Architecture & Design

`StateManager.cs` intentionally remains lightweight: it contains only the `ComponentState` enum definition and the `ComponentStateExtensions.ToMessageString` extension method. All runtime state-machine logic—transition validation, debounce timing, and hash tracking—is centralized in [ComponentStateManager](./ComponentStateManager.md). This separation keeps the domain model (states and their human-readable labels) independent from orchestration concerns.
