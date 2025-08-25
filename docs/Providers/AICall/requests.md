# Requests

Covers `IAIRequest`, `AIRequestBase`, and `AIRequestCall`.

## IAIRequest

- File: `IAIRequest.cs`
- Members:
  - `string Provider`, `IAIProvider ProviderInstance`
  - `string Model`, `AICapability Capability`
  - `AIBody Body`
  - `List<AIRuntimeMessage> Messages`
  - `IsValid() : (bool IsValid, List<AIRuntimeMessage> Errors)`
  - `Exec() : Task<AIReturn>`

## AIRequestBase

- File: `AIRequestBase.cs`
- Adds: `Endpoint`, default `Body`, `Capability`
- Model resolution (provider-scoped):
  - The `Model` property resolves to a provider-capable model for the requested `Capability`.
  - If a requested model is incompatible or missing, a provider default is selected via the model registry.
  - Validation notes are surfaced via structured `AIRuntimeMessage`s.
- Initialization helpers:
  - `Initialize(provider, model, body, endpoint, capability)`
  - `Initialize(provider, model, interactions, endpoint, capability, toolFilter?)`

## AIRequestCall

- File: `AIRequestCall.cs`
- HTTP wiring: `HttpMethod` (default `POST`), `Authentication` (default `bearer`), `ContentType` (`application/json`)
- Encoded payload: `EncodedRequestBody` uses `ProviderInstance.Encode(this)` if valid
- Validation extends base:
  - Requires `Provider`, `Endpoint`, `HttpMethod`, `Authentication`
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

## Examples

### Single turn (no tools)

```csharp
var body = new AIBody();
body.AddInteraction(AIAgent.System, "You are helpful");
body.AddInteraction(AIAgent.User, "Summarize the current doc");

var req = new AIRequestCall();
req.Initialize(provider: "OpenAI", model: "gpt-5-mini", body: body, endpoint: "/v1/chat/completions", capability: AICapability.Text2Text);

var result = await req.Exec();
var last = result.Body.GetLastInteraction();
```

### Multi‑turn with tools (use ConversationSession)

```csharp
var body = new AIBody();
body.AddInteraction(AIAgent.System, "You are helpful");
body.AddInteraction(AIAgent.User, "Summarize the current doc and fetch issue count");
body.ToolFilter = "*"; // enable all tools

var req = new AIRequestCall();
req.Initialize(provider: "OpenAI", model: "gpt-5-mini", body: body, endpoint: "/v1/chat/completions", capability: AICapability.Text2Text);

var session = new ConversationSession(req);
var options = new SessionOptions
{
    ProcessTools = true,
    MaxTurns = 3,
    MaxToolPasses = 2,
};

var result = await session.RunToStableResult(options);
var last = result.Body.GetLastInteraction();
```
