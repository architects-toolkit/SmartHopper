# AIProviderComponentBase

Base for components that must let the user pick an AI provider/model and persist that choice.

## Purpose

Expose provider selection via context menu and store the selection so derived components can query the active provider.

## Key features

- Context menu for provider selection.
- Persists selected provider id/name with the component.
- Integrates with the provider manager to obtain the actual provider instance.
- Works with custom attributes to show a provider badge.

## Usage

- Derive when your component talks to an AI provider.
- Query the current provider through the base rather than caching your own reference.
- Validate model capabilities before executing provider calls.

## Related

- [AIComponentAttributes](../Helpers/AIComponentAttributes.md) – draws a provider logo/badge on the component.
- [AIStatefulAsyncComponentBase](./StatefulAsyncComponentBase.md) – combines this base with the async state machine.
