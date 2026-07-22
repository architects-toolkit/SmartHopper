# SmartHopper Component Base SDK Extraction — Evaluation

**Date:** 2026-07-21  
**Scope:** Evaluate extracting SmartHopper's Grasshopper component bases into two reusable external libraries, following the `SmartHopper.ProviderSdk` precedent (PR #475).  
**Status:** Evaluation only — no implementation requested.

## 1. Context and Goal

The `dev` branch contains a rich, layered hierarchy of Grasshopper component bases under `src/SmartHopper.Core/ComponentBase`. Third-party plugin authors could reuse these bases for their own Rhino/Grasshopper plugins without cloning the full SmartHopper repository. The goal is to mirror the `SmartHopper.ProviderSdk` extraction in PR #475 and define two new SDK packages.

## 2. Provider SDK Precedent (PR #475)

PR #475 extracts a standalone, MIT-licensed `SmartHopper.ProviderSdk` assembly that provider authors can compile against on a clean machine. Key design choices:

- **Target frameworks:** `net7.0;net7.0-windows`
- **License:** MIT (SmartHopper itself stays LGPLv3)
- **Host abstraction:** `ProviderSdkHost` static composition root with replaceable interfaces (`IProviderTrustHost`, `IProviderRegistryHost`, `IPolicyPipelineHost`, `IContextProviderHost`, `IToolRegistryHost`, `IProviderSettingsStore`, `IProviderLogger`, `IProviderHttpClientFactory`, `IProviderDiagnostics`), each with safe null/no-op defaults
- **Version attributes:** `BuiltAgainstSdkAttribute`, `MinHostSdkAttribute`, `SmartHopperProviderSdkVersionAttribute`
- **Discovery:** `SmartHopper.Providers.*.dll` naming convention plus user-local `%AppData%/SmartHopper/Providers`
- **Trust model:** cryptographic (strong-name + Authenticode + SHA-256 manifest)

The same model can be adapted for component-base SDKs.

## 3. Current Component Base Architecture (dev)

Source location: `src/SmartHopper.Core/ComponentBase` (note: the auto-generated docs incorrectly list `src/SmartHopper.Core.Grasshopper/ComponentBase`).

Hierarchy (from `docs/Components/ComponentBase/index.md`):

```text
GH_Component
├── AsyncComponentBase
│   └── StatefulComponentBase
│       ├── AIProviderComponentBase
│       │   └── AIStatefulAsyncComponentBase (8 partial files)
│       │       ├── AISelectingStatefulAsyncComponentBase
│       │       └── AIOutputAdapterBase
│       └── SelectingStatefulComponentBase
├── ProviderComponentBase
├── SelectingComponentBase
└── AIInputAdapterBase
```

Key helpers: `AsyncWorkerBase`, `ComponentStateManager`, `GHPersistenceService`, `DataTreeProcessor`, `SelectingComponentCore`, `ProviderSelectionCore`, `AIRequestParametersGooParser`, `BatchSentinel`, `WellKnownInputs`, `PersistenceKeys`, `ProgressInfo`, `StateManager`, `ComponentState`, plus attribute/render classes.

## 4. Proposed External Libraries

### 4.1 `SmartHopper.ComponentBase` — Async / Stateful / Selecting

**What it is:** A Grasshopper-specific SDK containing the generic async/stateful/selecting component machinery, with no AI provider coupling.

**Contents:**
- `AsyncComponentBase`, `AsyncWorkerBase`
- `StatefulComponentBase`
- `ComponentStateManager`, `StateManager`, `ProgressInfo`
- `GHPersistenceService`, `WellKnownInputs`, `PersistenceKeys`
- `DataTreeProcessor` and `ProcessingOptions`
- `SelectingComponentBase`, `SelectingStatefulComponentBase`
- `ISelectingComponent`, `SelectingComponentCore`
- `SelectingComponentAttributes`, `SelectingButtonBehavior`, `InlineLabelRenderer`

**Dependencies:**
- `Grasshopper` / `RhinoCommon` (McNeel NuGet)
- `System.Drawing.Common`, `System.Windows.Forms` (macOS reference assemblies)
- `Newtonsoft.Json` (already used by Grasshopper wrappers)
- No AI, no `ProviderManager`, no `SmartHopperSettings`

