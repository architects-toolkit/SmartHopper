# AIComponentAttributes

Custom Grasshopper attributes that add a provider badge to AI components.

## Purpose

Visually convey the active AI provider by drawing a badge strip beneath the component when a provider is selected.

## Key features

- Extends `GH_ComponentAttributes`.
- Increases component layout height to make room for a provider strip.
- Draws a logo at small size (e.g., 16px) with a minimum zoom threshold to avoid clutter.
- Only renders the strip when a provider name is available.

## Usage

- Used by [AIProviderComponentBase](./AIProviderComponentBase.md)‑derived components; you typically do not need to subclass this.
- Ensure your provider exposes an icon for best results.

## Related

- [AIProviderComponentBase](./AIProviderComponentBase.md) – supplies the provider context.
