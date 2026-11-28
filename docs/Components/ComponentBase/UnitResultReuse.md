# Incremental Unit-Result Reuse

How stateful components avoid re-executing unchanged work: a persistent,
per-component cache of processing-unit results keyed by **logical work
identity**, not tree position.

## The core distinction

Two different identities, deliberately separate:

| Identity | Includes | Purpose |
|---|---|---|
| Input-tree revision | Paths, branch boundaries, item order, typed values | Detect edits and rebuild the processing layout (existing `InputHash_*` machinery) |
| Processing-unit key | Component reuse context + semantic fingerprint of the unit's *effective* inputs | Decide whether a previous result remains valid |

Entwine re-indexing changes the first but not the second — results follow the
content to its new path seat.

## Pieces

- **`CanonicalValues`** (`Core/DataTree/CanonicalValues.cs`) — Grasshopper-free
  canonical serialization: primitives, `AIRequestParameters` (with sorted
  `Extras`), `JToken` (sorted keys, order-significant arrays), fallback tokens.
- **`SemanticFingerprint`** (`Core/DataTree/SemanticFingerprint.cs`) — the goo-aware
  surface: `Of(item, strict)`, `OfGoo`, `OfInputs`. Two modes:
  - `strict = false` (change detection): unknown values → display string (stable,
    status-quo insensitivity).
  - `strict = true` (unit keys): unknown values → runtime-instance token → can
    never hit the cache → always recomputed. Safe direction. **Custom
    `ToString()` overrides are not trusted** — two distinct objects can print the
    same description, so strict mode always uses opaque identity for them.
  - **Rhino geometry** (`GeometryBase` via `ScriptVariable` or direct values):
    fingerprinted by `geometry.ToJSON()` content hash — edits to control points,
    trims, or vertex data change the key even when the display text is identical.
    Serialization failures degrade to opaque/display tokens.
  - **Custom data types** can register a content-fingerprint adapter via
    `RegisterValueFingerprinter(Func<object,string>)` (returns a complete token
    or null to defer). This is the supported extension point; there is no
    automatic `ToString()`/reflection identity for arbitrary types.
- **`UnitResultStore`** (`Core/ComponentBase/Cores/UnitResultStore.cs`) —
  per-component store with two scopes:
  - **Committed**: results from the last completed run; persisted in the `.gh`
    file (`PersistenceKeys.ResultCache`) via `SafeGooCodec`.
  - **Run**: fragments produced/copied during the in-flight run; at
    `CommitRun()` only fragments *without unresolved batch sentinels* are kept —
    failed/unresolved units simply re-execute next run.
  - **Provenance**: each unit key also holds its execution metrics
    (`List<AIMetrics>`, one entry per provider/tool call in order). Cache hits
    replay outputs *and* their original metrics — the Metrics output always
    describes the complete current snapshot, not only the latest run's calls.
  - **Placements**: every scheduled unit records
    `(unitKey, inputIndices, itemIndex, targetSeats, reused)` — paths as raw
    index sequences so layout/ordering/persistence stay decoupled from
    Grasshopper types (GH_Path only at emit boundaries). `BuildRunTrees`
    rebuilds every output channel from fragments+placements — the canonical
    assembly for mixed reused/new results at batch completion (typed
    heterogeneous channels, including secondary outputs that never carried
    sentinels). `RunMetricsProjection.Build` is the Metrics-channel
    counterpart: it emits provenance in *placement* order at the same seats,
    so metrics items always align with result items even when completion
    order diverges (reused replay during processing vs. fresh decode at poll
    completion); unplaced and unit-less ("orphan") provenance appends last.

## Flow

```
NeedsRun (gate, unchanged) → Processing → worker → RunProcessingAsync
  → ApplyResultReuse wraps the unit function (only when SupportsResultReuse)
  → per unit: ComputeUnitKey(inputs) → TryGetFragment → hit: replay / miss: execute + StoreFragment
  → OnWorkerCompleted / FinishResults → CommitRun
```

Unit key = `sha256(reuseContext | fingerprint(effective inputs))` where
`reuseContext` = component type + provider + model + execution-relevant request
parameters (`CanonicalParameters(..., executionFieldsOnly: true)` — drops
`TimeoutSeconds`, `BatchTier`, `ShareIdenticalResults`, which change transport
or policy, not produced output).

