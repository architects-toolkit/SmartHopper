# SmartHopper Conceptual Quality Review

**Date**: August 28, 2026  
**Purpose**: Analyze the conceptual quality of the SmartHopper codebase from a senior staff engineer / software architect perspective  
**Related Issue**: N/A  
**Severity**: MEDIUM

---

## Executive Summary

- SmartHopper has a sound layered architecture: `SmartHopper.ProviderSdk` is a self-contained, MIT-licensed contract for third-party providers; `SmartHopper.Infrastructure` owns managers, settings, and the call pipeline; `SmartHopper.Core`/`SmartHopper.Core.Grasshopper` build the Grasshopper component hierarchy; and every first-party provider depends only on `SmartHopper.ProviderSdk`.
- The request/response domain model (`AIBody`, `AIRequestCall`, `AIReturn`, `AICapability`) is well-named, mostly immutable, and supported by a clear `PolicyPipeline` and `ConversationSession` lifecycle.
- **Duplication is the dominant maintenance risk.** Model-selection logic is now consolidated in `AIModelCapabilityRegistry`, and JSON schema wrapping, API-key access, icon loading, and `MaxTokens`/`Temperature` validation have been moved to the `AIProvider`/`AIProviderSettings` base classes. Remaining duplication includes OpenAI-compatible role mapping, hardcoded model capability lists in every provider, and the macOS WinForms `net48` workaround repeated in ~18 `.csproj` files.
- The component base hierarchy is deep and accumulating concerns. `AIStatefulAsyncComponentBase` is split across eight partial files and directly depends on `SmartHopper.Infrastructure` managers (`AIToolManager`, `AIModels`), indicating the base is doing too much.
- Security concepts are strong but rely on validation warnings rather than enforcement. In `Soft` mode an unverified provider still executes, `DEBUG` builds bypass strict integrity checks, and secret-encryption failures are silent.

**Single highest-value fix (now mostly implemented):** Consolidate model capability selection into one source of truth (`AIModelCapabilityRegistry` in the Provider SDK), remove or repurpose the redundant `ModelManager`, and expose shared provider-agnostic helpers (`BaseJsonSchemaAdapter`, `AIProvider.GetApiKey`/`LoadIcon`, validation utilities) so the per-provider projects shrink and cannot diverge.

## Remediation status

- `ModelManager` has been removed and `AIModelCapabilityRegistry.Instance` is now the single source of truth for model selection, defaults, and streaming validation. Unique test coverage was ported to `AIModelCapabilityRegistryTests`.
- `OpenAIJsonSchemaAdapter`, `MistralAIJsonSchemaAdapter`, `LocalAIJsonSchemaAdapter`, and `OllamaJsonSchemaAdapter` have been removed. `OpenAICompatibleJsonSchemaAdapter` in `SmartHopper.ProviderSdk.AICall.JsonSchemas` now owns the shared object-root wrapping logic. `DeepSeekJsonSchemaAdapter` inherits from it and overrides only the provider-specific `Unwrap` behavior.
- `AIBody.InteractionsNew` is now `IReadOnlyList<int>`, `ResetNew()` has been removed, and `AIBody` is constructed through `AIBodyBuilder`.
- `AIInteractionBase`/`IAIInteraction` expose `init`-only properties and provide `With...` helpers; streaming aggregation uses nested `Builder` classes.
- Common provider helpers have been moved to base classes: `AIProvider.GetApiKey()`, `AIProvider.LoadIconFromResources()`, and `AIProviderSettings.ValidateMaxTokens()`/`ValidateTemperature()`. All first-party providers use them.
- A shared `ProviderTestComponentBase` has been extracted in `SmartHopper.Components.Test` so the ~43 per-provider test runners share common setup/teardown while remaining independent runners.

---

## Implementation Status Update

*Verified on 2026-09-01.*

The following review suggestions have been implemented since the review was written (or were already implemented but not marked as complete in the original document):

### Completed

