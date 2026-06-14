# AIProviderComponentBase

Async + stateful base that adds an AI provider selection menu to [StatefulComponentBase](./StatefulComponentBase.md). Implements [`IProviderComponent`](./index.md). Delegates the menu and serialization to [ProviderSelectionCore](./ProviderSelectionCore.md).

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Core.Grasshopper/ComponentBase/AIProviderComponentBase.cs` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

This base class is the foundation for any component that needs an AI provider selection menu, persisted across save/load, without the full AI request orchestration stack. If you are building a custom component that needs provider selection, this documentation explains the contract and persistence model.

**You should read this if you:**

- Are building a new component that requires AI provider selection
- Need to understand how provider names are persisted and resolved
- Want to know how provider changes trigger the state machine re-execution

---

## End-User Guide

Let the user pick an AI provider per component (or fall back to the system default), persist the choice across save/load, and expose the resolved provider to derived classes.

### When to derive

- You need provider selection but **not** the AI request orchestration (Settings input, Metrics output, badges, batch). For those use [AIStatefulAsyncComponentBase](./AIStatefulAsyncComponentBase.md).
- For non-async provider components (e.g. `AIModels` listing) use [ProviderComponentBase](./ProviderComponentBase.md).

---

## Developer Reference

### Key members

```csharp
string SelectedProviderName { get; }              // the literal stored value (may be "Default")
string GetActualAIProviderName();                // resolves "Default" through ProviderManager
AIProvider GetActualAIProvider();                // the concrete provider instance, or null
bool HasProviderChanged();                       // true exactly once after user changed selection
void SetSelectedProviderName(string name);

```

### Persistence

```csharp
// Provider name is written through ProviderSelectionCore under the "AIProvider" key:
// WriteProvider(writer, SelectedProviderName)
// ReadProvider(reader) -> returns the stored name or "Default" if unavailable

// Example override in a derived component:
protected override void WriteAdditionalData(GH_IWriter writer)
{
    base.WriteAdditionalData(writer);
    // ProviderSelectionCore handles the actual "AIProvider" key serialization
}

```

---

## Architecture & Design

- **`"Default"` is a sentinel.** Stored as the literal string `"Default"` (`ProviderSelectionCore.DEFAULT_PROVIDER`); resolved at use-time through `ProviderManager.GetDefaultAIProvider()`. This keeps documents portable across machines with different default providers.
- **Provider change is a real input change.** `InputsChanged()` is overridden to add `"AIProvider"` to the list whenever `HasProviderChanged()` is true, so the state machine treats it like any other input change. `StatefulComponentBase` further forces `NeedsRun` for that input.
- **Custom attributes.** `CreateAttributes()` installs `AIProviderComponentAttributes`, which renders the provider logo and the small status badge. Derived classes that need a richer overlay (e.g. AI components with model badges) override this.

### Related

- [ProviderComponentBase](./ProviderComponentBase.md)
- [AIStatefulAsyncComponentBase](./AIStatefulAsyncComponentBase.md)
- `IProviderComponent`, `ProviderSelectionCore`, `AIProviderComponentAttributes`
