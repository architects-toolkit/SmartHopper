# Model Selection Policy

Location: `src/SmartHopper.Infrastructure/AIModels/ModelManager.cs`

This page describes the simplified, capability-first model selection policy and how to manage defaults using `SetDefault`.

## Policy (capability-first)

When selecting a model for a given `provider` and `requiredCapability`:

1. __User-specified model__
   - If known and capable → use it.
   - If unknown → pass through (assume valid upstream).
   - Else → fallback.

2. __Preferred default__ (e.g., settings) if capable.

3. __Default for the requested capability__ among registered models.

   - Tie-breakers:
     - Verified: true before false.
     - Rank: higher first.
     - Deprecated: false before true.
     - Name: ascending (ordinal, case-insensitive).

4. __Best of the rest__: any capable model ordered by the same tie-breakers.

Notes:

- Models must be concrete API-ready names. No wildcard resolution.
- Selection is fully handled by `ModelManager.SelectBestModel()`; the registry is internal for storage only.
- The policy intentionally excludes a separate "default-compatible" tier to reduce complexity.

## Is "Last Resort" necessary?

Not applicable. Registry-level fallback has been removed. `ModelManager` encapsulates selection logic entirely, and `AIModelCapabilityRegistry` is internal to `ModelManager` for thread-safe storage and retrieval.

## Managing defaults with SetDefault

Use `ModelManager.SetDefault(provider, model, caps, exclusive = true)` to set default models per capability.

- `caps` is an `AICapability` bitmask. You can set multiple capability defaults at once.
- `exclusive = true` clears those capability bits from other models of the same provider to ensure a single default per capability.

### Examples

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

## Tie-breaker guidance

- __Verified__: mark true only after end-to-end success within SmartHopper.
- __Deprecated__: set true to steer selection away unless explicitly chosen or no alternatives exist.
- __Rank__: use to fine-tune preference among similar verified, non-deprecated models.

## Related docs

- `AIModelCapabilities` model: `AIModelCapabilities.md`
- Providers integration: `AIProviderModels.md`, `ProviderManager.md`
- AICall module: `Providers/AICall/index.md`
