# AIProviderComponentBase

`src/SmartHopper.Core/ComponentBase/AIProviderComponentBase.cs`

Async + stateful base that adds an AI provider selection menu to [StatefulComponentBase](./StatefulComponentBase.md). Implements [`IProviderComponent`](./index.md). Delegates the menu and serialization to [ProviderComponentHelper](./ProviderComponentHelper.md).

---

Let the user pick an AI provider per component (or fall back to the system default), persist the choice across save/load, and expose the resolved provider to derived classes.

## Design criteria

- **`"Default"` is a sentinel.** Stored as the literal string `"Default"` (`ProviderComponentHelper.DEFAULT_PROVIDER`); resolved at use-time through `ProviderManager.GetDefaultAIProvider()`. This keeps documents portable across machines with different default providers.
- **Provider change is a real input change.** `InputsChanged()` is overridden to add `"AIProvider"` to the list whenever `HasProviderChanged()` is true, so the state machine treats it like any other input change. `StatefulComponentBase` further forces `NeedsRun` for that input.
- **Custom attributes.** `CreateAttributes()` installs `AIProviderComponentAttributes`, which renders the provider logo and the small status badge. Derived classes that need a richer overlay (e.g. AI components with model badges) override this.

## Key members

- `string SelectedProviderName` — the literal stored value (may be `"Default"`).
- `string GetActualAIProviderName()` — resolves `"Default"` through `ProviderManager`.
- `AIProvider GetActualAIProvider()` — the concrete provider instance, or `null`.
- `bool HasProviderChanged()` — true exactly once after the user changed the selection (clears the previous-selection cache as a side effect).
- `void SetSelectedProviderName(string name)`.

## Persistence

Provider name is written through `ProviderComponentHelper.WriteProvider/ReadProvider` under the `"AIProvider"` key. If a saved provider is no longer available, the read silently falls back to `"Default"`.

## When to derive

- You need provider selection but **not** the AI request orchestration (Settings input, Metrics output, badges, batch). For those use [AIStatefulAsyncComponentBase](./AIStatefulAsyncComponentBase.md).
- For non-async provider components (e.g. `AIModels` listing) use [ProviderComponentBase](./ProviderComponentBase.md).

This base class is the foundation for any component that needs an AI provider selection menu, persisted across save/load, without the full AI request orchestration stack. If you are building a custom component that needs provider selection, this documentation explains the contract and persistence model.

- [ProviderComponentBase](./ProviderComponentBase.md)
- [AIStatefulAsyncComponentBase](./AIStatefulAsyncComponentBase.md)
- `IProviderComponent`, `ProviderComponentHelper`, `AIProviderComponentAttributes`
