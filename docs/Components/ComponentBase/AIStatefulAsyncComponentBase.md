# AIStatefulAsyncComponentBase

`src/SmartHopper.Core/ComponentBase/AIStatefulAsyncComponentBase.*.cs` â€” partial class split across **8 files**:

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

---

The canonical base for any component that talks to an AI provider through SmartHopper. It owns the provider/model resolution, the AI request lifecycle (sync or batch), the metrics emission and the on-canvas badge rendering, so derived components only need to express *what* they do (system prompt, tool, output mapping).

## Inputs / outputs added

- `Settings` (`S`, `IGH_Goo`, optional, item) â€” accepts an `AIRequestParameters` (preferred) or any value cast to a model name string. Drives the `_requestParameters` field used for model, temperature, max tokens, batch tier, custom timeout and provider-specific extras.
- `Run?` (`R`, bool, item, default `false`) â€” inherited from `StatefulComponentBase`.
- `Metrics` (`M`, text, item) â€” JSON containing provider/model, all token counters, finish reason, completion time, context usage, data count and iteration count. See `SetMetricsOutput`.

## Capability-aware model selection

- `protected virtual AICapability RequiredCapability { get; set; }` â€” the component's declared capability. The getter merges in capabilities required by every tool listed in `UsingAiTools`.
- `protected virtual IReadOnlyList<string> UsingAiTools => Array.Empty<string>()` â€” names of tools used by this component. Drives the merged capability and the *not recommended* badge.
- `GetModel()` returns the user's model from `Settings`, or the provider's `GetDefaultModel(RequiredCapability, useSettings: true)` fallback.
- `UpdateBadgeCache()` runs a synthetic `AIRequestCall` through validation to populate the badge flags: *Verified*, *Deprecated*, *Invalid*, *Replaced*, *Not recommended*. Cached per solve.

## Two execution modes

Both modes converge at `FinishResults<T>` and emit metrics atomically.

### Non-batch (sync)

```text
DoWorkAsync()
  â””â”€â”€ RunProcessingAsync()                 [via DataTreeProcessor.RunAsync]
        â””â”€â”€ function(inputs)               [once per branch/item]
              1. PrepareInputs(inputs, ctx)            â† virtual hook
              2. CallAIToolAsync(...) / CallAIAsync(...)
              3. (return outputs dict)
        â””â”€â”€ FinishResults(primary, ...extras)
              â†’ SetPersistentOutput per output
              â†’ SetMetricsOutput(null)
  â””â”€â”€ Worker.SetOutput()  â†’ no-op
```

### Batch

```text
DoWorkAsync()
  â””â”€â”€ RunProcessingAsync()
        â””â”€â”€ function(inputs)
              1. PrepareInputs(inputs, ctx)
              2. CallAIToolAsync / CallAIAsync queues item, returns sentinel ##SH_BATCH:{customId}##
        â””â”€â”€ TrySubmitBatchAsync()         â†’ submits queue to provider
  â””â”€â”€ Worker.SetOutput()  â†’ no-op
[Stay in Processing]
PollBatchStatusAsync() [background timer]
  â””â”€â”€ OnBatchCompleted(results, messages)
        â””â”€â”€ ProcessBatchResults<T>(decode, messages)
              for each sentinel:
                item = decode(customId, body)
                SentinelTransformOutputs({ primary â†’ item }, ctx)
              SetAIReturnSnapshot(aggregatedMetrics)
              FinishResults(primary, ...extras)
[Completed] â†’ ExpireSolution â†’ RestorePersistentOutputs
```

## Virtual hooks

### `PrepareInputs(Dictionary<string, object> inputs, ProcessingUnitContext ctx)`

Fires inside `function(inputs)` in both modes, **before** the AI call. Override to inject derived fields, normalize values or validate early. Inputs are not available to `SentinelTransformOutputs` in batch mode â€” cache anything you need on the component.

### `SentinelTransformOutputs(Dictionary<string, IGH_Goo> decodedOutputs, ProcessingUnitContext ctx)`

Fires after each result is decoded, before `FinishResults`. Default returns the dictionary unchanged. Override to split one response into multiple outputs or to add computed fields. Extra keys (anything other than the primary output param name) are accumulated and forwarded to `FinishResults` as `additionalOutputs`.

> Renamed from the historical `TransformOutputs`. The previous name no longer exists.

### `ProcessingUnitContext`

```csharp
public readonly struct ProcessingUnitContext
{
    public GH_Path Path { get; init; }       // non-batch only
    public int? ItemIndex { get; init; }     // non-batch only
    public string SentinelId { get; init; }  // batch only
}
```

## `FinishResults<T>`

