# ComponentBase

The layered component base hierarchy that lets a Grasshopper component opt into async execution, state management, AI provider selection, canvas object selection, and AI request orchestration.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Core.Grasshopper/ComponentBase/` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

Every Grasshopper component in SmartHopper inherits from one of these base classes. Understanding the hierarchy tells you which features are available at each level and helps you choose the right base class when building new components.

**You should read this if you:**

- Are building a new SmartHopper component and need to choose a base class
- Want to understand how state management, provider selection, or async execution work
- Need to trace where a specific behavior (persistence, debounce, metrics) comes from

---

## End-User Guide

### What Is This?

When you use a SmartHopper component in Grasshopper, its behavior -- async execution, provider selection menus, the Run button, metrics output -- comes from the base classes documented here. You don't interact with these bases directly, but they determine what each component can do.

### Component Capabilities by Type

| If a component... | It inherits from |
| --- | --- |
| Runs asynchronously | `AsyncComponentBase` |
| Has a state indicator (Ready, Processing...) | `StatefulComponentBase` |
| Shows a provider selection menu | `AIProviderComponentBase` |
| Has a Select button on the canvas | `SelectingStatefulComponentBase` |
| Calls an AI model | `AIStatefulAsyncComponentBase` |
| Is an input adapter (Text2AI, Img2AI...) | `AIInputAdapterBase` |
| Is an output adapter (AI2Text, AI2Number...) | `AIOutputAdapterBase` |

<!-- PLACEHOLDER: Diagram showing the component hierarchy as a visual tree -->

---

## Developer Reference

### Hierarchy

```text
GH_Component
├── AsyncComponentBase                  -- async lifecycle (workers, tasks, cancellation)
│   └── StatefulComponentBase           -- + state machine, persistence, debounce, run/toggle
│       ├── AIProviderComponentBase     -- + AI provider selection menu and persistence
│       │   └── AIStatefulAsyncComponentBase  (partial, 8 files)
│       │       ├── AISelectingStatefulAsyncComponentBase  -- + canvas Select button
│       │       └── AIOutputAdapterBase -- AIInputPayload → AIReturn → typed outputs
│       └── SelectingStatefulComponentBase  -- + canvas Select button (no AI)
├── ProviderComponentBase               -- non-async AI provider component
├── SelectingComponentBase              -- non-async, non-stateful Select button base
└── AIInputAdapterBase                  -- non-AI sync component producing AIInputPayload

```

`AsyncWorkerBase` is the worker abstraction used by every async component to run compute off the UI thread.

### Class Documentation

**Lifecycle and state:**

- [AsyncComponentBase](./AsyncComponentBase.md) -- two-phase async lifecycle
- [AsyncWorkerBase](./AsyncWorkerBase.md) -- worker contract
- [StatefulComponentBase](./StatefulComponentBase.md) -- state machine, debounce, persistence
- [ComponentStateManager](./ComponentStateManager.md) -- centralized state and hash tracking
- [ComponentState / StateManager](./StateManager.md) -- state enum and friendly messages
- [ProgressInfo](./ProgressInfo.md) -- progress payload

**Selection:**

- [SelectingComponentBase](./SelectingComponentBase.md) -- Select button on a plain `GH_Component`
- [SelectingStatefulComponentBase](./SelectingStatefulComponentBase.md) -- Select button on a stateful component

**AI provider:**

- [AIProviderComponentBase](./AIProviderComponentBase.md) -- stateful + provider selection
- [ProviderComponentBase](./ProviderComponentBase.md) -- non-async provider selection
- [ProviderSelectionCore](./ProviderSelectionCore.md) -- instance-owned provider state with `ProviderChanged` event, idempotent commit, menu/persistence wiring
- [AIStatefulAsyncComponentBase](./AIStatefulAsyncComponentBase.md) -- core AI component base
- [AISelectingStatefulAsyncComponentBase](./AISelectingStatefulAsyncComponentBase.md) -- AI + canvas selection

**Shared cores and constants** (introduced in the deep refactor):

- `SelectingButtonBehavior` (`internal`) -- mouse/hover/render state shared by selecting component attributes
- `WellKnownInputs` -- constants for canonical input/output parameter names (`AIProvider`, `Run?`, `Settings`, `Metrics`, ...)
- `PersistenceKeys` (`internal`) -- central registry of every GH file key written by these bases
- `AIRequestParametersGooParser` -- `TryFromGoo(IGH_Goo, out AIRequestParameters)` for the `Settings` input wire

**AI input/output adapters:**

- [AIInputAdapterBase](./AIInputAdapterBase.md) -- synchronous adapters that build `AIInputPayload`
- [AIOutputAdapterBase](./AIOutputAdapterBase.md) -- AI components driven by `AIInputPayload` trees

**Batch helpers:**

- [BatchSentinel](./BatchSentinel.md) -- `##SH_BATCH:{customId}##` placeholder protocol

