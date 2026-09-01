# SmartHopper Conceptual Quality Review

**Date**: August 28, 2026  
**Purpose**: Analyze the conceptual quality of the SmartHopper codebase from a senior staff engineer / software architect perspective  
**Related Issue**: N/A  
**Severity**: MEDIUM

---

## Executive Summary

- SmartHopper has a sound layered architecture: `SmartHopper.ProviderSdk` is a self-contained, MIT-licensed contract for third-party providers; `SmartHopper.Infrastructure` owns managers, settings, and the call pipeline; `SmartHopper.Core`/`SmartHopper.Core.Grasshopper` build the Grasshopper component hierarchy; and every first-party provider depends only on `SmartHopper.ProviderSdk`.
- The request/response domain model (`AIBody`, `AIRequestCall`, `AIReturn`, `AICapability`) is well-named, mostly immutable, and supported by a clear `PolicyPipeline` and `ConversationSession` lifecycle.
- **Duplication is the dominant maintenance risk.** Model-selection logic is now consolidated in `AIModelCapabilityRegistry`, and JSON schema wrapping, API-key access, icon loading, and `MaxTokens`/`Temperature` validation have been moved to the `AIProvider`/`AIProviderSettings` base classes. The macOS WinForms `net48` workaround has been centralized in `Directory.Build.props`. Remaining duplication includes OpenAI-compatible role mapping, hardcoded model capability lists in every provider, and ad-hoc `Debug.WriteLine` logging that bypasses the `IProviderLogger` abstraction.
- The component base hierarchy is deep and accumulating concerns. `AIStatefulAsyncComponentBase` is split across eight partial files and directly depends on `SmartHopper.Infrastructure` managers (`AIToolManager`, `AIModels`), indicating the base is doing too much.
- Security concepts are strong and are now enforced before the provider call via `ProviderTrustPolicy`. In `Soft` mode a trust warning is recorded but the call still executes; `DEBUG` builds bypass strict integrity checks, and secret-encryption failures are silent.

**Single highest-value fix (remaining):** Consolidate the duplicated OpenAI-compatible role mapping into a shared `StandardRoleMapper` in the Provider SDK, move the hardcoded model-capability lists out of every `*ProviderModels.cs` into a shared builder or external manifest, and replace the ad-hoc `Debug.WriteLine` logging with a structured `IProviderLogger` and an `AIMetrics` export sink.

## Remediation status

- `ModelManager` has been removed and `AIModelCapabilityRegistry.Instance` is now the single source of truth for model selection, defaults, and streaming validation. Unique test coverage was ported to `AIModelCapabilityRegistryTests`.
- `OpenAIJsonSchemaAdapter`, `MistralAIJsonSchemaAdapter`, `LocalAIJsonSchemaAdapter`, and `OllamaJsonSchemaAdapter` have been removed. `OpenAICompatibleJsonSchemaAdapter` in `SmartHopper.ProviderSdk.AICall.JsonSchemas` now owns the shared object-root wrapping logic. `DeepSeekJsonSchemaAdapter` inherits from it and overrides only the provider-specific `Unwrap` behavior.
- `AIBody.InteractionsNew` is now `IReadOnlyList<int>`, `ResetNew()` has been removed, and `AIBody` is constructed through `AIBodyBuilder`.
- `AIInteractionBase`/`IAIInteraction` expose `init`-only properties and provide `With...` helpers; streaming aggregation uses nested `Builder` classes.
- Common provider helpers have been moved to base classes: `AIProvider.GetApiKey()`, `AIProvider.LoadIconFromResources()`, and `AIProviderSettings.ValidateMaxTokens()`/`ValidateTemperature()`. All first-party providers use them.
- A shared `ProviderTestComponentBase` has been extracted in `SmartHopper.Components.Test` so the ~43 per-provider test runners share common setup/teardown while remaining independent runners.
- `ProviderTrustPolicy` has been introduced and is evaluated in `AIRequestCall.IsValid()` and `AIRequestCall.Exec()` to decide whether to block, warn, or allow a provider call before it executes.
- `IProviderHttpClientFactory` has been introduced; `SmartHopperProviderHttpClientFactory` and `TestProviderHttpClientFactory` are wired through `ProviderSdkHost.HttpClientFactory` and used by `AIProvider.CallApi`, batch, and streaming paths.
- Provider contract / round-trip `Encode/Decode` tests now run in `SmartHopper.ProviderSdk.Tests/AIProviders/AIProviderCallTests` using `FakeAIProvider` and the in-memory HTTP client factory.
- The macOS WinForms `net48` reference-assembly workaround has been moved to `Directory.Build.props` and removed from the individual `.csproj` files.

