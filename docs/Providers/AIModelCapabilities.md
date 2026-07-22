# AI Model Capabilities

This document describes `AIModelCapabilities` and how SmartHopper uses it to drive model selection and badges.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Infrastructure/AIModels/AIModelCapabilities.cs` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

`AIModelCapabilities` is the central data structure that tells SmartHopper what each model can do, which model to pick by default, and how to surface compatibility warnings. If you are adding or tuning model support, this is the document to read.

**You should read this if you:**

- Are adding a new model to a provider and need to declare its capabilities
- Want to understand how SmartHopper chooses the default model for a given task
- Need to mark a model as verified, deprecated, or discouraged for specific tools

---

## End-User Guide

### Fields

- **Provider**
  Provider id (e.g., "openai", "mistralai").
  Normalized to lower-case by `ModelManager` when registering capabilities.

- **Model**
  Concrete API-ready model name (e.g., "gpt-4o-mini", "mistral-small-latest").

- **Capabilities**
  `AICapability` bitmask of what the model can do
  (for example `Text2Text`, `ToolChat`, `Text2Json`, `Text2Image`, etc.).

- **Default**
  `AICapability` bitmask of the capabilities this model is the default for.
  A model can be capable of multiple things but be default only for a subset.

- **Verified**
  Boolean; model is verified to work end-to-end in SmartHopper.

- **Deprecated**
  Boolean; avoid unless selected explicitly or no better option exists.

- **Rank**
  Integer tie-breaker; higher is preferred after `Verified` / `Deprecated` filters.

- **Aliases**
  List of alternative names resolving to this model.
  Lookups in `AIModelCapabilityRegistry` use the exact model name or any alias.

- **SupportsStreaming**
  Bool; provider supports streaming with this model.

- **SupportsPromptCaching**
  Bool; provider supports prompt caching with this model.

- **CacheKeyStrategy**
  Optional provider-defined cache key hint/strategy name.

- **DiscouragedForTools**
  List of AI tool names for which this model is discouraged.
  Used by `ComponentBadgesAttributes` to show a "not recommended" badge when
  components that use those tools select this model.

### Best practices

- **Defaults per capability**
  Mark at most one default per capability per provider.
  Use `ModelManager.SetDefault(...)` with `exclusive = true` to enforce this.

- **Concrete models only**
  Keep `Model` concrete (no wildcards). Use `Aliases` for alternative names;
  lookup and selection use exact/alias matching only.

- **Ranking and metadata**
  Use `Verified`, `Deprecated`, and `Rank` sparingly but deliberately.
  `ModelManager.SelectBestModel(...)` uses these to choose between candidates
  that satisfy the required capability.

- **Tool-specific guidance**
  Use `DiscouragedForTools` only when a model is technically compatible but
  known to be sub-optimal for a particular tool (for example poor JSON
  structure for `list_generate`). This does not block the model; it only
  surfaces a warning badge.

### Examples

- **Text-only default**
  - Capabilities: `Text2Text | ToolChat`
  - Default: `Text2Text`

- **Tool-call default**
  - Capabilities: `Text2Text | ToolChat`
  - Default: `ToolChat`

### See also

- Model selection policy: `ModelSelection.md`
- Manager API: `ProviderManager.md`, `AIProviderModels.md`
- Badges and UI integration: `../Components/Helpers/AIComponentAttributes.md`

---

## Developer Reference

`AIModelCapabilities` is typically populated by a provider's `AIProviderModels` implementation and registered with `ModelManager` during initialization.

```csharp
// Declaring capabilities for a model
var capabilities = new AIModelCapabilities
{
    Provider = "openai",
    Model = "gpt-4o-mini",
    Capabilities = AICapability.Text2Text | AICapability.ToolChat | AICapability.Text2Json,
    Default = AICapability.Text2Text,
    Verified = true,
    Deprecated = false,
    Rank = 100,
    Aliases = new List<string> { "gpt-4o-mini-2024-07-18" },
    SupportsStreaming = true,
    SupportsPromptCaching = false,
    DiscouragedForTools = new List<string>()
};

ModelManager.RegisterCapabilities(capabilities);

```

```csharp
// Registering a default with exclusivity
ModelManager.SetDefault(
    provider: "openai",
    capability: AICapability.Text2Text,
    model: "gpt-4o-mini",
    exclusive: true);

```

---

## Architecture & Design

`AIModelCapabilities` acts as the bridge between provider-specific model knowledge and SmartHopper's centralized model selection policy. Each provider declares what it knows; `ModelManager` consumes these declarations as the single source of truth.

The design uses bitmasks for capabilities and defaults so that a single model can serve multiple roles, while still having a clear default for each role. The `Verified`, `Deprecated`, and `Rank` fields create a simple scoring system that `ModelManager.SelectBestModel` uses when multiple models satisfy the requested capability.

`DiscouragedForTools` is intentionally non-blocking: it feeds UI badges rather than hard restrictions, keeping the system flexible while still guiding users toward better choices.
