# AsyncWorkerBase

Lightweight worker abstraction that hosts the actual compute logic for async components.

---

Separate UI/component concerns from the algorithm that runs off the UI thread. Provides thread-safe runtime message collection so that messages generated on background threads are marshalled correctly to Grasshopper.

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Core.Grasshopper/Workers/AsyncWorkerBase.cs` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

`AsyncWorkerBase` separates UI/component concerns from the algorithm that runs off the UI thread. It provides thread-safe runtime message collection so that messages generated on background threads are marshalled correctly to Grasshopper.

**You should read this if you:**

- Are implementing an async Grasshopper component and need to understand the worker pattern
- Need to collect runtime messages from background threads safely
- Want to learn how `AsyncComponentBase` and `StatefulComponentBase` interact with workers

---

## End-User Guide

### Purpose

Separate UI/component concerns from the algorithm that runs off the UI thread. Provides thread-safe runtime message collection so that messages generated on background threads are marshalled correctly to Grasshopper.

### Key Features

- Runs with a cancellation token provided by the component.
- Receives an immutable snapshot of inputs.
- Reports progress via [ProgressInfo](../Helpers/ProgressInfo.md) callback.
- **Thread-safe message collection** via `CollectMessage` â€” messages generated during `DoWorkAsync` are queued and flushed to the GH component on the UI thread after `SetOutput`.
- `ResetCollectedMessages()` is called automatically by `AsyncComponentBase` after `GatherInput`; workers do not need to call it manually.
- Returns results or throws; errors are caught and surfaced by the component.

## Runtime message collection API

| Member | Visibility | Thread | Description |
|--------|-----------|--------|-------------|
| `CollectMessage(SHRuntimeMessage)` | `protected` | Any | Enqueues a structured message for later flush. |
| `CollectMessage(severity, message, origin)` | `protected` | Any | Convenience overload; creates `SHRuntimeMessage` with `SHMessageCode.Unknown`. |
| `FlushCollectedMessages()` | `internal` | UI | Writes all queued messages to the GH component. Called by `AsyncComponentBase` after `SetOutput`. |
| `ResetCollectedMessages()` | `internal` | UI | Clears the queue. Called automatically by `AsyncComponentBase` after `GatherInput`. |
| `PromoteCollectedToPersistent(Action<â€¦>)` | `internal` | UI | Iterates queued messages and calls the provided callback for each surfaceable one. Used by `StatefulComponentBase` to persist messages across Error-state transitions. |

## Usage

- Derive a sealed inner worker class per component.
- Implement `GatherInput`, `DoWorkAsync`, and `SetOutput`.
- Use `CollectMessage` for any messages emitted from `DoWorkAsync` or its helper methods.
- Do not access GH UI from within `DoWorkAsync`.

### Related

- [AsyncComponentBase](../ComponentBase/AsyncComponentBase.md) â€“ calls `ResetCollectedMessages` and `FlushCollectedMessages` automatically.
- [StatefulComponentBase](../ComponentBase/StatefulComponentBase.md) â€“ calls `PromoteCollectedToPersistent` on Error transitions.
- [ProgressInfo](../Helpers/ProgressInfo.md) â€“ progress payload.

---

## Developer Reference

### Runtime Message Collection API

| Member | Visibility | Thread | Description |
| --- | --- | --- |--------|-------------|
| `CollectMessage(SHRuntimeMessage)` | `protected` | Any | Enqueues a structured message for later flush. |
| `CollectMessage(severity, message, origin)` | `protected` | Any | Convenience overload; creates `SHRuntimeMessage` with `SHMessageCode.Unknown`. |
| `FlushCollectedMessages()` | `internal` | UI | Writes all queued messages to the GH component. Called by `AsyncComponentBase` after `SetOutput`. |
| `ResetCollectedMessages()` | `internal` | UI | Clears the queue. Called automatically by `AsyncComponentBase` after `GatherInput`. |
| `PromoteCollectedToPersistent(Action<â€¦>)` | `internal` | UI | Iterates queued messages and calls the provided callback for each surfaceable one. Used by `StatefulComponentBase` to persist messages across Error-state transitions. |

### Implementing a Worker

Derive a sealed inner worker class and implement the required methods:

```csharp
public class MyAsyncComponent : AsyncComponentBase
{
    private sealed class Worker : AsyncWorkerBase
    {
        private List<GeometryBase> _inputGeometry;
        private List<Curve> _outputCurves;

        protected override void GatherInput(IGH_DataAccess DA)
        {
            _inputGeometry = new List<GeometryBase>();
            DA.GetDataList(0, _inputGeometry);
        }

        protected override async Task DoWorkAsync(CancellationToken token)
        {
            foreach (var geo in _inputGeometry)
            {
                token.ThrowIfCancellationRequested();

                if (geo == null)
                {
                    CollectMessage(
                        GH_RuntimeMessageLevel.Warning,
                        "Null geometry item skipped",
                        "GatherInput");
                    continue;
                }

                // Perform background computation
                var curves = await ProcessGeometryAsync(geo, token);
                _outputCurves.AddRange(curves);
            }
        }

        protected override void SetOutput(IGH_DataAccess DA)
        {
            DA.SetDataList(0, _outputCurves);
        }
    }
}

```

### Collecting Messages from Helpers

Use `CollectMessage` inside helper methods so that warnings and errors surface on the component:

```csharp
private void ValidateInput(GeometryBase geo)
{
    if (geo is Brep brep && brep.Faces.Count == 0)
    {
        CollectMessage(
            GH_RuntimeMessageLevel.Warning,
            "Brep has no faces",
            "ValidateInput");
    }
}

```

---

## Architecture & Design

`AsyncWorkerBase` enforces a strict separation between the Grasshopper UI thread and background computation. By receiving an immutable snapshot of inputs during `GatherInput`, the worker ensures that `DoWorkAsync` never races with the canvas. All messages emitted on background threads are queued internally and flushed to the component only when the component is back on the UI thread. This design prevents cross-thread access exceptions and guarantees that `SetOutput` always runs synchronously with the Grasshopper solution.

## Metadata

- Source Code: See source repository.
- Since Version: 2.0.0
- Last Updated: 2026-07-21
- Documentation Maintainer: Marc Roca Musach

---
