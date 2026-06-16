# AICall (Requests, Interactions, Returns)

Documents the core request/response flow used by providers and tools.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Infrastructure/AICall/` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

This page is the entry point for understanding how SmartHopper structures AI requests, handles interactions, and returns results. It explains the fundamental pipeline from building an `AIBody` to receiving an `AIReturn`, and when to use single-call execution versus session-based orchestration.

**You should read this if you:**

- Are new to the SmartHopper AI provider internals
- Need to decide between `Exec()` and `ConversationSession` for your use case
- Want to understand how interactions, requests, and returns fit together

---

## End-User Guide

### Quick Flow

1. Build interactions (system/user/context/tool)
2. Put them in `AIBody` (optionally set `ToolFilter`, `ContextFilter`, `JsonOutputSchema`)
3. Initialize an `AIRequestCall` with provider, model, endpoint, capability
4. `Exec()` -> single provider call -> `AIReturn` (no orchestration)
5. For tools/multi-turn, use `ConversationSession.RunToStableResult(options)`; it orchestrates provider calls and appends tool results to the session `AIBody`. For streaming, use `ConversationSession.Stream(options, streamingOptions)` to receive incremental `AIReturn` deltas

### Choosing Exec vs ConversationSession

- Use `Exec()` when:
  - You need a single provider call
  - No tool orchestration is required
  - You want the simplest path to an `AIReturn`

- Use `ConversationSession` when:
  - Tools may be called (e.g., `Body.ToolFilter` is set)
  - Multi-turn interaction or tool pass loops are expected
  - You need observer callbacks or tighter control via `SessionOptions` (e.g., `ProcessTools`, `MaxTurns`, `MaxToolPasses`)

Note: Prefer creating an explicit `ConversationSession` when you need deterministic orchestration settings.

---

## Developer Reference

### Building Blocks

- Location: `src/SmartHopper.Infrastructure/AICall/`
- Building blocks:
  - `IAIInteraction` + concrete types for messages and tool I/O
  - `IAIRenderInteraction` (render contract for UI)
  - `IAIKeyedInteraction` (stable keys for streaming aggregation and de-duplication)
  - `AIBody` container (interactions, tool/context filters, JSON schema)
  - `IAIRequest` + `AIRequestBase`, `AIRequestCall`
  - `IAIReturn` + `AIReturn`
  - `AIMetrics` and `AICallStatus`
  - `AIAgent` roles (Context, System, User, Assistant, ToolCall, ToolResult)
  - `PolicyPipeline` (always-on request/response policies: validation, decoding, normalization)

### Example: Single Provider Call with Exec

```csharp
// Build body with interactions
var body = new AIBodyBuilder()
    .Add(systemPrompt)
    .Add(userMessage)
    .Build();

// Create request
var request = new AIRequestCall(provider, model, capability, body);

// Execute single call
var result = await request.Exec();

```

### Example: Session-Based Orchestration

```csharp
// Create a session for multi-turn / tool support
var session = new ConversationSession(request);
var options = new SessionOptions
{
    ProcessTools = true,
    MaxTurns = 5,
};

// Run to completion
var result = await session.RunToStableResult(options);

```

---

## Architecture & Design

### Navigation

- Interactions: [./interactions.md](./interactions.md)
- Tools: [./tools.md](./tools.md)
- Requests: [./requests.md](./requests.md)
- Policy Pipeline: [./policy-pipeline.md](./policy-pipeline.md)
- Body, Metrics, Status, Return: [./body-metrics-status.md](./body-metrics-status.md)
- Messages and Aggregation: [./messages.md](./messages.md)
- Conversation Session: [./ConversationSession.md](./ConversationSession.md)
- Streaming: [./Streaming.md](./Streaming.md)

Notes:

- UI consumers (e.g., WebChat) use `IAIRenderInteraction` to render messages and `IAIKeyedInteraction` to aggregate streaming deltas by `GetStreamKey()` and persist final items by `GetDedupKey()` (see Streaming for re-keying details).


