# ProgressInfo

Lightweight progress payload used by async components and workers.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Core.Grasshopper/Helpers/ProgressInfo.cs` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

ProgressInfo decouples long-running worker threads from the UI by carrying a small, allocation-friendly progress snapshot. Understanding this payload helps you report meaningful progress without blocking the Grasshopper canvas or flooding the message pipeline.

**You should read this if you:**

- Are building async components that run background workers
- Want to show progress bars or status messages during long computations
- Need to stream progress updates from a worker thread to the main UI thread

---

## End-User Guide

### Purpose

Communicate progress from worker threads to the component/UI without tight coupling.

### Key features

- Holds current item, total items, and a short progress message.
- Designed for frequent updates with minimal allocation.
- Optional: can be used to compute percentage and ETA by the component.

### Usage

- Emit periodically from your worker to update the component state or UI.
- Keep messages concise; avoid flooding Grasshopper with rendering updates.

---

## Developer Reference

### Creating a Progress Update

Construct and emit a `ProgressInfo` instance from inside an async worker:

```csharp
var progress = new ProgressInfo
{
    CurrentItem = 12,
    TotalItems = 100,
    Message = "Processing mesh 12 of 100"
};

progressReporter.Report(progress);

```

### Consuming Progress in a Component

Read the payload in the component's progress handler to update the UI or output parameters:

```csharp
protected override void OnProgressReported(ProgressInfo progress)
{
    double percent = (double)progress.CurrentItem / progress.TotalItems * 100;
    this.Message = $"{progress.Message} ({percent:F1}%)";
    this.ExpireSolution(true);
}

```

---

## Architecture & Design

- [AsyncWorkerBase](../Workers/AsyncWorkerBase.md), [AsyncComponentBase](../ComponentBase/AsyncComponentBase.md), [StatefulComponentBase](../ComponentBase/StatefulComponentBase.md) – typical consumers.
