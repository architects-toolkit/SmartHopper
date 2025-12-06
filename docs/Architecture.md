# SmartHopper Architecture

This document summarizes the core architecture of SmartHopper: a modular, secure AI layer integrated into Grasshopper3D.

- Target platforms: Rhino 8 (Grasshopper 1)
- Frameworks: .NET 7 (Windows/macOS)
- Solution structure:
  - `src/SmartHopper.Core/` — Core abstractions shared across components
  - `src/SmartHopper.Core.Grasshopper/` — Grasshopper utilities and AI Tools
  - `src/SmartHopper.Components/` — End‑user Grasshopper components
  - `src/SmartHopper.Infrastructure/` — Settings, Provider/Model managers, Context manager
  - `src/SmartHopper.Providers.*` — Provider plugins (external DLLs, loaded securely)

## 1. High‑level Design

SmartHopper integrates with external AI providers through a secure plug‑in model. Providers declare models and capabilities, which are centrally validated and resolved at runtime. Components offer UI for selecting providers/models, orchestrate context gathering, invoke AI calls, and surface metrics and results to the user.

Data flow:

1) Component input/state → 2) Context providers → 3) Provider + Model resolution → 4) AI call (tools optional) → 5) Response decoding → 6) Metrics → 7) Component outputs


## 2. Provider Discovery, Trust, and Loading

- Manager: `src/SmartHopper.Infrastructure/AIProviders/ProviderManager.cs` — docs: [ProviderManager](./Providers/ProviderManager.md)
  - Discovers external provider assemblies: `SmartHopper.Providers.*.dll`
  - Dual verification for supply‑chain safety:
    - Authenticode signature (thumbprint match)
    - Strong‑name public key token match
  - First discovery prompts trust; persisted in settings
  - Initializes providers asynchronously; updates availability and model catalog

- Threat controls:
  - Restrict load paths to expected directories
  - Verify signatures before activation
  - Persist and respect user trust decisions

## 3. Provider Contract and Base Implementation

- Contract: `src/SmartHopper.Infrastructure/AIProviders/IAIProvider.cs` — docs: [IAIProvider](./Providers/IAIProvider.md)
  - Properties: `Name`, `Icon`, `IsEnabled`, `Models`
  - Lifecycle: `InitializeProviderAsync()`, `RefreshCachedSettings()`
  - Call pipeline: `PreCall()`, `Call()`, `PostCall()`
  - Encoding/Decoding helpers: `Encode*` / `Decode*`
  - Model selection: `GetDefaultModel()` and `SelectModel()` delegating to `ModelManager`/`AIModelCapabilities` for capability‑aware defaults and fallbacks.
  
- Base class: `src/SmartHopper.Infrastructure/AIProviders/AIProvider.cs` — docs: [AIProvider](./Providers/AIProvider.md)
  - Shared initialization and settings injection
  - Registers model capabilities with `ModelManager`
  - Default model resolution and tool formatting
  - HTTP calling with Bearer auth; error/response handling hooks
  
- Provider-side models: `src/SmartHopper.Infrastructure/AIProviders/AIProviderModels.cs` — docs: [AIProviderModels](./Providers/AIProviderModels.md)

## 4. Model Management and Capability Registry

- Manager: `src/SmartHopper.Infrastructure/AIModels/ModelManager.cs`
  - Singleton access to capabilities via registry
  - Provider×Model registration, validation, and queries
  - Default model resolution by capability

- Capability model: `src/SmartHopper.Infrastructure/AIModels/AIModelCapabilities.cs`
  - Captures provider name, model name, capability flags and metadata (verified/deprecated, rank, aliases, streaming/prompt‑caching support, tool‑specific discouragement hints).
  - Helpers for key generation, capability checks, and reading this metadata.

- Registry: `src/SmartHopper.Infrastructure/AIModels/AIModelCapabilityRegistry.cs`
  - Stores capabilities keyed by `provider.model`
  - Exact name and alias matching only; no wildcard resolution in the registry
  - Model selection and defaults are handled by `ModelManager`; the registry is storage only

## 5. Context Providers

- Contract: `src/SmartHopper.Infrastructure/AIContext/IAIContextProvider.cs`
  - `ProviderId`
  - `GetContext(): IDictionary<string, string>` returning context key‑values

- Implementations:
  - `EnvironmentContextProvider.cs` — OS, Rhino version, platform
  - `TimeContextProvider.cs` — current time and timezone

These are injected to enrich AI prompts with environment and time metadata.

## 6. Component Base Classes

- Provider UI/persistence: `src/SmartHopper.Core/ComponentBase/AIProviderComponentBase.cs` — see docs: [AIProviderComponentBase](./Components/ComponentBase/AIProviderComponentBase.md)
  - UI for provider selection and persistence in GH file
  - Resolves default provider, change detection and events

