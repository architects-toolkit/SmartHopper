# AsyncWorkerBase

Lightweight worker abstraction that hosts the actual compute logic for async components.

## Purpose

Separate UI/component concerns from the algorithm that runs off the UI thread. Provides thread-safe runtime message collection so that messages generated on background threads are marshalled correctly to Grasshopper.

## Key features

- Runs with a cancellation token provided by the component.
- Receives an immutable snapshot of inputs.
- Reports progress via [ProgressInfo](../Helpers/ProgressInfo.md) callback.
- **Thread-safe message collection** via `CollectMessage` — messages generated during `DoWorkAsync` are queued and flushed to the GH component on the UI thread after `SetOutput`.
- `ResetCollectedMessages()` is called automatically by `AsyncComponentBase` after `GatherInput`; workers do not need to call it manually.
- Returns results or throws; errors are caught and surfaced by the component.

## Runtime message collection API

| Member | Visibility | Thread | Description |
|--------|-----------|--------|-------------|
| `CollectMessage(SHRuntimeMessage)` | `protected` | Any | Enqueues a structured message for later flush. |
| `CollectMessage(severity, message, origin)` | `protected` | Any | Convenience overload; creates `SHRuntimeMessage` with `SHMessageCode.Unknown`. |
| `FlushCollectedMessages()` | `internal` | UI | Writes all queued messages to the GH component. Called by `AsyncComponentBase` after `SetOutput`. |
| `ResetCollectedMessages()` | `internal` | UI | Clears the queue. Called automatically by `AsyncComponentBase` after `GatherInput`. |
| `PromoteCollectedToPersistent(Action<…>)` | `internal` | UI | Iterates queued messages and calls the provided callback for each surfaceable one. Used by `StatefulComponentBase` to persist messages across Error-state transitions. |

## Usage

- Derive a sealed inner worker class per component.
- Implement `GatherInput`, `DoWorkAsync`, and `SetOutput`.
- Use `CollectMessage` for any messages emitted from `DoWorkAsync` or its helper methods.
- Do not access GH UI from within `DoWorkAsync`.

## Related

- [AsyncComponentBase](../ComponentBase/AsyncComponentBase.md) – calls `ResetCollectedMessages` and `FlushCollectedMessages` automatically.
- [StatefulComponentBase](../ComponentBase/StatefulComponentBase.md) – calls `PromoteCollectedToPersistent` on Error transitions.
- [ProgressInfo](../Helpers/ProgressInfo.md) – progress payload.