## Batch integration

- At queue time the sentinel's `customId` is associated with the current unit
  key (`RegisterSentinel`).
- The fragment storing the sentinel stays "pending" — dropped by `CommitRun`
  until every queued call is decoded.
- `ProcessBatchResults` / `ProcessMappingsBatchResults` call `ResolveSentinel`
  with the decoded outputs, completing fragments in place.
- **Resolution contract**: after a `customId` resolves, no sentinel bearing it
  may remain in the fragment — `ResolveSentinel` sweeps leftover placeholders
  from sibling channels and logs a warning. Producers must therefore store the
  sentinel only in channel(s) the decode pass resolves; a placeholder sentinel
  in an uncovered channel was previously left behind and silently invalidated
  the fragment at commit (disabling cross-run reuse entirely for `t2b`/`l2b`).
- Fragment channels store the component's *canonical output types* under the
  output parameter names (e.g. `Result`/`Used Fallback` holding `GH_Boolean`)
  in both sync and batch modes — sentinel strings are transport-only and are
  replaced by typed items at decode. Keeping fragment content mode-independent
  is what lets a committed unit replay verbatim in sync, batch, and mixed runs.
- An in-flight batch survives save/reopen: `PendingResultCache` +
  `UnitCustomIdMap` persistence keys restore fragments and the map.
- `OnEnteringProcessing` does **not** reset the run store while a submission is
  in flight (`AIStatefulAsyncComponentBase.ResetRunResults`).

## Sharing policy

`Settings → Share` (default true): identical units inside a run share one
produced result (`AIRequestParameters.ShareIdenticalResults`, local policy —
never sent to providers). When false, occurrences get deterministic `#N` slot
suffixes so each generates independently **and** planner-level
`GroupIdenticalBranches` is disabled for the run — collapsing siblings before
the store sees them would silently re-share. Unchanged units keep their results
in both modes. Shared placements carry the same `execution_id` in their metrics
so usage is deduplicated instead of charged once per occurrence.

## Opt-out contract

`SupportsResultReuse` (default `false` on `StatefulComponentBase`, `true` on
`AIStatefulAsyncComponentBase`). Components whose work has side effects or
consumes external state **must** stay opted out — currently: chat, report,
selecting components (canvas mutations), file/web fetchers, and the forum/web
summarizers (fetch inside workers). Deterministic local components opt in.

## Clearing

Component menu → **Clear output cache** (`ClearResultCache()`): cancels
in-flight work, wipes the store and persistent outputs, forces `NeedsRun` —
and reprocesses immediately when `Run` remains true.

## Metrics replay

- Metrics emit in **placement order** (`RunMetricsProjection`), never in
  completion order — a `PAPA,SIERRA,ROMEO` result tree produces metrics in
  the same sequence even when the fresh entry decoded last. `reused` is
  recorded per *occurrence* (seat), so a shared unit emits `reused: true`
  only on seats that replayed it.
- Stored provenance replays per cache hit with `reused: true`,
  `data_count: 0`, `iterations_count: 0` — historical usage, cost, provider,
  model, `completed_at` and `execution_id` are preserved, while zeroed counts
  keep "processed this run" aggregation honest.
- Batch elapsed-time stamping skips `reused` entries — replayed provenance
  keeps its original duration.
- Replayed entries appear at the unit's own path (or grafted child for
  `ItemGraft`) and replicate to grouped siblings like fresh metrics.
- `Combine Metrics` deduplicates entries sharing an `execution_id`; each real
  execution contributes usage once no matter how many placements replay it.

## Limits

- Latest snapshot only — no A → B → A history revival.
- File-path/URL-typed inputs are keyed by the path value, not file content
  (same limitation as input change detection).
- Bitmap-kind images are opaque in strict mode → always recomputed (canvas
  screenshots never hit the cache).
- Legacy persisted entries (format v1) restore outputs without provenance —
  their metrics stay empty until re-executed; timestamps are never fabricated.
- A silently-mutated object that exposes neither complete content nor a
  revision cannot be detected by any cache — adapters cover that contract.
