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
4) `Exec()` -> single provider call -> `AIReturn` (no orchestration)
5) For tools/multi‑turn, use `ConversationSession.RunToStableResult(options)`; it orchestrates provider calls and appends tool results to the session `AIBody`

## Choosing Exec vs ConversationSession

- Use `Exec()` when:
  - You need a single provider call
  - No tool orchestration is required
  - You want the simplest path to an `AIReturn`

- Use `ConversationSession` when:
  - Tools may be called (e.g., `Body.ToolFilter` is set)
  - Multi‑turn interaction or tool pass loops are expected
  - You need observer callbacks or tighter control via `SessionOptions` (e.g., `ProcessTools`, `MaxTurns`, `MaxToolPasses`)

Note: Prefer creating an explicit `ConversationSession` when you need deterministic orchestration settings.

Navigation:

- Interactions: [./interactions.md](./interactions.md)
- Requests: [./requests.md](./requests.md)
- Body, Metrics, Status, Return: [./body-metrics-status.md](./body-metrics-status.md)
- Messages and Aggregation: [./messages.md](./messages.md)
- Conversation Session: [./ConversationSession.md](./ConversationSession.md)
