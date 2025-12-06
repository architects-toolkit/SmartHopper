# AI Component Attributes

Helpers that extend Grasshopper component attributes to show AI provider and model status on components.

## AIProviderComponentAttributes

Custom Grasshopper attributes that add a provider badge strip to AI components.

### Purpose (provider strip)

Visually convey the active AI provider by drawing a strip beneath the component when a provider is selected.

### Key features (provider strip)

- Extends `GH_ComponentAttributes`.
- Increases component layout height to make room for the provider strip only when a provider is configured.
- Draws the provider icon at small size (e.g., 16px) with a minimum zoom threshold to avoid clutter.
- Resolves the effective provider (including the `Default` sentinel) via `SmartHopperSettings.DefaultAIProvider` and `ProviderManager`.
- Shows an inline tooltip ("Connected to {provider}") when hovering the icon, with a 5‑second auto‑hide timer.

### Usage (provider strip)

- Used by [AIProviderComponentBase](../ComponentBase/AIProviderComponentBase.md)‑derived components; you typically do not need to subclass this directly.
- Ensure your provider exposes an icon for best results.

## ComponentBadgesAttributes

Custom attributes that extend `AIProviderComponentAttributes` to render AI model badges above the component.

### Purpose (model badges)

Surface model status (verified/deprecated/invalid/replaced/not‑recommended) directly on the canvas, based on `AIModelCapabilities` metadata and the last `AIReturn` metrics.

### Key features

- Extends `AIProviderComponentAttributes`; keeps the provider strip and adds floating badges above the component.
- Uses `AIStatefulAsyncComponentBase.UpdateBadgeCache()` and `TryGetCachedBadgeFlags(...)` to read badge flags (Verified, Deprecated, Invalid, Replaced, NotRecommended).
- Shows at most one primary badge for clarity with priority: Replaced > Invalid > NotRecommended > Verified; `Deprecated` can co‑exist with any primary badge.
- Renders badges only when zoomed in enough and extends the attribute bounds upward so hover/tooltips work correctly.
- Displays inline badge tooltips on hover with a 5‑second auto‑hide timer.
- Provides `GetAdditionalBadges()` so derived attributes can add their own custom badges.

### Usage

- Used by [AIStatefulAsyncComponentBase](../ComponentBase/AIStatefulAsyncComponentBase.md), which overrides `CreateAttributes()` to attach `ComponentBadgesAttributes`.
- You normally consume badges by inheriting from `AIStatefulAsyncComponentBase`; custom attributes are only needed if you want extra badges via `GetAdditionalBadges()`.

## Related

- [AIProviderComponentBase](../ComponentBase/AIProviderComponentBase.md) – supplies the provider context and selection menu.
- [AIStatefulAsyncComponentBase](../ComponentBase/AIStatefulAsyncComponentBase.md) – exposes badge state and metrics used by `ComponentBadgesAttributes`.
- [AIModelCapabilities](../../Providers/AIModelCapabilities.md) – source of model metadata (Verified/Deprecated/DiscouragedForTools, etc.).
