# AsyncComponentBase

`src/SmartHopper.Core/ComponentBase/AsyncComponentBase.cs`

Base class for Grasshopper components that run long-running work on a background `Task`. Inherits from `GH_Component`. Adapted from Speckle's `GrasshopperAsyncComponent`.

---

Run compute-heavy or I/O-bound work off the UI thread while staying inside Grasshopper's solve lifecycle. Provides cancellation, exception propagation and a strict pre-/post-solve handshake so outputs are written exactly once per run.

## Design criteria

- **Two-phase solve.** Pre-solve gathers inputs and starts the task; post-solve consumes the result. Tracked by `_state` (worker completion counter) and `_setData` (output-phase latch).
- **Workers, not tasks.** Compute logic lives in an `AsyncWorkerBase` returned from `CreateWorker(progressReporter)`. The component owns the lifecycle; the worker owns the compute.
- **LIFO output ordering.** Workers are reversed before output to match Grasshopper's expected ordering when multiple workers are present.
- **UI thread safety.** `SetOutput` is invoked through `Rhino.RhinoApp.InvokeOnUiThread`. Workers must never call GH/Rhino UI directly.
- **Cancellation is first-class.** Each task gets a dedicated `CancellationTokenSource`. The context-menu item *Cancel current process* and `RequestTaskCancellation()` cancel all sources; the cancellation is detected in the continuation, which raises `OnTasksCancelDetected()`.

## Key members

- `protected abstract AsyncWorkerBase CreateWorker(Action<string> progressReporter)` â€” factory.
- `protected virtual void OnSolveInstancePreSolve(IGH_DataAccess DA)` / `OnSolveInstancePostSolve(IGH_DataAccess DA)` â€” hooks.
- `protected virtual void OnWorkerCompleted()` â€” called when all workers have set their outputs.
- `protected virtual void OnTasksCancelDetected()` â€” fires after task cancellation; output phase is skipped.
- `protected void ResetAsyncState()` â€” clears tasks, workers, cancellation sources and resets `_state`/`_setData`. Used when re-entering work.
- `public virtual void RequestTaskCancellation()` â€” cancels every active token source.
- `protected int DataCount { get; }` / `SetDataCount(int)` â€” surfaces the data count for state messages and metrics.
- `public bool InPreSolve` â€” exposed for `IGH_TaskCapableComponent` callers.

## Lifecycle

1. **`BeforeSolveInstance`** â€“ cancels in-flight tasks (unless we are already in the output phase) and calls `ResetAsyncState`.
2. **`SolveInstance` (pre-solve)** â€“ first pass: creates a worker, calls `worker.GatherInput`, starts `Task.Run(worker.DoWorkAsync)`, then returns so Grasshopper proceeds to `AfterSolveInstance`.
3. **`AfterSolveInstance`** â€“ `Task.WhenAll` of all worker tasks. On success: sets `_state = Workers.Count`, `_setData = 1`, reverses workers (LIFO). On cancel: resets state, raises `OnTasksCancelDetected`. On fault: surfaces task errors as runtime messages and still proceeds to the output phase. Always re-expires the solution to drive the second solve pass.
4. **`SolveInstance` (post-solve)** â€“ second pass: invokes `worker.SetOutput` for each worker on the UI thread, decrements `_state`. When `_state == 0` clears tasks/workers/sources and calls `OnWorkerCompleted`.

## When to derive

- You need async work but **do not** need a state machine, debounce or `Run` button semantics. For all of those use [StatefulComponentBase](./StatefulComponentBase.md).
- Keep mutable state out of the worker; pass an immutable input snapshot in `GatherInput`.

## End-User Guide

- [AsyncWorkerBase](./AsyncWorkerBase.md)
- [StatefulComponentBase](./StatefulComponentBase.md)
- [ProgressInfo](./ProgressInfo.md)

## Metadata

- Source Code: See source repository.
- Since Version: 2.0.0
- Last Updated: 2026-07-21
- Documentation Maintainer: Marc Roca Musach

---


## Why Read This?

This document provides details about AsyncComponentBase.


## Developer Reference

Example usage:

`csharp
// Placeholder example
``r

`csharp
// Another placeholder example
``r


## Architecture & Design

Architecture and design notes for AsyncComponentBase.

```csharp
// Example code for Developer Reference
```

```csharp
// Additional example for Developer Reference
```