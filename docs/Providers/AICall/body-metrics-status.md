# Body, Metrics, Status, Return

Covers `AIBody`, `AIMetrics`, `AICallStatus`, and `AIReturn`.

## AIBody

- File: `AIBody.cs`
- Holds interactions and optional controls:
  - `ToolFilter` (default `-*` = disable all)
  - `ContextFilter` (default `-*` = disable)
  - `JsonOutputSchema` (enables JSON output inference)
- Context injection:
  - Getter for `Interactions` injects a synthesized `AIAgent.Context` message at index 0 when `ContextFilter` matches non-empty data from the active AI context
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
  - `CreateSuccess(JObject raw, IAIRequest? request = null)` stores raw provider JSON; decoding to interactions is handled by response policies
  - `CreateError(string message, IAIRequest? request = null)`
  - `CreateProviderError(string rawMessage, IAIRequest? request = null)` and `CreateNetworkError(...)` add standardized structured messages while preserving raw error text
- `SetBody(...)` overloads update `Body` directly
- `AIReturnExtensions.ToJObject(...)` maps selected fields from `AIReturn`, `Request`, `Metrics` via reflection; customizable mapping

Policy notes:

- Always-on `PolicyPipeline` applies request policies before provider execution and response policies after execution. Response policies decode/normalize raw JSON into interactions and add diagnostics via `AIReturn.AddRuntimeMessage(...)`.

## Notes: safety and performance

- Tools: filter with `AIBody.ToolFilter` to avoid unintended tool loading; validate tool args before execution
- JSON schema: ensure `JsonOutputSchema` is trusted/user-controlled (avoid TOCTOU injections)
- Context: avoid leaking sensitive data when enabling `ContextFilter`
- Reflection in `ToJObject(...)`: prefer caching mappings in hot paths
