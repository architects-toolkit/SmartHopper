# ProviderComponentHelper

> **DEPRECATED** — This class has been replaced by `ProviderSelectionCore`. Please see [ProviderSelectionCore.md](./ProviderSelectionCore.md) for current documentation.
>
> The `ProviderComponentHelper` class described here no longer exists in the source code. It has been replaced by `ProviderSelectionCore` (instance-owned, located at `src/SmartHopper.Core/ComponentBase/Cores/ProviderSelectionCore.cs`).

This documentation has been moved to [ProviderSelectionCore.md](./ProviderSelectionCore.md).

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `DEPRECATED - Replaced by ProviderSelectionCore` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

This page exists for backwards compatibility and to preserve historical context about how provider menus were originally implemented. All new code should use `ProviderSelectionCore` instead.

**You should read this if you:**

- Are migrating an older component from `ProviderComponentHelper` to `ProviderSelectionCore`.
- Need to understand the evolution of the provider-selection architecture in SmartHopper.
- Encounter a legacy reference to `ProviderComponentHelper` in an old branch or fork.

---

## End-User Guide

This documentation has been moved to [ProviderSelectionCore.md](./ProviderSelectionCore.md).

The `ProviderComponentHelper` class described here no longer exists in the source code. It has been replaced by `ProviderSelectionCore` (instance-owned, located at `src/SmartHopper.Core/ComponentBase/Cores/ProviderSelectionCore.cs`).

**Please update your bookmarks to:** [ProviderSelectionCore.md](./ProviderSelectionCore.md)

---

## Developer Reference

### Migration from ProviderComponentHelper to ProviderSelectionCore

```csharp
// OLD (ProviderComponentHelper)
// _helper = new ProviderComponentHelper(this);
// _helper.BuildMenu();

// NEW (ProviderSelectionCore)
_core = new ProviderSelectionCore(this);
_core.BuildMenu();

```

### Legacy serialization pattern

```csharp
// ProviderComponentHelper previously handled:
//   writer.SetString("Provider", _selectedProviderName);
//   _selectedProviderName = reader.GetString("Provider");
// ProviderSelectionCore now owns the same logic internally.

```

---

## Architecture & Design

`ProviderComponentHelper` was a static/static-like helper that built the right-click provider menu and managed name serialization. Its responsibilities were:

- Build the `"Default"` + registered providers submenu.
- Persist `SelectedProviderName` in `GH_IWriter`/`GH_IReader`.
- Resolve the effective provider through `ProviderManager`.

These duties have been consolidated into `ProviderSelectionCore`, which is instance-owned and lives in `src/SmartHopper.Core/ComponentBase/Cores/ProviderSelectionCore.cs`. The move from a helper class to a core class improves testability and allows per-instance state isolation.
