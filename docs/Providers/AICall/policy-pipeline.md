# Policy Pipeline

The always-on request/response policy pipeline that runs around every provider call.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Infrastructure/AICall/Policies/` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

This page explains the always-on request/response policy pipeline that runs around every provider call. Policies validate, normalize, and standardize requests and responses, ensuring consistent behavior across all AI providers.

**You should read this if you:**

- Are adding new providers and need to integrate with the pipeline
- Want to understand how requests are validated and normalized before being sent
- Need to understand how raw provider responses are decoded and standardized
- Are implementing custom request or response policies

---

## End-User Guide

### What Policies Do

- Request policies run before the provider call to validate and normalize the request.
- Response policies run after the provider call to decode raw JSON, standardize fields, and attach diagnostics via `AIReturn.AddRuntimeMessage(...)`.

### Default Pipeline Behavior

The pipeline runs **6 request policies** and **2 response policies** in sequence. Each policy has a specific responsibility, such as tool validation, context injection, timeout normalization, schema attachment/validation, and finish reason normalization. This ensures that every provider call follows the same preparation and cleanup steps regardless of which provider is used.

---

## Developer Reference

### Core Types

- `PolicyPipeline` — orchestrates request and response policies
- `IRequestPolicy` — contract for request-phase policies
- `IResponsePolicy` — contract for response-phase policies

### Request Policies (in order)

| Policy | File | Trigger | Description |
| --- | --- | --- | --- |
| `AIToolValidationRequestPolicy` | `Request/AIToolValidationRequestPolicy.cs` | Request | Validates pending tool calls in the request body: existence, JSON schema, and capability compatibility. Uses composed validators (ToolExistsValidator, ToolJsonSchemaValidator, ToolCapabilityValidator). Emits structured diagnostics via `AddRuntimeMessage()`; makes request invalid on Error-level issues to block early before provider call. |
| `ContextInjectionRequestPolicy` | `Request/ContextInjectionRequestPolicy.cs` | Request | Injects a context interaction immutably at the beginning of the request body based on `ContextFilter`. Collects current context from `AIContextManager` according to filter rules and prepends as a System-agent interaction. |
| `RequestTimeoutPolicy` | `Request/RequestTimeoutPolicy.cs` | Request | Normalizes the per-request timeout on `AIRequestBase` derivatives. Applies defaults when unset and clamps to safe range (min/max from `TimeoutDefaults`). Adds lightweight diagnostic as system interaction when adjustments are made. |
| `SchemaAttachRequestPolicy` | `Request/SchemaAttachRequestPolicy.cs` | Request | Attaches JSON output schema to the request when `AIBody.JsonOutputSchema` is set. Passes schema to provider encoding layer for structured output inference. |
| `SchemaValidateRequestPolicy` | `Request/SchemaValidateRequestPolicy.cs` | Request | Validates JSON output schema syntax and compatibility with the selected model's capabilities. Emits validation errors as structured messages; may auto-add schema if required by capability. |
| `ToolFilterNormalizationRequestPolicy` | `Request/ToolFilterNormalizationRequestPolicy.cs` | Request | Normalizes and validates tool filter expressions (e.g., `+*`, `-*`, `+tool1`, `-tool2`). Ensures filter syntax is valid; emits warnings for invalid patterns. |

### Response Policies (in order)

| Policy | File | Trigger | Description |
| --- | --- | --- | --- |
| `FinishReasonNormalizeResponsePolicy` | `Response/FinishReasonNormalizeResponsePolicy.cs` | Response | Standardizes provider-specific finish reasons into canonical `AIFinishReason` values (e.g., `stop`, `length`, `tool_calls`, `content_filter`). Maps provider-specific strings to unified enum. |
| `SchemaValidateResponsePolicy` | `Response/SchemaValidateResponsePolicy.cs` | Response | Validates the response against the requested JSON schema (if set). Uses `JsonSchemaResponseValidator` to check structure. Emits validation errors as structured messages via `AddRuntimeMessage()`. |

### Implementing a Custom Request Policy

```csharp
public class MyCustomRequestPolicy : IRequestPolicy
{
    public void Apply(AIRequestBase request)
    {
        // Validate or normalize the request before the provider call
        if (request.Body == null)
        {
            request.Messages.Add(new SHRuntimeMessage(
                SHRuntimeMessageSeverity.Error,
                SHRuntimeMessageOrigin.Validation,
                SHMessageCode.BodyInvalid,
                "Request body is required."));
        }
    }
}

```

### Implementing a Custom Response Policy

```csharp
public class MyCustomResponsePolicy : IResponsePolicy
{
    public void Apply(AIReturn response, AIRequestBase request)
    {
        // Decode and standardize the raw response after the provider call
        if (response.RawJson != null)
        {
            var finishReason = response.RawJson["choices"]?.FirstOrDefault()?["finish_reason"]?.ToString();
            if (finishReason == "max_tokens")
            {
                response.AddRuntimeMessage(
                    SHRuntimeMessageSeverity.Warning,
                    SHRuntimeMessageOrigin.Provider,
                    SHMessageCode.StreamingUnsupportedModel,
                    "Response was truncated due to max token limit.");
            }
        }
    }
}

```

### Developer Guidance

- When adding new providers, implement `Encode(...)` and `Decode(...)` so the pipeline can operate consistently.
- Prefer attaching structured diagnostics through `AIReturn.AddRuntimeMessage(...)` rather than writing directly to logs.
- Avoid mutating interaction lists outside the decoding phase; rely on `AIBody` aggregation rules.

---

## Architecture & Design

The policy pipeline is designed as a centralized, extensible middleware layer that wraps every provider call. By splitting concerns into request-phase and response-phase policies, the system ensures that:

- Validation and normalization happen consistently before any network request is made
- Provider-specific quirks are isolated in response policies that standardize the output
- New behaviors can be added by implementing `IRequestPolicy` or `IResponsePolicy` without changing provider code
- Structured diagnostics flow through the same `AIReturn.Messages` aggregation channel used by all other components

The pipeline is orchestrated by `PolicyPipeline`, which iterates over registered policies in a fixed order. Request policies are given the opportunity to modify or invalidate the request, while response policies receive both the raw return and the original request for context-aware processing.

## See also

- Requests: `AIRequestCall` — `docs/Providers/AICall/requests.md`
- Body and message aggregation — `docs/Providers/AICall/body-metrics-status.md` and `docs/Providers/AICall/messages.md`
