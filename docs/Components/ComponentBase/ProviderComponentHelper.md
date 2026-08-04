# ProviderComponentHelper

`src/SmartHopper.Core/ComponentBase/ProviderComponentHelper.cs`

Public static helper that owns the **shared logic** for AI provider selection, menu rendering, default-provider resolution and serialization. Used by both [AIProviderComponentBase](./AIProviderComponentBase.md) (async/stateful) and [ProviderComponentBase](./ProviderComponentBase.md) (sync) so the two paths cannot drift apart.

## Why it exists

The two provider bases inherit from different ancestors (`StatefulComponentBase` vs `GH_Component`) and therefore cannot share code through inheritance. Extracting the duplicated logic into a static helper keeps a single source of truth for:

- The literal sentinel value `"Default"`.
- The shape of the *Select AI Provider* submenu.
- How `"Default"` resolves to a concrete provider at use-time.
- The Grasshopper file key (`"AIProvider"`) used for persistence.

## Public API

```csharp
public static class ProviderComponentHelper
{
    public const string DEFAULT_PROVIDER = "Default";

    public static void   AppendProviderMenuItems(ToolStripDropDown menu, string currentProvider, Action<string> onProviderSelected);
    public static string GetActualProviderName(string selectedProvider);
    public static AIProvider GetActualProvider(string selectedProvider);
    public static bool   WriteProvider(GH_IWriter writer, string selectedProvider);
    public static bool   ReadProvider(GH_IReader reader, out string selectedProvider);
}
```

## Design criteria

- **`"Default"` is a portable sentinel.** Stored verbatim in `.gh` files; resolved lazily through `ProviderManager.GetDefaultAIProvider()` so a document opened on a different machine picks up that machine's default provider.
- **Single radio group.** `AppendProviderMenuItems` builds a *Select AI Provider* submenu with a `"Default"` entry first, then every registered provider. Each click unchecks siblings and invokes the callback so the host base can store the value and `ExpireSolution`.
- **Tolerant deserialization.** `ReadProvider` returns `true` even when the stored provider name no longer exists in the registry â€” it silently falls back to `"Default"`. Logged via `Debug.WriteLine` for diagnostics.
- **Type-safety on resolution.** `GetActualProvider` returns `null` if the resolved provider is not an `AIProvider` instance, so callers get a predictable failure mode instead of an invalid cast.

## Used by

- [AIProviderComponentBase](./AIProviderComponentBase.md) â€” calls `AppendProviderMenuItems` from `AppendAdditionalComponentMenuItems`, `GetActualProviderName` / `GetActualProvider` from the `IProviderComponent` accessors, and `WriteProvider` / `ReadProvider` from `Write` / `Read`.
- [ProviderComponentBase](./ProviderComponentBase.md) â€” same call sites; additionally fires its `OnProviderChanged()` hook from inside the menu callback.

## Related

- `IProviderComponent` â€” the interface both bases implement.
- `AIProviderComponentAttributes` â€” renders the provider badge using the resolved name.

## Metadata

- Source Code: See source repository.
- Since Version: 2.0.0
- Last Updated: 2026-07-21
- Documentation Maintainer: Marc Roca Musach

---


## Why Read This?

This document provides details about ProviderComponentHelper.


## End-User Guide

End-user guidance for ProviderComponentHelper.


## Developer Reference

Example usage:

`csharp
// Placeholder example
``r

`csharp
// Another placeholder example
``r


## Architecture & Design

Architecture and design notes for ProviderComponentHelper.

```csharp
// Example code for Developer Reference
```

```csharp
// Additional example for Developer Reference
```