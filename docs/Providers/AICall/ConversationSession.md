# ConversationSession (Phase 1: non-streaming)

Location: `src/SmartHopper.Infrastructure/AICall/Sessions/`

Purpose: Centralize multi-turn conversation orchestration. Phase 1 introduces a minimal, non-streaming session that delegates to `AIRequestCall` while setting the stage for future streaming and policy pipeline integration.

## Types

- `IConversationSession`
  - `AIRequestCall Request { get; }`
  - `Task<AIReturn> RunToStableResult(SessionOptions options, CancellationToken ct = default)`
  - `void Cancel()`
- `IConversationObserver` (non-streaming callbacks for now)
  - `OnStart(AIRequestCall request)`
  - `OnPartial(AIReturn delta)`
  - `OnToolCall(AIInteractionToolCall toolCall)`
  - `OnToolResult(AIInteractionToolResult toolResult)`
  - `OnFinal(AIReturn finalResult)`
  - `OnError(Exception error)`
- `SessionOptions`
  - `ProcessTools` (bool): process pending tool calls in the result.
  - `MaxTurns`, `MaxToolPasses`, `AllowParallelTools` (reserved for future phases)
  - `CancellationToken`
- `ConversationSession`
  - Minimal implementation. For Phase 1 it calls `AIRequestCall.Exec()` for provider calls, runs bounded turns/tool passes, and forwards lifecycle events to `IConversationObserver`.
  - Tool execution
    - Pending tool calls (`AIInteractionToolCall`) are executed via the Tool Manager during tool passes.
    - For executing exactly one pending tool call directly, see `AIToolCall` in `src/SmartHopper.Infrastructure/AICall/Tools/AIToolCall.cs` and the Tools docs.

## Usage

```csharp
// Explicit session (recommended for tools/multi-turn)
var session = new ConversationSession(req);
var options = new SessionOptions
{
    ProcessTools = true,
    MaxTurns = 3,
    MaxToolPasses = 2,
};
var result = await session.RunToStableResult(options);
```

Notes:

- `Exec()` performs a single provider call. Use `ConversationSession` for orchestration.
- `ExecCore` preserves the original single-turn execution path used as fallback.
- Future phases will add streaming adapters and policy pipeline hooks.
- See also: Tools overview and `AIToolCall` usage in [./tools.md](./tools.md).
