# AISelectingStatefulAsyncComponentBase

Extends [AIStatefulAsyncComponentBase](./AIStatefulAsyncComponentBase.md) with the canvas selection UI. Implements `ISelectingComponent`. Delegates selection to the shared `SelectingComponentCore` helper.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Core.Grasshopper/ComponentBase/AISelectingStatefulAsyncComponentBase.cs` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

This base class bridges AI-driven Grasshopper components with interactive canvas selection, allowing users to pick objects that become part of the AI workflow. It is essential for any SmartHopper component that needs to reference other document objects.

**You should read this if you:**

- Are building an AI component that needs to pick other Grasshopper objects on the canvas
- Want to understand how selection state is persisted alongside AI batch state
- Need to know how the Select button cooperates with badge rendering

---

## End-User Guide

### Purpose

AI components that need the user to pick other Grasshopper objects on the canvas as part of their input — for example *Smart Connect* and other canvas-aware AI utilities.

### What it adds

- Constructor creates a `SelectingComponentCore` and subscribes to document events.
- `CreateAttributes` installs `AISelectingComponentAttributes`, which extends [`ComponentBadgesAttributes`](./AIStatefulAsyncComponentBase.md) so badges (provider, model) coexist with the Select button. The provider tooltip is rendered last so it stays above the Select overlay.
- Adds a *Select Components* item to the context menu.
- `Write` / `Read` chain into `SelectingComponentCore` for GUID-based persistence on top of all the AI persistence (batch state, sentinels, hashes, outputs).
- `RemovedFromDocument` unsubscribes the selection core from document events.

---

## Developer Reference

### Public selection API

```csharp
public List<IGH_DocumentObject> SelectedObjects { get; }
public void EnableSelectionMode();

```

`SelectedObjects` is auto-pruned of deleted references on every read.

### Example: implementing a selecting AI component

```csharp
public class MySelectingComponent : AISelectingStatefulAsyncComponentBase
{
    public MySelectingComponent()
        : base("MySelecting", "MSC", "An AI component that selects objects")
    {
    }

    protected override void OnSolveInstancePostSolve(IGH_DataAccess DA)
    {
        // Access user-selected objects from the canvas
        var selected = this.SelectedObjects;
        // ...
        base.OnSolveInstancePostSolve(DA);
    }
}

```

---

## Architecture & Design

### Design criteria

Same as [SelectingComponentBase](./SelectingComponentBase.md) — selection logic is shared via `SelectingComponentCore`, GUIDs are persisted, UI work runs on Rhino's UI thread. The AI base contributes nothing new to the selection pipeline; it only ensures the Select button cooperates with badge rendering.

### Related

- [AIStatefulAsyncComponentBase](./AIStatefulAsyncComponentBase.md)
- [SelectingComponentBase](./SelectingComponentBase.md)
