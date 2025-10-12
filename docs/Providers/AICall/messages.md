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

## Aggregation flow

- `AIBody.Messages` aggregates:
  - Validation messages from `AIBody.IsValid()`.
  - Messages from all interactions: `AIInteractionToolResult.Messages` and `AIInteractionImage.Messages`.
  - Deduplicates by message text while preserving severity and origin.
- `AIReturn.Messages` aggregates:
  - Private structured messages added during processing via `AddRuntimeMessage()` or `Create*Error()` methods.
  - `Request.Messages` (structured validation/capability notes).
  - `Body.Messages` (aggregated interaction and body validation messages).
  - Performs final deduplication and severity-first ordering.

## Wrapper guidance (Core.Grasshopper/AITools)

- When returning a successful tool result:
  - Use `AIBody.AddInteractionToolResult(jObject, metrics, messages)` and pass the inner return's `Metrics` and `Messages`.
- On provider/tool errors:
  - Standardize with `output.CreateToolError(errorText, toolCall)` (wrappers) or appropriate `AIReturn.Create*Error(...)` helpers (infra).
  - All `Create*Error()` methods add structured messages directly to `Messages` collection.
  - Propagate messages: `output.Messages = result.Messages` to preserve all error context.
- For image outputs:
  - Attach any structured messages to `AIInteractionImage.Messages` so they flow via `AIBody.Messages`.

## Infrastructure guidance

- `AIToolManager.ExecuteTool(...)` and conversation orchestration should not manually merge tool messages into the main return.
  - Central aggregation via `AIBody` and `AIReturn` replaces previous explicit merges.
  - Tool results are appended as interactions; their messages are discovered during aggregation.
  - Use `ConversationSession` for multi‑turn/tool flows; it appends tool results as `AIInteractionToolResult` interactions to the session `AIBody`.

## Example

```csharp
// Inside a tool wrapper after calling the provider
var result = await request.Exec().ConfigureAwait(false);

if (!result.Success)
{
    // Propagate all structured messages from the AI call
    output.Messages = result.Messages;
    return output;
}

var toolResult = new JObject { ["result"] = result.Body?.GetLastInteraction(AIAgent.Assistant)?.ToString() ?? string.Empty };
var toolBody = AIBodyBuilder.Create()
    .AddToolResult(toolResult, id: toolInfo?.Id, name: toolName, metrics: result.Metrics, messages: result.Messages)
    .Build();

output.CreateSuccess(toolBody);
return output;
```

## Benefits

- Centralized logic reduces duplication and error-prone manual merges.
- Preserves structured data (severity, origin), enabling accurate UI surfacing.
- Consistent behavior across providers and tools, with clear single-source aggregation.

## Message codes (machine-readable)

- Purpose: allow robust programmatic checks without parsing message text.
- Model: `AIRuntimeMessage` now includes `Code: AIMessageCode`.
- Default: `AIMessageCode.Unknown (0)` to keep existing emits backward compatible.

### Initial codes

- Provider/model selection: `ProviderMissing`, `UnknownProvider`, `UnknownModel`, `NoCapableModel`, `CapabilityMismatch`.
- Streaming: `StreamingDisabledProvider`, `StreamingUnsupportedModel`.
- Tools/validation: `ToolValidationError`, `BodyInvalid`, `ReturnInvalid`.
- Network/auth: `NetworkTimeout`, `AuthenticationMissing`, `AuthorizationFailed`, `RateLimited`.

### Emission guidance

- Prefer setting `Code` when raising messages in validators, providers, and policies.
- Keep `Message` human-readable; `Code` is for logic/tests/telemetry.
- Backward compatibility: existing calls to `new AIRuntimeMessage(sev, origin, text)` automatically default `Code` to `Unknown`.

### Example (with codes)

```csharp
// Streaming validation in request
if (settings != null && settings.EnableStreaming == false)
{
    messages.Add(new AIRuntimeMessage(
        AIRuntimeMessageSeverity.Error,
        AIRuntimeMessageOrigin.Validation,
        AIMessageCode.StreamingDisabledProvider,
        $"Streaming requested but provider '{provider}' has streaming disabled in settings."));
}
```

### Provider/model validation (AIRequestCall.IsValid())

`AIRequestCall.IsValid()` now emits machine-readable codes for provider/model resolution:

- `ProviderMissing`, `UnknownProvider`
- `NoCapableModel` (no model supports the required capability)
- `UnknownModel` (requested model not registered for provider)
- `CapabilityMismatch` (requested model known but lacks required capability; may be replaced by fallback)
- Body/endpoint issues are tagged as `BodyInvalid`

Examples:

```csharp
// Unknown model
messages.Add(new AIRuntimeMessage(
    AIRuntimeMessageSeverity.Warning,
    AIRuntimeMessageOrigin.Validation,
    AIMessageCode.UnknownModel,
    $"Requested model '{requestedModel}' is not registered for provider '{provider}'."));

// Capability mismatch (selection replaced)
messages.Add(new AIRuntimeMessage(
    AIRuntimeMessageSeverity.Warning,
    AIRuntimeMessageOrigin.Validation,
    AIMessageCode.CapabilityMismatch,
    $"Requested model '{requestedModel}' does not support {capability}; selected '{resolvedModel}' instead."));
```

UI components (e.g., `AIStatefulAsyncComponentBase`) should prefer `Message.Code` for badge/state logic and only fall back to text parsing when `Code == Unknown`.
