# StatefulComponentBase

Stateful async base built on `ComponentStateManager`.

## Purpose

Unify long-running execution with a clear state machine so components behave predictably with buttons/toggles and input changes. Provides automatic persistence and restoration of output data across document save/load cycles.

## Key features

- **State machine** via `ComponentState` (Waiting, NeedsRun, Processing, Completed, Cancelled, Error).
- **Debounce** using `ComponentStateManager.StartDebounce(...)` to prevent bursty input changes from triggering repeated runs.
- **Input change detection** via hash-based comparison of input data and branch structure, owned by `ComponentStateManager`.
- **Progress tracking** with user-friendly state messages and iteration counts.
- **Persistent runtime messages** keyed by identifier for accumulation and selective clearing.
- **Persistent output storage** via [IO Persistence (V2)](../IO/Persistence.md) for document save/load.
- **RunOnlyOnInputChanges** flag (default: true) controlling whether Run=true always triggers processing or only when inputs change.

## Key lifecycle flow

- `SolveInstance(IGH_DataAccess)` reads Run, updates pending input hashes, dispatches per-state handlers, then delegates input-change handling to the state manager.
- `OnWorkerCompleted()` commits input hashes and transitions to Completed.
- `Write(GH_IWriter)` / `Read(GH_IReader)` persist and restore input hashes and output trees via `GHPersistenceService`.

## Code location

- `src/SmartHopper.Core/ComponentBase/StatefulComponentBaseV2.cs` (type name: `StatefulComponentBase`).

## Legacy

- `StatefulAsyncComponentBase` is legacy and retained temporarily for migration.

## Related

- [StateManager](../Helpers/StateManager.md) – defines states and friendly messages.
- [AsyncComponentBase](./AsyncComponentBase.md) – lower-level async base with worker coordination.
- [AsyncWorkerBase](../Workers/AsyncWorkerBase.md) – worker abstraction for compute logic.
- [ProgressInfo](../Helpers/ProgressInfo.md) – report incremental progress.
- [IO Persistence (V2)](../IO/Persistence.md) – safe, versioned storage of output trees used by this base.