| # | Item | Evidence |
| --- | --- | --- |
| 1 | `ModelManager` removed; `AIModelCapabilityRegistry.Instance` is the single source of truth | No `ModelManager` class or references in `src/`; `AIModelCapabilityRegistry` is used by `ModalityFallbackResolver`, validators, and session code; tests live in `src/SmartHopper.ProviderSdk.Tests/AIModels/AIModelCapabilityRegistryTests.cs` |
| 2 | Shared `OpenAICompatibleJsonSchemaAdapter` in Provider SDK | `src/SmartHopper.ProviderSdk/AICall/JsonSchemas/OpenAICompatibleJsonSchemaAdapter.cs` exists; OpenAI/Mistral/Local/Ollama providers use it; only `DeepSeekJsonSchemaAdapter` remains for provider-specific `Unwrap` |
| 3 | Common provider helpers moved to base classes (`AIProvider.GetApiKey`, `AIProvider.LoadIconFromResources`, `AIProviderSettings.ValidateMaxTokens`/`ValidateTemperature`) | `AIProvider.cs` (lines 563, 573, 600); `AIProviderSettings.cs` (lines 103, 136); all providers now call `this.GetApiKey()` and `this.LoadIconFromResources(...)`; settings classes call base `ValidateMaxTokens`/`ValidateTemperature` |
| 4 | Shared test-harness base for `SmartHopper.Components.Test` | `ProviderTestComponentBase` at `src/SmartHopper.Components.Test/Providers/ProviderTestComponentBase.cs`; ~43 per-provider test components inherit from it |
| 5 | `AIBody` immutability and `AIInteractionBase` init-only properties | `AIBody` is a `sealed record` with `IReadOnlyList<int> InteractionsNew` and no `ResetNew()`; `AIInteractionBase` properties use `init` and provide `With...` helpers |

### Still pending / not implemented

| # | Item | Evidence |
| --- | --- | --- |
| 1 | `ProviderTrustPolicy` enforcement before provider calls | `AIRequestCall.IsValid` still only adds warnings in `Soft` mode; no centralized policy blocks execution |
| 2 | `SmartHopper.Components.Test` Release build mapping | `SmartHopper.sln` still has `Release\|*` build entries for `{B932CFFA-0C82-4A1F-92F2-003CDE1C94AE}` (Components.Test) |
| 3 | Centralize macOS/WinForms `net48` reference-assembly workaround | The workaround is still duplicated in ~18 `.csproj` files, including `SmartHopper.Components.Test.csproj` |
| 4 | Use `IProviderHttpClientFactory` in `AIProvider.CallApi` / streaming / batch | `IProviderHttpClientFactory` exists and is registered in `SmartHopperInitializer`, but `AIProvider.CallApi` and `AIProviderStreamingAdapter.CreateHttpClient` still call `new HttpClient()` |
| 5 | Provider contract / round-trip `Encode/Decode` tests | No round-trip contract tests for text/tool/image/audio interactions across providers |
| 6 | Structured logging/tracing and metrics export | `IProviderLogger` exists and is registered, but provider code still uses ad-hoc `Debug.WriteLine`; `AIMetrics` are not exported to an external sink |
| 7 | Hardcoded model capability lists | Each provider still returns a large `List<AIModelCapabilities>` from `*ProviderModels.cs` |
| 8 | OpenAI-compatible role mapping consolidation | OpenAI, Mistral, LocalAI, and Ollama still contain identical `switch` role mappings in their `EncodeToJToken` paths |

### Partially implemented

| # | Item | Evidence |
| --- | --- | --- |
| 1 | Provider logging abstraction | `IProviderLogger` and `SmartHopperProviderLogger` exist and are wired into `ProviderSdkHost`, but `AIProvider` and streaming adapters still use `Debug.WriteLine` directly |
| 2 | Provider HTTP client factory | `IProviderHttpClientFactory` and `SmartHopperProviderHttpClientFactory` exist and are wired, but not consumed by the provider HTTP call paths |

### Inconsistencies with this review's original claims

1. **Duplication Map** marks "Provider API-key accessor", "Provider icon loading", and "Settings validation (MaxTokens/Temperature)" as **High** but not **Remediated**. In fact, all three have been consolidated into the `AIProvider`/`AIProviderSettings` base classes and are used by every provider.
2. **Prioritized Action Plan** item 3 ("Move common provider helpers into the base classes") is not marked **Done**, but the helpers exist and are in use.
3. **Prioritized Action Plan** item 4 ("Document and harness the `SmartHopper.Components.Test` suite") is not marked **Done**, but `ProviderTestComponentBase` already provides a shared test-harness base.

---

## Dimension Scores

