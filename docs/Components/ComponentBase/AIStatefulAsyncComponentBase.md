# AIStatefulAsyncComponentBase

The canonical base for any component that talks to an AI provider through SmartHopper. It owns the provider/model resolution, the AI request lifecycle (sync or batch), the metrics emission and the on-canvas badge rendering, so derived components only need to express *what* they do (system prompt, tool, output mapping).

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Core.Grasshopper/ComponentBase/AIStatefulAsyncComponentBase.cs` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

This is the most important base class for all AI-driven SmartHopper components. It handles everything from model selection and request parameters to batch processing and metrics tracking, freeing component authors to focus on their specific AI task.

**You should read this if you:**

- Are creating a new AI component in SmartHopper
- Need to understand the batch processing lifecycle
- Want to customize model selection, metrics, or output transformation
- Need to implement capability-aware AI tool usage

---

## End-User Guide

`src/SmartHopper.Core/ComponentBase/AIStatefulAsyncComponentBase.*.cs` — partial class split across **8 files**:

| File | Concern |
| --- | --- |
| `AIStatefulAsyncComponentBase.Main.cs` | Fields, constructor, parameter registration, `RequiredCapability` / `UsingAiTools`. |
| `AIStatefulAsyncComponentBase.Lifecycle.cs` | `SolveInstance`, state hooks, `OnEnteringNeedsRun`, removal, cancel. |
| `AIStatefulAsyncComponentBase.AI.cs` | `CallAIToolAsync`, `CallAIAsync`, batch interception, timeout config. |
| `AIStatefulAsyncComponentBase.Batch.cs` | Batch queue, submission, polling, sentinel/ID lifecycle. |
| `AIStatefulAsyncComponentBase.Processing.cs` | `ProcessBatchResults`, `ReconstructOutputTree`. |
| `AIStatefulAsyncComponentBase.Metrics.cs` | `FinishResults`, `SetMetricsOutput`, `PrepareInputs` / `SentinelTransformOutputs`, `_persistedMetrics`. |
| `AIStatefulAsyncComponentBase.Persistence.cs` | `Read`/`Write` for batch state, *Load results from file*, *Check batch status*. |
| `AIStatefulAsyncComponentBase.UI.cs` | `CreateAttributes` (badges), `UpdateBadgeCache`, `GetStateMessage` overrides. |

Inherits from [AIProviderComponentBase](./AIProviderComponentBase.md). Adds a `Settings` input (parameters, model, extras) and a `Metrics` output, AI request orchestration, capability-aware model selection, badge rendering and a full batch-processing pipeline.

### Purpose

The canonical base for any component that talks to an AI provider through SmartHopper. It owns the provider/model resolution, the AI request lifecycle (sync or batch), the metrics emission and the on-canvas badge rendering, so derived components only need to express *what* they do (system prompt, tool, output mapping).

### Inputs / outputs added

- `Settings` (`S`, `IGH_Goo`, optional, item) — accepts an `AIRequestParameters` (preferred) or any value cast to a model name string. Drives the `_requestParameters` field used for model, temperature, max tokens, batch tier, custom timeout and provider-specific extras.
- `Run?` (`R`, bool, item, default `false`) — inherited from `StatefulComponentBase`.
- `Metrics` (`M`, text, item) — JSON containing provider/model, all token counters, finish reason, completion time, context usage, data count and iteration count. See `SetMetricsOutput`. When processing multiple branches the payload is a JSON array with one entry per branch so downstream components (e.g. `Deconstruct Metrics`) can expand it into parallel lists.

### Capability-aware model selection

- `protected virtual AICapability RequiredCapability { get; set; }` — the component's declared capability. The getter merges in capabilities required by every tool listed in `UsingAiTools`.
- `protected virtual IReadOnlyList<string> UsingAiTools => Array.Empty<string>()` — names of tools used by this component. Drives the merged capability and the *not recommended* badge.
- `GetModel()` returns the user's model from `Settings`, or the provider's `GetDefaultModel(RequiredCapability, useSettings: true)` fallback.
- `UpdateBadgeCache()` runs a synthetic `AIRequestCall` through validation to populate the badge flags: *Verified*, *Deprecated*, *Invalid*, *Replaced*, *Not recommended*. Cached per solve.

### Two execution modes

Both modes converge at `FinishResults<T>` and emit metrics atomically.

#### Non-batch (sync)

```text
DoWorkAsync()
  └── RunProcessingAsync()                 [via DataTreeProcessor.RunAsync]
        └── function(inputs)               [once per branch/item]

              1. PrepareInputs(inputs, ctx)            ← virtual hook
              2. CallAIToolAsync(...) / CallAIAsync(...)
              3. (return outputs dict)
        └── FinishResults(primary, ...extras)
              → SetPersistentOutput per output
              → SetMetricsOutput(null)
  └── Worker.SetOutput()  → no-op

```

#### Batch

```text
DoWorkAsync()
  └── RunProcessingAsync()
        └── function(inputs)

              1. PrepareInputs(inputs, ctx)
              2. CallAIToolAsync / CallAIAsync queues item, returns sentinel ##SH_BATCH:{customId}##
        └── TrySubmitBatchAsync()         → submits queue to provider
  └── Worker.SetOutput()  → no-op
[Stay in Processing]
PollBatchStatusAsync() [background timer]
  └── OnBatchCompleted(results, messages)
        └── ProcessBatchResults<T>(decode, messages)
              for each sentinel:
                item = decode(customId, body)
                SentinelTransformOutputs({ primary → item }, ctx)
              SetAIReturnSnapshot(aggregatedMetrics)
              FinishResults(primary, ...extras)
