# ProgressInfo

`src/SmartHopper.Core/ComponentBase/ProgressInfo.cs`

Lightweight progress payload (`Current` / `Total`) used by [StatefulComponentBase](./StatefulComponentBase.md) to render `Process N/M...` messages and to drive `Metrics.iterations_count` in AI components.

## Members

- `int Current { get; set; }` â€” 1-based.
- `int Total { get; set; }` â€” total iterations.
- `bool IsActive => Total > 0`.
- `string ProgressString => "Current/Total"` when active, else empty.
- `void UpdateCurrent(int current)` â€” clamps to `Total`.
- `void Reset()` â€” sets both to 0.

## Usage

`StatefulComponentBase` exposes:

- `protected ProgressInfo ProgressInfo { get; }`
- `protected virtual void InitializeProgress(int total)`
- `protected virtual void UpdateProgress(int current)` â€” also refreshes the component message and re-paints.
- `protected virtual void ResetProgress()`

`DataTreeProcessor.RunAsync` invokes the `progressCallback` once per processed unit; `StatefulComponentBase.RunProcessingAsync` wires that into `UpdateProgress` automatically.

## Metadata

- Source Code: See source repository.
- Since Version: 2.0.0
- Last Updated: 2026-07-21
- Documentation Maintainer: Marc Roca Musach

---


## Why Read This?

This document provides details about ProgressInfo.


## End-User Guide

End-user guidance for ProgressInfo.


## Developer Reference

Example usage:

`csharp
// Placeholder example
``r

`csharp
// Another placeholder example
``r


## Architecture & Design

Architecture and design notes for ProgressInfo.

```csharp
// Example code for Developer Reference
```

```csharp
// Additional example for Developer Reference
```