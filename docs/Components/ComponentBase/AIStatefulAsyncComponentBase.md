# AIStatefulAsyncComponentBase

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

---

The canonical base for any component that talks to an AI provider through SmartHopper. It owns the provider/model resolution, the AI request lifecycle (sync or batch), the metrics emission and the on-canvas badge rendering, so derived components only need to express *what* they do (system prompt, tool, output mapping).

## Inputs / outputs added

- `Settings` (`S`, `IGH_Goo`, optional, item) — accepts an `AIRequestParameters` (preferred) or any value cast to a model name string. Drives the `_requestParameters` field used for model, temperature, max tokens, batch tier, custom timeout and provider-specific extras.
- `Run?` (`R`, bool, item, default `false`) — inherited from `StatefulComponentBase`.
- `Metrics` (`M`, text, item) — JSON containing provider/model, all token counters, finish reason, completion time, context usage, data count and iteration count. See `SetMetricsOutput`.

## Capability-aware model selection

- `protected virtual AICapability RequiredCapability { get; set; }` — the component's declared capability. The getter merges in capabilities required by every tool listed in `UsingAiTools`.
- `protected virtual IReadOnlyList<string> UsingAiTools => Array.Empty<string>()` — names of tools used by this component. Drives the merged capability and the *not recommended* badge.
- `GetModel()` returns the user's model from `Settings`, or the provider's `GetDefaultModel(RequiredCapability, useSettings: true)` fallback.
- `UpdateBadgeCache()` runs a synthetic `AIRequestCall` through validation to populate the badge flags: *Verified*, *Deprecated*, *Invalid*, *Replaced*, *Not recommended*. Cached per solve.

## Two execution modes

Both modes converge at `FinishResults<T>` and emit metrics atomically.

### Non-batch (sync)

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

### Batch

```text
DoWorkAsync()
  └── RunProcessingAsync()
        └── function(inputs)
              1. PrepareInputs(inputs, ctx)
              2. CallAIToolAsync / CallAIAsync queues item, returns sentinel ##SH_BATCH:{customId}##
        └── OnProcessingOutputsReady(result.Outputs)   → base captures trees
  └── Worker.SetOutput()  → no-op
OnWorkerCompleted()
  └── queue populated? → derive sentinel trees from captured outputs
        └── BeginPendingSubmission()     // epoch + marker — BatchInFlight true from here
        └── SubmitCollectedBatchAsync()  → SubmitBatchQueueAsync() → provider submission
        └── no sentinel-bearing output? → Error (orphaned queue never completes)
        └── CompleteSubmissionSetup()    // sentinels installed → drain staged import + start poll
[Stay in Processing — BatchInFlight holds guards through the whole poll]
PollBatchStatusAsync() [background timer, FinalizeEpoch re-checked before finalize]
  └── OnBatchCompleted(results, messages)
        └── ProcessBatchResults<T>(decode, messages)
              for each unique customId (decode-once / place-many):
                items = decode(customId, body)   // decode failure → recorded item error
                SentinelTransformOutputs({ primary → item }, ctx)  // may replace primary
                ResultStore.ResolveSentinel(customId, resolved)    // idempotent
              SetAIReturnSnapshot(aggregatedMetrics)
              BuildRunTrees() → FinishResults(primary, ...extras)  // sentinels stripped
CompleteBatchAndTransition(): per-ID outcomes
  └── resolved  → counts toward success
  └── provider-failed (terminal) → warning, never auto-resubmitted
  └── missing / decode-failed (retryable) → NeedsRun, re-executes
      (Processing → NeedsRun is a legal edge reserved for this demotion,
       reason ResultsIncomplete; NeedsRun → Processing re-fires while Run?=true
       and the retry collects only the missing units)
  └── all failed → Error
[Completed] → ExpireSolution → RestorePersistentOutputs
```

### Lifecycle ownership invariant

`OnWorkerCompleted` is the mandatory collect→submit boundary: a worker may queue
deferred calls via `CallAIToolAsync`/`CallAIAsync` and **never** needs to remember
a submission call — the base submits any populated queue itself. The historical
`AIListFilter` failure mode (sentinels published as data, component `Done`, no
remote job) is unreachable by construction:

