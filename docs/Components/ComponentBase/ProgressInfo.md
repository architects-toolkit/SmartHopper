# ProgressInfo

Lightweight progress payload (`Current` / `Total`) used by [StatefulComponentBase](./StatefulComponentBase.md) to render `Process N/M...` messages and to drive `Metrics.iterations_count` in AI components.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Core.Grasshopper/ComponentBase/ProgressInfo.cs` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

`ProgressInfo` is the standard mechanism for tracking and displaying iteration progress inside SmartHopper's stateful components. It provides a simple `Current/Total` model that automatically generates human-readable progress strings and integrates with the component's on-canvas message.

**You should read this if you:**

- Are building a stateful component that processes items in batches and wants to show progress.
- Need to understand how `Metrics.iterations_count` is populated for AI components.
- Want to customize or troubleshoot the `Process N/M...` UI messages.

---

## End-User Guide

`StatefulComponentBase` exposes the following progress-related protected members:

- `protected ProgressInfo ProgressInfo { get; }`
- `protected virtual void InitializeProgress(int total)`
- `protected virtual void UpdateProgress(int current)` — also refreshes the component message and re-paints.
- `protected virtual void ResetProgress()`

`DataTreeProcessor.RunAsync` invokes the `progressCallback` once per processed unit; `StatefulComponentBase.RunProcessingAsync` wires that into `UpdateProgress` automatically.

---

## Developer Reference

### Members

- `int Current { get; set; }` — 1-based.
- `int Total { get; set; }` — total iterations.
- `bool IsActive => Total > 0`.
- `string ProgressString => "Current/Total"` when active, else empty.
- `void UpdateCurrent(int current)` — clamps to `Total`.
- `void Reset()` — sets both to 0.

### Basic usage inside a stateful component

```csharp
protected override async Task<WorkerResult> RunProcessingAsync(CancellationToken ct)
{
    int total = Inputs.Count;
    InitializeProgress(total);

    for (int i = 0; i < total; i++)
    {
        // ... process item ...
        UpdateProgress(i + 1);
    }

    ResetProgress();
    return new WorkerResult(outputs);
}

```

### Accessing progress state for metrics

```csharp
protected override void AppendMetrics(Dictionary<string, object> metrics)
{
    if (ProgressInfo.IsActive)
    {
        metrics["iterations_count"] = ProgressInfo.Current;
    }
}

```

---

## Architecture & Design

`ProgressInfo` is intentionally lightweight: two integers and computed properties. It avoids heavy UI coupling so it can be used inside tight loops. The rendering side (`StatefulComponentBase`) polls the same instance and translates `Current/Total` into Grasshopper's `Message` property and canvas re-paints.

Because `DataTreeProcessor.RunAsync` accepts a `progressCallback`, batch-processing workers can report progress without knowing anything about Grasshopper UI. `StatefulComponentBase` bridges that callback into `UpdateProgress`, keeping UI concerns in the base class and business logic in the processor.
