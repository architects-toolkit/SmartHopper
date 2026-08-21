# ProviderComponentBase

`src/SmartHopper.Core/ComponentBase/ProviderComponentBase.cs`

Non-async, non-stateful counterpart to [AIProviderComponentBase](./AIProviderComponentBase.md). Inherits directly from `GH_Component` and implements `IProviderComponent`. Delegates the menu and serialization to [ProviderComponentHelper](./ProviderComponentHelper.md). Used by lightweight provider-aware components (e.g. listing models, showing settings) that do not need the async/state machinery.

## Purpose

Provide the same provider-selection menu, badge and persistence as `AIProviderComponentBase` without dragging in `AsyncComponentBase`/`StatefulComponentBase`.

## Design criteria

- **Same `"Default"` sentinel** as `AIProviderComponentBase`, resolved through `ProviderManager`.
- **`OnProviderChanged()` hook.** Virtual; called inside the menu callback after the new provider is stored, before `ExpireSolution`.
- **Tooltip template.** Sets `AIProviderComponentAttributes.ProviderTooltipTemplate = "Settings for %provider%"` so the badge tooltip reflects the menu's intent.

## Key members

- `string SelectedProviderName`, `void SetSelectedProviderName(string)`
- `string GetActualAIProviderName()`, `AIProvider GetActualAIProvider()`
- `bool HasProviderChanged()`
- `protected virtual void OnProviderChanged()`

## When to derive

- Synchronous helper components that need the provider selection UI but no async work.
- Anything async or stateful should use [AIProviderComponentBase](./AIProviderComponentBase.md) instead.

## Metadata

- Source Code: See source repository.
- Since Version: 2.0.0
- Last Updated: 2026-07-21
- Documentation Maintainer: Marc Roca Musach

---


## Why Read This?

This document provides details about ProviderComponentBase.


## End-User Guide

End-user guidance for ProviderComponentBase.


## Developer Reference

Example usage:

`csharp
// Placeholder example
``r

`csharp
// Another placeholder example
``r


## Architecture & Design

Architecture and design notes for ProviderComponentBase.

```csharp
// Example code for Developer Reference
```

```csharp
// Additional example for Developer Reference
```