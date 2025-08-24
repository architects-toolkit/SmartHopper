# Requests

Covers `IAIRequest`, `AIRequestBase`, `AIRequestCall`, and `AIToolCall`.

## IAIRequest

- File: `IAIRequest.cs`
- Members:
  - `string Provider`, `IAIProvider ProviderInstance`
  - `string Model`, `AICapability Capability`
  - `AIBody Body`
  - `IsValid() : (bool IsValid, List<string> Errors)`
  - `Exec() : Task<AIReturn>`

## AIRequestBase

- File: `AIRequestBase.cs`
- Adds: `Endpoint`, default `Body`, `Capability`
- Model resolution:
  - `Model` getter uses `GetModelToUse()`
  - If no model specified or not capable, uses provider default via `ProviderInstance.GetDefaultModel(Capability)`
  - Capability validation via `ModelManager.Instance.ValidateCapabilities(provider, model, Capability)`
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
  - If `Body.JsonOutputSchema` set but `Capability` lacks `JsonOutput`, it is inferred
  - If `Body.ToolFilter` allows tools and `Capability` lacks `FunctionCalling`, it is inferred
  - Validates `Body.IsValid()` and emits info notes (non-fatal) from inference
- Execution:
  - `Exec()` executes a single provider call and returns an `AIReturn`. It does not orchestrate tools or multi‑turn flows.
  - To process pending tool calls and run multi‑turn conversations, use `ConversationSession.RunToStableResult` with `SessionOptions`.
  - See: `docs/Providers/AICall/ConversationSession.md`.

## AIToolCall

- File: `AIToolCall.cs`
- Purpose: execute a single pending tool call via `AIToolManager`
- Validation: body must contain exactly one pending tool call; must have tool `Name`; `Arguments` may be null
- Exec: `AIToolManager.ExecuteTool(this)`; returns `AIReturn` with the tool result interaction appended
- Helpers: `FromToolCallInteraction(AIInteractionToolCall, provider?, model?)`, `GetToolCall()`

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
