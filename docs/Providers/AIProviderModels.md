# AIProviderModels

Base class for provider-side model retrieval and capability resolution.

- Source: `src/SmartHopper.Infrastructure/AIProviders/AIProviderModels.cs`

## Purpose

Expose model lists, capabilities, and defaults to the provider during initialization and runtime selection.

## Key members

- `GetModel(string requestedModel = "")` — choose requested or provider default.
- `Task<List<string>> RetrieveAvailable()` — fetch available model names (override in concrete providers).
- `Task<Dictionary<string, AICapability>> RetrieveCapabilities()` — build capabilities map; calls `RetrieveAvailable()` and `RetrieveCapabilities(model)`.
- `AICapability RetrieveCapabilities(string model)` — resolves concrete names via wildcard patterns.
- `Dictionary<string, AICapability> RetrieveDefault()` — default models per capability (override to declare defaults).

## Relationships

- Used by `AIProvider.InitializeProviderAsync()` to register capabilities/defaults with `ModelManager`.
- Works with `AIModelCapabilities` and `AIModelCapabilityRegistry` on the central registry side.

## Notes

- The base implementation returns empty lists by default; providers should override to query their APIs.
- Wildcard matching lets providers publish patterns like `mistral-small*` while resolving concrete names at runtime.
