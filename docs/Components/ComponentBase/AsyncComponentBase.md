# AsyncComponentBase

Base class for long‑running Grasshopper components that execute work off the UI thread.

## Purpose

Provide a robust async skeleton: snapshot inputs, run on a background task with cancellation, and marshal results and messages back to Grasshopper safely.

## Key features

- Task lifecycle and cancellation token handling.
- Input snapshotting to avoid race conditions.
- Background execution with exception capture and runtime message reporting.
- Hooks for progress updates using [ProgressInfo](../Helpers/ProgressInfo.md).
- Separation of UI thread vs. worker thread responsibilities.

## Usage

- Derive your component from [AsyncComponentBase](./AsyncComponentBase.md) when you need async work without a full state machine.
- Implement your execution logic in a background worker (see [AsyncWorkerBase](./AsyncWorkerBase.md)) or the provided async hook.
- Keep mutable state out of the worker; pass an immutable snapshot of inputs.
- Only access Grasshopper/Rhino UI on the UI thread.
- Use progress callbacks sparingly; throttle if needed.

## Related

- [StatefulAsyncComponentBase](./StatefulAsyncComponentBase.md) – adds state machine, debouncing, and Run handling on top.
- [AsyncWorkerBase](../Workers/AsyncWorkerBase.md) – worker abstraction to host the actual compute logic.
- [ProgressInfo](../Helpers/ProgressInfo.md) – lightweight progress reporting payload.