| # | Dimension | Score | Justification |
| --- | --- | --- | --- |
| 1 | Base classes, base entities, and core abstractions | 3 | Deep 5-level hierarchy, but mitigated by composition cores. `AIStatefulAsyncComponentBase` is becoming a “god base class” and the adapter hierarchy is asymmetric. |
| 2 | Project organization and maintainability | 3 | Clean layers and docs, but the macOS WinForms workaround is duplicated across ~20 projects, `Components.Test` is built in Release, and project references are inconsistent. |
| 3 | Conceptual correctness of data objects and domain model | 3 | Strong value objects and explicit lifecycle. `AIBody.InteractionsNew` is now `IReadOnlyList<int>`, `ResetNew()` is removed, and `AIInteractionBase`/`IAIInteraction` use `init`-only properties. `AIRequestBase` is still not abstract and its `Exec` method throws `NotImplementedException`. |
| 4 | Duplication of code and responsibilities | 3 | Model selection and JSON schema wrapping are consolidated; API-key, icon loading, and `MaxTokens`/`Temperature` validation have moved to `AIProvider`/`AIProviderSettings` base classes. OpenAI-compatible role mapping and hardcoded model capability lists are still repeated per provider; the macOS WinForms workaround is duplicated in ~18 `.csproj` files. The ~43 per-provider test runners are intentional and now share a `ProviderTestComponentBase`. |
| 5 | Duplication and consistency of stored data | 3 | No heavy persisted denormalization. `AIModelCapabilityRegistry` is now the only model capability singleton; `TrustedProviderRecord` still exists alongside a legacy `Dictionary<string,bool>`. |
| 6 | Unreferenced, orphaned, or dead code/data | 3 | `TrustedProviderRecord` is still unused. `AIBody.ResetNew()` and `ModelManager` have been removed. `Components.Test` build mappings still contradict the stated intent. |
| 7 | Coupling, cohesion, and changeability | 3 | Provider SDK host abstractions provide clean seams, but `SmartHopper.Core` component bases directly depend on `SmartHopper.Infrastructure` managers. |
| 8 | Security, auth, and lifecycle guardrails | 3 | Secrets, provider integrity checks, and trust classifications are modeled, but trust is not enforced at a security boundary, `DEBUG` builds weaken integrity, and encryption failures are silent. |
| 9 | API contracts and client/server alignment | 3 | `IAIProvider` and `AIRequestCall`/`AIReturn` are clear, but there is no contract versioning, no round-trip contract tests, and no OpenAPI/exported spec. |
| 10 | Testability and observability | 3 | Good DI seams, fakes, and a dedicated `SmartHopper.ProviderSdk.Tests` project. `IProviderLogger` and `IProviderHttpClientFactory` abstractions exist and are registered, but provider code still uses `Debug.WriteLine` and creates `new HttpClient()` directly; `AIMetrics` are not exported to an external sink. |

---

## Detailed Findings

### 1. Base classes, base entities, and core abstractions

- **[Certain]** The component inheritance chain reaches five levels from `GH_Component`: `AsyncComponentBase` → `StatefulComponentBase` → `AIProviderComponentBase` → `AIStatefulAsyncComponentBase` → `AIOutputAdapterBase` / `AISelectingStatefulAsyncComponentBase`. This is an acknowledged trade-off in the design documentation.
- **[Certain]** The team mitigates the hierarchy with composition cores: `ComponentStateManager`, `ProviderSelectionCore`, and `SelectingSupport` extract orthogonal responsibilities.
- **[Certain]** `AIStatefulAsyncComponentBase` is split into eight partial files (`Main`, `AI`, `Batch`, `Lifecycle`, `Metrics`, `Persistence`, `Processing`, `UI`) to manage its size, at `src/SmartHopper.Core/ComponentBase/`.
- **[Suspicious]** `AIStatefulAsyncComponentBase` is a “god base class” in practice: it owns Grasshopper lifecycle, async workers, provider/model selection, AI tool capability merging, batch and non-batch execution, metrics, persistence, and UI. It directly calls `AIToolManager.DiscoverTools()` and `AIToolManager.GetTools()` from `SmartHopper.Infrastructure.AITools` in `SmartHopper.Core.ComponentBase.AIStatefulAsyncComponentBase.Main.cs` (lines 274-313). A `SmartHopper.Core` component base should not need to discover tools; that concern belongs to the infrastructure or tool pipeline.
- **[Certain]** `AIProviderComponentBase` delegates provider selection and menu wiring to a composed `ProviderSelectionCore`, which is a clean pattern.
- **[Suspicious]** Asymmetry in adapters: `AIOutputAdapterBase` inherits from `AIStatefulAsyncComponentBase` while `AIInputAdapterBase` inherits directly from `GH_Component`. The intended split is not documented.
- **[Question]** Should `BatchRunState` or a similar `BatchProcessingCore` be extracted from `AIStatefulAsyncComponentBase` the same way `ComponentStateManager` was extracted from `StatefulComponentBase`?

### 2. Project organization and maintainability