---

## Implementation Status Update

*Verified on 2026-09-01.*

The following review suggestions have been implemented since the review was written (verified on 2026-09-01 on branch `feature/2.0.0-dev.260901-provider-http-client-factory`):

### Completed

| # | Item | Evidence |
| --- | --- | --- |
| 1 | `ModelManager` removed; `AIModelCapabilityRegistry.Instance` is the single source of truth | No `ModelManager` class or references in `src/`; `AIModelCapabilityRegistry` is used by `ModalityFallbackResolver`, validators, and session code; tests live in `src/SmartHopper.ProviderSdk.Tests/AIModels/AIModelCapabilityRegistryTests.cs` |
| 2 | Shared `OpenAICompatibleJsonSchemaAdapter` in Provider SDK | `src/SmartHopper.ProviderSdk/AICall/JsonSchemas/OpenAICompatibleJsonSchemaAdapter.cs` exists; OpenAI/Mistral/Local/Ollama providers register it; only `DeepSeekJsonSchemaAdapter` remains for provider-specific `Unwrap` |
| 3 | Common provider helpers moved to base classes | `AIProvider.GetApiKey()` and `AIProvider.LoadIconFromResources()` in `AIProvider.cs` (lines 563, 573, 600); `AIProviderSettings.ValidateMaxTokens()` and `ValidateTemperature()` in `AIProviderSettings.cs` (lines 103, 136); all providers call them |
| 4 | Shared test-harness base for `SmartHopper.Components.Test` | `ProviderTestComponentBase` at `src/SmartHopper.Components.Test/Providers/ProviderTestComponentBase.cs`; ~43 per-provider test components inherit from it |
| 5 | `AIBody` immutability and `AIInteractionBase` init-only properties | `AIBody` is a `sealed record` with `IReadOnlyList<int> InteractionsNew` and no `ResetNew()`; `AIInteractionBase` properties use `init` and provide `With...` helpers |
| 6 | `ProviderTrustPolicy` enforcement before provider calls | `ProviderTrustPolicy` in `src/SmartHopper.ProviderSdk/AICall/Validation/ProviderTrustPolicy.cs`; evaluated by `AIRequestCall.IsValid()` (line 117) and `AIRequestCall.Exec()` (line 280); tests in `src/SmartHopper.ProviderSdk.Tests/AICall/Validation/ProviderTrustPolicyTests.cs` |
| 7 | Use `IProviderHttpClientFactory` in `AIProvider.CallApi` / streaming / batch | `AIProvider.CreateHttpClient()` in `AIProvider.cs` (line 763) delegates to `ProviderSdkHost.HttpClientFactory`; `SmartHopperProviderHttpClientFactory` and `TestProviderHttpClientFactory` are wired in `SmartHopperInitializer.cs` (line 72) and `ProviderSdkHostAdapters.cs` (line 206) |
| 8 | Provider contract / round-trip `Encode/Decode` tests | `FakeAIProvider` and `TestProviderHttpClientFactory` in `src/SmartHopper.ProviderSdk.Tests/TestHelpers` support deterministic text and tool-call round-trip tests in `src/SmartHopper.ProviderSdk.Tests/AIProviders/AIProviderCallTests.cs` |
| 9 | macOS/WinForms `net48` reference-assembly workaround centralized | `Directory.Build.props` (lines 41-45) contains the workaround; no `net48` blocks remain in `src/**/*.csproj` |

### Still pending / not implemented

