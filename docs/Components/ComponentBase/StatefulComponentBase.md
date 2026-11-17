# StatefulComponentBase

`src/SmartHopper.Core/ComponentBase/StatefulComponentBase.cs`

Async base that adds a state machine, input-change detection, debounce, persistent outputs and persistent runtime messages on top of [AsyncComponentBase](./AsyncComponentBase.md). Delegates state to a [ComponentStateManager](./ComponentStateManager.md) and persistence to `GHPersistenceService`.

---

Make long-running components behave predictably across button/toggle Run inputs, bursty input changes and document save/load. Outputs survive a save/load cycle without re-running.

## Design criteria

- **Centralized state machine.** All transitions go through `ComponentStateManager.RequestTransition`. Per-state handlers (`OnStateCompleted`, `OnStateProcessing`, …) live on the base.
- **Hash-based input change detection.** Stable, deterministic hashes for each input parameter (FNV-1a on string form, type-aware for goo) plus per-input branch counts. Computed on every solve, committed by `OnWorkerCompleted`, restored from `.gh` files.
- **Debounce only when needed.** Bursty input changes start a debounce timer (minimum 1000 ms, configurable via `SmartHopperSettings.DebounceTime`). A direct `Run?` false→true edge skips debounce and goes straight to `Processing`.
- **Uniform Run-edge semantics.** Every stateful component treats a `Run?` false→true edge as a processing pulse, even when no other input changed. A `Run?` value that stays true across solves is *not* an edge, and result reuse replays unchanged units, so the pulse is cheap — the component visibly cycles `Completed → Processing → Completed` without paying for re-execution.
- **Run-scoped input snapshot.** Entering `Processing` captures the pending input hashes (`ComponentStateManager.CaptureRunSnapshot`); completion commits that snapshot (`CommitRunSnapshot`), not the live pending values. Edits arriving while work is in flight therefore stay pending and retrigger exactly one follow-up run instead of being silently acknowledged — same ownership contract as the batch path, which diffs pending inputs against `GetRunSnapshotHashes` (excluding `Run?`) instead of a second ad-hoc baseline.
- **Persistence is GUID-keyed (V2).** Outputs and hashes are written through `GHPersistenceService.WriteOutputsV2` so renaming output parameters does not corrupt restoration. The legacy `Value_*` / `Type_*` pre-V2 reader and the `PersistenceConstants.EnableLegacyRestore` flag were removed in the ComponentBase deep refactor; pre-V2 alpha files lose persistent outputs on first open.
- **Runtime messages persist by key.** `SetPersistentRuntimeMessage(key, level, message)` accumulates messages that survive solves; `ClearOnePersistentRuntimeMessage` and `ClearPersistentRuntimeMessages` purge them. The store is cleared automatically whenever the state machine enters `NeedsRun` or `Processing`, so diagnostics from a previous run never linger into a new solve; messages produced by the new run are stored and re-applied as usual.
- **Restoration suppression.** During `Read` the state manager enters `BeginRestoration`/`EndRestoration` so the first solve does not misclassify restored hashes as a real input change.

## Key inputs / outputs

- Adds an automatic `Run?` boolean input (`R`, default `false`). Subclasses register their inputs through `RegisterAdditionalInputParams`.
- Subclasses register outputs through `RegisterAdditionalOutputParams`.

## Key members

- `public bool DisableResultCaching { get; set; }` — per-instance opt-out of unit-result reuse, persisted and toggled via the checkable **Cache outputs** context-menu item (shown only on components whose `SupportsResultReuse` is true). While disabled, runs skip the committed-result lookup but still do run-scoped bookkeeping (fragment capture, sentinel resolution, within-run dedup); the store commit is also skipped so the previously committed snapshot survives instead of being wiped.
- `public ComponentState CurrentState => StateManager.CurrentState`.
- `protected virtual ProcessingOptions ComponentProcessingOptions` — defaults to `ItemToItem`, used by `RunProcessingAsync`.
- `protected virtual bool AutoRestorePersistentOutputs => true` — set to `false` if a derived class wants full control of output replay.
- `protected void SetPersistentOutput(string paramName, object value, IGH_DataAccess DA)` — stores the value in `persistentOutputs` and writes it through `DA` if provided.
- `protected T GetPersistentOutput<T>(string paramName, T defaultValue = default)`.
- `protected async Task<...> RunProcessingAsync<T,U>(...)` — wraps `DataTreeProcessor.RunAsync`, wires progress and surfaces tree-processing messages.
- `public override void RequestTaskCancellation()` — also forces `Cancelled` state.

