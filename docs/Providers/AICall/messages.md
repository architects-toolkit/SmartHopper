# Runtime Messages and Aggregation

Centralized propagation and aggregation of structured runtime messages (SHRuntimeMessage) across the AI call stack.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Infrastructure/AICall/Core/Messages/` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

This document explains how structured runtime messages are created, propagated, and aggregated throughout the AI call lifecycle. Understanding this system is essential for building reliable provider integrations and tool wrappers that surface meaningful diagnostics to end users.

**You should read this if you:**

- Are implementing providers, tool wrappers, or infrastructure
- Need to ensure informational, warning, and error messages surface consistently to callers and the Grasshopper UI
- Want to understand the centralized message aggregation system
- Need to emit or handle machine-readable message codes for programmatic checks

---

## End-User Guide

Messages in SmartHopper are structured diagnostics that flow through every layer of an AI call. Each message carries a severity level (Debug, Info, Warning, Error), an origin (who emitted it), a machine-readable code, and human-readable text. Messages are automatically collected from request validation, body validation, interaction outputs, and provider responses, then deduplicated and ordered by severity before being surfaced to the UI.

Key benefits for end users:

- Clear, structured error and warning reporting in the Grasshopper UI
- Consistent message behavior across all providers and tools
- Accurate severity-based surfacing (errors, warnings, and info are displayed appropriately)

---

## Developer Reference

### SHRuntimeMessage Class

- File: `src/SmartHopper.Infrastructure/Diagnostics/SHRuntimeMessage.cs`
- **Immutable class** carrying severity, origin, machine-readable code, and human-readable text
- Constructor: `SHRuntimeMessage(SHRuntimeMessageSeverity severity, SHRuntimeMessageOrigin origin, SHMessageCode code, string message, bool surfaceable = true)`
- Properties (all read-only):
  - `Severity` (SHRuntimeMessageSeverity) — message severity level
    - `Debug` — low-level diagnostic, typically hidden from end users
    - `Info` — informational message suitable for end-user visibility
    - `Warning` — non-fatal issue that the user should be aware of
    - `Error` — error condition that interrupted or degraded the operation
  - `Origin` (SHRuntimeMessageOrigin) — who emitted this message
    - `Request` — emitted during request validation
    - `Return` — emitted during return processing
    - `Provider` — emitted by the AI provider
    - `Tool` — emitted during tool execution
    - `Network` — emitted due to network issues
    - `Validation` — emitted during validation
    - `Worker` — emitted by a Grasshopper worker/component
  - `Code` (SHMessageCode) — machine-readable code for programmatic checks (defaults to Unknown/0)
  - `Message` (string) — human-readable diagnostic text
  - `Surfaceable` (bool) — whether this message should be shown to end users in the UI (defaults to true)

### Message Codes (Machine-Readable)

- Purpose: allow robust programmatic checks without parsing message text.
- Model: `SHRuntimeMessage` now includes `Code: SHMessageCode`.
- Default: `SHMessageCode.Unknown (0)` to keep existing emits backward compatible.

#### Initial Codes

- Provider/model selection: `ProviderMissing`, `UnknownProvider`, `UnknownModel`, `NoCapableModel`, `CapabilityMismatch`.
- Streaming: `StreamingDisabledProvider`, `StreamingUnsupportedModel`.
- Tools/validation: `ToolValidationError`, `BodyInvalid`, `ReturnInvalid`.
- Network/auth: `NetworkTimeout`, `AuthenticationMissing`, `AuthorizationFailed`, `RateLimited`.
- Batch processing: `BatchItemError`, `BatchItemCanceled`, `BatchItemExpired`.

#### Emission Guidance

- Prefer setting `Code` when raising messages in validators, providers, and policies.
- Keep `Message` human-readable; `Code` is for logic/tests.
- Backward compatibility: existing calls to `new SHRuntimeMessage(sev, origin, text)` automatically default `Code` to `Unknown`.

#### Example (with codes)

```csharp
// Streaming validation in request
if (settings != null && settings.EnableStreaming == false)
{
    messages.Add(new SHRuntimeMessage(
        SHRuntimeMessageSeverity.Error,
        SHRuntimeMessageOrigin.Validation,
        SHMessageCode.StreamingDisabledProvider,
        $"Streaming requested but provider '{provider}' has streaming disabled in settings."));
}

```

#### Provider/Model Validation (AIRequestCall.IsValid())

`AIRequestCall.IsValid()` now emits machine-readable codes for provider/model resolution:

- `ProviderMissing`, `UnknownProvider`
- `NoCapableModel` (no model supports the required capability)
- `UnknownModel` (requested model not registered for provider)
- `CapabilityMismatch` (requested model known but lacks required capability; may be replaced by fallback)
- Body/endpoint issues are tagged as `BodyInvalid`

Examples:

```csharp
// Unknown model
messages.Add(new SHRuntimeMessage(
    SHRuntimeMessageSeverity.Warning,
    SHRuntimeMessageOrigin.Validation,
    SHMessageCode.UnknownModel,
    $"Requested model '{requestedModel}' is not registered for provider '{provider}'."));