- **[Certain]** Project layering is mostly clean: `SmartHopper.ProviderSdk` has no project dependencies, `SmartHopper.Infrastructure` depends only on the SDK, `SmartHopper.Core` depends on Infrastructure, and each `SmartHopper.Providers.*` project depends only on `SmartHopper.ProviderSdk` (`src/SmartHopper.Providers.OpenAI/SmartHopper.Providers.OpenAI.csproj`).
- **[Certain]** The macOS WinForms workaround (NET Framework 4.8 reference assemblies) is duplicated across many `.csproj` files, e.g., `src/SmartHopper.Core/SmartHopper.Core.csproj` and `src/SmartHopper.Providers.OpenAI/SmartHopper.Providers.OpenAI.csproj`.
- **[Certain]** `src/SmartHopper.Components.Test/SmartHopper.Components.Test.csproj` is mapped to `Release` build configurations in `SmartHopper.sln`, but `.devin/rules/solution-structure.md` states it “is not built in Release.”
- **[Certain]** Some project references use explicit GUID metadata (`SmartHopper.Core.Grasshopper.csproj`) while most use simple references.
- **[Suspicious]** `Directory.Build.props` enables `StyleCop.Analyzers` with `TreatWarningsAsErrors=false` and does not appear to run style enforcement in CI.
- **[Question]** Is the `net48` reference-assembly workaround still required for Rhino 8.11+? The project comments say “Rhino 8.10 and earlier.”

### 3. Conceptual correctness of data objects and domain model

- **[Certain]** `AIBody` is a `sealed record` intended to be immutable, at `src/SmartHopper.ProviderSdk/AICall/Core/Interactions/AIBody.cs`. `AICapability` is a `[Flags]` enum with composite flags and clear extension methods, at `src/SmartHopper.ProviderSdk/AIModels/AICapability.cs`.
- **[Certain]** `AIRequestBase` exposes provider, model (computed via `GetModelToUse` with memoization), capability, body, parameters, and timeout. `AIReturn` computes `Success` from the severity of `Messages` and aggregates body, request, and validation diagnostics, at `src/SmartHopper.ProviderSdk/AICall/Core/Returns/AIReturn.cs`.
- **[Certain]** `AIBody.InteractionsNew` is a mutable `List<int>` inside an immutable record, and `ResetNew()` is the only mutable method on the record (`src/SmartHopper.ProviderSdk/AICall/Core/Interactions/AIBody.cs`, lines 38-146). This breaks the immutability invariant. *[Remediated: `InteractionsNew` is now `IReadOnlyList<int>`, `ResetNew()` is removed, and the type uses an immutable record contract with an `AIBodyBuilder` for safe construction.]*
- **[Suspicious]** `AIInteractionBase` has public setters for `TurnId`, `Time`, `Agent`, and `Metrics`, yet is used as a value inside `AIBody`. `AIBodyBuilder` and `AIReturn` mutate interaction metrics after construction. *[Remediated: `AIInteractionBase`/`IAIInteraction` now expose `init`-only record properties; interaction records are copied with `with` expressions or `With...` helpers, and streaming aggregation uses nested `Builder` classes.]*
- **[Suspicious]** `AIRequestBase` is not abstract and its `Exec` method throws `NotImplementedException`. `AIRequestCall` and `AIToolCall` both subclass it and add execution behavior, blurring the DTO/service boundary. `AIToolCall` is in `src/SmartHopper.Infrastructure/AICall/Tools/AIToolCall.cs`.
- **[Suspicious]** `AIModelCapabilities` is a mutable class used as a value object in `AIModelCapabilityRegistry`; public setters allow post-registration mutation.
- **[Suspicious]** `ConversationSession` uses implicit state via `_generateGreeting`, `_greetingEmitted`, and `_lastReturn` instead of an explicit `SessionState` enum, at `src/SmartHopper.Infrastructure/AICall/Sessions/ConversationSession.cs`.
- **[Question]** Do the overlapping bit positions in `AICapability.AudioInput` / `AudioOutput` (which include `SpeechInput` / `SpeechOutput`) cause any ambiguity in `HasCapability` checks?

### 4. Duplication of code and responsibilities

