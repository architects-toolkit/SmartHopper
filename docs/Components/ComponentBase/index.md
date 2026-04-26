# ComponentBase

Component base classes located in `src/SmartHopper.Core/ComponentBase/`. Together they form a layered hierarchy that lets a component opt into async execution, a state machine, AI provider selection, on-canvas object selection and AI request orchestration.

## Hierarchy

```text
GH_Component
├── AsyncComponentBase                  ← async lifecycle (workers, tasks, cancellation)
│   └── StatefulComponentBase           ← + state machine, persistence, debounce, run/toggle
│       ├── AIProviderComponentBase     ← + AI provider selection menu and persistence
│       │   └── AIStatefulAsyncComponentBase  (partial, 7 files)
│       │       ├── AISelectingStatefulAsyncComponentBase  ← + canvas Select button
│       │       └── AIOutputAdapterBase ← AIInputPayload → AIReturn → typed outputs
│       └── SelectingStatefulComponentBase  ← + canvas Select button (no AI)
├── ProviderComponentBase               ← non-async AI provider component
├── SelectingComponentBase              ← non-async, non-stateful Select button base
└── AIInputAdapterBase                  ← non-AI sync component producing AIInputPayload
```

`AsyncWorkerBase` is the worker abstraction used by every async component to run compute off the UI thread.

## Files

- **Lifecycle and state**
  - [AsyncComponentBase](./AsyncComponentBase.md) – two-phase async lifecycle.
  - [AsyncWorkerBase](./AsyncWorkerBase.md) – worker contract.
  - [StatefulComponentBase](./StatefulComponentBase.md) – state machine, debounce, persistence.
  - [ComponentStateManager](./ComponentStateManager.md) – centralized state and hash tracking.
  - [ComponentState / StateManager](./StateManager.md) – state enum and friendly messages.
  - [ProgressInfo](./ProgressInfo.md) – progress payload.
- **Selection**
  - [SelectingComponentBase](./SelectingComponentBase.md) – Select button on a plain `GH_Component`.
  - [SelectingStatefulComponentBase](./SelectingStatefulComponentBase.md) – Select button on a stateful component.
- **AI provider**
  - [AIProviderComponentBase](./AIProviderComponentBase.md) – stateful + provider selection.
  - [ProviderComponentBase](./ProviderComponentBase.md) – non-async provider selection.
  - [ProviderComponentHelper](./ProviderComponentHelper.md) – shared menu / default-resolution / serialization helper.
  - [AIStatefulAsyncComponentBase](./AIStatefulAsyncComponentBase.md) – core AI component base.
  - [AISelectingStatefulAsyncComponentBase](./AISelectingStatefulAsyncComponentBase.md) – AI + canvas selection.
- **AI input/output adapters**
  - [AIInputAdapterBase](./AIInputAdapterBase.md) – synchronous adapters that build `AIInputPayload`.
  - [AIOutputAdapterBase](./AIOutputAdapterBase.md) – AI components driven by `AIInputPayload` trees.
- **Batch helpers**
  - [BatchSentinel](./BatchSentinel.md) – `##SH_BATCH:{customId}##` placeholder protocol.
- **Data tree processing**
  - [Data tree processing schema](./DataTreeProcessingSchema.md)
  - [Flat-tree broadcasting](./FlatTreeBroadcasting.md)

## Design criteria

1. **Single-responsibility layers.** Each base adds exactly one orthogonal concern (async, state, provider, selection, adapter shape).
2. **Inherit upward, never sideways.** Do not duplicate logic between selecting and AI bases — use the shared `SelectingComponentCore` helper. Do not duplicate logic between provider components and AI bases - use the shared `ProviderComponentHelper`.
3. **UI calls go through Rhino's UI thread.** Workers must never touch GH/Rhino UI directly; use `Rhino.RhinoApp.InvokeOnUiThread`.
4. **Outputs are persisted, not recomputed.** `StatefulComponentBase` writes outputs through `GHPersistenceService` so saved files restore without re-running.
5. **Single finalization point.** AI components emit outputs and metrics atomically through `FinishResults<T>`; both batch and non-batch paths converge there.
6. **Capability-aware model selection.** AI components declare `RequiredCapability` (and `UsingAiTools`) so the model badge, validation and provider fallback logic stay correct.
