# StatefulAsyncComponentBase

Async base with built‑in component state management, debouncing, progress, and error handling.

## Purpose

Unify long‑running execution with a clear state machine so components behave predictably with buttons/toggles and input changes.

## Key features

- Component states via [ComponentState](../Helpers/StateManager.md) (Waiting, NeedsRun, Processing, Completed, Cancelled, Error).
- Debounce timer for bursty input changes; immediate run when appropriate (e.g., Run toggle=true and inputs change).
- Progress tracking and user‑friendly state messages.
- Centralized runtime message handling and safe transitions.
- Persistence of relevant flags (e.g., debounce delay) across document reloads.

## Usage

- Derive when you need Run button/toggle semantics and resilient re‑execution.
- Provide a worker (see [AsyncWorkerBase](./AsyncWorkerBase.md)) or override the async execution hook used during `Processing`.
- Respect state transitions; avoid manual UI updates from worker threads.
- Emit clear messages for validation failures and early exits (remain in Waiting/NeedsRun).

## Related

- [StateManager](../Helpers/StateManager.md) – defines states and friendly messages.
- [AsyncComponentBase](./AsyncComponentBase.md) – lower‑level async base.
- [ProgressInfo](../Helpers/ProgressInfo.md) – report incremental progress.
- [IO Persistence (V2)](../IO/Persistence.md) – safe, versioned storage of output trees used by this base
