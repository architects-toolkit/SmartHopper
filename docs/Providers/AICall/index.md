# AICall (Requests, Interactions, Returns)

This section documents the core request/response flow used by providers and tools.

- Location: `src/SmartHopper.Infrastructure/AICall/`
- Building blocks:
  - `IAIInteraction` + concrete types for messages and tool I/O
  - `AIBody` container (interactions, tool/context filters, JSON schema)
  - `IAIRequest` + `AIRequestBase`, `AIRequestCall`, `AIToolCall`
  - `IAIReturn` + `AIReturn`
  - `AIMetrics` and `AICallStatus`
  - `AIAgent` roles (Context, System, User, Assistant, ToolCall, ToolResult)

Quick flow:

1) Build interactions (system/user/context/tool)
2) Put them in `AIBody` (optionally set `ToolFilter`, `ContextFilter`, `JsonOutputSchema`)
3) Initialize an `AIRequestCall` with provider, model, endpoint, capability
4) `Exec()` -> provider `Call()` -> `AIReturn`
5) Optionally process pending tool calls with `AIToolCall` and append results

Navigation:

- Interactions: [./interactions.md](./interactions.md)
- Requests: [./requests.md](./requests.md)
- Body, Metrics, Status, Return: [./body-metrics-status.md](./body-metrics-status.md)
 - Messages and Aggregation: [./messages.md](./messages.md)
