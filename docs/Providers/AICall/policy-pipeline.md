# Policy Pipeline

This page explains the always-on request/response policy pipeline that runs around every provider call.

- Location: `src/SmartHopper.Infrastructure/AICall/Policies/`
- Core types:
  - `PolicyPipeline` — orchestrates request and response policies
  - `IRequestPolicy` — contract for request-phase policies
  - `IResponsePolicy` — contract for response-phase policies

## What policies do

- Request policies run before the provider call to validate and normalize the request.
- Response policies run after the provider call to decode raw JSON, standardize fields, and attach diagnostics via `AIReturn.AddRuntimeMessage(...)`.

## Default pipeline behavior

The pipeline runs **6 request policies** and **2 response policies** in sequence:

### Request policies (in order)

| Policy | File | Trigger | Description |
| --- | --- | --- | --- |
| `AIToolValidationRequestPolicy` | `Request/AIToolValidationRequestPolicy.cs` | Request | Validates pending tool calls in the request body: existence, JSON schema, and capability compatibility. Uses composed validators (ToolExistsValidator, ToolJsonSchemaValidator, ToolCapabilityValidator). Emits structured diagnostics via `AddRuntimeMessage()`; makes request invalid on Error-level issues to block early before provider call. |
| `ContextInjectionRequestPolicy` | `Request/ContextInjectionRequestPolicy.cs` | Request | Injects a context interaction immutably at the beginning of the request body based on `ContextFilter`. Collects current context from `AIContextManager` according to filter rules and prepends as a System-agent interaction. |
| `RequestTimeoutPolicy` | `Request/RequestTimeoutPolicy.cs` | Request | Normalizes the per-request timeout on `AIRequestBase` derivatives. Applies defaults when unset and clamps to safe range (min/max from `TimeoutDefaults`). Adds lightweight diagnostic as system interaction when adjustments are made. |
| `SchemaAttachRequestPolicy` | `Request/SchemaAttachRequestPolicy.cs` | Request | Attaches JSON output schema to the request when `AIBody.JsonOutputSchema` is set. Passes schema to provider encoding layer for structured output inference. |
| `SchemaValidateRequestPolicy` | `Request/SchemaValidateRequestPolicy.cs` | Request | Validates JSON output schema syntax and compatibility with the selected model's capabilities. Emits validation errors as structured messages; may auto-add schema if required by capability. |
| `ToolFilterNormalizationRequestPolicy` | `Request/ToolFilterNormalizationRequestPolicy.cs` | Request | Normalizes and validates tool filter expressions (e.g., `+*`, `-*`, `+tool1`, `-tool2`). Ensures filter syntax is valid; emits warnings for invalid patterns. |

### Response policies (in order)

| Policy | File | Trigger | Description |
| --- | --- | --- | --- |
| `FinishReasonNormalizeResponsePolicy` | `Response/FinishReasonNormalizeResponsePolicy.cs` | Response | Standardizes provider-specific finish reasons into canonical `AIFinishReason` values (e.g., `stop`, `length`, `tool_calls`, `content_filter`). Maps provider-specific strings to unified enum. |
| `SchemaValidateResponsePolicy` | `Response/SchemaValidateResponsePolicy.cs` | Response | Validates the response against the requested JSON schema (if set). Uses `JsonSchemaResponseValidator` to check structure. Emits validation errors as structured messages via `AddRuntimeMessage()`. |

## Developer guidance

- When adding new providers, implement `Encode(...)` and `Decode(...)` so the pipeline can operate consistently.
- Prefer attaching structured diagnostics through `AIReturn.AddRuntimeMessage(...)` rather than writing directly to logs.
- Avoid mutating interaction lists outside the decoding phase; rely on `AIBody` aggregation rules.

## See also

- Requests: `AIRequestCall` — `docs/Providers/AICall/requests.md`
- Body and message aggregation — `docs/Providers/AICall/body-metrics-status.md` and `docs/Providers/AICall/messages.md`
