# ConversationSession

Location: `src/SmartHopper.Infrastructure/AICall/Sessions/`

Purpose: Centralize multi-turn conversation orchestration with optional streaming. The session delegates provider calls to `AIRequestCall` and can stream incremental `AIReturn` deltas using provider-specific streaming adapters.

## Types

- `IConversationSession`
  - `AIRequestCall Request { get; }`
  - `Task<AIReturn> RunToStableResult(SessionOptions options, CancellationToken ct = default)`
  - `IAsyncEnumerable<AIReturn> Stream(SessionOptions options, StreamingOptions streamingOptions, CancellationToken ct = default)`
  - `void Cancel()`
- `IConversationObserver`
  - `OnStart(AIRequestCall request)`
  - `OnDelta(IAIInteraction interaction)`
  - `OnInteractionCompleted(IAIInteraction interaction)`
  - `OnToolCall(AIInteractionToolCall toolCall)`
  - `OnToolResult(AIInteractionToolResult toolResult)`
  - `OnFinal(AIReturn finalResult)`
  - `OnError(Exception error)`
- `SessionOptions`
  - `ProcessTools` (bool): process pending tool calls in the result.
  - `MaxTurns`, `MaxToolPasses`, `AllowParallelTools` (reserved for future phases)
  - `CancellationToken`
- `ConversationSession`
  - Orchestrates provider calls via `AIRequestCall.Exec()` for non-streaming and via provider streaming adapters for streaming, runs bounded turns/tool passes, and forwards lifecycle events to `IConversationObserver`.
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

## Streaming

When the selected provider/model supports streaming, use `Stream(...)` to consume incremental deltas:

```csharp
var streaming = new StreamingOptions
{
    CoalesceTokens = true,
    CoalesceDelayMs = 40,
    PreferredChunkSize = 24,
};

await foreach (var delta in session.Stream(options, streaming, ct))
{
    // Render partial UI, inspect delta.Body.Interactions, etc.
}
```

Notes:

- `ConversationSession.Stream(...)` gates streaming using request validation rules. If streaming is unsupported or disabled, an error `AIReturn` is yielded and the sequence ends.
- For the canonical and detailed streaming behavior (adapter probing, delta vs partial events, persistence timing, fallback, and tool passes), see `docs/Providers/AICall/Streaming.md`. This page intentionally summarizes to avoid duplication.

## Additional notes

- `Exec()` performs a single provider call. Use `ConversationSession` for orchestration and streaming.
- Policy pipeline hooks remain active in both streaming and non-streaming paths.
- See also: Tools overview and `AIToolCall` usage in [./tools.md](./tools.md).

