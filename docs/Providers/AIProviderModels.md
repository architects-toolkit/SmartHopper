# AIProviderModels

Base class for provider-side model retrieval and capability resolution.

- Source: `src/SmartHopper.Infrastructure/AIProviders/AIProviderModels.cs`

## Purpose

Expose model lists, capabilities, and defaults to the provider during initialization and runtime selection. Supports both static, curated capabilities and dynamic discovery via provider APIs.

## Key members

- `Task<List<AIModelCapabilities>> RetrieveModels()` — asynchronously fetch full model metadata (name, capabilities, defaults, verification, rank) for registration in `ModelManager`.
- `Task<List<string>> RetrieveApiModels()` — asynchronously fetch the raw list of available model identifiers from the provider API (e.g., `/models`). Intended for UI listing (e.g., model pickers) and not for capability registration. Implementations should:
  - Use provider-authenticated HTTP calls.
  - Return an empty list on any error (network/JSON), enabling silent fallback to static lists.
  - Return distinct, case-insensitive, alphabetically sorted names.

## Relationships

- Used by `AIProvider.InitializeProviderAsync()` to retrieve and register provider models with `ModelManager` via `RetrieveModels()`.
- `RetrieveApiModels()` is used by UI components like `AIModelsComponent` to populate dynamic model dropdowns/lists. When the API list is unavailable, components fall back to `RetrieveModels()` results.
- Works with `AIModelCapabilities` and `ModelManager` as the single source of truth for capabilities and defaults.

## Notes

- Implementations should fetch concrete models and metadata from provider APIs. Registration is handled centrally by `ModelManager`.
- For `RetrieveApiModels()`, prefer simple, resilient parsing (e.g., extract `id` or `name` from `data[]`). Handle exceptions internally and return `[]`.
- Example providers with dynamic lists: `OpenAIProviderModels`, `MistralAIProviderModels`.