- **[Certain]** **Model selection is duplicated.** `AIModelCapabilityRegistry.SelectBestModel` and `ModelManager.SelectBestModel` implement the same fallback algorithm: user model → preferred default → exact default → compatible default → any registered (`src/SmartHopper.ProviderSdk/AIModels/AIModelCapabilityRegistry.cs`, lines 278-353 and `src/SmartHopper.Infrastructure/AIModels/ModelManager.cs`, lines 282-383). *[Remediated: `ModelManager` removed; `AIModelCapabilityRegistry.Instance` is the single source of truth.]*
- **[Certain]** **JSON schema wrapping is duplicated.** `OpenAIJsonSchemaAdapter`, `MistralAIJsonSchemaAdapter`, `LocalAIJsonSchemaAdapter`, `OllamaJsonSchemaAdapter`, and `DeepSeekJsonSchemaAdapter` all contain the same `Wrap()` logic for object-root schema conversion (`src/SmartHopper.Providers.OpenAI/OpenAIJsonSchemaAdapter.cs`, lines 29-77). *[Remediated: consolidated into `OpenAICompatibleJsonSchemaAdapter` in Provider SDK; DeepSeek overrides only `Unwrap`.]*
- **[Certain]** **API-key helper is duplicated.** Eight providers define an identical `internal string GetApiKey() { return this.GetSetting<string>("ApiKey"); }` (e.g., `src/SmartHopper.Providers.OpenAI/OpenAIProvider.cs`, line 106; `src/SmartHopper.Providers.Anthropic/AnthropicProvider.cs`, line 88; `src/SmartHopper.Providers.SmartHopperCloud/SmartHopperCloudProvider.cs`, line 401). *[Remediated: `AIProvider.GetApiKey()` is now a protected base helper and all providers use it.]*
- **[Certain]** **Icon loading is duplicated.** Provider `Icon` properties repeat the same `MemoryStream`/`Image.FromStream`/`new Bitmap` pattern with the same fallback across all providers. *[Remediated: `AIProvider.LoadIconFromResources(byte[])` and `LoadIconFromResources(Image)` are protected base helpers and all providers use them.]*
- **[Certain]** **Settings validation is duplicated.** `OpenAIProviderSettings`, `AnthropicProviderSettings`, `DeepSeekProviderSettings`, etc. all hand-roll `MaxTokens > 0` and `0.0 <= Temperature <= 2.0` validation with the same diagnostics pattern (`src/SmartHopper.Providers.OpenAI/OpenAIProviderSettings.cs`, lines 179-218). *[Remediated: `AIProviderSettings.ValidateMaxTokens` and `ValidateTemperature` are protected base helpers and all settings classes use them.]*
- **[Certain]** **OpenAI role mapping is duplicated.** `OpenAI`, `MistralAI`, `LocalAI`, and `Ollama` each map `AIAgent.System/Context` → `"system"`, `User` → `"user"`, `Assistant/ToolCall` → `"assistant"`, `ToolResult` → `"tool"` independently.
- **[Certain (intentional)]** **Per-provider test runner components in `SmartHopper.Components.Test`.** The ~43 files under `src/SmartHopper.Components.Test/Providers/` (e.g., `Test{Provider}{Feature}Component.cs`) are an intended test suite. Each component is a separate Grasshopper test runner that the developer executes to validate a specific provider, similar to running unit tests. They are not accidental duplication and should not be consolidated into a single generic component. *[Updated: a shared `ProviderTestComponentBase` has been extracted for common setup/teardown; each per-provider runner still remains independent.]*
- **[Certain]** **Model lists are hardcoded per provider.** Each `*ProviderModels.cs` returns a large `List<AIModelCapabilities>` with the same property assignments.

### 5. Duplication and consistency of stored data

- **[Certain]** `SmartHopperSettings.TrustedProviders` is declared as `Dictionary<string, bool>` (`src/SmartHopper.Infrastructure/Settings/SmartHopperSettings.cs`, lines 74-78), while `TrustedProviderRecord` exists as a separate, more structured type that is never used (`src/SmartHopper.Infrastructure/Settings/TrustedProviderRecord.cs`).
- **[Resolved]** `AIModelCapabilityRegistry` and `ModelManager` held overlapping in-memory capability data. `ModelManager` has been removed and `AIModelCapabilityRegistry.Instance` is now the only singleton.
- **[Certain]** `AIModelCapabilities.Default` is a subset of `AIModelCapabilities.Capabilities`, but this invariant is not enforced in the type; `ModelManager.SetDefault` and `AIModelCapabilityRegistry.SetDefault` both clear bits manually.
- **[Question]** Are provider settings effectively persisted in both `SmartHopperSettings.ProviderSettings` and the per-provider `AIProvider._injectedSettings` cache? Which is the authoritative copy after `RefreshCachedSettings`?

### 6. Unreferenced, orphaned, or dead code/data

