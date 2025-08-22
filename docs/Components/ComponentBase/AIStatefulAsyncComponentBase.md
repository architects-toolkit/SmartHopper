# AIStatefulAsyncComponentBase

Combines provider selection with the stateful async execution model for AI‑powered components.

## Purpose

Offer a turnkey base to build AI components: choose provider/model, build a request, execute, and surface metrics/messages while following the component state machine.

## Key features

- Provider and model selection from [AIProviderComponentBase](./AIProviderComponentBase.md).
- Stateful execution: debouncing, Run handling, progress, and errors.
- Common AI pipeline helpers (build request, execute, capture metrics).
- Plays well with capability validation to avoid unsupported calls.

## Usage

- Derive when your component sends prompts/requests to an AI service.
- In your worker, construct the request body, call the provider, and map outputs to GH types.
- Check model capabilities required by your tool before executing.

## Related

- [AIProviderComponentBase](./AIProviderComponentBase.md) – provider UI/persistence.
- [StatefulAsyncComponentBase](./StatefulAsyncComponentBase.md) – async state machine foundation.