// Capability mismatch (selection replaced)
messages.Add(new SHRuntimeMessage(
    SHRuntimeMessageSeverity.Warning,
    SHRuntimeMessageOrigin.Validation,
    SHMessageCode.CapabilityMismatch,
    $"Requested model '{requestedModel}' does not support {capability}; selected '{resolvedModel}' instead."));

```

UI components (e.g., `AIStatefulAsyncComponentBase`) should prefer `Message.Code` for badge/state logic and only fall back to text parsing when `Code == Unknown`.

### Batch Processing Messages

Batch operations (via `IAIBatchProvider`) surface item-level results through a unified `IReadOnlyList<SHRuntimeMessage>` on `AIBatchStatus.Messages`:

| Code | Severity | Origin | When emitted |
| --- | --- | --- | --- |
| `BatchItemError` | Error | Provider | Item returned an error (invalid request, server error, etc.) |
| `BatchItemCanceled` | Error | Provider | Item was canceled before processing (user/system cancellation) |
| `BatchItemExpired` | Warning | Provider | Item expired before being sent to model (24h batch limit exceeded) |

Providers emitting batch messages:

- **Anthropic**: Emits all three codes based on `result.type` (`errored`, `canceled`, `expired`)
- **OpenAI**: Emits `BatchItemError` for non-2xx HTTP responses
- **MistralAI**: Emits `BatchItemError` for non-2xx HTTP responses

All batch messages flow through `ProcessBatchResults()` in `AIStatefulAsyncComponentBase`, which surfaces them via `AIReturn.AddRuntimeMessage()` → `SurfaceMessagesFromReturn()`, mapping severity to Grasshopper runtime message levels (Error → GH Error, Warning → GH Warning).

### Wrapper Example

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

---

## Architecture & Design

### Sources of Messages

- Request validation: `AIRequestBase.IsValid()` and `AIRequestCall.IsValid()` emit structured messages (errors, warnings, info).
- Body validation: `AIBody.IsValid()` contributes body-level validation messages.
- Interaction messages:
  - `AIInteractionToolResult.Messages` for tool outputs.
  - `AIInteractionImage.Messages` for image outputs.

### Aggregation Flow

- `AIBody.Messages` aggregates:
  - Validation messages from `AIBody.IsValid()`.
  - Messages from all interactions: `AIInteractionToolResult.Messages` and `AIInteractionImage.Messages`.
  - Deduplicates by message text while preserving severity and origin.
- `AIReturn.Messages` aggregates:
  - Private structured messages added during processing via `AddRuntimeMessage()` or `Create*Error()` methods.
  - `Request.Messages` (structured validation/capability notes).
  - `Body.Messages` (aggregated interaction and body validation messages).
  - Performs final deduplication and severity-first ordering.

### Wrapper Guidance (Core.Grasshopper/AITools)

- When returning a successful tool result:
  - Use `AIBody.AddInteractionToolResult(jObject, metrics, messages)` and pass the inner return's `Metrics` and `Messages`.
- On provider/tool errors:
  - Standardize with `output.CreateToolError(errorText, toolCall)` (wrappers) or appropriate `AIReturn.Create*Error(...)` helpers (infra).
  - All `Create*Error()` methods add structured messages directly to `Messages` collection.
  - Propagate messages: `output.Messages = result.Messages` to preserve all error context.
- For image outputs:
  - Attach any structured messages to `AIInteractionImage.Messages` so they flow via `AIBody.Messages`.

### Infrastructure Guidance

- `AIToolManager.ExecuteTool(...)` and conversation orchestration should not manually merge tool messages into the main return.
  - Central aggregation via `AIBody` and `AIReturn` replaces previous explicit merges.
  - Tool results are appended as interactions; their messages are discovered during aggregation.
  - Use `ConversationSession` for multi‑turn/tool flows; it appends tool results as `AIInteractionToolResult` interactions to the session `AIBody`.

### Benefits

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
- Batch processing: `BatchItemError`, `BatchItemCanceled`, `BatchItemExpired`.

### Batch processing messages

Batch operations (via `IAIBatchProvider`) surface item-level results through a unified `IReadOnlyList<AIRuntimeMessage>` on `AIBatchStatus.Messages`:

| Code | Severity | Origin | When emitted |
|------|----------|--------|--------------|
| `BatchItemError` | Error | Provider | Item returned an error (invalid request, server error, etc.) |
| `BatchItemCanceled` | Error | Provider | Item was canceled before processing (user/system cancellation) |
| `BatchItemExpired` | Warning | Provider | Item expired before being sent to model (24h batch limit exceeded) |

Providers emitting batch messages:

- **Anthropic**: Emits all three codes based on `result.type` (`errored`, `canceled`, `expired`)
- **OpenAI**: Emits `BatchItemError` for non-2xx HTTP responses
- **MistralAI**: Emits `BatchItemError` for non-2xx HTTP responses

All batch messages flow through `ProcessBatchResults()` in `AIStatefulAsyncComponentBase`, which surfaces them via `AIReturn.AddRuntimeMessage()` → `SurfaceMessagesFromReturn()`, mapping severity to Grasshopper runtime message levels (Error → GH Error, Warning → GH Warning).

### Emission guidance

- Prefer setting `Code` when raising messages in validators, providers, and policies.
- Keep `Message` human-readable; `Code` is for logic/tests.
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