- **[Certain]** `TrustedProviderRecord` in `SmartHopper.Infrastructure.Settings` is defined but has zero references outside its own file.
- **[Certain]** `AIBody.ResetNew()` is declared but is only invoked by `AIBodyValidationTests`; no production usage. *[Remediated: `ResetNew()` removed and the obsolete test coverage deleted.]*
- **[Resolved]** `ModelManager` was a thin wrapper around `AIModelCapabilityRegistry` and was only used by `FallbackSettingsPage` and tests; it has been removed and `AIModelCapabilityRegistry` is used directly.
- **[Suspicious]** `SmartHopper.Components.Test` is built in Release solution configurations, contradicting the rule that it is test-only.
- **[Suspicious]** Some `AICapability` values such as `VideoInput`, `VideoOutput`, and `EmbedOutput` appear to be declared for future use; no production references were found.

### 7. Coupling, cohesion, and changeability

- **[Certain]** `ProviderSdkHost` provides clean static seams (`IProviderTrustHost`, `IProviderRegistryHost`, `IPolicyPipelineHost`, etc.) with null/default implementations, at `src/SmartHopper.ProviderSdk/Hosting/ProviderSdkHost.cs`.
- **[Certain]** `SmartHopper.Core` component bases import `SmartHopper.Infrastructure.AIModels` and `SmartHopper.Infrastructure.AITools` (`src/SmartHopper.Core/ComponentBase/AIStatefulAsyncComponentBase.Main.cs`, lines 34-35), coupling presentation/component logic to infrastructure managers.
- **[Certain]** `AsyncComponentBase` in `SmartHopper.Core` imports `SmartHopper.Infrastructure.Mcp` and toggles `McpCanvasLockState` (`src/SmartHopper.Core/ComponentBase/AsyncComponentBase.cs`, lines 39-40), meaning a generic async base knows about MCP.
- **[Suspicious]** `AIOutputAdapterBase` and `AIStatefulAsyncComponentBase` pass a merged body through a string key `"_MergedBody"` (`src/SmartHopper.Core/ComponentBase/AIOutputAdapterBase.cs`, lines 385-767), which is stringly typed and not part of the `AIBody` contract.
- **[Question]** Should `AIStatefulAsyncComponentBase` be split into a smaller orchestrator (state/Grasshopper) plus a composed `AICallOrchestrator` that lives in Infrastructure?

### 8. Security, auth, and lifecycle guardrails

- **[Certain]** Secret storage uses OS secure store (DPAPI/Keychain) with AES-256 and versioned prefixes (`SH03:`, legacy `SH02:`). `SmartHopperSettings.GetSetting` masks secrets in debug output (`src/SmartHopper.Infrastructure/Settings/SmartHopperSettings.cs`, lines 428-430).
- **[Certain]** `ProviderManager` performs Authenticode (Windows), strong-name, and SHA-256 hash checks with three integrity modes, at `src/SmartHopper.Infrastructure/AIProviders/ProviderManager.cs`.
- **[Certain]** `SmartHopperSettings.EffectiveProviderIntegrityCheckMode` forces `Soft` in `DEBUG` builds (`src/SmartHopper.Infrastructure/Settings/SmartHopperSettings.cs`, lines 92-103).
- **[Suspicious]** `AIRequestCall.IsValid` adds trust/integrity warnings, but the request still executes in `Soft` mode; there is no centralized `ProviderTrustPolicy` that blocks the call (`src/SmartHopper.ProviderSdk/AICall/Core/Requests/AIRequestCall.cs`, lines 115-184).
- **[Suspicious]** `SmartHopperSettings.SetSetting` catches encryption exceptions and silently removes the secret (`src/SmartHopper.Infrastructure/Settings/SmartHopperSettings.cs`, lines 261-270); `Decrypt` returns `null` on any failure (`src/SmartHopper.Infrastructure/Settings/SmartHopperSettings.cs`, lines 477-510). Users could lose configured secrets without notice.
- **[Suspicious]** The settings file (`%AppData%\Grasshopper\SmartHopper.json`) is unencrypted; only individual secret values are encrypted inside it.
- **[Question]** Is there a centralized authorization decision point for `SmartHopperCloud` settings sync, or does the OAuth bearer token alone govern all `/context/settings` access?

### 9. API contracts and client/server alignment

