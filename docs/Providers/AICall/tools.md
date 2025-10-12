# Tools (AIToolCall, Tool Interactions, Orchestration)

This page documents how tool calls are represented, executed, and orchestrated within AICall.

- Location: `src/SmartHopper.Infrastructure/AICall/`
- Related files:
  - Tool request wrapper: `src/SmartHopper.Infrastructure/AICall/Tools/AIToolCall.cs`
  - Interactions: `src/SmartHopper.Infrastructure/AICall/Core/Interactions/AIInteractionToolCall.cs`, `AIInteractionToolResult.cs`
  - Session orchestration: `src/SmartHopper.Infrastructure/AICall/Sessions/ConversationSession.cs`

## Concepts

- A tool call is proposed by the model as an interaction of type `AIInteractionToolCall`.
- Executing a tool produces an `AIInteractionToolResult` interaction with a `Result` payload (JSON) and any structured runtime messages.
- Tool availability is controlled by `AIBody.ToolFilter`:
  - Default is `-*` (disable all tools)
  - Set to `*` to allow all registered tools, or to a glob pattern to allow a subset (e.g., `script_*`).
- For single provider calls, `AIRequestCall.Exec()` does not execute tools. Use `ConversationSession` to orchestrate multi‑turn flows and tool passes.

## AIToolCall (single tool execution)

- File: `src/SmartHopper.Infrastructure/AICall/Tools/AIToolCall.cs`
- Purpose: execute exactly one pending tool call contained in the provided `AIBody` and return a result body consisting of a single `AIInteractionToolResult`.
- Validation:
  - Requires a single `AIInteractionToolCall` pending in `Body` (1 call, 0 results for that call `Id`).
  - Validates tool availability against `Body.ToolFilter`.
- Execution:
  - Delegates to the Tool Manager to run the tool and collect a JSON result plus runtime messages.
  - Produces an `AIReturn` whose `Body` contains the corresponding `AIInteractionToolResult`.

### Example: executing a single tool call

```csharp
// Assume you have a pending tool call interaction (e.g., produced by a provider)
var toolCall = new AIInteractionToolCall
{
    Id = "call_123",
    Name = "script_new",
    Arguments = new JObject { ["prompt"] = "Create a 10x10 grid" }
};

var body = new AIBody
{
    ToolFilter = "*" // enable tools you intend to allow
};
body.AddInteraction(toolCall);

var call = new AIToolCall
{
    Body = body
};

var result = await call.Exec();
if (!result.Success)
{
    // Handle errors - all error details are in result.Messages collection
    foreach (var msg in result.Messages.Where(m => m.Severity == AIRuntimeMessageSeverity.Error))
    {
        Console.WriteLine($"[{msg.Origin}] {msg.Message}");
    }
}
else
{
    // The output body contains an AIInteractionToolResult
    var toolResult = result.Body.GetLastInteraction(AIAgent.ToolResult) as AIInteractionToolResult;
}
```

Notes:

- Prefer `ConversationSession` for multi‑turn flows where the provider may produce tool calls across turns.
- `AIToolCall` is a focused API for executing a single pending tool call when you already have one.

## ConversationSession tool loop (recommended)

- File: `src/SmartHopper.Infrastructure/AICall/Sessions/ConversationSession.cs`
- Behavior:
  - Runs `AIRequestCall.Exec()` to obtain a provider response.
  - If `SessionOptions.ProcessTools` is true and the result contains pending tool calls, iterates tool passes:
    - Executes each pending tool call (delegating to the Tool Manager).
    - Appends `AIInteractionToolResult` interactions to the session `AIBody`.
    - Performs another provider call with updated context until a stable result is reached or bounds are hit (`MaxTurns`, `MaxToolPasses`).
- Observability: `IConversationObserver` receives `OnToolCall` and `OnToolResult` callbacks to render progress (e.g., in UI).

## Runtime messages and aggregation

- Tool wrapper/runtime diagnostics should attach structured messages to the `AIInteractionToolResult` or `AIReturn`.
- Aggregation is centralized:
  - `AIBody.Messages` includes messages from tool results and body validation.
  - `AIReturn.Messages` aggregates request, body, and error mirror.
- See: `docs/Providers/AICall/messages.md` for examples and guidance.

## Safety and capability notes

- Keep `ToolFilter` restrictive; enable only the tools you need for the task.
- Ensure tool argument validation and defensive coding in tool implementations.
- Providers may infer `FunctionCalling` capability when tools are enabled via `AIRequestCall.IsValid()`; verify capability selection and errors through structured runtime messages.

## See also

- Interactions: `AIInteractionToolCall`, `AIInteractionToolResult` — `docs/Providers/AICall/interactions.md`
- Conversation orchestration — `docs/Providers/AICall/ConversationSession.md`
- Requests (single provider call) — `docs/Providers/AICall/requests.md`
- Message aggregation — `docs/Providers/AICall/messages.md`
