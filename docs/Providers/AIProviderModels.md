# AIProviderModels

Base class for provider-side model retrieval and capability resolution.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Infrastructure/Providers/AIProviderModels.cs` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

`AIProviderModels` is where each provider declares what models it supports and what those models can do. This class connects provider-specific API knowledge to SmartHopper's centralized model registry.

**You should read this if you:**

- Are implementing model discovery for a new provider
- Need to define which models support streaming, prompt caching, or tool calling
- Want to fetch live model lists from a provider API rather than using static definitions

---

## End-User Guide

### Purpose

Expose model lists, capabilities, and defaults to the provider during initialization and runtime selection. Supports both static, curated capabilities and dynamic discovery via provider APIs.

### Key members

- `Task<List<AIModelCapabilities>> RetrieveModels()` — asynchronously fetch full model metadata (name, capabilities, defaults, verification, rank) for registration in `ModelManager`.
- `Task<List<string>> RetrieveApiModels()` — asynchronously fetch the raw list of available model identifiers from the provider API (e.g., `/models`). Intended for UI listing (e.g., model pickers) and not for capability registration. Implementations should:
  - Use provider-authenticated HTTP calls.
  - Return an empty list on any error (network/JSON), enabling silent fallback to static lists.
  - Return distinct, case-insensitive, alphabetically sorted names.

### Relationships

- Used by `AIProvider.InitializeProviderAsync()` to retrieve and register provider models with `ModelManager` via `RetrieveModels()`.
- `RetrieveApiModels()` is used by UI components like `AIModelsComponent` to populate dynamic model dropdowns/lists. When the API list is unavailable, components fall back to `RetrieveModels()` results.
- Works with `AIModelCapabilities` and `ModelManager` as the single source of truth for capabilities and defaults.

### Notes

- Implementations should fetch concrete models and metadata from provider APIs. Registration is handled centrally by `ModelManager`.
- For `RetrieveApiModels()`, prefer simple, resilient parsing (e.g., extract `id` or `name` from `data[]`). Handle exceptions internally and return `[]`.
- Example providers with dynamic lists: `OpenAIProviderModels`, `MistralAIProviderModels`, `GeminiProviderModels`.