**What stays host-side:**
- Concrete SmartHopper components (`SmartHopper.Components`)
- AI request logic, provider manager, settings
- GhJSON converters and document processing (`SmartHopper.Core.Grasshopper`)

**Feasibility:** High. These classes are already decoupled from AI concerns (`DataTreeProcessor` is a generic `GH_Structure<T>` utility, `ComponentStateManager` only tracks state/hash/debounce, `SelectingComponentCore` only touches the Grasshopper canvas). The main work is moving files, adjusting namespaces, adding public API surface/docs, and strong-naming.

### 4.2 `SmartHopper.ComponentBase.AI` — Provider / AI / Adapters

**What it is:** A higher-level SDK that sits on top of `SmartHopper.ComponentBase` and `SmartHopper.ProviderSdk`, adding provider selection, AI request orchestration, and input/output adapters.

**Contents:**
- `ProviderComponentBase`
- `AIProviderComponentBase`
- `AIStatefulAsyncComponentBase` (8 partial files)
- `AISelectingStatefulAsyncComponentBase`
- `AIInputAdapterBase`, `AIOutputAdapterBase`
- `AIProviderComponentAttributes`, `AISelectingComponentAttributes`, `ComponentBadgesAttributes`
- `ProviderSelectionCore` (refactored, see below)
- `AIRequestParametersGooParser`, `BatchSentinel`
- Component-specific model types: `AIInputPayload`, `GH_AIInputPayload`, `AIInputPayloadParameter`, `OutputMapping`

**Dependencies:**
- `SmartHopper.ComponentBase`
- `SmartHopper.ProviderSdk` (for `AIRequestCall`, `AIReturn`, `AIToolCall`, `AICapability`, `AIRequestParameters`, `IAIProvider`, `AIProvider`, `AIModel`, etc.)
- `Grasshopper` / `RhinoCommon` (for canvas UI and attributes)
- `Newtonsoft.Json`, `JsonSchema.Net`, `System.Text.Json`

**What stays host-side:**
- `ProviderManager` (provider discovery/loading)
- `AIToolManager` (tool registration and execution)
- `SmartHopperSettings` (global settings)
- `PolicyPipeline`, `AIContextManager`, `ConversationSession`, `WebChat`, badge cache refresh provider

**Feasibility:** Medium. The base itself is currently tightly coupled to three host singletons:
- `ProviderManager.Instance` (provider resolution)
- `AIToolManager` (tool discovery/execution)
- `SmartHopperSettings.Instance` (default provider)

`ProviderSelectionCore` already encapsulates `ProviderManager` and `SmartHopperSettings`; it would need to be converted to consume host abstractions. `AIStatefulAsyncComponentBase.AI.cs`/`Batch.cs` would need an `IAIToolInvoker` abstraction for `CallAIToolAsync`.

## 5. Cross-Cutting Concerns

### 5.1 Host Abstractions (`ComponentBaseAIHost`)

Following `ProviderSdkHost`, introduce a static composition root for the AI component base SDK:

```csharp
public static class ComponentBaseAIHost
{
    public static IProviderResolver ProviderResolver { get; set; } = new NullProviderResolver();
    public static ISettingsAccessor Settings { get; set; } = new NullSettingsAccessor();
    public static IAIToolInvoker ToolInvoker { get; set; } = new NullAIToolInvoker();
    public static IBatchService BatchService { get; set; } = new NullBatchService();
    public static IModelBadgeResolver ModelBadgeResolver { get; set; } = new NullModelBadgeResolver();
}
```

The SmartHopper host sets concrete implementations at plugin startup.

### 5.2 Dependency on `SmartHopper.ProviderSdk`

On `dev`, `AIRequestCall`, `AIReturn`, `AIToolCall`, `AICapability`, and `AIRequestParameters` still live in `SmartHopper.Infrastructure` because PR #475 has not been merged and currently has merge conflicts. **The AI component base SDK cannot be cleanly extracted until PR #475 is resolved and the AICall.Core DTOs are available in `SmartHopper.ProviderSdk`.**

### 5.3 Licensing and Packaging

