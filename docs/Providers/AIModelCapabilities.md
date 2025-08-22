# AI Model Capabilities

This document describes `AIModelCapabilities` and how SmartHopper uses it to drive model selection.

Location: `src/SmartHopper.Infrastructure/AIModels/AIModelCapabilities.cs`

## Fields

- Provider: provider id in lower-case (e.g., "openai", "mistralai").
- Model: concrete API-ready model name (e.g., "gpt-4o-mini", "mistral-small-latest").
- Capabilities: `AICapability` bitmask of what the model can do (e.g., Text2Text, ToolChat, Text2Image, Text2Json, ReasoningChat, etc.).
- Default: `AICapability` bitmask of the capabilities this model is the default for.
  - A model can be capable of multiple things but be default only for a subset.
  - Example: `mistral-small-latest` capable of Text2Text + ToolChat, but Default only for Text2Text.
- Verified: boolean; model is verified to work end-to-end in SmartHopper.
- Deprecated: boolean; avoid unless selected explicitly or no better option.
- Rank: integer tie-breaker; higher is preferred after Verified/Deprecated filters.
- Aliases: list of alternative names resolving to this model.
- SupportsStreaming: bool; provider supports streaming with this model.
- SupportsPromptCaching: bool; provider supports prompt-caching with this model.
- CacheKeyStrategy: optional provider-defined cache key hint.

## Best practices

- Mark at most one default per capability per provider. Use `ModelManager.SetDefault(...)` with `exclusive=true` to enforce this.
- Keep `Model` concrete (no wildcards). Wildcards belong to registry fallback only.
- Use `Rank` sparingly; it only fine-tunes order after Verified and non-Deprecated.

## Examples

- Text-only default:
  - Capabilities: Text2Text | ToolChat
  - Default: Text2Text
- Tool-call default:
  - Capabilities: Text2Text | ToolChat
  - Default: ToolChat

## See also

- Model selection policy: `ModelSelection.md`
- Manager API: `ProviderManager.md`, `AIProviderModels.md`
