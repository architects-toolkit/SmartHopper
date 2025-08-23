# AIProviderModels

Base class for provider-side model retrieval and capability resolution.

- Source: `src/SmartHopper.Infrastructure/AIProviders/AIProviderModels.cs`

## Purpose

Expose model lists, capabilities, and defaults to the provider during initialization and runtime selection.

## Key members

- `Task<List<AIModelCapabilities>> RetrieveModels()` — asynchronously fetch full model metadata (name, capabilities, defaults, verification, rank) for registration in `ModelManager`.

## Relationships

- Used by `AIProvider.InitializeProviderAsync()` to retrieve and register provider models with `ModelManager`.
- Works with `AIModelCapabilities` and `ModelManager` as the single source of truth.

## Notes

- Implementations should fetch concrete models and metadata from provider APIs. Registration is handled centrally by `ModelManager`.