- **License:** MIT for both SDK assemblies (same rationale as `SmartHopper.ProviderSdk`: let closed-source plugin authors link without LGPL copyleft).
- **Target frameworks:** `net7.0;net7.0-windows`
- **Strong-name, XML docs, Source Link, and NuGet packages** (`SmartHopper.ComponentBase` and `SmartHopper.ComponentBase.AI`) with package IDs matching assembly names.

### 5.4 Versioning

Introduce `SmartHopperComponentBaseSdkVersionAttribute` and `BuiltAgainstComponentBaseSdkAttribute` so the SmartHopper host can reject plugins built against an incompatible SDK major version, matching the ProviderSdk `BuiltAgainstSdk`/`MinHostSdk` pattern.

## 6. Implementation Phases

| Phase | Work | Risk | Depends On |
|---|---|---|---|
| 0 | Resolve/merge PR #475; move `AIRequestParameters` and `AICall.Core` DTOs to `SmartHopper.ProviderSdk` | Medium | PR #475 |
| 1 | Create `SmartHopper.ComponentBase`; move async/stateful/selecting bases + helpers; update `SmartHopper.Core` references | Low | None |
| 2 | Define `ComponentBaseAIHost` and interfaces; refactor `ProviderSelectionCore` and `AIStatefulAsyncComponentBase` off singletons | Medium | Phase 1 |
| 3 | Create `SmartHopper.ComponentBase.AI`; move AI bases, adapters, attributes, and payload/model types | Medium | Phases 0–2 |
| 4 | Add strong naming, NuGet metadata, samples, and `BuiltAgainstComponentBaseSdk` attributes | Low | Phase 3 |
| 5 | Update `SmartHopper.Core`, `SmartHopper.Core.Grasshopper`, and `SmartHopper.Components` to reference the new packages | Medium | Phase 4 |

## 7. Risks and Open Questions

- **Grasshopper coupling:** Unlike `ProviderSdk`, these SDKs are inherently Grasshopper/Rhino-specific. That is still valuable (plugin authors need Grasshopper anyway), but the package is not a pure .NET SDK.
- **Host singletons:** `AIStatefulAsyncComponentBase` currently reaches for `ProviderManager`, `AIToolManager`, and `SmartHopperSettings`. Refactoring to `ComponentBaseAIHost` is straightforward but touches many call sites across partial files.
- **`AIOutputAdapterBase` complexity:** At 37 KB, `AIOutputAdapterBase` is a large specialized subclass; it belongs in the AI SDK but may carry `JsonSchema`/`GhJSON` output conversion logic that needs review.
- **Payload type ownership:** `AIInputPayload`, `GH_AIInputPayload`, and `AIInputPayloadParameter` are shared between input adapters, output adapters, and provider execution. They likely belong in `SmartHopper.ComponentBase.AI`, but some interaction types (`AIInteractionAudio`) currently live in `SmartHopper.Infrastructure` and may need to move to `SmartHopper.ProviderSdk` or the AI base SDK.
- **Namespace/location drift:** The auto-generated docs say the source is in `SmartHopper.Core.Grasshopper/ComponentBase`, but the actual code is in `SmartHopper.Core/ComponentBase`. Per `.devin/rules/solution-structure.md`, component bases belong in `SmartHopper.Core`, so the code is correct and the docs should be aligned, not the other way around.

## 8. Recommendation

1. **Proceed with `SmartHopper.ComponentBase` first.** It has a clean boundary, high reuse value, and low risk. It does not depend on PR #475.
2. **Defer `SmartHopper.ComponentBase.AI` until PR #475 lands** and the AICall.Core DTOs are in `SmartHopper.ProviderSdk`; use the intervening time to design `ComponentBaseAIHost` and migrate the three singleton dependencies.
3. **Run a pilot:** before publishing NuGets, build a minimal external plugin that derives from `StatefulComponentBase` and `AIStatefulAsyncComponentBase` to prove the public API is usable without a SmartHopper clone.
4. **Keep both SDKs MIT-licensed**, strongly named, and source-linked, following the `SmartHopper.ProviderSdk` packaging conventions.

---

*Prepared for review before implementation.*
