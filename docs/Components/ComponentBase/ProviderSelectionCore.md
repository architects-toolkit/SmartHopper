# ProviderSelectionCore

`src/SmartHopper.Core/ComponentBase/Cores/ProviderSelectionCore.cs`

Instance-owned helper that manages AI provider selection state on a single component. Replaces the legacy static `ProviderComponentHelper` with a composition-based approach: the component owns a core instance and delegates menu rendering, persistence, resolution, and change detection to it.

## Why it exists

The composition pattern (instance-owned core) provides:

- **Idempotent change detection** — `HasPendingChange` is a pure query; only `CommitChange()` mutates state.
- **Event-driven updates** — `ProviderChanged` event fires when the user picks a new provider through the context menu, allowing the component to expire its solution and trigger state transitions.
- **Centralized persistence** — `Write()` and `Read()` handle serialization to/from Grasshopper files with fallback to `"Default"` if a stored provider is no longer registered.
- **Lazy resolution** — `"Default"` resolves to the machine's default provider at use-time, making documents portable across machines.

## Public API

```csharp
public sealed class ProviderSelectionCore
{
    public const string DEFAULT_PROVIDER = "Default";

    public ProviderSelectionCore(GH_Component owner);

    public string CurrentProvider { get; }
    public void SetCurrentProvider(string providerName);

    public bool HasPendingChange { get; }
    public void CommitChange();

    public string GetActualProviderName();
    public AIProvider GetActualProvider();

    public void AppendMenuItems(ToolStripDropDown menu);
    public bool Write(GH_IWriter writer);
    public bool Read(GH_IReader reader);

    public event EventHandler ProviderChanged;
}
```

## Key behaviors

### Sentinel value

- **`"Default"` is portable.** Stored verbatim in `.gh` files; resolved lazily through `ProviderManager.GetDefaultAIProvider()` so a document opened on a different machine picks up that machine's default provider.

### Menu rendering

- **Single radio group.** `AppendMenuItems()` builds a *Select AI Provider* submenu with a `"Default"` entry first, then every registered provider. Each click unchecks siblings, updates `CurrentProvider`, raises `ProviderChanged`, and calls `owner.ExpireSolution(true)`.

### Change detection

- **Idempotent query.** `HasPendingChange` returns `true` if the current selection differs from the last committed value. Safe to call any number of times per solve without side effects.
- **Explicit commit.** `CommitChange()` acknowledges the pending change by advancing the commit baseline to the current selection. This is typically called after the component has finished processing.

### Persistence

- **Tolerant deserialization.** `Read()` returns `true` even when the stored provider name no longer exists in the registry — it silently falls back to `"Default"`. Logged via `Debug.WriteLine` for diagnostics.
- **Automatic alignment.** After a successful read, the committed baseline is aligned with the restored value so the first solve after load does not report a phantom change.

### Resolution

- **Type-safe resolution.** `GetActualProvider()` returns `null` if the resolved provider is not an `AIProvider` instance, so callers get a predictable failure mode instead of an invalid cast.
- **Lazy resolution.** `GetActualProviderName()` resolves `"Default"` to the concrete default-provider name at the moment of the call.

## Used by

- [AIProviderComponentBase](./AIProviderComponentBase.md) — creates a `ProviderSelectionCore` instance and delegates menu, persistence, and resolution to it.
- [ProviderComponentBase](./ProviderComponentBase.md) — same pattern; additionally fires its `OnProviderChanged()` hook when `ProviderChanged` is raised.

## Related

- `IProviderComponent` — the interface both bases implement.
- `AIProviderComponentAttributes` — renders the provider badge using the resolved name.
- `ProviderManager` — global registry of available providers.
