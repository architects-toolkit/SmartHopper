# ComponentBase

Component base classes located in `src/SmartHopper.Core/ComponentBase/`. Together they form a layered hierarchy that lets a component opt into async execution, a state machine, AI provider selection, on-canvas object selection and AI request orchestration.

## Hierarchy

```text
GH_Component
â”œâ”€â”€ AsyncComponentBase                  â† async lifecycle (workers, tasks, cancellation)
â”‚   â””â”€â”€ StatefulComponentBase           â† + state machine, persistence, debounce, run/toggle
â”‚       â”œâ”€â”€ AIProviderComponentBase     â† + AI provider selection menu and persistence
â”‚       â”‚   â””â”€â”€ AIStatefulAsyncComponentBase  (partial, 7 files)
â”‚       â”‚       â”œâ”€â”€ AISelectingStatefulAsyncComponentBase  â† + canvas Select button
â”‚       â”‚       â””â”€â”€ AIOutputAdapterBase â† AIInputPayload â†’ AIReturn â†’ typed outputs
â”‚       â””â”€â”€ SelectingStatefulComponentBase  â† + canvas Select button (no AI)
â”œâ”€â”€ ProviderComponentBase               â† non-async AI provider component
â”œâ”€â”€ SelectingComponentBase              â† non-async, non-stateful Select button base
â””â”€â”€ AIInputAdapterBase                  â† non-AI sync component producing AIInputPayload
```

`AsyncWorkerBase` is the worker abstraction used by every async component to run compute off the UI thread.

## Files

- **Lifecycle and state**
  - [AsyncComponentBase](./AsyncComponentBase.md) â€“ two-phase async lifecycle.
  - [AsyncWorkerBase](./AsyncWorkerBase.md) â€“ worker contract.
  - [StatefulComponentBase](./StatefulComponentBase.md) â€“ state machine, debounce, persistence.
  - [ComponentStateManager](./ComponentStateManager.md) â€“ centralized state and hash tracking.
  - [ComponentState / StateManager](./StateManager.md) â€“ state enum and friendly messages.
  - [ProgressInfo](./ProgressInfo.md) â€“ progress payload.
- **Selection**
  - [SelectingComponentBase](./SelectingComponentBase.md) â€“ Select button on a plain `GH_Component`.
  - [SelectingStatefulComponentBase](./SelectingStatefulComponentBase.md) â€“ Select button on a stateful component.
- **AI provider**
  - [AIProviderComponentBase](./AIProviderComponentBase.md) â€“ stateful + provider selection.
  - [ProviderComponentBase](./ProviderComponentBase.md) â€“ non-async provider selection.
  - [ProviderComponentHelper](./ProviderComponentHelper.md) â€“ legacy static helper, superseded by `ProviderSelectionCore`.
  - [AIStatefulAsyncComponentBase](./AIStatefulAsyncComponentBase.md) â€“ core AI component base.
  - [AISelectingStatefulAsyncComponentBase](./AISelectingStatefulAsyncComponentBase.md) â€“ AI + canvas selection.
- **Shared cores & constants** (introduced in the deep refactor)
  - `ProviderSelectionCore` â€“ instance-owned provider state with `ProviderChanged` event, idempotent commit, menu/persistence wiring. Replaces `ProviderComponentHelper`.
  - `SelectingButtonBehavior` (`internal`) â€“ mouse/hover/render state shared by `SelectingComponentAttributes` and `AISelectingComponentAttributes`.
  - `WellKnownInputs` â€“ constants for canonical input/output parameter names (`AIProvider`, `Run?`, `Settings`, `Metrics`, â€¦).
  - `PersistenceKeys` (`internal`) â€“ central registry of every GH file key written by these bases.
  - `AIRequestParametersGooParser` â€“ `TryFromGoo(IGH_Goo, out AIRequestParameters)`; single source of truth for the `Settings` input wire conversion.
- **AI input/output adapters**
  - [AIInputAdapterBase](./AIInputAdapterBase.md) â€“ synchronous adapters that build `AIInputPayload`.
  - [AIOutputAdapterBase](./AIOutputAdapterBase.md) â€“ AI components driven by `AIInputPayload` trees.
- **Batch helpers**
  - [BatchSentinel](./BatchSentinel.md) â€“ `##SH_BATCH:{customId}##` placeholder protocol.
- **Data tree processing**
  - [Data tree processing schema](./DataTreeProcessingSchema.md)
  - [Flat-tree broadcasting](./FlatTreeBroadcasting.md)

## Design criteria

1. **Single-responsibility layers.** Each base adds exactly one orthogonal concern (async, state, provider, selection, adapter shape).
2. **Inherit upward, never sideways.** Do not duplicate logic between selecting and AI bases â€” use the shared `SelectingComponentCore` helper. Do not duplicate logic between provider components and AI bases - use the shared `ProviderComponentHelper`.
3. **UI calls go through Rhino's UI thread.** Workers must never touch GH/Rhino UI directly; use `Rhino.RhinoApp.InvokeOnUiThread`.
4. **Outputs are persisted, not recomputed.** `StatefulComponentBase` writes outputs through `GHPersistenceService` so saved files restore without re-running.
5. **Single finalization point.** AI components emit outputs and metrics atomically through `FinishResults<T>`; both batch and non-batch paths converge there.
6. **Capability-aware model selection.** AI components declare `RequiredCapability` (and `UsingAiTools`) so the model badge, validation and provider fallback logic stay correct.

## Metadata

- Source Code: See source repository.
- Since Version: 2.0.0
- Last Updated: 2026-07-21
- Documentation Maintainer: Marc Roca Musach

---


## Why Read This?

This document provides details about index.


## End-User Guide

End-user guidance for index.


## Developer Reference

Example usage:

`csharp
// Placeholder example
``r

`csharp
// Another placeholder example
``r


## Architecture & Design

Architecture and design notes for index.

```csharp
// Example code for Developer Reference
```

```csharp
// Additional example for Developer Reference
```