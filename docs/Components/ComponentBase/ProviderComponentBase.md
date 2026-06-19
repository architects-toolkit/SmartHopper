# ProviderComponentBase

Non-async, non-stateful counterpart to [AIProviderComponentBase](./AIProviderComponentBase.md). Inherits directly from `GH_Component` and implements `IProviderComponent`. Delegates the menu and serialization to [ProviderSelectionCore](./ProviderSelectionCore.md). Used by lightweight provider-aware components (e.g. listing models, showing settings) that do not need the async/state machinery.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Core.Grasshopper/ComponentBase/ProviderComponentBase.cs` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

If you need a Grasshopper component that lets users pick an AI provider but does not perform async AI calls itself, `ProviderComponentBase` gives you the selection UI, badge and persistence without the overhead of the full async/stateful stack.

**You should read this if you:**

- Are building a lightweight helper component that only needs provider selection (e.g. model listers, settings panels).
- Want the same `"Default"` sentinel and provider badge behaviour as `AIProviderComponentBase` without `AsyncComponentBase`/`StatefulComponentBase`.
- Need to understand how provider menus are wired in synchronous components.

---

## End-User Guide

### Purpose

Provide the same provider-selection menu, badge and persistence as `AIProviderComponentBase` without dragging in `AsyncComponentBase`/`StatefulComponentBase`.

### Design criteria

- **Same `"Default"` sentinel** as `AIProviderComponentBase`, resolved through `ProviderManager`.
- **`OnProviderChanged()` hook.** Virtual; called inside the menu callback after the new provider is stored, before `ExpireSolution`.
- **Tooltip template.** Sets `AIProviderComponentAttributes.ProviderTooltipTemplate = "Settings for %provider%"` so the badge tooltip reflects the menu's intent.

### When to derive

- Synchronous helper components that need the provider selection UI but no async work.
- Anything async or stateful should use [AIProviderComponentBase](./AIProviderComponentBase.md) instead.

---

## Developer Reference

### Key members

- `string SelectedProviderName`, `void SetSelectedProviderName(string)`
- `string GetActualAIProviderName()`, `AIProvider GetActualAIProvider()`
- `bool HasProviderChanged()`
- `protected virtual void OnProviderChanged()`

### Minimal synchronous provider component

```csharp
public class MySettingsComponent : ProviderComponentBase
{
    public MySettingsComponent()
        : base("MySettings", "MS", "Shows settings for the selected provider.", "SmartHopper", "Provider")
    { }

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        // No inputs
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddTextParameter("Provider Name", "P", "Selected provider name", GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess DA)
    {
        var provider = GetActualAIProvider();
        DA.SetData(0, provider?.Name ?? "None");
    }
}

```

### Reacting to provider changes

```csharp
protected override void OnProviderChanged()
{
    // Re-query provider-specific defaults when the user switches providers
    _cachedModels = null;
    ExpireSolution(true);
}

```

---

## Architecture & Design

`ProviderComponentBase` sits between raw `GH_Component` and the full AI async stack. It inherits `GH_Component` directly and only mixes in provider selection through `IProviderComponent`. This keeps the inheritance tree shallow for components that do not need state machines, async workers or progress tracking.

Menu building and provider name serialization are delegated to `ProviderSelectionCore`. The base class itself is a thin wrapper: it stores the selected name, offers the `"Default"` sentinel resolution via `ProviderManager`, and exposes the `OnProviderChanged` virtual hook so subclasses can refresh cached data when the provider changes.
