# AsyncWorkerBase

`src/SmartHopper.Core/ComponentBase/AsyncWorkerBase.cs`

Worker abstraction owned by an [AsyncComponentBase](./AsyncComponentBase.md). The component drives the lifecycle; the worker holds the compute logic and an immutable snapshot of the inputs.

## Contract

```csharp
public abstract class AsyncWorkerBase
{
    protected GH_Component Parent { get; }
    protected Action<GH_RuntimeMessageLevel, string> AddRuntimeMessage { get; }

    public abstract void GatherInput(IGH_DataAccess DA, out int dataCount);
    public abstract Task DoWorkAsync(CancellationToken token);
    public abstract void SetOutput(IGH_DataAccess DA, out string message);
}
```

## Design criteria

- **One worker = one solve.** A new instance is created in pre-solve and discarded after `SetOutput`.
- **Pure compute in `DoWorkAsync`.** No GH/Rhino UI access. Use the `AddRuntimeMessage` callback or signal back through fields read from `SetOutput`.
- **`GatherInput` runs on the UI thread.** Snapshot what you need into worker fields here; do not hold references to `IGH_DataAccess`.
- **`SetOutput` is invoked through `Rhino.RhinoApp.InvokeOnUiThread`.** It is the only place to talk to `DA`.
- **Cancellation is cooperative.** Honor `CancellationToken` inside `DoWorkAsync`; throwing `OperationCanceledException` is the canonical way to bail out.

## Notes for AI components

`AIStatefulAsyncComponentBase` worker implementations follow a stricter contract:

- Non-batch: `DoWorkAsync` calls `parent.FinishResults("Output", tree)` itself; `SetOutput` is a no-op.
- Batch: `DoWorkAsync` calls `parent.TrySubmitBatchAsync(...)`; result decoding happens later in `OnBatchCompleted` → `ProcessBatchResults` → `FinishResults`.

See [AIStatefulAsyncComponentBase](./AIStatefulAsyncComponentBase.md) for the full pattern.
