# ProviderSelectionCore

Instance-owned helper that manages AI provider selection state, menu rendering, persistence, and change detection for a single component. Used by both [AIProviderComponentBase](./AIProviderComponentBase.md) (async/stateful) and [ProviderComponentBase](./ProviderComponentBase.md) (sync) through composition rather than static helpers.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Core/ComponentBase/Cores/ProviderSelectionCore.cs` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-13 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

The two provider bases inherit from different ancestors (`StatefulComponentBase` vs `GH_Component`) and therefore cannot share code through inheritance. This core provides a composition-based approach where each component owns an instance of the core.

**You should read this if you:**

- Are building a component that needs provider selection functionality
- Need to understand how provider state is persisted per component
- Want to react to provider changes in your component

---

## End-User Guide

### What Does This Do?

ProviderSelectionCore manages the "Select AI Provider" menu that appears when you right-click a SmartHopper component. It:

- Tracks which provider is currently selected for each component
- Renders the menu with all available providers
- Persists the selection across Grasshopper sessions
- Notifies when the provider changes

### How It Works

When you right-click a component:

1. The core builds a submenu with all registered providers
2. Your current selection is marked with a checkmark
3. When you select a different provider, the core updates the state
4. The component is notified and re-solves with the new provider

---

## Developer Reference

### Public API

```csharp
public sealed class ProviderSelectionCore
{
    public string CurrentProvider { get; set; }
    public event EventHandler ProviderChanged;

    public ProviderSelectionCore(
        GH_Component owner,
        Func<AIProviderData> getter,
        Action<AIProviderData> setter);

    public void AppendMenu(ToolStripDropDownMenu menu);
    public void Commit(string providerId);
    public bool HasChanged(string candidate);
}

```

### Key Methods

| Method | Parameters | Returns | Purpose |
| --- | --- | --- | --- |
| `Commit` | `string providerId` | `void` | Updates current provider and raises `ProviderChanged` |
| `HasChanged` | `string candidate` | `bool` | Checks if candidate differs from current (idempotent) |
| `AppendMenu` | `ToolStripDropDownMenu menu` | `void` | Adds provider submenu to right-click menu |

### Code Examples

#### Creating a ProviderSelectionCore

```csharp
// In your component's constructor
public MyComponent()
{
    this.providerCore = new ProviderSelectionCore(
        this,
        () => this.providerData,
        data => this.providerData = data);
    
    // Subscribe to provider changes
    this.providerCore.ProviderChanged += (s, e) =>
    {
        this.AddRuntimeMessage(
            GH_RuntimeMessageLevel.Remark,
            $"Provider changed to: {this.providerCore.CurrentProvider}");
    };
}

```

#### Appending the Menu

```csharp
// In your component's AppendAdditionalComponentMenuItems override
protected override void AppendAdditionalComponentMenuItems(ToolStripDropDownMenu menu)
{
    base.AppendAdditionalComponentMenuItems(menu);
    this.providerCore.AppendMenu(menu);
}

```

### Menu Integration

The core appends a "Select AI Provider" submenu with:

- A `"Default"` entry first (represents the global default)
- All registered providers listed alphabetically
- Radio-button style selection (single selection)
- Provider name badges for visual identification

When the user selects an entry:

1. `CurrentProvider` is updated
2. `ProviderChanged` event is raised
3. Owner component's `ExpireSolution(true)` is called

### Persistence Format

- Key: `PersistenceKeys.SelectedProvider` (string: `"AIProvider"`)
- Value: Selected provider ID (e.g., `"OpenAI"`, `"MistralAI"`, `"Anthropic"`)
- Storage: Per-component, persisted via GH archiver

---

## Architecture & Design

### Design Rationale

**Problem**: Two provider bases (`AIProviderComponentBase` and `ProviderComponentBase`) need the same provider selection logic but inherit from different ancestors.

**Approach**: Composition over inheritance. Each component owns a `ProviderSelectionCore` instance.

**Trade-offs**:

- **Benefit**: Code reuse without forcing a common base class
- **Benefit**: Per-component state isolation
- **Cost**: Slightly more memory (one instance per component)

### System Relationships

```text
[AIProviderComponentBase] ──owns──> [ProviderSelectionCore] <──owns── [ProviderComponentBase]
                                        │
                                        v
                              [ProviderManager] (global registry)

```

### Related Documentation

- [AIProviderComponentBase](./AIProviderComponentBase.md) -- async/stateful provider base
- [ProviderComponentBase](./ProviderComponentBase.md) -- sync provider base
- [ProviderManager](../../Providers/ProviderManager.md) -- global provider registry