- A populated queue with no sentinel-bearing output transitions to `Error`,
  never `Completed` (orphaned queue cannot silently pass).
- Single submission owner: `PendingSubmission` + `BatchInFlight` cover the
  whole submit window on **both** submission paths. The parent handoff marks
  it synchronously on the UI thread; `SubmitBatchQueueAsync` itself claims the
  marker for direct worker submissions (`TrySubmitBatchAsync`), so the
  provider HTTP call is always covered while `Submission` is still null —
  the processing watchdog can no longer spawn a phantom worker that replays
  pending fragments as reused units and races the run to `Completed`.
- Sentinel-install before poll: `SubmitBatchQueueAsync` never starts the poll
  timer. Every submission path calls `CompleteSubmissionSetup()` only after
  `StoreSentinelTree` — it drains a staged file import (collect-only path)
  and starts polling, so neither completion nor polling can observe an empty
  reconstruction map.
- Ownership-gated callbacks: a poll or remote-cancel callback may only mutate
  shared state while its captured submission is still installed and
  `FinalizeEpoch` is unchanged — the guard runs before timer teardown,
  progress writes, submission clears and finalization, not just before
  publication. `NoteSubmissionInstalled()` bumps `FinalizeEpoch` when a new
  submission installs, so finalizations queued by the previous run are
  discarded. The poll timer records which submission it serves;
  `StopBatchPollTimer(expected)` is a no-op for stale callers.
- `Epoch`/`FinalizeEpoch` invalidate late continuations: a provider submit
  returning after the run was cleared/cancelled/superseded is remotely
  cancelled and dropped, and a poll completion re-checks the finalize epoch
  before mutating outputs — stale callbacks cannot revive dead runs.
- Restore before resume: `Read` installs the submission, clock, decode
  context, pending fragments, customId map and sentinel trees first, then
  schedules the immediate first poll — a reopened batch can never complete
  against partially restored state. `Write` persists the authoritative
  submission clock (`BatchRunState.SubmissionTimestamp` = restored
  `StartTime`), so successive reopen cycles keep the original deadline and
  `completion_time` instead of resetting them to the previous reopen.
- Manual import owns completion: `ImportBatchResultContents` — shared by the
  *Load results from file* menu entry and the `gh_batch_import_results` AI
  tool — detaches the local poll (including a still-pending submit), keeps
  the submitted provider for decode, invalidates in-flight callbacks and
  passes the known expected ids to the completion accounting, so a
  partially matching import counts its missing items correctly. The remote
  job is left running upstream untouched. Ownership is only taken when the
  import can proceed: an unparseable file (`loaded = 0`) or a fallback
  without active batch mode returns an error without mutating run state.
  The direct path additionally requires every matched id to be freshly
  resolvable (`UnitResultStore.CanResolveSentinel` →
  `SentinelResolution.CanResolve`: live customId→unitKey map AND pending
  fragment AND not-already-resolved) — the map and fragments only persist
  while a submission is in flight, so a sentinel id surviving on a
  completed component is stale bookkeeping that would classify unresolved
  and resubmit paid work. Unresolvable matches take the collect-only
  fallback, whose armed run bypasses the committed result cache
  (`BypassCommittedResultReuse` + `TryGetFragment(includeCommitted)`) so
  units re-collect and emit fresh sentinels instead of letting the queue
  run empty on cache hits. The collect-only arm is bounded to one
  collection cycle — `ClearCollectOnlyArm` runs on
  Completed/Error/Cancelled/cache-clear so a stale arm can never re-key
  old results into an unrelated batch.
- Submission failure transitions to `Error` and expires the solution — the
  ambiguous `TrySubmitBatchAsync` bool no longer gates output publication.
- `SetPersistentOutput` is the resolved-output boundary: sentinels resolvable
  from the committed snapshot (customId → unit key → committed fragment) are
  republished in place at their original paths; unresolvable ones are stripped,
  so `##SH_BATCH:` placeholders can never leak into outputs — not via workers
  bypassing `FinishResults`, not via restore. `FinishResults` strips leftovers
  too; unresolved units stay uncommitted and re-execute on the next run.