```csharp
protected void FinishResults<T>(
    string primaryOutputParamName,
    GH_Structure<T> primaryTree,
    params (string name, object value)[] additionalOutputs)
    where T : IGH_Goo;
```

Single finalization point invoked by both modes. Routing for `additionalOutputs.value`:

- `IGH_Structure` â†’ `DA.SetDataTree`
- `IEnumerable` (non-string) â†’ `DA.SetDataList`
- anything else â†’ `GH_Convert.ToGoo` â†’ `DA.SetData`

It also stamps `CompletionTime` from `_batchCompletionTime` when present and emits the `Metrics` output via `SetMetricsOutput(null)`.

## Metrics

Two emission patterns:

| Component shape | Where metrics are emitted |
| --- | --- |
| Standard AI components | `FinishResults` (non-batch) or `ProcessBatchResults` â†’ `FinishResults` (batch). |
| Sync-output components (e.g. `AIChatComponent`) | Call `SetMetricsOutput(DA)` directly inside their `SolveInstance`; do not use `FinishResults`. |

`OnSolveInstancePostSolve` does not call `SetMetricsOutput`. The single authoritative metrics instance is `_persistedMetrics`; `AIReturn.Metrics` re-aggregates on every read so writes to it are no-ops, hence the dedicated field.

## Batch features

- `IsBatchRequest()` â€” true when `Settings.BatchTier == true` **and** the provider implements `IAIBatchProvider`. Surfaces a one-shot remark when batch is requested but unsupported.
- `TrySubmitBatchAsync<T>(outputName, resultDict, ct)` â€” convenience: stores the sentinel tree and submits the queue.
- `_sentinelTrees`, `_batchSentinelIds`, `_batchQueue`, `_batchSubmission` â€” explicit lifecycle, see XML docs in `Main.cs`. Persisted by `Write/Read` so polling resumes after file save/load and *Load results from file* can rebuild trees.
- Cancellation: `OnTasksCancelDetected` immediately surfaces *"Cancelling batch â€¦"*, calls `IAIBatchProvider.CancelBatchAsync`, then updates the message with success/failure.
- Context menu: *Load results from file* (multi-select) and *Check batch status* (enabled only while a batch is active).

## Implementation requirements (standard AI component)

```csharp
// Worker.DoWorkAsync â€” non-batch branch must call FinishResults:
var batchSubmitted = await parent.TrySubmitBatchAsync("Result", result, token);
if (!batchSubmitted)
    parent.FinishResults("Result", result["Result"]);

// Worker.SetOutput â€” must be a no-op:
public override void SetOutput(IGH_DataAccess DA, out string message)
    => message = string.Empty;

// OnBatchCompleted â€” delegate entirely to ProcessBatchResults:
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

For multi-output components, pass extras to `FinishResults`:

```csharp
parent.FinishResults(
    "Markdown", markdownTree,
    ("Images", imagesTree),
    ("Format", formatTree));
```

## Usage checklist

- Override `RequiredCapability` (and `UsingAiTools` if the component routes through tools).
- Use `CallAIToolAsync` for tool-based work and `CallAIAsync` for full chat-completion calls (e.g. output adapters with forced tool calls).
- Override `PrepareInputs` / `SentinelTransformOutputs` for input/output shaping.
- Implement `OnBatchCompleted` and call `ProcessBatchResults<T>` if your component supports batch.
- Use `SetPersistentRuntimeMessage` (inherited) for errors that should persist across solves.
- Use `SetAIReturnSnapshot(ret)` if an external source (e.g. WebChat) provides the response.

This is the most important base class for all AI-driven SmartHopper components. It handles everything from model selection and request parameters to batch processing and metrics tracking, freeing component authors to focus on their specific AI task.

- [AIProviderComponentBase](./AIProviderComponentBase.md), [StatefulComponentBase](./StatefulComponentBase.md)
- [AIOutputAdapterBase](./AIOutputAdapterBase.md), [AISelectingStatefulAsyncComponentBase](./AISelectingStatefulAsyncComponentBase.md)
- [BatchSentinel](./BatchSentinel.md), [Data tree processing schema](./DataTreeProcessingSchema.md)

## Metadata

- Source Code: See source repository.
- Since Version: 2.0.0
- Last Updated: 2026-07-21
- Documentation Maintainer: Marc Roca Musach

---


## Why Read This?

This document provides details about AIStatefulAsyncComponentBase.


## End-User Guide

End-user guidance for AIStatefulAsyncComponentBase.


## Developer Reference

Example usage:

`csharp
// Placeholder example
``r

`csharp
// Another placeholder example
``r


## Architecture & Design

Architecture and design notes for AIStatefulAsyncComponentBase.