- Async AI orchestration: `src/SmartHopper.Core/ComponentBase/AIStatefulAsyncComponentBase.cs` — see docs: [AIStatefulAsyncComponentBase](./Components/ComponentBase/AIStatefulAsyncComponentBase.md)
  - Adds inputs (e.g., `Model`, `Run`) and metrics outputs
  - Lifecycle for async operations and tool execution helpers
  - Injects chosen provider/model into AI Tools
  - Stores and formats metrics (duration, token usage, etc.)

## 7. AI Tools (Grasshopper Utilities)

- Directory: `src/SmartHopper.Core.Grasshopper/AITools/`
  - Families: `gh_*`, `text_*`, `list_*`, `script_*`, `img_*`, `web_*`
  - Tools can be used standalone as components or invoked by AI workflows
  - Utilities to interact with GH canvas (e.g., reading/writing, toggling preview)

## 8. Security and Secrets

- Provider loading security:
  - Authenticode + strong‑name verification before enabling providers
  - Trust prompt and persisted decisions

- Secrets management:
  - API keys and provider settings handled by `SmartHopperSettings` with secure persistence
  - No static secrets committed to source; per‑user configuration

## 9. Execution Pipeline (Typical Call)

1) User triggers `Run` in a component derived from [`AIStatefulAsyncComponentBase`](./Components/ComponentBase/AIStatefulAsyncComponentBase.md).
2) Component resolves provider and model (via [`AIProviderComponentBase`](./Components/ComponentBase/AIProviderComponentBase.md) + `ModelManager`).
3) Context is aggregated from registered `IAIContextProvider` implementations.
4) Provider `PreCall()` prepares payload; `Call()` executes HTTP/API; `PostCall()` parses.
5) Response is decoded; metrics are recorded and emitted to component outputs.

## 10. Extensibility Guidelines

- Adding a new Provider (`SmartHopper.Providers.MyProvider`):
  1) Implement `IAIProvider` or derive from `AIProvider` and register models/capabilities
  2) Sign the assembly (Authenticode + strong‑name)
  3) Ship as `SmartHopper.Providers.MyProvider.dll` in the host folder
  4) On first run, user will be prompted to trust/enable

- Adding a new Model:
  - Register `AIModelCapabilities` via `ModelManager` during provider initialization

- Adding a new Context Provider:
  - Implement `IAIContextProvider` and register with the context manager

- Adding a new Tool:
  - Create a component in `src/SmartHopper.Core.Grasshopper/AITools/` following existing patterns

## 11. Concurrency, Reliability, and Metrics

- Model registry lookups use exact model names and aliases only; `ModelManager.SelectBestModel` centralizes capability‑aware selection and fallbacks.
- Default model resolution prefers concrete names to avoid API errors and uses `Verified`/`Deprecated`/`Rank` metadata as tie‑breakers.
- Components maintain run state and metrics; the async bases ensure metrics are not cleared mid‑processing in toggle scenarios.
- The AICall `PolicyPipeline` runs request/response policies (timeouts, tool validation, context injection, schema attach/validate, finish‑reason normalization) for every call.
- Conversation sessions (`ConversationSession`) orchestrate multi‑turn calls and tool loops on top of `AIRequestCall.Exec`, aggregating per‑turn metrics for the UI.
- Providers should implement timeouts, retries with jitter, cancellation, and error classification.

## 12. Directory Map (Key Paths)

- Providers
  - `src/SmartHopper.Infrastructure/AIProviders/ProviderManager.cs`
  - `src/SmartHopper.Infrastructure/AIProviders/IAIProvider.cs`
  - `src/SmartHopper.Infrastructure/AIProviders/AIProvider.cs`
- Models
  - `src/SmartHopper.Infrastructure/AIModels/ModelManager.cs`
  - `src/SmartHopper.Infrastructure/AIModels/AIModelCapabilities.cs`
  - `src/SmartHopper.Infrastructure/AIModels/AIModelCapabilityRegistry.cs`
- Context
  - `src/SmartHopper.Infrastructure/AIContext/IAIContextProvider.cs`
  - `src/SmartHopper.Core/AIContext/EnvironmentContextProvider.cs`
  - `src/SmartHopper.Core/AIContext/TimeContextProvider.cs`
- Components
  - `src/SmartHopper.Core/ComponentBase/AIProviderComponentBase.cs` — [docs](./Components/ComponentBase/AIProviderComponentBase.md)
  - `src/SmartHopper.Core/ComponentBase/AIStatefulAsyncComponentBase.cs` — [docs](./Components/ComponentBase/AIStatefulAsyncComponentBase.md)
- Tools
  - `src/SmartHopper.Core.Grasshopper/AITools/`

---

This document is a living summary. When adding new providers, models, or tools, update the relevant sections to keep the architecture current.
