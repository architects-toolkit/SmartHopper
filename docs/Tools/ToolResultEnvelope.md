# ToolResultEnvelope

A non-breaking, standardized metadata wrapper for AI tool results. It stores machine- and human-friendly metadata under a reserved root key "__envelope" while leaving the actual payload at predictable paths like "result" or "list".

- Root reserved key: "__envelope" (JObject)
- Payload remains at your chosen key (default "result")
- Designed for consistent parsing and better human readability

## Why it matters

- Non-breaking: Existing payloads remain under their usual keys; envelope adds metadata under a reserved key.
- Consistent parsing: Downstream consumers can reliably find payload and classify content type.
- Traceability: Tracks tool, provider, model, and tool call identifiers.
- Human readable: Allows people to quickly understand what a result is and where to find content.

## Schema (fields in "__envelope")

- version: string. Envelope semver (e.g., "1.0").
- tool: string. Tool name (e.g., "text_generate", "list_generate", "img_generate").
- provider: string. Provider name (e.g., "OpenAI", "MistralAI", "DeepSeek").
- model: string. Model used.
- toolCallId: string. Correlates to the tool call, when available.
- contentType: enum ToolResultContentType. One of: Unknown, Text, List, Object, Image, Binary.
- payloadPath: string. Path to the payload relative to the root (defaults to "result").
- schemaRef: string. Optional JSON Schema reference (URL or in-repo path).
- compat: object. Optional compatibility keys for downstream logic.
- createdAt: string. ISO timestamp.
- tags: array of strings. Arbitrary labels to help catalog results.

See `src/SmartHopper.Infrastructure/AICall/Tools/ToolResultEnvelope.cs` for the authoritative implementation and
`src/SmartHopper.Infrastructure/AICall/Tools/ToolResultEnvelopeExtensions.cs` for convenience helpers.

## Content types

Use `ToolResultContentType` to classify payloads:

- Text: single string value under "result"
- List: array under "list" or a result that is a list
- Object: structured JSON object
- Image: image URL/base64 or a bitmap-like object
- Binary: opaque bytes or binary-like structure

## Usage pattern

Typical usage inside a tool implementation:

1. Build a `JObject` as the root for your tool result payload.
2. Put the actual data at a predictable key (e.g., `result`, `list`, or a domain-specific key).
3. Attach the envelope with metadata.
4. Add the tool result to the interaction stream via `AIBody.AddInteractionToolResult(...)`.

```csharp
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall;

// 1) Build payload
var toolResult = new JObject
{
    ["result"] = JToken.FromObject(generatedText)
};

// 2) Create and attach envelope
var env = ToolResultEnvelope.Create(
    tool: "text_generate",
    provider: providerName,
    model: modelName,
    contentType: ToolResultContentType.Text,
    payloadPath: "result");

// Attach under "__envelope"
toolResult.WithEnvelope(env);

// 3) Add to AIBody
var body = new AIBody();
body.AddInteractionToolResult(toolResult);

return AIReturn.CreateSuccess(body);
```

Shorthands available in `ToolResultEnvelopeExtensions`:

- `root.WithEnvelope(envelope)`
- `root.EnsureEnvelope(tool, type, payloadPath)`
- `root.GetEnvelope()`
- `root.GetPayload()`
- `payload.WrapPayload(envelope, payloadKey)`

## Relation to AddInteractionToolResult

- Keep using `AIBody.AddInteractionToolResult(JObject, AIMetrics?, List<AIRuntimeMessage>?)`.
- The envelope and `AddInteractionToolResult` are complementary:
  - Envelope = metadata attached to the payload (`JObject`) under `"__envelope"`.
  - `AddInteractionToolResult` = how the wrapped payload is appended to the conversation/interaction stream.

## Backward compatibility

- Existing tool results remain valid; envelope is additive under a reserved root key.
- Consumers not aware of the envelope can ignore `"__envelope"` and still read the payload keys.

## Extensibility guidelines

- Prefer adding new metadata fields inside `"__envelope"` rather than altering payload shape.
- Use `schemaRef` for linking a JSON Schema when structured output is expected.
- Use `tags` to add optional categorization or hints.
- Use `compat` for feature flags or migration aids when evolving consumers.

## Examples in repo

- `src/SmartHopper.Core.Grasshopper/AITools/text_generate.cs`
- `src/SmartHopper.Core.Grasshopper/AITools/list_generate.cs`
- `src/SmartHopper.Core.Grasshopper/AITools/img_generate.cs`

