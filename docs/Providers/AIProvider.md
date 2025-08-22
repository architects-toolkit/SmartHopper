# AIProvider (base class)

Abstract base class implementing `IAIProvider` with shared initialization, settings, and HTTP orchestration.

- Source: `src/SmartHopper.Infrastructure/AIProviders/AIProvider.cs`

## Purpose

Offer a template-method style pipeline for providers: register models, load settings, encode → call → decode, and record metrics.

## Highlights

- Initialization (`InitializeProviderAsync`)
  - Retrieves `Models.RetrieveCapabilities()` and `RetrieveDefault()`.
  - Registers model capabilities and defaults with `ModelManager`.
  - Loads default setting values from descriptors and merges with stored settings.
- Settings helpers
  - `GetSetting<T>(key)` with type conversion and recursion guard.
  - `SetSetting(key, value)` persists to `SmartHopperSettings` within provider scope.
  - `RefreshCachedSettings(settings)` merges external updates into the cached dictionary.
  - `IEnumerable<SettingDescriptor> GetSettingDescriptors()` via `ProviderManager.GetProviderSettings(Name)`.
- Default model resolution
  - `GetDefaultModel(requiredCapability, useSettings)` validates capability with `ModelManager`.
- HTTP/API orchestration
  - `Call(request)` handles PreCall, validation, `CallApi`, metrics, PostCall.
  - `CallApi` supports GET, POST, DELETE, PATCH, Bearer auth, JSON content.
- Tools formatting
  - `GetFormattedTools(toolFilter)` converts registered AITools to function definitions for provider APIs.
- Abstracts
  - `Encode(AIRequestCall)`, `Encode(IAIInteraction)`, `Decode(string)`, `DefaultServerUrl`.
- Generic singleton variant `AIProvider<T>`
  - Provides `Instance` for providers that prefer a static singleton access pattern.

## Extending

Override `Encode/Decode/PreCall/PostCall` and set `DefaultServerUrl`.
Use `Models` to implement provider-specific model retrieval/capabilities.

## Security & reliability

- Bearer token pulled from settings (`ApiKey`) with clear error if missing.
- Exceptions are wrapped with provider context for easier diagnostics.
- Metrics include provider name, model, completion time.
