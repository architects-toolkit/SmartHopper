# AIStatefulAsyncComponentBase

Combines provider selection with the stateful async execution model for AI‑powered components.

## Purpose

Offer a turnkey base to build AI components: choose provider/model, build a request, execute, and surface metrics/messages while following the component state machine.

## Key features

- Builds on [AIProviderComponentBase](./AIProviderComponentBase.md) and [StatefulComponentBase](./StatefulComponentBase.md) to add a `Model` input and a `Metrics` output.
- Capability‑aware model selection via `RequiredCapability` and `UsingAiTools`, delegating to provider `SelectModel()` / `ModelManager.SelectBestModel`.
- `CallAiToolAsync` helper that injects provider/model into AI Tools, executes them, and stores the last `AIReturn` snapshot.
- Centralized metrics output (JSON with provider, model, tokens, completion time, data/iteration counts).
- Surfaces structured provider/tool diagnostics from `AIReturn.Messages` as persistent Grasshopper runtime messages.
- Integrates with `ComponentBadgesAttributes` by maintaining a cached badge state (Verified/Deprecated/Invalid/Replaced/Not‑recommended models).

## Usage

- Derive when your component sends prompts/requests to an AI provider (typically by calling AI Tools or an `AIRequestCall`).
- In your async worker, call the AI provider/tool and either use `CallAiToolAsync` (recommended) or call `SetAIReturnSnapshot` yourself so metrics and badges see the final `AIReturn`.
- Override `RequiredCapability` (and optionally `UsingAiTools`) so model selection, validation, and badges use the correct capability flags.

## Related

- [AIProviderComponentBase](./AIProviderComponentBase.md) – provider UI/persistence.
- [StatefulComponentBase](./StatefulComponentBase.md) – stateful execution foundation.