## State transition logic

| Current state | Trigger | New state |
| --- | --- | --- |
| Completed / Waiting / Cancelled / Error | Only `Run?` changed → false | `NeedsRun` (disarmed; a later true edge or input change re-fires) |
| Completed / Waiting / Cancelled / Error | `Run?` false→true edge | `Processing` (unchanged units replay cached results) |
| Completed / Waiting / Cancelled / Error | Other input changed, `Run = false` | debounce → `NeedsRun` |
| Completed / Waiting / Cancelled / Error | Other input changed, `Run = true` | debounce → `Processing` |
| Any | `AIProvider` changed | cancel debounce → `NeedsRun` |
| `NeedsRun` | `Run = true` | `Processing` |
| `Processing` | Worker completed | `Completed` |
| `Processing` | Tasks cancelled | `Cancelled` |

> Run-edge detection is *edge-aware*, not value-aware: `volatile` sources (e.g. `GH_Button`) that do not perturb persistent-data hashes are still detected via the separate `previousRun`/`hasPreviousRun` fields. Restoration initializes the baseline from the persisted value, so a saved `Run=true` document does not produce a phantom edge on reopen; a Run pulse on a `Completed` component recycles through the normal pipeline and unit-result reuse replays unchanged work — "re-run" never means "re-charge".

## Lifecycle hooks

- `BeforeSolveInstance` — guards `Processing` from being reset.
- `SolveInstance` — reads `Run?`, recomputes hashes, dispatches per-state, runs change detection.
- `protected virtual List<string> InputsChanged()` — the change-detection seam: returns `StateManager.GetChangedInputs()` by default; derived bases append synthetic pseudo-inputs (e.g. `AIProvider`, `Selection`) so non-parameter triggers share the same debounce/transition path.
- `OnWorkerCompleted` — commits the captured run snapshot (`CommitRunSnapshot`), cancels debounce, promotes run-time messages, transitions to `Completed` (or `Error` when `RuntimeMessageLevel` is `Error`).

> Message promotion stores into `runtimeMessages` — before the transition — every worker-`CollectMessage`d entry **plus** any live runtime message that bypassed the collected pipeline (legacy workers calling `AddRuntimeMessage` directly), deduplicated by text. Messages are stored without re-applying (`StorePersistentRuntimeMessage`), so nothing duplicates in the current solve; `OnStateError`/`OnStateCompleted` re-apply them after Grasshopper clears runtime messages on the next solve.
- `OnTasksCancelDetected` — transitions `Processing → Cancelled` (override of the `AsyncComponentBase` hook).
- `Write` / `Read` — persist hashes, branch counts and outputs via `GHPersistenceService`.

## Debug menu (DEBUG builds only)

A *Debug* submenu exposes "Force Completed", "Force NeedsRun" and "Reset StateManager" to help diagnose stuck state transitions.

## When to derive

- You need predictable Run/Toggle semantics, debounce, or persistent outputs across save/load.
- You do not need AI provider integration. For AI workflows derive from [AIStatefulAsyncComponentBase](./AIStatefulAsyncComponentBase.md) instead.

## End-User Guide

- [AsyncComponentBase](./AsyncComponentBase.md), [AsyncWorkerBase](./AsyncWorkerBase.md)
- [ComponentStateManager](./ComponentStateManager.md), [ComponentState](./StateManager.md), [ProgressInfo](./ProgressInfo.md)
- [Data tree processing schema](./DataTreeProcessingSchema.md)

## Metadata

- Source Code: See source repository.
- Since Version: 2.0.0
- Last Updated: 2026-07-21
- Documentation Maintainer: Marc Roca Musach

---


## Why Read This?

This document provides details about StatefulComponentBase.


## Developer Reference

Example usage:

`csharp
// Placeholder example
``r

`csharp
// Another placeholder example
``r


## Architecture & Design

Architecture and design notes for StatefulComponentBase.

```csharp
// Example code for Developer Reference
```

```csharp
// Additional example for Developer Reference
```