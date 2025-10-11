# ProgressInfo

Lightweight progress payload used by async components and workers.

## Purpose

Communicate progress from worker threads to the component/UI without tight coupling.

## Key features

- Holds current item, total items, and a short progress message.
- Designed for frequent updates with minimal allocation.
- Optional: can be used to compute percentage and ETA by the component.

## Usage

- Emit periodically from your worker to update the component state or UI.
- Keep messages concise; avoid flooding Grasshopper with rendering updates.

## Related

- [AsyncWorkerBase](../Workers/AsyncWorkerBase.md), [AsyncComponentBase](../ComponentBase/AsyncComponentBase.md), [StatefulAsyncComponentBase](../ComponentBase/StatefulAsyncComponentBase.md) – typical consumers.