- **[Certain]** `IAIProvider` is a clear contract: `Encode/Decode`, `Call`, `PreCall/PostCall`, `SelectModel`, `GetDefaultModel`, `GetStreamingAdapter`, at `src/SmartHopper.ProviderSdk/AIProviders/IAIProvider.cs`.
- **[Certain]** `AIRequestCall`/`AIReturn` are the central request/response DTOs, and providers adapt to/from them. `SmartHopperCloudProvider.PreCall` sets the `X-Smarthopper-Environment` header.
- **[Suspicious]** `IAIProvider` has no version. A breaking change to `Encode`/`Decode` signatures requires coordinated updates to all 9+ providers.
- **[Suspicious]** There are no automated contract/round-trip tests for provider `Encode/Decode` across text, tool, image, and audio interactions.
- **[Suspicious]** `SchemaAttachRequestPolicy` and `SchemaValidateRequestPolicy` are separate; it is not documented which providers support which or why both exist.
- **[Question]** Should `IAIProvider` separate the stateful provider singleton (`AIProvider`) from pure request/response contract interfaces to make fakes and versioning easier?

### 10. Testability and observability

- **[Certain]** `ProviderSdkHost` uses default null/fake implementations (`NullProviderTrustHost`, `InMemoryProviderSettingsStore`, `DebugProviderLogger`), at `src/SmartHopper.ProviderSdk/Hosting/ProviderSdkHost.cs`.
- **[Certain]** `SmartHopper.ProviderSdk.Tests` has 19 test files covering `AIBody`, `AIRequestBase`, `AIReturn`, `AICapability`, `AIMetrics`, etc.
- **[Certain]** `ConversationSessionTests` uses `TestableAIRequestCall` and `MockProviderExecutor` to bypass real providers.
- **[Suspicious]** Logging is ad-hoc `Debug.WriteLine` and `RhinoApp.WriteLine`. `IProviderLogger` exists in the Provider SDK and is wired to a `SmartHopperProviderLogger` in the host, but provider code still writes directly to `Debug.WriteLine` rather than through the abstraction, so there is no effective structured logging or correlation ID in practice.
- **[Suspicious]** `AIProvider.CallApi` and `AIProviderStreamingAdapter` still create `new HttpClient()` directly (`src/SmartHopper.ProviderSdk/AIProviders/AIProvider.cs` and `AIProviderStreamingAdapter.cs`) rather than using `IProviderHttpClientFactory`. The factory abstraction and a host-side pooled implementation exist and are registered, but provider call paths do not consume them yet, making provider HTTP calls hard to fake.
- **[Suspicious]** `AIMetrics` are collected but not exported to external monitoring; they appear to be used only for in-memory validation and UI display.
- **[Question]** Do provider `Encode/Decode` paths have deterministic fakes, or are the only tests integration-style?

---

## Duplication Map

| Concept | Locations | Proposed Single Source of Truth | Certainty |
| --- | --- | --- | --- |
| Model selection / fallback | `AIModelCapabilityRegistry.SelectBestModel` and `ModelManager.SelectBestModel` | `AIModelCapabilityRegistry` in Provider SDK (delete `ModelManager`) | High — **Remediated** |
| JSON schema object-root wrapping | `OpenAIJsonSchemaAdapter`, `MistralAIJsonSchemaAdapter`, `LocalAIJsonSchemaAdapter`, `OllamaJsonSchemaAdapter`, `DeepSeekJsonSchemaAdapter` | `OpenAICompatibleJsonSchemaAdapter` in `SmartHopper.ProviderSdk.AICall.JsonSchemas`; providers override only `Unwrap` | High — **Remediated** |
| Provider API-key accessor | Eight providers’ `internal string GetApiKey()` | `AIProvider` protected helper or direct `GetSetting<string>("ApiKey")` | High — **Remediated** |
| Provider icon loading | Eight providers’ `Image Icon` properties | `AIProvider` protected helper such as `LoadIconFromResources(byte[])` | High — **Remediated** |
| Settings validation (MaxTokens/Temperature) | `OpenAIProviderSettings`, `AnthropicProviderSettings`, `DeepSeekProviderSettings`, etc. | `AIProviderSettings` protected helpers `ValidateMaxTokens` / `ValidateTemperature` | High — **Remediated** |
| OpenAI-compatible role mapping | `OpenAIProvider`, `MistralAIProvider`, `LocalAIProvider`, `OllamaProvider` | `StandardRoleMapper` in Provider SDK | Medium |
| Per-provider test runner components | 43 files in `src/SmartHopper.Components.Test/Providers/` | Keep as independent provider test runners; extract a shared test-harness base for setup/teardown | Intentional (not duplication) — **Harness added** |
| Hardcoded model capability lists | Every `*ProviderModels.cs` | JSON/fluent builder or external model manifest loaded by `AIProviderModels` | Medium |
| WinForms/macOS reference workaround | ~20 `.csproj` files | Centralized in `Directory.Build.props` | High |

