# AsyncComponentBase

Base class for Grasshopper components that run long-running work on a background `Task`. Inherits from `GH_Component`. Adapted from Speckle's `GrasshopperAsyncComponent`.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Core.Grasshopper/ComponentBase/AsyncComponentBase.cs` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

This base class enables Grasshopper components to execute heavy or I/O-bound operations without freezing the Rhino UI. Understanding its two-phase solve and worker model is essential for building responsive asynchronous components.

**You should read this if you:**

- Need to run compute-heavy or I/O-bound work off the Grasshopper UI thread
- Want to understand the pre-solve / post-solve handshake and output-phase mechanics
- Are deriving from [StatefulComponentBase](./StatefulComponentBase.md) and want to understand the underlying async machinery

---

## End-User Guide

### Purpose

Run compute-heavy or I/O-bound work off the UI thread while staying inside Grasshopper's solve lifecycle. Provides cancellation, exception propagation and a strict pre-/post-solve handshake so outputs are written exactly once per run.

---

## Developer Reference

### Key members

- `protected abstract AsyncWorkerBase CreateWorker(Action<string> progressReporter)` — factory.
- `protected virtual void OnSolveInstancePreSolve(IGH_DataAccess DA)` / `OnSolveInstancePostSolve(IGH_DataAccess DA)` — hooks.
- `protected virtual void OnWorkerCompleted()` — called when all workers have set their outputs.
- `protected virtual void OnTasksCancelDetected()` — fires after task cancellation; output phase is skipped.
- `protected void ResetAsyncState()` — clears tasks, workers, cancellation sources and resets `_state`/`_setData`. Used when re-entering work.
- `public virtual void RequestTaskCancellation()` — cancels every active token source.
- `protected int DataCount { get; }` / `SetDataCount(int)` — surfaces the data count for state messages and metrics.
- `public bool InPreSolve` — exposed for `IGH_TaskCapableComponent` callers.

### Example: overriding the solve lifecycle hooks

```csharp
public class MyAsyncComponent : AsyncComponentBase
{
    protected override void OnSolveInstancePreSolve(IGH_DataAccess DA)
    {
        // Gather any pre-solve state or validate inputs here
        base.OnSolveInstancePreSolve(DA);
    }

    protected override void OnSolveInstancePostSolve(IGH_DataAccess DA)
    {
        // Perform any post-processing after workers have set outputs
        base.OnSolveInstancePostSolve(DA);
    }
}

```

### Example: requesting cancellation programmatically

```csharp
public override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
{
    base.AppendAdditionalComponentMenuItems(menu);
    Menu_AppendItem(menu, "Abort", (s, e) => this.RequestTaskCancellation());
}

```

---

## Architecture & Design

### Design criteria

- **Two-phase solve.** Pre-solve gathers inputs and starts the task; post-solve consumes the result. Tracked by `_state` (worker completion counter) and `_setData` (output-phase latch).
- **Workers, not tasks.** Compute logic lives in an `AsyncWorkerBase` returned from `CreateWorker(progressReporter)`. The component owns the lifecycle; the worker owns the compute.
- **LIFO output ordering.** Workers are reversed before output to match Grasshopper's expected ordering when multiple workers are present.
- **UI thread safety.** `SetOutput` is invoked through `Rhino.RhinoApp.InvokeOnUiThread`. Workers must never call GH/Rhino UI directly.
- **Cancellation is first-class.** Each task gets a dedicated `CancellationTokenSource`. The context-menu item *Cancel current process* and `RequestTaskCancellation()` cancel all sources; the cancellation is detected in the continuation, which raises `OnTasksCancelDetected()`.

### Lifecycle

1. **`BeforeSolveInstance`** – cancels in-flight tasks (unless we are already in the output phase) and calls `ResetAsyncState`.
2. **`SolveInstance` (pre-solve)** – first pass: creates a worker, calls `worker.GatherInput`, starts `Task.Run(worker.DoWorkAsync)`, then returns so Grasshopper proceeds to `AfterSolveInstance`.
3. **`AfterSolveInstance`** – `Task.WhenAll` of all worker tasks. On success: sets `_state = Workers.Count`, `_setData = 1`, reverses workers (LIFO). On cancel: resets state, raises `OnTasksCancelDetected`. On fault: surfaces task errors as runtime messages and still proceeds to the output phase. Always re-expires the solution to drive the second solve pass.
4. **`SolveInstance` (post-solve)** – second pass: invokes `worker.SetOutput` for each worker on the UI thread, decrements `_state`. When `_state == 0` clears tasks/workers/sources and calls `OnWorkerCompleted`.

### When to derive

- You need async work but **do not** need a state machine, debounce or `Run` button semantics. For all of those use [StatefulComponentBase](./StatefulComponentBase.md).
- Keep mutable state out of the worker; pass an immutable input snapshot in `GatherInput`.

### Related

- [AsyncWorkerBase](./AsyncWorkerBase.md)
- [StatefulComponentBase](./StatefulComponentBase.md)
- [ProgressInfo](./ProgressInfo.md)