[Completed] → ExpireSolution → RestorePersistentOutputs

```

### Metrics

Two emission patterns:

| Component shape | Where metrics are emitted |
| --- | --- |
| Standard AI components | `FinishResults` (non-batch) or `ProcessBatchResults` → `FinishResults` (batch). |
| Sync-output components (e.g. `AIChatComponent`) | Call `SetMetricsOutput(DA)` directly inside their `SolveInstance`; do not use `FinishResults`. |

When the component processes multiple branches, emit one metric entry per branch. Non-batch workers accumulate entries via `CombineIntoPersistedMetrics`; batch workers collect them automatically in `ProcessBatchResults`. The resulting `AIMetricsList` is serialized as a single JSON object when all entries share the same provider/model, or as a JSON array when they differ (e.g. after modality fallback).

`OnSolveInstancePostSolve` does not call `SetMetricsOutput`. The single authoritative metrics instance is `PersistedMetricsList`; `AIReturn.Metrics` re-aggregates on every read so writes to it are no-ops, hence the dedicated field.

### Batch features

- `IsBatchRequest()` — true when `Settings.BatchTier == true` **and** the provider implements `IAIBatchProvider`. Surfaces a one-shot remark when batch is requested but unsupported.
- `TrySubmitBatchAsync<T>(outputName, resultDict, ct)` — convenience: stores the sentinel tree and submits the queue.
- `_sentinelTrees`, `_batchSentinelIds`, `_batchQueue`, `_batchSubmission` — explicit lifecycle, see XML docs in `Main.cs`. Persisted by `Write/Read` so polling resumes after file save/load and *Load results from file* can rebuild trees.
- Cancellation: `OnTasksCancelDetected` immediately surfaces *"Cancelling batch ..."*, calls `IAIBatchProvider.CancelBatchAsync`, then updates the message with success/failure.
- Context menu: *Load results from file* (multi-select) and *Check batch status* (enabled only while a batch is active).

---

## Developer Reference

### Virtual hooks

#### `PrepareInputs(Dictionary<string, object> inputs, ProcessingUnitContext ctx)`

Fires inside `function(inputs)` in both modes, **before** the AI call. Override to inject derived fields, normalize values or validate early. Inputs are not available to `SentinelTransformOutputs` in batch mode — cache anything you need on the component.

#### `SentinelTransformOutputs(Dictionary<string, IGH_Goo> decodedOutputs, ProcessingUnitContext ctx)`

Fires after each result is decoded, before `FinishResults`. Default returns the dictionary unchanged. Override to split one response into multiple outputs or to add computed fields. Extra keys (anything other than the primary output param name) are accumulated and forwarded to `FinishResults` as `additionalOutputs`.

> Renamed from the historical `TransformOutputs`. The previous name no longer exists.

#### `ProcessingUnitContext`

```csharp
public readonly struct ProcessingUnitContext
{
    public GH_Path Path { get; init; }       // non-batch only
    public int? ItemIndex { get; init; }     // non-batch only
    public string SentinelId { get; init; }  // batch only
}

```

### `FinishResults<T>`

```csharp
protected void FinishResults<T>(
    string primaryOutputParamName,
    GH_Structure<T> primaryTree,
    params (string name, object value)[] additionalOutputs)
    where T : IGH_Goo;

```

Single finalization point invoked by both modes. Routing for `additionalOutputs.value`:

- `IGH_Structure` → `DA.SetDataTree`
- `IEnumerable` (non-string) → `DA.SetDataList`
- anything else → `GH_Convert.ToGoo` → `DA.SetData`

It also stamps `CompletionTime` from `_batchCompletionTime` when present and emits the `Metrics` output via `SetMetricsOutput(null)`.

### Implementation requirements (standard AI component)

```csharp
// Worker.DoWorkAsync — non-batch branch must call FinishResults:
var batchSubmitted = await parent.TrySubmitBatchAsync("Result", result, token);
if (!batchSubmitted)
    parent.FinishResults("Result", result["Result"]);

// Worker.SetOutput — must be a no-op:
public override void SetOutput(IGH_DataAccess DA, out string message)
    => message = string.Empty;

// OnBatchCompleted — delegate entirely to ProcessBatchResults:
protected override void OnBatchCompleted(
    IReadOnlyDictionary<string, JObject> results,
    IReadOnlyList<SHRuntimeMessage> messages = null)
{
    var sentinel = this.GetSentinelTree("Result");
    if (results == null || sentinel == null) return;
    this.ProcessBatchResults<GH_String>(
        "Result",
        sentinel,
        results,
        (customId, body) => new GH_String(Decode(body)),
        messages);
}

```

### Context menu overrides

- `AppendAdditionalComponentMenuItems` adds *Load results from file* and *Check batch status*.
- `Menu_LoadResultsFromFile_Click` opens a file dialog and feeds files into `ProcessBatchResults`.
- `Menu_CheckBatchStatus_Click` forces an immediate poll.

---

## Architecture & Design

### Related

- [AIProviderComponentBase](./AIProviderComponentBase.md)
- [AISelectingStatefulAsyncComponentBase](./AISelectingStatefulAsyncComponentBase.md)
- [AsyncComponentBase](./AsyncComponentBase.md), [AsyncWorkerBase](./AsyncWorkerBase.md)
- [BatchSentinel](./BatchSentinel.md), [Data tree processing schema](./DataTreeProcessingSchema.md)