| # | Item | Evidence |
| --- | --- | --- |
| 1 | `SmartHopper.Components.Test` Release build mapping | `SmartHopper.sln` still has `Release\|*` build entries for `{B932CFFA-0C82-4A1F-92F2-003CDE1C94AE}` (Components.Test) |
| 2 | Hardcoded model capability lists | Each provider still returns a large `List<AIModelCapabilities>` from `*ProviderModels.cs` (e.g. `OpenRouterProviderModels.cs` line 51) |
| 3 | OpenAI-compatible role mapping consolidation | OpenAI, Mistral, LocalAI, Ollama, OpenRouter, and DeepSeek still contain identical `switch (interaction.Agent)` mappings (e.g. `OpenAIProvider.cs` lines 255-277) |
| 4 | Structured logging/tracing and metrics export | `IProviderLogger` exists and is registered, but provider code still uses ad-hoc `Debug.WriteLine`; `AIMetrics` are not exported to an external sink |

### Partially implemented

| # | Item | Evidence |
| --- | --- | --- |
| 1 | Provider logging abstraction | `IProviderLogger` and `SmartHopperProviderLogger` are wired into `ProviderSdkHost`, but provider code still calls `Debug.WriteLine` directly instead of routing through the abstraction |
| 2 | `TrustedProviderRecord` / trusted-provider storage | `TrustedProviderRecord` exists in `SmartHopper.Infrastructure.Settings` but is never referenced; `SmartHopperSettings.TrustedProviders` remains a `Dictionary<string, bool>` |

