# ProgressInfo

`src/SmartHopper.Core/ComponentBase/ProgressInfo.cs`

Lightweight progress payload (`Current` / `Total`) used by [StatefulComponentBase](./StatefulComponentBase.md) to render `Process N/M...` messages and to drive `Metrics.iterations_count` in AI components.

## Members

- `int Current { get; set; }` — 1-based.
- `int Total { get; set; }` — total iterations.
- `bool IsActive => Total > 0`.
- `string ProgressString => "Current/Total"` when active, else empty.
- `void UpdateCurrent(int current)` — clamps to `Total`.
- `void Reset()` — sets both to 0.

## Usage

`StatefulComponentBase` exposes:

- `protected ProgressInfo ProgressInfo { get; }`
- `protected virtual void InitializeProgress(int total)`
- `protected virtual void UpdateProgress(int current)` — also refreshes the component message and re-paints.
- `protected virtual void ResetProgress()`

`DataTreeProcessor.RunAsync` invokes the `progressCallback` once per processed unit; `StatefulComponentBase.RunProcessingAsync` wires that into `UpdateProgress` automatically.
