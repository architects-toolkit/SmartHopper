# AsyncComponentBase

Base class for long‑running Grasshopper components that execute work off the UI thread.

## Purpose

Provide a robust async skeleton: snapshot inputs, run on a background task with cancellation, and marshal results and messages back to Grasshopper safely.

## Key features

- **Task lifecycle** with cancellation token handling and proper cleanup.
- **Worker-based execution** via `CreateWorker()` abstract method.
- **Two-phase solve pattern**: pre-solve (gather inputs, start tasks) and post-solve (set outputs).
- **State tracking** with `_state` counter and `_setData` latch for coordinating async completion.
- **LIFO worker processing**: workers are reversed before output phase for expected ordering.
- **Automatic message reset**: calls `worker.ResetCollectedMessages()` after `GatherInput` so each run starts with a clean message queue without requiring workers to do it manually.
- **Automatic message flush**: calls `worker.FlushCollectedMessages()` on the UI thread immediately after each `worker.SetOutput()`, surfacing all messages collected during `DoWorkAsync`.
- Separation of UI thread vs. worker thread responsibilities.

## Key lifecycle flow

1. **BeforeSolveInstance()** – Cancels running tasks, resets async state (if not in output phase).
2. **SolveInstance() [pre-solve]** – `InPreSolve=true`; creates worker, calls `GatherInput`, calls `worker.ResetCollectedMessages()`, starts Task.
3. **AfterSolveInstance()** – Waits for tasks via `Task.WhenAll`, then sets `_state` to worker count and `_setData=1`.
4. **SolveInstance() [post-solve]** – `InPreSolve=false`; for each worker (LIFO): calls `SetOutput()`, then calls `FlushCollectedMessages()`, then decrements `_state`.
5. **OnWorkerCompleted()** – Called when `_state` reaches 0 after output phase.

## Internal state variables

- `_state` – Tracks worker completion count; starts at 0, set to `Workers.Count` when all tasks complete.
- `_setData` – Latch (0/1) indicating output phase is ready.
- `InPreSolve` – Flag distinguishing input-gathering phase from output-setting phase.

## Usage

- Derive your component from `AsyncComponentBase` when you need async work without a full state machine.
- Implement `CreateWorker(Action<string>)` returning an `AsyncWorkerBase`.
- Keep mutable state out of the worker; pass an immutable snapshot of inputs.
- Only access Grasshopper/Rhino UI on the UI thread.
- Use `CollectMessage` in workers for messages from background code; `AddRuntimeMessage` remains valid in `GatherInput` (UI thread).

## Related

- [StatefulComponentBase](./StatefulComponentBase.md) – adds state machine, debouncing, and Run handling on top; also promotes collected messages to persistent store on Error.
- [AsyncWorkerBase](../Workers/AsyncWorkerBase.md) – worker abstraction with thread-safe message collection.
- [ProgressInfo](../Helpers/ProgressInfo.md) – lightweight progress reporting payload.
