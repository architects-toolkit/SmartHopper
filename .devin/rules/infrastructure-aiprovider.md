---
trigger: model_decision
description: Information about the Provider Manager (AIProvider)
---

# AIProviders
- **Purpose**
  - Define provider-agnostic contracts, base classes, and runtime management for AI providers.
  - Handle discovery/loading, capability registration, request execution hooks, settings, and trust/security.

- **Core Interfaces & Base Types**
  - IAIProvider: Core provider contract
  - AIProvider: Base provider logic + generic singleton wrapper (`AIProvider<T>.Instance`).
    - Request lifecycle hooks: PreCall() → Call() → PostCall().
    - Encoding helpers: Encode(IAIInteraction | List<IAIInteraction> | AIRequestCall), `Decode(string)`.
    - Tooling: GetFormattedTools() to emit function defs from `AITools`.
    - Settings: typed `GetSetting<T>()`, `SetSetting()`, `RefreshCachedSettings()`.
    - Models: `GetDefaultModel(requiredCapability)`, async initialization registers capabilities/defaults.
    - HTTP: `CallApi<T>()` for authenticated requests; returns `IAIReturn`.
  - IAIProviderFactory: Factory for discovery: `CreateProvider()`, `CreateProviderSettings()`.
  - IAIProviderSettings: Settings descriptors (`GetSettingDescriptors()`), validation (`ValidateSettings()`), base class for provider-specific UI/validation.

- **Model Management**
  - AIProviderModels: Base for provider model operations (`IAIProviderModels` is in `AIModels`).
    - Retrieves provider model metadata and concrete API-ready model names.
    - GetModel(requestedModel) chooses user-requested or provider default.
  - Initialization path registers capabilities/defaults with `ModelManager`.

- **Discovery, Registration & Trust**
  - ProviderManager (ProviderManager.cs)
    - Discovers external providers: scans local directory for `SmartHopper.Providers.*.dll`.
    - Security: Authenticode certificate and strong-name verification (VerifySignature()).
    - Registers provider + settings (via IAIProviderFactory), keeps maps of providers, settings UIs, and assemblies.
    - GetProviders(includeUntrusted=false) filters by trust; GetProvider(name) and helpers.
    - RefreshProviders() rescans and refreshes persisted settings.

- **Settings Flow**
  - UpdateProviderSettings(providerName, settings)
    - Validates via provider `IAIProviderSettings.ValidateSettings()`.
    - Persists via `SmartHopperSettings`, masks secrets in logs using `SettingDescriptor.IsSecret`.
    - Calls provider RefreshCachedSettings() to merge into cache.

- **Request Lifecycle (per provider)**
  1. PreCall(AIRequestCall) — provider-specific pre-processing.
  2. Call(AIRequestCall) — HTTP call via `CallApi()` or custom logic; returns IAIReturn.
  3. PostCall(IAIReturn) — normalize results, attach metrics/tool calls if needed.

- **Typical Usage**
  1. External provider DLL supplies IAIProviderFactory to create IAIProvider and IAIProviderSettings.
  2. ProviderManager discovers, verifies, loads, and registers them.
  3. App asks provider for a model via `provider.SelectModel(requiredCapability, requestedModel)`.
  4. AI requests flow through provider hooks; encoding/decoding and tools are normalized.

- **Best Practices**
  - Keep InitializeProviderAsync() non-blocking; register capabilities before resolving defaults.
  - Use GetSettingDescriptors() to describe required keys and mark secrets.
  - Use concrete model names for API calls and model metadata. No wildcard resolution.
  - Do not call `ModelManager.SelectBestModel()` directly from requests or components; go through `IAIProvider.SelectModel(...)`.
  - `CallApi()` should not be overridden by AIProvider implementations; use the base class. Override PreCall and PostCall if needed.
  - Keep `Decode()` resilient to provider payload variations; include tool calls and metrics where applicable.