*The inconsistencies noted in the original review (Duplication Map and Prioritized Action Plan #3/#4) are now resolved in the tables above.*

---

## Dimension Scores

| # | Dimension | Score | Justification |
| --- | --- | --- | --- |
| 1 | Base classes, base entities, and core abstractions | 3 | Deep 5-level hierarchy, but mitigated by composition cores. `AIStatefulAsyncComponentBase` is becoming a “god base class” and the adapter hierarchy is asymmetric. |
| 2 | Project organization and maintainability | 3 | Clean layers and docs. The macOS WinForms workaround is centralized in `Directory.Build.props`, explicit GUID `ProjectReference` metadata is gone, but `Components.Test` is still mapped to Release builds and `StyleCop` remains warning-only. |
| 3 | Conceptual correctness of data objects and domain model | 3 | Strong value objects and explicit lifecycle. `AIBody.InteractionsNew` is now `IReadOnlyList<int>`, `ResetNew()` is removed, and `AIInteractionBase`/`IAIInteraction` use `init`-only properties. `AIRequestBase` is still not abstract and its `Exec` method throws `NotImplementedException`. |
| 4 | Duplication of code and responsibilities | 3 | Model selection, JSON schema wrapping, API-key, icon loading, and `MaxTokens`/`Temperature` validation are consolidated. The provider HTTP client factory is shared. OpenAI-compatible role mapping and hardcoded model capability lists are still repeated per provider. The ~43 per-provider test runners are intentional and share a `ProviderTestComponentBase`. |
| 5 | Duplication and consistency of stored data | 3 | No heavy persisted denormalization. `AIModelCapabilityRegistry` is now the only model capability singleton; `TrustedProviderRecord` still exists alongside a legacy `Dictionary<string,bool>`. |
| 6 | Unreferenced, orphaned, or dead code/data | 3 | `TrustedProviderRecord` is still unused. `AIBody.ResetNew()` and `ModelManager` have been removed. `Components.Test` build mappings still contradict the stated intent. |
| 7 | Coupling, cohesion, and changeability | 3 | Provider SDK host abstractions provide clean seams, but `SmartHopper.Core` component bases directly depend on `SmartHopper.Infrastructure` managers. |
| 8 | Security, auth, and lifecycle guardrails | 3 | Secrets, provider integrity checks, and trust classifications are modeled; trust is now enforced by `ProviderTrustPolicy` before the provider call, but `DEBUG` builds still weaken integrity and encryption failures are silent. |
| 9 | API contracts and client/server alignment | 3 | `IAIProvider` and `AIRequestCall`/`AIReturn` are clear, and provider contract round-trip tests now exist. There is still no contract versioning, no OpenAPI/exported spec, and `IAIProvider` mixes the stateful provider singleton with the pure request/response contract. |
| 10 | Testability and observability | 3 | Good DI seams, fakes, and a dedicated `SmartHopper.ProviderSdk.Tests` project. `IProviderHttpClientFactory` is now used by provider call, batch, and streaming paths. `IProviderLogger` exists but provider code still uses `Debug.WriteLine`; `AIMetrics` are not exported to an external sink. |

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
- **[Certain (remediated)]** The macOS WinForms workaround (NET Framework 4.8 reference assemblies) is centralized in `Directory.Build.props` (lines 41-45); no `net48` blocks remain in individual `src/**/*.csproj` files.
- **[Certain]** `src/SmartHopper.Components.Test/SmartHopper.Components.Test.csproj` is mapped to `Release` build configurations in `SmartHopper.sln`, but `.devin/rules/solution-structure.md` states it “is not built in Release.”
- **[Resolved]** Project references no longer use explicit GUID metadata; all `ProjectReference` entries use simple references.
- **[Suspicious]** `Directory.Build.props` enables `StyleCop.Analyzers` with `TreatWarningsAsErrors=false` and does not appear to run style enforcement in CI.
- **[Question]** Is the `net48` reference-assembly workaround still required for Rhino 8.11+? The project comments say “Rhino 8.10 and earlier.”

### 3. Conceptual correctness of data objects and domain model

- **[Certain]** `AIBody` is a `sealed record` intended to be immutable, at `src/SmartHopper.ProviderSdk/AICall/Core/Interactions/AIBody.cs`. `AICapability` is a `[Flags]` enum with composite flags and clear extension methods, at `src/SmartHopper.ProviderSdk/AIModels/AICapability.cs`.
- **[Certain]** `AIRequestBase` exposes provider, model (computed via `GetModelToUse` with memoization), capability, body, parameters, and timeout. `AIReturn` computes `Success` from the severity of `Messages` and aggregates body, request, and validation diagnostics, at `src/SmartHopper.ProviderSdk/AICall/Core/Returns/AIReturn.cs`.
- **[Certain (remediated)]** `AIBody` is a `sealed record` constructed through `AIBodyBuilder`; `InteractionsNew` is `IReadOnlyList<int>` and `ResetNew()` has been removed.
- **[Certain (remediated)]** `AIInteractionBase`/`IAIInteraction` now use `init`-only record properties and provide `With...` helpers; `AIBodyBuilder` and `AIReturn` copy interactions with `with` expressions or helper methods.
- **[Suspicious]** `AIRequestBase` is not abstract and its `Exec` method throws `NotImplementedException`. `AIRequestCall` and `AIToolCall` both subclass it and add execution behavior, blurring the DTO/service boundary. `AIToolCall` is in `src/SmartHopper.Infrastructure/AICall/Tools/AIToolCall.cs`.
- **[Suspicious]** `AIModelCapabilities` is a mutable class used as a value object in `AIModelCapabilityRegistry`; public setters allow post-registration mutation.
- **[Suspicious]** `ConversationSession` uses implicit state via `_generateGreeting`, `_greetingEmitted`, and `_lastReturn` instead of an explicit `SessionState` enum, at `src/SmartHopper.Infrastructure/AICall/Sessions/ConversationSession.cs`.
- **[Question]** `AICapability.AudioInput` / `AudioOutput` intentionally include the `SpeechInput` / `SpeechOutput` bits, and `AICapabilityExtensions.HasFlag` handles this correctly. `VideoInput`, `VideoOutput`, and `EmbedOutput` are now referenced in provider model lists (OpenRouter, OpenAI, Mistral, Gemini) but it is unclear whether the rest of the pipeline fully exercises them.

### 4. Duplication of code and responsibilities

- **[Certain (remediated)]** **Model selection is consolidated.** `ModelManager` has been removed; `AIModelCapabilityRegistry.Instance` is the single source of truth for model selection, defaults, and streaming validation.
- **[Certain (remediated)]** **JSON schema wrapping is consolidated.** `OpenAICompatibleJsonSchemaAdapter` in the Provider SDK now owns the shared object-root wrapping logic; OpenAI, Mistral, LocalAI, and Ollama register it directly, and `DeepSeekJsonSchemaAdapter` inherits from it and overrides only `Unwrap`.
- **[Certain (remediated)]** **API-key access is consolidated in `AIProvider.GetApiKey()`.** All first-party providers call the protected base helper from their `CallApi`, `Batch`, and streaming paths.
- **[Certain (remediated)]** **Icon loading is consolidated in `AIProvider.LoadIconFromResources()`.** All first-party providers use the protected base helpers for their `Icon` property.
- **[Certain (remediated)]** **Settings validation is consolidated in `AIProviderSettings`.** `ValidateMaxTokens()` and `ValidateTemperature()` are protected base helpers called by every provider settings class.
- **[Certain]** **OpenAI role mapping is duplicated.** OpenAI, Mistral, LocalAI, Ollama, OpenRouter, and DeepSeek each implement the same `switch (interaction.Agent)` mapping from `AIAgent` values to OpenAI-compatible role strings.
- **[Certain (intentional)]** **Per-provider test runner components in `SmartHopper.Components.Test`.** The ~43 files under `src/SmartHopper.Components.Test/Providers/` are an intended test suite; each is a separate Grasshopper test runner. They are not accidental duplication. A shared `ProviderTestComponentBase` has been extracted for common setup/teardown.
- **[Certain]** **Model lists are hardcoded per provider.** Each `*ProviderModels.cs` returns a large `List<AIModelCapabilities>` with the same property assignments.

### 5. Duplication and consistency of stored data

- **[Certain]** `SmartHopperSettings.TrustedProviders` is declared as `Dictionary<string, bool>` (`src/SmartHopper.Infrastructure/Settings/SmartHopperSettings.cs`, lines 74-78), while `TrustedProviderRecord` exists as a separate, more structured type that is never used (`src/SmartHopper.Infrastructure/Settings/TrustedProviderRecord.cs`).
- **[Resolved]** `AIModelCapabilityRegistry` and `ModelManager` held overlapping in-memory capability data. `ModelManager` has been removed and `AIModelCapabilityRegistry.Instance` is now the only singleton.
- **[Certain]** `AIModelCapabilities.Default` is a subset of `AIModelCapabilities.Capabilities`, but this invariant is not enforced in the type; `AIModelCapabilityRegistry.SetDefault` clears bits manually.
- **[Question]** Are provider settings effectively persisted in both `SmartHopperSettings.ProviderSettings` and the per-provider `AIProvider._injectedSettings` cache? Which is the authoritative copy after `RefreshCachedSettings`?

### 6. Unreferenced, orphaned, or dead code/data

- **[Certain]** `TrustedProviderRecord` in `SmartHopper.Infrastructure.Settings` is defined but has zero references outside its own file.
- **[Certain (remediated)]** `AIBody.ResetNew()` has been removed and the obsolete test coverage deleted.
- **[Resolved]** `ModelManager` was a thin wrapper around `AIModelCapabilityRegistry` and was only used by `FallbackSettingsPage` and tests; it has been removed and `AIModelCapabilityRegistry` is used directly.
- **[Suspicious]** `SmartHopper.Components.Test` is built in Release solution configurations, contradicting the rule that it is test-only.
- **[Suspicious]** `AICapability` values `VideoInput`, `VideoOutput`, and `EmbedOutput` are now referenced in provider model lists (OpenRouter, OpenAI, Mistral, Gemini) but it is unclear whether the rest of the pipeline fully exercises them.

### 7. Coupling, cohesion, and changeability

- **[Certain]** `ProviderSdkHost` provides clean static seams (`IProviderTrustHost`, `IProviderRegistryHost`, `IPolicyPipelineHost`, etc.) with null/default implementations, at `src/SmartHopper.ProviderSdk/Hosting/ProviderSdkHost.cs`.
- **[Certain]** `SmartHopper.Core` component bases import `SmartHopper.Infrastructure.AIModels` and `SmartHopper.Infrastructure.AITools` (`src/SmartHopper.Core/ComponentBase/AIStatefulAsyncComponentBase.Main.cs`, lines 34-35), coupling presentation/component logic to infrastructure managers.
- **[Certain]** `AsyncComponentBase` in `SmartHopper.Core` imports `SmartHopper.Infrastructure.Mcp` and toggles `McpCanvasLockState` (`src/SmartHopper.Core/ComponentBase/AsyncComponentBase.cs`, lines 39-40), meaning a generic async base knows about MCP.
- **[Suspicious]** `AIOutputAdapterBase` and `AIStatefulAsyncComponentBase` pass a merged body through a string key `_MergedBody` (`src/SmartHopper.Core/ComponentBase/AIOutputAdapterBase.cs`, lines 385-767), which is stringly typed and not part of the `AIBody` contract.
- **[Question]** Should `AIStatefulAsyncComponentBase` be split into a smaller orchestrator (state/Grasshopper) plus a composed `AICallOrchestrator` that lives in Infrastructure?

### 8. Security, auth, and lifecycle guardrails

- **[Certain]** Secret storage uses OS secure store (DPAPI/Keychain) with AES-256 and versioned prefixes (`SH03:`, legacy `SH02:`). `SmartHopperSettings.GetSetting` masks secrets in debug output (`src/SmartHopper.Infrastructure/Settings/SmartHopperSettings.cs`, lines 428-430).
- **[Certain]** `ProviderManager` performs Authenticode (Windows), strong-name, and SHA-256 hash checks with three integrity modes, at `src/SmartHopper.Infrastructure/AIProviders/ProviderManager.cs`.
- **[Certain]** `SmartHopperSettings.EffectiveProviderIntegrityCheckMode` forces `Soft` in `DEBUG` builds (`src/SmartHopper.Infrastructure/Settings/SmartHopperSettings.cs`, lines 92-103).
- **[Certain (remediated)]** `ProviderTrustPolicy` is evaluated by `AIRequestCall.IsValid()` (line 117) and `AIRequestCall.Exec()` (line 280) and blocks the call in `ProviderTrustVerdict.Block` before the provider executes; warnings are still allowed in `Soft` mode.
- **[Suspicious]** `SmartHopperSettings.SetSetting` catches encryption exceptions and silently removes the secret (`src/SmartHopper.Infrastructure/Settings/SmartHopperSettings.cs`, lines 261-270); `Decrypt` returns `null` on any failure (`src/SmartHopper.Infrastructure/Settings/SmartHopperSettings.cs`, lines 477-510). Users could lose configured secrets without notice.
- **[Suspicious]** The settings file at `%AppData%/Grasshopper/SmartHopper.json` is unencrypted; only individual secret values are encrypted inside it.
- **[Question]** Is there a centralized authorization decision point for `SmartHopperCloud` settings sync, or does the OAuth bearer token alone govern all `/context/settings` access?

### 9. API contracts and client/server alignment

- **[Certain]** `IAIProvider` is a clear contract: `Encode/Decode`, `Call`, `PreCall/PostCall`, `SelectModel`, `GetDefaultModel`, `GetStreamingAdapter`, at `src/SmartHopper.ProviderSdk/AIProviders/IAIProvider.cs`.
- **[Certain]** `AIRequestCall`/`AIReturn` are the central request/response DTOs, and providers adapt to/from them. `SmartHopperCloudProvider.PreCall` sets the `X-Smarthopper-Environment` header.
- **[Suspicious]** `IAIProvider` has no version. A breaking change to `Encode`/`Decode` signatures requires coordinated updates to all 9+ providers.
- **[Certain (remediated)]** Automated provider contract / round-trip `Encode/Decode` tests now exist in `AIProviderCallTests` using `FakeAIProvider` and the in-memory HTTP client factory, covering text and tool-call interactions.
- **[Suspicious]** `SchemaAttachRequestPolicy` and `SchemaValidateRequestPolicy` are separate; it is not documented which providers support which or why both exist.
- **[Question]** Should `IAIProvider` separate the stateful provider singleton (`AIProvider`) from pure request/response contract interfaces to make fakes and versioning easier?

### 10. Testability and observability

- **[Certain]** `ProviderSdkHost` uses default null/fake implementations (`NullProviderTrustHost`, `InMemoryProviderSettingsStore`, `DebugProviderLogger`), at `src/SmartHopper.ProviderSdk/Hosting/ProviderSdkHost.cs`.
- **[Certain]** `SmartHopper.ProviderSdk.Tests` has 19 test files covering `AIBody`, `AIRequestBase`, `AIReturn`, `AICapability`, `AIMetrics`, etc.
- **[Certain]** `ConversationSessionTests` uses `TestableAIRequestCall` and `MockProviderExecutor` to bypass real providers.
- **[Suspicious]** Logging is ad-hoc `Debug.WriteLine` and `RhinoApp.WriteLine`. `IProviderLogger` exists and is wired to `SmartHopperProviderLogger`, but provider code still calls `Debug.WriteLine` directly, so there is no effective structured logging or correlation ID in practice.
- **[Resolved]** Provider HTTP client creation is now centralized through `ProviderSdkHost.HttpClientFactory`. `SmartHopperProviderHttpClientFactory` caches one `HttpMessageHandler` per provider while returning a fresh `HttpClient` on each call, sets a provider-specific `User-Agent`, and applies per-request timeouts. `TestProviderHttpClientFactory` enables deterministic, network-free HTTP fakes.
- **[Suspicious]** `AIMetrics` are collected but not exported to external monitoring; they appear to be used only for in-memory validation and UI display.
- **[Resolved]** `FakeAIProvider` and `TestProviderHttpClientFactory` provide deterministic fakes for `Encode/Decode` and HTTP round-trips. `AIProviderCallTests` exercises text and tool-call round-trips without network access.

---

## Duplication Map

| Concept | Locations | Proposed Single Source of Truth | Certainty |
| --- | --- | --- | --- |
| Model selection / fallback | `AIModelCapabilityRegistry.SelectBestModel` and `ModelManager.SelectBestModel` | `AIModelCapabilityRegistry` in Provider SDK (delete `ModelManager`) | High — **Remediated** |
| JSON schema object-root wrapping | Former `OpenAIJsonSchemaAdapter`, `MistralAIJsonSchemaAdapter`, `LocalAIJsonSchemaAdapter`, `OllamaJsonSchemaAdapter`, `DeepSeekJsonSchemaAdapter` | `OpenAICompatibleJsonSchemaAdapter` in `SmartHopper.ProviderSdk.AICall.JsonSchemas`; providers override only `Unwrap` | High — **Remediated** |
| Provider API-key accessor | Former eight providers’ `GetApiKey` methods | `AIProvider.GetApiKey()` protected helper | High — **Remediated** |
| Provider icon loading | Former eight providers’ `Icon` properties | `AIProvider.LoadIconFromResources()` protected helpers | High — **Remediated** |
| Settings validation (MaxTokens/Temperature) | Former `OpenAIProviderSettings`, `AnthropicProviderSettings`, `DeepSeekProviderSettings`, etc. | `AIProviderSettings.ValidateMaxTokens()` / `ValidateTemperature()` | High — **Remediated** |
| Provider HTTP client creation | `new HttpClient()` and per-provider handlers in first-party providers | `ProviderSdkHost.HttpClientFactory` backed by `SmartHopperProviderHttpClientFactory` | High — **Remediated** |
| OpenAI-compatible role mapping | `OpenAIProvider`, `MistralAIProvider`, `LocalAIProvider`, `OllamaProvider`, `OpenRouterProvider`, `DeepSeekProvider` | `StandardRoleMapper` in Provider SDK | Medium |
| Per-provider test runner components | 43 files in `src/SmartHopper.Components.Test/Providers/` | Keep as independent provider test runners; shared `ProviderTestComponentBase` for setup/teardown | Intentional (not duplication) — **Harness added** |
| Hardcoded model capability lists | Every `*ProviderModels.cs` | JSON/fluent builder or external model manifest loaded by `AIProviderModels` | Medium |
| WinForms/macOS reference workaround | ~20 `.csproj` files | Centralized in `Directory.Build.props` | High — **Remediated** |

---

## Orphan / Dead-Code List

| Artifact | Status | Evidence |
| --- | --- | --- |
| `SmartHopper.Infrastructure.Settings.TrustedProviderRecord` | **Dead** | Class is defined but referenced by no other file; `SmartHopperSettings.TrustedProviders` remains `Dictionary<string, bool>`. |
| `SmartHopper.ProviderSdk.AICall.Core.Interactions.AIBody.ResetNew()` | **Removed** | `ResetNew()` has been removed and the obsolete test coverage deleted. |
| `SmartHopper.Infrastructure.AIModels.ModelManager` | **Removed** | Removed; `AIModelCapabilityRegistry.Instance` is the single source of truth for model selection, defaults, and streaming validation. |
| `SmartHopper.Components.Test` Release build mapping | **Contradicts intent** | `SmartHopper.sln` includes `Release` build entries, but `.devin/rules/solution-structure.md` says it is not built in Release. |
| `AICapability.VideoInput/VideoOutput/EmbedOutput` and related | **Future-facing / partially used** | Referenced in provider model lists (OpenRouter, OpenAI, Mistral, Gemini); unclear whether the runtime pipeline fully exercises them. |
| `SmartHopper.Core.ComponentBase.AsyncComponentBase` → `SmartHopper.Infrastructure.Mcp` | **Suspicious coupling** | A generic async base class knows about MCP canvas lock state. |

---

## Prioritized Action Plan

| Rank | Change | Effort | Expected Impact |
| --- | --- | --- | --- |
| 1 | **Make `AIModelCapabilityRegistry` the single source of truth for model selection and remove/repurpose `ModelManager`.** | Small | High. Removes the most dangerous source of model-selection divergence and simplifies every provider call. **Done.** |
| 2 | **Add a shared `OpenAICompatibleJsonSchemaAdapter` in the Provider SDK.** | Small | High. Cuts ~4 copy-paste adapters and makes schema changes safe. **Done.** |
| 3 | **Move common provider helpers into the base classes.** Add `AIProvider.GetApiKey()`, `AIProvider.LoadIconFromResources()`, and `AIProviderSettings.ValidateMaxTokens`/`ValidateTemperature()`. | Small | High. Removes the bulk of per-provider boilerplate. **Done.** |
| 4 | **Document and harness the `SmartHopper.Components.Test` suite.** Clarify that each `Test{Provider}{Feature}Component` is an intentional per-provider test runner; extract `ProviderTestComponentBase`. | Small | Medium. Preserves the intended test surface while reducing common boilerplate. **Done.** |
| 5 | **Fix `AIBody` immutability and `AIInteractionBase` mutability.** Make `AIBody.InteractionsNew` immutable, remove `ResetNew()`, and use `init`-only setters or a builder for `AIInteractionBase`. | Small | Medium. Aligns the domain model with the documented immutability goal. **Done.** |
| 6 | **Introduce a `ProviderTrustPolicy` that enforces integrity checks before the provider call.** | Small | High. Closes the security enforcement gap without changing the contract. **Done.** |
| 7 | **Fix the `SmartHopper.Components.Test` Release build mapping.** Add `DisableBuild` condition or remove Release solution configurations for the project. | Small | Medium. Aligns build behavior with documented intent. **Pending.** |
| 8 | **Centralize macOS/WinForms workaround and standardize project references.** Move the `net48` reference-assembly logic to `Directory.Build.props` and remove explicit GUIDs from `ProjectReference`. | Small–Medium | Medium. Reduces csproj duplication and build maintenance. **Done.** |
| 9 | **Add provider contract / round-trip tests and HTTP fakes.** Use `IProviderHttpClientFactory` in `AIProvider.CallApi` and provide a mock message handler. | Medium | Medium. Improves testability and catches provider drift. **Done.** |
| 10 | **Introduce structured logging/tracing and metrics export.** Replace ad-hoc `Debug.WriteLine` with an `IProviderLogger` implementation that supports scopes/correlation; export `AIMetrics` to a sink. | Large | Medium. Needed for production observability but can follow the other items. |
| 11 | **Consolidate OpenAI-compatible role mapping into a `StandardRoleMapper` in the Provider SDK.** | Small | High. Removes ~6 copy-paste `switch (interaction.Agent)` blocks and makes role changes safe. |
| 12 | **Move hardcoded model capability lists into a shared builder or external manifest.** | Medium | High. Removes the largest remaining per-provider duplication and makes model metadata easier to maintain. |
