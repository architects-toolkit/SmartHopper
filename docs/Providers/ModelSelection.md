# Model Selection Policy

This page describes the simplified, capability-first model selection policy and how to manage defaults using `SetDefault`.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Infrastructure/Providers/ModelSelection.cs` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

Understanding the model selection policy is important if you need to know how SmartHopper chooses the right AI model for a given capability and provider. This document explains the centralized policy that keeps model selection consistent across all components.

**You should read this if you:**

- Need to understand how SmartHopper selects models at runtime
- Want to set or change default models for specific capabilities
- Are building a component that interacts with the model selection system

---

## End-User Guide

### Callers: How to Select a Model

- Always call `provider.SelectModel(requiredCapability, requestedModel)`.
- Do not call `ModelManager.SelectBestModel` directly from requests/components. The provider base class delegates to it internally by default, keeping policy centralized while maintaining proper abstraction.

### Policy (Capability-First)

When selecting a model for a given `provider` and `requiredCapability`:

1. **User-specified model**
   - If known and capable → use it.
   - If unknown → pass through (assume valid upstream).
   - Else → fallback.

2. **Preferred default** (e.g., settings) if capable.

3. **Default for the requested capability** among registered models.

   - Tie-breakers:
     - Verified: true before false.
     - Rank: higher first.
     - Deprecated: false before true.
     - Name: ascending (ordinal, case-insensitive).

4. **Default-compatible**: any model marked as default for other capabilities but still compatible with the required capability, ordered by the same tie-breakers.

5. **Best of the rest**: any capable model ordered by the same tie-breakers.

Notes:

- Models must be concrete API-ready names. No wildcard resolution.
- Selection is fully handled by `ModelManager.SelectBestModel()`; the registry is internal for storage only. Callers should go through `IAIProvider.SelectModel(...)`.

### Is "Last Resort" Necessary?

Not applicable. Registry-level fallback has been removed. `ModelManager` encapsulates selection logic entirely, and `AIModelCapabilityRegistry` is internal to `ModelManager` for thread-safe storage and retrieval.

---

## Developer Reference

### Managing Defaults with SetDefault

Use `ModelManager.SetDefault(provider, model, caps, exclusive = true)` to set default models per capability.

- `caps` is an `AICapability` bitmask. You can set multiple capability defaults at once.
- `exclusive = true` clears those capability bits from other models of the same provider to ensure a single default per capability.

#### Example: Setting Defaults

```csharp
// Make mistral-small-latest the default for Text2Text
ModelManager.Instance.SetDefault("mistralai", "mistral-small-latest", AICapability.Text2Text);

// Make gpt-4o-mini the default for ToolChat and Text2Json, exclusively
ModelManager.Instance.SetDefault(
    "openai",
    "gpt-4o-mini",
    AICapability.ToolChat | AICapability.Text2Json,
    exclusive: true
);

```

#### Example: Selecting a Model in Code

```csharp
var provider = ProviderManager.GetProvider("openai");
var selectedModel = provider.SelectModel(AICapability.Text2Json, requestedModel: null);

// selectedModel now contains the best available model for JSON output
var request = new AIRequestCall
{
    Model = selectedModel,
    Messages = new List<Message> { new Message { Role = "user", Content = "Generate a JSON object" } }
};

```

---

## Architecture & Design

### Tie-Breaker Guidance

- **Verified**: mark true only after end-to-end success within SmartHopper.
- **Deprecated**: set true to steer selection away unless explicitly chosen or no alternatives exist.
- **Rank**: use to fine-tune preference among similar verified, non-deprecated models.

### Related Docs

- `AIModelCapabilities` model: `AIModelCapabilities.md`
- Providers integration: `AIProviderModels.md`, `ProviderManager.md`
- AICall module: `Providers/AICall/index.md`
