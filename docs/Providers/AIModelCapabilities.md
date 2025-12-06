# AI Model Capabilities

This document describes `AIModelCapabilities` and how SmartHopper uses it to drive model selection and badges.

Location: `src/SmartHopper.Infrastructure/AIModels/AIModelCapabilities.cs`

## Fields

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

## Best practices

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

## Examples

- **Text-only default**
  - Capabilities: `Text2Text | ToolChat`
  - Default: `Text2Text`

- **Tool-call default**
  - Capabilities: `Text2Text | ToolChat`
  - Default: `ToolChat`

## See also

- Model selection policy: `ModelSelection.md`
- Manager API: `ProviderManager.md`, `AIProviderModels.md`
- Badges and UI integration: `../Components/Helpers/AIComponentAttributes.md`
