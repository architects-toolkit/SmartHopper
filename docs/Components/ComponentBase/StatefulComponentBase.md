# StatefulComponentBase

`src/SmartHopper.Core/ComponentBase/StatefulComponentBase.cs`

Async base that adds a state machine, input-change detection, debounce, persistent outputs and persistent runtime messages on top of [AsyncComponentBase](./AsyncComponentBase.md). Delegates state to a [ComponentStateManager](./ComponentStateManager.md) and persistence to `GHPersistenceService`.

---

Make long-running components behave predictably across button/toggle Run inputs, bursty input changes and document save/load. Outputs survive a save/load cycle without re-running.

## Design criteria

- **Centralized state machine.** All transitions go through `ComponentStateManager.RequestTransition`. Per-state handlers (`OnStateCompleted`, `OnStateProcessing`, â€¦) live on the base.
- **Hash-based input change detection.** Stable, deterministic hashes for each input parameter (FNV-1a on string form, type-aware for goo) plus per-input branch counts. Computed on every solve, committed by `OnWorkerCompleted`, restored from `.gh` files.
- **Debounce only when needed.** Bursty input changes start a debounce timer (minimum 1000 ms, configurable via `SmartHopperSettings.DebounceTime`). A direct Run = true pulse on a `RunOnlyOnInputChanges = false` component skips debounce and goes straight to `Processing`.
- **Persistence is GUID-keyed (V2).** Outputs and hashes are written through `GHPersistenceService.WriteOutputsV2` so renaming output parameters does not corrupt restoration. The legacy `Value_*` / `Type_*` pre-V2 reader and the `PersistenceConstants.EnableLegacyRestore` flag were removed in the ComponentBase deep refactor; pre-V2 alpha files lose persistent outputs on first open.
- **Runtime messages persist by key.** `SetPersistentRuntimeMessage(key, level, message)` accumulates messages that survive solves; `ClearOnePersistentRuntimeMessage` and `ClearPersistentRuntimeMessages` purge them.
- **Restoration suppression.** During `Read` the state manager enters `BeginRestoration`/`EndRestoration` so the first solve does not misclassify restored hashes as a real input change.

## Key inputs / outputs

- Adds an automatic `Run?` boolean input (`R`, default `false`). Subclasses register their inputs through `RegisterAdditionalInputParams`.
- Subclasses register outputs through `RegisterAdditionalOutputParams`.

## Key members

- `public bool RunOnlyOnInputChanges { get; set; } = true` â€” when `false`, any Run = true edge transitions to `Processing` even if no input data changed (button-pulse semantics).
- `public ComponentState CurrentState => StateManager.CurrentState`.
- `protected virtual ProcessingOptions ComponentProcessingOptions` â€” defaults to `ItemToItem`, used by `RunProcessingAsync`.
- `protected virtual bool AutoRestorePersistentOutputs => true` â€” set to `false` if a derived class wants full control of output replay.
- `protected void SetPersistentOutput(string paramName, object value, IGH_DataAccess DA)` â€” stores the value in `persistentOutputs` and writes it through `DA` if provided.
- `protected T GetPersistentOutput<T>(string paramName, T defaultValue = default)`.
- `protected async Task<...> RunProcessingAsync<T,U>(...)` â€” wraps `DataTreeProcessor.RunAsync`, wires progress and surfaces tree-processing messages.
- `public override void RequestTaskCancellation()` â€” also forces `Cancelled` state.

## State transition logic

| Current state | Trigger | New state |
| --- | --- | --- |
| Completed / Waiting / Cancelled / Error | Only `Run?` changed â†’ false | stay |
| Completed / Waiting / Cancelled / Error | Only `Run?` changed â†’ true, `RunOnlyOnInputChanges = true` | `Waiting` |
| Completed / Waiting / Cancelled / Error | Only `Run?` changed â†’ true, `RunOnlyOnInputChanges = false` | `Processing` |
| Completed / Waiting / Cancelled / Error | Other input changed, `Run = false` | debounce â†’ `NeedsRun` |
| Completed / Waiting / Cancelled / Error | Other input changed, `Run = true` | debounce â†’ `Processing` |
| Any | `AIProvider` changed | cancel debounce â†’ `NeedsRun` |
| `NeedsRun` | `Run = true` | `Processing` |
| `Processing` | Worker completed | `Completed` |
| `Processing` | Tasks cancelled | `Cancelled` |

> The behaviour for `RunOnlyOnInputChanges = false` is *Run-edge-aware*: `volatile` data sources (e.g. `GH_Button`) that do not perturb persistent-data hashes are still detected via a separate `previousRun` field.

## Lifecycle hooks

- `BeforeSolveInstance` â€” guards `Processing` from being reset.
- `SolveInstance` â€” reads `Run?`, recomputes hashes, dispatches per-state, runs change detection.
- `OnWorkerCompleted` â€” commits hashes, cancels debounce, transitions to `Completed`.
- `OnTasksCancelDetected` â€” transitions `Processing â†’ Cancelled` (override of the `AsyncComponentBase` hook).
- `Write` / `Read` â€” persist hashes, branch counts and outputs via `GHPersistenceService`.

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