- On restore, `MarkNeedsRunForOrphanedSentinels` scans persisted outputs after
  committed fragments and batch state are restored: when no resumable
  submission exists and a sentinel still cannot resolve, the component is
  forced to `NeedsRun` — save-mid-flight never leaves a `Completed` component
  publishing zero items (a sentinel tree counts as outputs, so forced
  `NeedsRun` bypasses the first-solve restore suppression).
- `OnBatchCompleted` exceptions set `CompletionFailed` → `Error`; decode
  exceptions are recorded per item instead of retrying the same decoder.
- Completion requires resolution: delivered response bodies alone never
  complete a run — expected customIds must resolve inside their fragments
  (provider-failed ids are terminal, never auto-resubmitted).
- Runs execute against an immutable snapshot: entering `Processing` captures
  the pending input hashes plus the effective `AIRequestParameters` for the
  run (`_runParameters`), so every unit in one run observes the same
  inputs/settings. Edits arriving mid-run are *not* mixed into the in-flight
  work — completion commits the captured snapshot (`CommitRunSnapshot`),
  leaving the newer edits pending for exactly one follow-up run. The
  mid-batch input-edit check (`PendingInputsDifferFromRunSnapshot`) diffs
  pending hashes against that same run snapshot and ignores `Run?`, so a
  momentary button releasing back to false is not mistaken for changed work
  content.

## Virtual hooks

### `PrepareInputs(Dictionary<string, object> inputs, ProcessingUnitContext ctx)`

Fires inside `function(inputs)` in both modes, **before** the AI call. Override to inject derived fields, normalize values or validate early. Inputs are not available to `SentinelTransformOutputs` in batch mode — cache anything you need on the component.

### `SentinelTransformOutputs(Dictionary<string, IGH_Goo> decodedOutputs, ProcessingUnitContext ctx)`

Fires after each result is decoded, before `FinishResults`. Default returns the dictionary unchanged. Override to split one response into multiple outputs or to add computed fields. Returning the primary output param name replaces the decoded items — the transform owns the final unit output shape. Extra keys are accumulated and forwarded to `FinishResults` as `additionalOutputs`.

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

- `IGH_Structure` → `DA.SetDataTree`
- `IEnumerable` (non-string) → `DA.SetDataList`
- anything else → `GH_Convert.ToGoo` → `DA.SetData`

It also stamps `CompletionTime` from `_batchCompletionTime` when present and emits the `Metrics` output via `SetMetricsOutput(null)`.

## Metrics

Two emission patterns:

| Component shape | Where metrics are emitted |
| --- | --- |
| Standard AI components | `FinishResults` (non-batch) or `ProcessBatchResults` → `FinishResults` (batch). |
| Sync-output components (e.g. `AIChatComponent`) | Call `SetMetricsOutput(DA)` directly inside their `SolveInstance`; do not use `FinishResults`. |

`OnSolveInstancePostSolve` does not call `SetMetricsOutput`. The single authoritative metrics instance is `_persistedMetrics`; `AIReturn.Metrics` re-aggregates on every read so writes to it are no-ops, hence the dedicated field.

### Complete-snapshot metrics

The `Metrics` tree always describes the **entire current output**, not only the
latest run's calls:

- Every call recorded during a unit also lands in the unit's **provenance**
  inside `UnitResultStore` (`RecordMetrics` / `RecordMetricsForSentinel`) and
  persists alongside the output fragments.
- Cache hits replay the stored provenance (`AppendReusedMetrics`) at the unit's
  own path with `reused: true` and zeroed `data_count`/`iterations_count` —
  original provider/model/usage/cost/`completed_at`/`execution_id` survive.
- `completed_at` is a UTC `DateTimeOffset` stamped when a sync call completes
  (`AIBodyBuilder.SetCompletionTime`) or when a batch result is decoded;
  `execution_id` is a stable per-execution id (batch uses its customId) so
  shared placements deduplicate instead of double-charging.
- Batch elapsed-time stamping (`StampCompletionTimeIntoMetricsTree`) skips
  `reused` entries — replayed provenance keeps its original duration.
- Batch reconstruction uses `UnitResultStore.BuildRunTrees` when placements
  exist: every output channel is rebuilt from typed fragments at their recorded
  targets — reused units and secondary channels are no longer lost.
- Serialization for all entry points (component output, provenance store,
  Combine/Deconstruct Metrics) shares `AIMetricsJson` — one schema.

