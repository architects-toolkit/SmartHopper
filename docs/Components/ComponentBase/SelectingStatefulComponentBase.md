# SelectingStatefulComponentBase

Combines [StatefulComponentBase](./StatefulComponentBase.md) with the *Select Components* button machinery, without any AI. Implements `ISelectingComponent` and delegates to `SelectingComponentCore` (same helper used by [SelectingComponentBase](./SelectingComponentBase.md) and [AISelectingStatefulAsyncComponentBase](./AISelectingStatefulAsyncComponentBase.md)).

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Core.Grasshopper/ComponentBase/SelectingStatefulComponentBase.cs` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

`SelectingStatefulComponentBase` is the bridge between persistent stateful processing and canvas object selection. It gives you the full power of `StatefulComponentBase` (progress, metrics, persistent outputs) plus the ability for users to pick other components, params, groups or panels as inputs.

**You should read this if you:**

- Need a component that processes data persistently and also references other canvas objects via the Select button.
- Want AI-less selection behaviour (no provider/model badges) but still need state-machine handling.
- Are deciding between `SelectingComponentBase`, `SelectingStatefulComponentBase` and `AISelectingStatefulAsyncComponentBase`.

---

## End-User Guide

### When to derive

- You need a Select button and persistent outputs, but no AI provider integration.
- For a non-stateful version use [SelectingComponentBase](./SelectingComponentBase.md). For AI use [AISelectingStatefulAsyncComponentBase](./AISelectingStatefulAsyncComponentBase.md).

### Notes

- Uses `SelectingComponentAttributes` (the non-AI flavour) so the component shows the Select button without provider badges.
- `Write` / `Read` chain to both `StatefulComponentBase` (state, hashes, outputs) and `SelectingComponentCore` (selection GUIDs).

See [SelectingComponentBase](./SelectingComponentBase.md) for the full description of the selection pipeline and design criteria — they are shared.

---

## Developer Reference

### Inheritance and construction

```csharp
public class MySelectorComponent : SelectingStatefulComponentBase
{
    public MySelectorComponent()
        : base("MySelector", "MS", "Selects canvas objects and processes statefully.", "SmartHopper", "Selection")
    { }

    // SelectingComponentCore is created automatically by the base constructor.
}

```

### Accessing selected objects in Solve

```csharp
protected override async Task<WorkerResult> RunProcessingAsync(CancellationToken ct)
{
    var selected = SelectedObjects;   // from ISelectingComponent
    var componentNames = selected
        .OfType<IGH_Component>()
        .Select(c => c.Name)
        .ToList();

    // ... run stateful processing using the selected objects ...

    return new WorkerResult(outputs);
}

```

---

## Architecture & Design

`SelectingStatefulComponentBase` inherits from `StatefulComponentBase` and mixes in `ISelectingComponent`. It does not re-implement any selection logic; instead it instantiates `SelectingComponentCore` in its constructor and lets that helper handle all canvas interaction, filtering, persistence and rendering.

Because both `StatefulComponentBase` and `SelectingComponentCore` need to participate in serialization, `Write` and `Read` are chained: the base class writes state/hashes/outputs first, then the core writes `SelectedObjectsCount` and `SelectedObject_0..N` as `InstanceGuid`s. On deserialization the reverse happens, and `OnDocumentAdded` triggers GUID-to-live-object resolution on the Rhino UI thread.

This architecture means you can add selection to any stateful component with almost no extra code — just derive from `SelectingStatefulComponentBase` and read `SelectedObjects` in your worker.
