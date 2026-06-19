# StateManager

Defines component states and helpers to present user‑friendly messages.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Core.Grasshopper/Helpers/StateManager.cs` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

StateManager provides the shared state model that drives execution flow and user feedback across all stateful components. Knowing how states are defined and translated into messages helps you build components that behave consistently with the rest of SmartHopper.

**You should read this if you:**

- Are building custom stateful components and want consistent state handling
- Need to understand how component lifecycle states map to on-canvas messages
- Want to add new states or customize user-facing state messages

---

## End-User Guide

### Purpose

Provide a compact state model used by stateful components to drive execution flow and UI feedback.

### Key features

- `ComponentState` enum: Completed, Waiting, NeedsRun, Processing, Cancelled, Error.
- Extensions to convert states to short, user‑facing messages.
- Shared by all stateful bases to keep behavior consistent.

### Usage

- Rely on base classes to transition state; avoid ad‑hoc state handling.
- Use the friendly messages on UI or outputs when appropriate.

---

## Developer Reference

### Querying and Displaying State

Use the `ComponentState` enum and extension methods to inspect or display current state:

```csharp
ComponentState currentState = myComponent.GetCurrentState();
string message = currentState.ToFriendlyMessage();

if (currentState == ComponentState.Error)
{
    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, message);
}

```

### Transitioning States in a Custom Component

Override the state transition hooks in a stateful base to add custom logic:

```csharp
protected override void OnStateChanged(ComponentState oldState, ComponentState newState)
{
    base.OnStateChanged(oldState, newState);

    if (newState == ComponentState.Processing)
    {
        StartProgressTimer();
    }
    else if (newState == ComponentState.Completed)
    {
        StopProgressTimer();
        ExpireSolution(true);
    }
}

```

---

## Architecture & Design

- [StatefulComponentBase](../ComponentBase/StatefulComponentBase.md), [AIStatefulAsyncComponentBase](../ComponentBase/AIStatefulAsyncComponentBase.md) – consumers of these states.
