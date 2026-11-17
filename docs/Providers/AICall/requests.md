# Requests

Covers `IAIRequest`, `AIRequestBase`, and `AIRequestCall`.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Infrastructure/AICall/Core/Requests/` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

This document covers the core request types used to construct, validate, and execute AI provider calls. Understanding these types is fundamental to using the SmartHopper AI infrastructure for both single-turn and multi-turn interactions.

**You should read this if you:**

- Need to construct and execute AI provider calls
- Want to understand model resolution and capability inference
- Are implementing multi-turn conversations with tools
- Need to understand request validation and the policy pipeline integration

---

## End-User Guide

SmartHopper provides a layered request model:

- `IAIRequest` defines the minimal contract any AI request must satisfy.
- `AIRequestBase` adds common fields like `Origin`, `Endpoint`, default `Body`, and `Capability`, plus model resolution.
- `AIRequestCall` is the concrete HTTP-ready request with method, authentication, and execution logic.

Two fields with related names carry deliberately different meanings:

- **`Origin`** — the *semantic* provenance label set by the caller: tool name (`gh_put`, `text2text`), component tag, fallback label (`fallback:stt`), chat surface (`canvas-chat`). It travels untouched to the wire copy and drives attribution (e.g., SmartHopper Cloud's `X-SmartHopper-Origin` header) and logging.
- **`Endpoint`** — the *wire* route resolved by the provider during `PreCallAsync` (e.g., `/chat/completions`, `/models/{model}:generateContent`). Callers should leave it unset; a non-empty value is an explicit wire-path override reserved for backoffice-style requests (`/models`, batch status paths).

To execute a request, you construct an `AIRequestCall`, initialize it with a provider, model, body, origin, and capability, then call `Exec()`. The execution automatically runs the always-on policy pipeline before and after the provider call.

For simple single-turn calls without tools, `Exec()` is sufficient. For multi-turn conversations with tool execution, wrap the request in a `ConversationSession` and use `RunToStableResult` with `SessionOptions`.

---

## Developer Reference

### IAIRequest

- File: `IAIRequest.cs`
- Members:
  - `string Provider`, `IAIProvider ProviderInstance`
  - `string Model`, `AICapability Capability`
  - `AIBody Body`
  - `List<SHRuntimeMessage> Messages`
  - `IsValid() : (bool IsValid, List<SHRuntimeMessage> Errors)`
  - `Exec() : Task<AIReturn>`

### AIRequestBase

- File: `AIRequestBase.cs`
- Adds: `Origin`, `Endpoint`, default `Body`, `Capability`
- Model resolution (provider-scoped):
  - The `Model` property resolves to a provider-capable model for the requested `Capability`.
  - If a requested model is incompatible or missing, a provider default is selected via the model registry.
  - Validation notes are surfaced via structured `SHRuntimeMessage`s.
- Initialization helpers:
  - `Initialize(provider, model, body, origin, capability)`
  - `Initialize(provider, model, interactions, origin, capability, toolFilter?)`

### AIRequestCall

- File: `AIRequestCall.cs`
- HTTP wiring: `HttpMethod` (default `POST`), `Authentication` (default `bearer`), `ContentType` (`application/json`)
- `Source`: set by `CreateExecutionCopy()` to the canonical (root) request; copies of copies flatten to the same root. Pipeline stages key per-request state (e.g., Cloud idempotency keys) on `Source` instead of the transient copy.
- Encoded payload: `EncodedRequestBody` uses `ProviderInstance.Encode(this)` if valid
- Validation extends base:
  - Requires `Provider`, `HttpMethod`, `Authentication`
  - Requires `Capability` with both input and output (e.g., `Text2Text`, `Text2Json`, etc.)
  - If `Body` requires JSON output but `Capability` lacks `JsonOutput`, it is inferred
  - If `Body.ToolFilter` enables tools and `Capability` lacks `FunctionCalling`, it is inferred
  - Validates `Body.IsValid()` and emits info notes (non-fatal) from inference
- Execution:
  - `Exec()` executes a single provider call and returns an `AIReturn`. It does not orchestrate tools or multi‑turn flows.
  - Always-on policies: the `PolicyPipeline` runs before and after the provider call (request validation/normalization and response decoding/standardization).
  - To process pending tool calls and run multi‑turn conversations, use `ConversationSession.RunToStableResult` with `SessionOptions`.
  - See: `docs/Providers/AICall/ConversationSession.md`.
- Related:
  - For executing exactly one pending tool call, use `AIToolCall` (`src/SmartHopper.Infrastructure/AICall/Tools/AIToolCall.cs`). See [Tools](./tools.md).

### Examples

#### Single Turn (No Tools)

```csharp
var body = AIBodyBuilder.Create()
    .AddText(AIAgent.System, "You are helpful")
    .AddUser("Summarize the current doc")
    .Build();

