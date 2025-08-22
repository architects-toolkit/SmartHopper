# Body, Metrics, Status, Return

Covers `AIBody`, `AIMetrics`, `AICallStatus`, and `AIReturn`.

## AIBody
- File: `AIBody.cs`
- Holds interactions and optional controls:
  - `ToolFilter` (default `- *` = disable all)
  - `ContextFilter` (default `- *` = disable)
  - `JsonOutputSchema` (enables JSON output inference)
- Context injection:
  - Getter for `Interactions` injects a synthesized `AIAgent.Context` message at index 0 when `ContextFilter` matches non-empty data from `AIContextManager.GetCurrentContext(filter)`
  - Original `_interactions` list is not mutated by this injection
- Metrics aggregation: `Metrics` sums per-interaction metrics
- Validation: `IsValid()` requires at least one interaction (after considering context injection)
- Helpers: many `Add*Interaction(...)`, `OverrideInteractions(...)`
- Tool utilities: `PendingToolCallsCount()`, `PendingToolCallsList()` match `AIInteractionToolCall.Id` to `AIInteractionToolResult.Id`

## AIMetrics
- File: `AIMetrics.cs`
- Fields: `Provider`, `Model`, token counters (input/output), `CompletionTime`, `FinishReason`
- Validation: non-empty `Provider`, `Model`, `FinishReason`; non-negative counters and time
- `Combine(other)` merges providers/models/reason and adds counters/time

## AICallStatus
- File: `AICallStatus.cs`
- States: `Idle`, `Processing`, `Streaming`, `CallingTools`, `Finished`
- Helpers: `.ToString()`, `.ToDescription()`, `FromString(string)`

## AIReturn and IAIReturn
- Files: `AIReturn.cs`, `IAIReturn.cs`
- `AIReturn` carries `Body`, `Request`, aggregated `Metrics`, `Status`, `ErrorMessage`, `Success`
- Validity: checks `Request.IsValid()`, `Metrics.IsValid()`, and that either `Body` or `ErrorMessage` is present
- Builders:
  - `CreateSuccess(AIBody body, IAIRequest? request = null)`
  - `CreateSuccess(List<IAIInteraction> interactions, IAIRequest? request = null, AIMetrics? metrics = null)`
  - `CreateSuccess(JObject raw, IAIRequest? request = null)` uses provider `Decode` to populate `Body`
  - `CreateError(string message, IAIRequest? request = null)`
- `SetBody(...)` overloads update `Body` directly
- `AIReturnExtensions.ToJObject(...)` maps selected fields from `AIReturn`, `Request`, `Metrics` via reflection; customizable mapping

## Notes: safety and performance
- Tools: filter with `AIBody.ToolFilter` to avoid unintended tool loading; validate tool args before execution
- JSON schema: ensure `JsonOutputSchema` is trusted/user-controlled (avoid TOCTOU injections)
- Context: avoid leaking sensitive data when enabling `ContextFilter`
- Reflection in `ToJObject(...)`: prefer caching mappings in hot paths
