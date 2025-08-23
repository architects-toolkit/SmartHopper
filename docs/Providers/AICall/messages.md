# Runtime Messages and Aggregation

This document explains the centralized propagation and aggregation of structured runtime messages (AIRuntimeMessage) across the AI call stack.

- Audience: developers implementing providers, tool wrappers, or infrastructure.
- Goal: ensure all informational, warning, and error messages surface consistently to callers and the Grasshopper UI.

## Sources of messages

- Request validation: `AIRequestBase.IsValid()` and `AIRequestCall.IsValid()` emit structured messages (errors, warnings, info).
- Body validation: `AIBody.IsValid()` contributes body-level validation messages.
- Interaction messages:
  - `AIInteractionToolResult.Messages` for tool outputs.
  - `AIInteractionImage.Messages` for image outputs.
- Error mirroring: `AIReturn.ErrorMessage` is mirrored into `AIReturn.Messages` for visibility with origin and severity.

## Aggregation flow

- `AIBody.Messages` aggregates:
  - Validation messages from `AIBody.IsValid()`.
  - Messages from all interactions: `AIInteractionToolResult.Messages` and `AIInteractionImage.Messages`.
  - Deduplicates by message text while preserving severity and origin.
- `AIReturn.Messages` aggregates:
  - Private structured messages added during processing.
  - Error mirror from `ErrorMessage`.
  - `Request.Messages` (structured validation/capability notes).
  - `Body.Messages` (aggregated interaction and body validation messages).
  - Performs final deduplication and severity-first ordering.

## Wrapper guidance (Core.Grasshopper/AITools)

- When returning a successful tool result:
  - Use `AIBody.AddInteractionToolResult(jObject, metrics, messages)` and pass the inner return's `Metrics` and `Messages`.
- On provider/tool errors:
  - Standardize with `output.CreateToolError(errorText, toolCall)` (wrappers) or appropriate `AIReturn.Create*Error(...)` helpers (infra).
  - Do not copy or synthesize legacy string messages; rely on `AIReturn.Messages` mirroring of `ErrorMessage`.
- For image outputs:
  - Attach any structured messages to `AIInteractionImage.Messages` so they flow via `AIBody.Messages`.

## Infrastructure guidance

- `AIToolManager.ExecuteTool(...)` and `AIRequestCall.Exec(processTools)` should not merge tool messages into the main return.
  - Central aggregation via `AIBody` and `AIReturn` replaces previous explicit merges.
  - Tool results are appended as interactions; their messages are discovered during aggregation.

## Example

```csharp
// Inside a tool wrapper after calling the provider
var result = await request.Exec().ConfigureAwait(false);

if (!string.IsNullOrEmpty(result.ErrorMessage))
{
    output.CreateToolError(result.ErrorMessage, toolCall);
    return output;
}

var toolResult = new JObject { ["result"] = result.Body?.GetLastInteraction(AIAgent.Assistant)?.ToString() ?? string.Empty };
var toolBody = new AIBody();
toolBody.AddInteractionToolResult(toolResult, result.Metrics, result.Messages);

output.CreateSuccess(toolBody);
return output;
```

## Benefits

- Centralized logic reduces duplication and error-prone manual merges.
- Preserves structured data (severity, origin), enabling accurate UI surfacing.
- Consistent behavior across providers and tools, with clear single-source aggregation.