var req = new AIRequestCall();
req.Initialize(
    provider: "openai",
    model: "gpt-4o-mini",
    body: body,
    origin: "my_feature",
    capability: AICapability.Text2Text);

var result = await req.Exec();
var last = result.Body.GetLastInteraction();
```

#### Multi‑Turn with Tools (Use ConversationSession)

```csharp
var body = AIBodyBuilder.Create()
    .AddText(AIAgent.System, "You are helpful")
    .AddUser("Summarize the current doc and fetch issue count")
    .WithToolFilter("*")
    .Build();

var req = new AIRequestCall();
req.Initialize(
    provider: "openai",
    model: "gpt-4o-mini",
    body: body,
    origin: "my_feature",
    capability: AICapability.ToolChat,
    toolFilter: "*");

var session = new ConversationSession(req);
var options = new SessionOptions
{
    ProcessTools = true,
    MaxAutonomousTime = TimeSpan.FromMinutes(3),
    MaxAutonomousTokens = 50_000,
};

var result = await session.RunToStableResult(options);
var last = result.Body.GetLastInteraction();

```

---

## Architecture & Design

### Model Resolution

`AIRequestBase` resolves the `Model` property within the scope of the selected provider. If the explicitly requested model is incompatible with the required capability or is not registered, the system falls back to the provider's default capable model. All resolution decisions are emitted as structured `SHRuntimeMessage`s so callers can observe when substitutions occur.

### Execution Flow

1. Caller constructs and initializes an `AIRequestCall` (the *canonical* request).
2. `Exec()` runs request policies and the provider trust check.
3. `PrepareExecutionAsync` produces a **detached execution copy** (media adaptation, approved-source materialization) — the canonical request keeps original session evidence.
4. The outbound gate (`ValidateOutboundRequest` + `IsValid`) runs on the copy.
5. `provider.Call(copy)` → `PreCallAsync` detaches **another** execution copy and hands it to `PreCallCoreAsync`, where the provider resolves wire fields (`Endpoint`, `HttpMethod`, `Authentication`, `Headers`) — the copy reaching the provider is never caller-owned, so preparation cannot mutate caller state.
6. `Encode(outbound)` produces the provider-specific wire format; `CallApi` executes the HTTP call.
7. Response policies standardize finish reasons, validate schemas, and attach diagnostics.
8. An `AIReturn` is returned with the final body, messages, metrics, and success state.

### Immutability Contract

- The canonical request is **never mutated** by pipeline stages: `PrepareExecutionAsync` (stage 1) and `PreCallAsync` (stage 2) each work on `CreateExecutionCopy()` output.
- `AIProvider.PreCallAsync` enforces this centrally — providers override `PreCallCoreAsync(outbound)` and mutate the supplied copy freely; they never see the caller's instance.
- Execution copies know their lineage: `copy.Source` resolves to the canonical root request, which is where per-logical-request state (idempotency keys, retry fingerprints) belongs.
- Batch submit paths (`EncodeBatchItemAsync`) run the same stage-2 preparation so queued requests get identical wire treatment.

### Policy Integration

The request lifecycle is tightly integrated with the `PolicyPipeline`. Every `Exec()` call automatically invokes registered request and response policies. This means validation, context injection, timeout handling, schema management, and tool filtering are all applied consistently without the caller needing to orchestrate them manually.
