# AsyncWorkerBase

Lightweight worker abstraction that hosts the actual compute logic for async components.

## Purpose

Separate UI/component concerns from the algorithm that runs off the UI thread.

## Key features

- Runs with a cancellation token provided by the component.
- Receives an immutable snapshot of inputs.
- Reports progress via [ProgressInfo](../Helpers/ProgressInfo.md) callback.
- Returns results or throws; errors are caught and surfaced by the component.

## Usage

- Create a worker type per component with a single `RunAsync` method taking your input snapshot.
- Do not access GH UI from within the worker.
- Keep the worker stateless beyond its constructor parameters.

## Related

- [AsyncComponentBase](../ComponentBase/AsyncComponentBase.md), [StatefulAsyncComponentBase](../ComponentBase/StatefulAsyncComponentBase.md) – hosts for workers.
- [ProgressInfo](../Helpers/ProgressInfo.md) – progress payload.