---

## Orphan / Dead-Code List

| Artifact | Status | Evidence |
| --- | --- | --- |
| `SmartHopper.Infrastructure.Settings.TrustedProviderRecord` | **Dead** | Class is defined but referenced by no other file; `SmartHopperSettings.TrustedProviders` remains `Dictionary<string, bool>`. |
| `SmartHopper.ProviderSdk.AICall.Core.Interactions.AIBody.ResetNew()` | **Removed** | `ResetNew()` has been removed and the obsolete test coverage deleted. |
| `SmartHopper.Infrastructure.AIModels.ModelManager` | **Removed** | Removed; `AIModelCapabilityRegistry.Instance` is the single source of truth for model selection, defaults, and streaming validation. |
| `SmartHopper.Components.Test` Release build mapping | **Contradicts intent** | `SmartHopper.sln` includes `Release` build entries, but `.devin/rules/solution-structure.md` says it is not built in Release. |
| `AICapability.VideoInput/VideoOutput/EmbedOutput` and related | **Suspicious** | Declared but no production references were located; may be future-proofing flags. |
| `SmartHopper.Core.ComponentBase.AsyncComponentBase` → `SmartHopper.Infrastructure.Mcp` | **Suspicious coupling** | A generic async base class knows about MCP canvas lock state. |

---

## Prioritized Action Plan

| Rank | Change | Effort | Expected Impact |
| --- | --- | --- | --- |
| 1 | **Make `AIModelCapabilityRegistry` the single source of truth for model selection and remove/repurpose `ModelManager`.** Eliminate duplicated `SelectBestModel`, `SetDefault`, `GetDefaultModel`, and streaming validation. | Small | High. Removes the most dangerous source of model-selection divergence and simplifies every provider call. **Done.** |
| 2 | **Add a shared `OpenAICompatibleJsonSchemaAdapter` in the Provider SDK.** Make OpenAI/Mistral/Local/Ollama use it; only DeepSeek inherits and overrides `Unwrap`. | Small | High. Cuts ~4 copy-paste adapters and makes schema changes safe. **Done.** |
| 3 | **Move common provider helpers into the base classes.** Add `AIProvider.GetApiKey()`, `AIProvider.LoadIconFromResources()`, and `AIProviderSettings.ValidateMaxTokens`/`ValidateTemperature`. | Small | High. Removes the bulk of per-provider boilerplate. **Done.** |
| 4 | **Document and, if needed, harness the `SmartHopper.Components.Test` suite.** Clarify that each `Test{Provider}{Feature}Component` is an intentional per-provider test runner. Only extract a shared test-harness base for common setup/teardown; do not collapse the ~43 provider-specific runners into a single component. | Small | Medium. Preserves the intended test surface while reducing common boilerplate. **Done.** |
| 5 | **Fix `AIBody` immutability and `AIInteractionBase` mutability.** Make `AIBody.InteractionsNew` immutable, remove `ResetNew()` if unused, and use init-only setters or a builder for `AIInteractionBase`. | Small | Medium. Aligns the domain model with the documented immutability goal. **Done.** |
| 6 | **Introduce a `ProviderTrustPolicy` that enforces integrity checks before the provider call.** In `Soft`/`Hard`/`Strict` modes, decide whether to block, warn, or allow; do not rely only on `IsValid` messages. | Small | High. Closes the security enforcement gap without changing the contract. |
| 7 | **Fix the `SmartHopper.Components.Test` Release build mapping.** Add `DisableBuild` condition or remove Release solution configurations for the project. | Small | Medium. Aligns build behavior with documented intent. |
| 8 | **Centralize macOS/WinForms workaround and standardize project references.** Move the `net48` reference-assembly logic to `Directory.Build.props` and remove explicit GUIDs from `ProjectReference`. | Small–Medium | Medium. Reduces csproj duplication and build maintenance. |
| 9 | **Add provider contract / round-trip tests and HTTP fakes.** Use `IProviderHttpClientFactory` in `AIProvider.CallApi` and provide a mock message handler. | Medium | Medium. Improves testability and catches provider drift. |
| 10 | **Introduce structured logging/tracing and metrics export.** Replace ad-hoc `Debug.WriteLine` with an `IProviderLogger` implementation that supports scopes/correlation; export `AIMetrics` to a sink. | Large | Medium. Needed for production observability but can follow the other items. |

*Implementation status updated on 2026-09-01 based on the current `src/` tree. No source code was modified to produce this review.*
