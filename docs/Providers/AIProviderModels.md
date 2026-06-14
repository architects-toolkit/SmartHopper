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

---

## Developer Reference

Each provider typically implements `AIProviderModels` to return either a static list of known models or a dynamic list fetched from the provider's API.

```csharp
public class OpenAIProviderModels : AIProviderModels
{
    public override async Task<List<AIModelCapabilities>> RetrieveModels()
    {
        return new List<AIModelCapabilities>
        {
            new AIModelCapabilities
            {
                Provider = "openai",
                Model = "gpt-4o",
                Capabilities = AICapability.Text2Text | AICapability.ToolChat,
                Default = AICapability.ToolChat,
                Verified = true,
                Rank = 200,
                SupportsStreaming = true,
                SupportsPromptCaching = false
            },
            new AIModelCapabilities
            {
                Provider = "openai",
                Model = "gpt-4o-mini",
                Capabilities = AICapability.Text2Text | AICapability.ToolChat | AICapability.Text2Json,
                Default = AICapability.Text2Text,
                Verified = true,
                Rank = 150,
                SupportsStreaming = true,
                SupportsPromptCaching = false
            }
        };
    }

    public override async Task<List<string>> RetrieveApiModels()
    {
        try
        {
            var response = await _httpClient.GetAsync("<https://api.openai.com/v1/models">);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var data = JObject.Parse(json)["data"];
            return data.Select(t => t["id"].ToString())
                       .Distinct(StringComparer.OrdinalIgnoreCase)
                       .OrderBy(s => s)
                       .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }
}

```

```csharp
// Registration during provider initialization
public override async Task InitializeProviderAsync()
{
    var models = await Models.RetrieveModels();
    foreach (var model in models)
    {
        ModelManager.RegisterCapabilities(model);
    }
    await base.InitializeProviderAsync();
}

```

---

## Architecture & Design

`AIProviderModels` separates the concern of "what models exist and what can they do" from "how do we select the best model." This separation lets providers focus on provider-specific knowledge while `ModelManager` owns the selection policy.

The dual methods (`RetrieveModels` and `RetrieveApiModels`) reflect two different use cases:

- **Capability registration** needs rich metadata and should be resilient; a static fallback is acceptable.
- **UI model pickers** want the freshest list possible and can tolerate an empty result by falling back to the static capability list.

By returning empty lists on errors rather than throwing, the system remains usable even when a provider API is temporarily unreachable.
