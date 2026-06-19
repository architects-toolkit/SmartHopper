# AsyncWorkerBase

Worker abstraction owned by an [AsyncComponentBase](./AsyncComponentBase.md). The component drives the lifecycle; the worker holds the compute logic and an immutable snapshot of the inputs.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Core.Grasshopper/ComponentBase/AsyncWorkerBase.cs` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

Workers are the compute units behind every asynchronous SmartHopper component. Understanding their contract and lifecycle is critical for implementing correct, thread-safe background processing.

**You should read this if you:**

- Are implementing a custom worker for an async component
- Need to understand how input snapshots are taken and how outputs are written back
- Want to know the rules for UI-thread access and cancellation inside a worker

---

## End-User Guide

### Contract

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

---

## Developer Reference

### Design criteria

- **One worker = one solve.** A new instance is created in pre-solve and discarded after `SetOutput`.
- **Pure compute in `DoWorkAsync`.** No GH/Rhino UI access. Use the `AddRuntimeMessage` callback or signal back through fields read from `SetOutput`.
- **`GatherInput` runs on the UI thread.** Snapshot what you need into worker fields here; do not hold references to `IGH_DataAccess`.
- **`SetOutput` is invoked through `Rhino.RhinoApp.InvokeOnUiThread`.** It is the only place to talk to `DA`.
- **Cancellation is cooperative.** Honor `CancellationToken` inside `DoWorkAsync`; throwing `OperationCanceledException` is the canonical way to bail out.

### Example: minimal worker implementation

```csharp
public class MyWorker : AsyncWorkerBase
{
    private List<GH_String> _inputData;
    private List<GH_String> _result;

    public MyWorker(GH_Component parent) : base(parent) { }

    public override void GatherInput(IGH_DataAccess DA, out int dataCount)
    {
        _inputData = new List<GH_String>();
        dataCount = DA.GetDataList(0, _inputData) ? _inputData.Count : 0;
    }

    public override async Task DoWorkAsync(CancellationToken token)
    {
        _result = new List<GH_String>();
        foreach (var item in _inputData)
        {
            token.ThrowIfCancellationRequested();
            _result.Add(new GH_String(item.Value.ToUpper()));
            await Task.Delay(10, token);
        }
    }

    public override void SetOutput(IGH_DataAccess DA, out string message)
    {
        DA.SetDataList(0, _result);
        message = $"Processed {_result.Count} items";
    }
}

```

### Notes for AI components

`AIStatefulAsyncComponentBase` worker implementations follow a stricter contract:

- Non-batch: `DoWorkAsync` calls `parent.FinishResults("Output", tree)` itself; `SetOutput` is a no-op.
- Batch: `DoWorkAsync` calls `parent.TrySubmitBatchAsync(...)`; result decoding happens later in `OnBatchCompleted` → `ProcessBatchResults` → `FinishResults`.

See [AIStatefulAsyncComponentBase](./AIStatefulAsyncComponentBase.md) for the full pattern.

---

## Architecture & Design

### Related

- [AsyncComponentBase](./AsyncComponentBase.md)
- [AIStatefulAsyncComponentBase](./AIStatefulAsyncComponentBase.md)
