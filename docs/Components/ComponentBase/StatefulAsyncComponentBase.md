# StatefulAsyncComponentBase

Legacy async base with built‑in component state management, debouncing, progress, error handling, and persistent output storage.

This base has been superseded by [StatefulComponentBase](./StatefulComponentBase.md).

## Purpose

Unify long‑running execution with a clear state machine so components behave predictably with buttons/toggles and input changes. Provides automatic persistence and restoration of output data across document save/load cycles.

## Key features

- **State machine** via [ComponentState](../Helpers/StateManager.md) (Waiting, NeedsRun, Processing, Completed, Cancelled, Error).
- **Debounce timer** for bursty input changes; configurable via settings with minimum 1000ms.
- **Input change detection** using hash-based comparison of input data and branch structure.
- **Progress tracking** with user‑friendly state messages and iteration counts.
- **Persistent runtime messages** keyed by identifier for accumulation and selective clearing.
- **Persistent output storage** via [IO Persistence (V2)](../IO/Persistence.md) for document save/load.
- **RunOnlyOnInputChanges** flag (default: true) controlling whether Run=true always triggers processing or only when inputs change.

## Key lifecycle methods

- `BeforeSolveInstance()` – Guards against resetting data during Processing state.
- `SolveInstance(IGH_DataAccess)` – Dispatches to state handlers, checks input changes, manages debounce.
- `OnWorkerCompleted()` – Updates input hashes, transitions to Completed, expires solution.
- `Write(GH_IWriter)` / `Read(GH_IReader)` – Persists and restores input hashes, outputs, and component state.

## State transition logic

- **Completed/Waiting/Cancelled/Error** → check `InputsChanged()`:
  - If only Run changed to false → stay in current state
  - If only Run changed to true → transition to Waiting or Processing (based on `RunOnlyOnInputChanges`)
  - If other inputs changed → restart debounce timer targeting NeedsRun (Run=false) or Processing (Run=true)
- **NeedsRun** → if Run=true, transition to Processing
- **Processing** → async work runs; on completion → Completed

## Usage

- Derive when you need Run button/toggle semantics and resilient re‑execution.
- Implement `CreateWorker(Action<string>)` returning an `AsyncWorkerBase`.
- Implement `RegisterAdditionalInputParams` and `RegisterAdditionalOutputParams`.
- Use `SetPersistentOutput()` to store outputs that survive document save/load.
- Use `SetPersistentRuntimeMessage()` for errors/warnings that persist across solves.
- Override `RunOnlyOnInputChanges` if the component should always process when Run=true.

## Related

- [StateManager](../Helpers/StateManager.md) – defines states and friendly messages.
- [AsyncComponentBase](./AsyncComponentBase.md) – lower‑level async base with worker coordination.
- [AsyncWorkerBase](../Workers/AsyncWorkerBase.md) – worker abstraction for compute logic.
- [ProgressInfo](../Helpers/ProgressInfo.md) – report incremental progress.
- [IO Persistence (V2)](../IO/Persistence.md) – safe, versioned storage of output trees used by this base.