**Data tree processing:**

- [Data tree processing schema](./DataTreeProcessingSchema.md)
- [Flat-tree broadcasting](./FlatTreeBroadcasting.md)

### Choosing a Base Class

```text
Do you need async execution?
├── No → GH_Component (or AIInputAdapterBase for input adapters)
└── Yes →
    Do you need state management (persistence, debounce)?
    ├── No → AsyncComponentBase
    └── Yes →
        Do you need AI provider selection?
        ├── No → StatefulComponentBase
        └── Yes →
            Do you need AI call orchestration?
            ├── No → AIProviderComponentBase
            └── Yes → AIStatefulAsyncComponentBase
                      (or AIOutputAdapterBase for output adapters)

```

### Code Example: Minimal AI Component

```csharp
public class MyAIComponent : AIStatefulAsyncComponentBase
{
    public override AICapability RequiredCapability => AICapability.Text2Text;

    protected override async Task<AIReturn> ExecuteAIAsync(
        AIBody body, CancellationToken ct)
    {
        // Provider and model are already resolved
        return await Provider.Call(body, ct);
    }
}

```

### Code Example: Minimal Stateful Component

```csharp
public class MyStatefulComponent : StatefulComponentBase
{
    public MyStatefulComponent()
        : base("MyStateful", "Stateful", "Persists outputs and debounces", "SmartHopper", "Utils") { }

    protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Data", "D", "Data to process", GH_ParamAccess.item);
    }

    protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddTextParameter("Result", "R", "Processed result", GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess DA)
    {
        if (CurrentState == ComponentState.Processing)
        {
            string data = null;
            if (!DA.GetData(0, ref data)) return;

            SetPersistentOutput("Result", data.Trim(), DA);
        }
    }
}

```

---

## Architecture & Design

### Design Criteria

1. **Single-responsibility layers.** Each base adds exactly one orthogonal concern (async, state, provider, selection, adapter shape).
2. **Inherit upward, never sideways.** Shared logic lives in `Core` helpers (`SelectingComponentCore`, `ProviderSelectionCore`), not duplicated between bases.
3. **UI calls go through Rhino's UI thread.** Workers must never touch GH/Rhino UI directly; use `Rhino.RhinoApp.InvokeOnUiThread`.
4. **Outputs are persisted, not recomputed.** `StatefulComponentBase` writes outputs through `GHPersistenceService` so saved files restore without re-running.
5. **Single finalization point.** AI components emit outputs and metrics atomically through `FinishResults<T>`; both batch and non-batch paths converge there.
6. **Capability-aware model selection.** AI components declare `RequiredCapability` (and `UsingAiTools`) so the model badge, validation, and provider fallback logic stay correct.

### Why a Deep Hierarchy?

The alternative -- a flat set of base classes with feature mixins -- was considered but rejected because:

- C# lacks multiple inheritance, so mixins would require extensive interface delegation
- The layered approach matches the natural progression of component complexity
- Each layer can be tested independently
- Most components only need the bottom 2-3 layers

### Related Documentation

- [Input Components](../Input/index.md) -- uses `AIInputAdapterBase`
- [Output Components](../Output/index.md) -- uses `AIOutputAdapterBase`
- [AI Components](../AI/index.md) -- uses `AIStatefulAsyncComponentBase`
- [Design Decisions](../../DESIGN_DECISIONS/index.md) -- rationale for the layered hierarchy