### Metrics `role` attribution

Each entry carries an optional `role` identifying which call produced the usage: `main` (primary component request), `tool:{name}` (direct `CallAIToolAsync` call), `fallback` (request-preparation helper), `passthrough:{name}` (conversion step). The label is stamped once when the entry is recorded so the metrics list and the emitted tree cannot diverge; batch items resolve it per custom ID from `SubmittedRoles` (recorded at queue time, persisted to the archive for save/reopen).

## Batch features

- `IsBatchRequest()` — true when `Settings.BatchTier == true` **and** the provider implements `IAIBatchProvider`. Surfaces a one-shot remark when batch is requested but unsupported.
- `TrySubmitBatchAsync<T>(outputName, resultDict, ct)` — optional early-submit convenience: submits the queue during `DoWorkAsync`, stores the sentinel tree, then runs `CompleteSubmissionSetup()` (drain staged import + start polling). Workers may omit it — `OnWorkerCompleted` submits any collected queue automatically and completes the same boundary.
- `CaptureBatchDecodeContext()` / `RestoreBatchDecodeContext(JObject)` — component-specific data needed to interpret results (list-filter source items, Boolean fallback value) is persisted with the in-flight batch so save/reopen decodes against submission-time inputs.
- Batch decode uses `GetSubmittedProviderName()` — the provider that ran the job, not the live selection — so mid-flight provider switches cannot misdirect decoding.
- `_sentinelTrees`, `_batchSentinelIds`, `_batchQueue`, `_batchSubmission` — explicit lifecycle, see XML docs in `Main.cs`. Persisted by `Write/Read` so polling resumes after file save/load and *Load results from file* can rebuild trees. `BatchSubmittedAt` is restored too — both into `_batchState.StartTime` and into the reconstructed `AIBatchSubmission.SubmittedAt` — keeping the 24 h deadline, `completion_time`, and any provider-side elapsed-time derivation on the original clock across reopens. `SubmittedModels`/`SubmittedRoles` persist the submitted run's per-item model/role identity so decoded metrics reflect what actually ran.
- `BatchRunState` bundles all batch fields plus run generations: `Epoch` (run identity — bumped on reset/invalidate), `FinalizeEpoch` (destructive-event generation), `PendingSubmissionEpoch` (in-flight submit marker backing `BatchInFlight`), `DecodeErrors`/`CompletionFailed` (completion accounting).
- Cancellation: `OnTasksCancelDetected` immediately surfaces *"Cancelling batch …"*, invalidates async work, calls `IAIBatchProvider.CancelBatchAsync`, then updates the message with success/failure. Because that continuation only fires while worker tasks exist, `OnEnteringCancelled` performs the same teardown on **any** `Cancelled` entry — during the poll phase it is the only reachable cancel path (invalidate epochs → remote-cancel or stop the timer for a pending-only submit). `CancelRemoteBatchAsync` claims `Submission` synchronously so duplicate cancel paths cannot double-cancel, and provider `CancelBatchAsync` implementations propagate non-success so the message reports the real outcome instead of a false success.
- Context menu: *Load results from file* (multi-select) and *Check batch status* (enabled only while a batch is active). The import is also exposed to AI tools / MCP as `gh_batch_import_results` (component GUID + host file paths; mutating + `openWorldHint` gated), which shares `ImportBatchResultContents` and returns mode/matched/expected statistics.

## Implementation requirements (standard AI component)

```csharp
// Worker.DoWorkAsync — run processing; then publish on the synchronous
// path only. Batch submission needs NO worker call: the base submits any
// populated queue at OnWorkerCompleted (TrySubmitBatchAsync remains as an
// optional early-submit convenience that also doubles as the mode check).
this.result = await parent.RunProcessingAsync(inputTree, function, processingOptions, token);
if (!parent.IsBatchRequest())
    parent.FinishResults("Result", this.result["Result"]);

// Worker.SetOutput — must be a no-op:
public override void SetOutput(IGH_DataAccess DA, out string message)
    => message = string.Empty;

// OnBatchCompleted — delegate entirely to ProcessBatchResults:
protected override void OnBatchCompleted(
    IReadOnlyDictionary<string, JObject>? results,
    IReadOnlyList<SHRuntimeMessage>? messages = null)
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
