# ToolResultEnvelope

A non-breaking, standardized metadata wrapper for AI tool results. It stores machine- and human-friendly metadata under a reserved root key "__envelope" while leaving the actual payload at predictable paths like "result" or "list".

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Core.Grasshopper/AITools/ToolResultEnvelope.cs` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

- version: string. Envelope semver (e.g., "1.0").
- tool: string. Tool name (e.g., "text_generate", "list_generate", "img_generate").
- provider: string. Provider name (e.g., "OpenAI", "MistralAI", "DeepSeek", "Gemini").
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

### Usage pattern

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
    tool: "text2text",
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

### Example: Reading envelope metadata from a result

```csharp
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall;

// Assume incoming JObject from a previous tool result
JObject incoming = GetToolResult();

// Extract envelope metadata
var envelope = incoming.GetEnvelope();
if (envelope != null)
{
    Console.WriteLine($"Tool: {envelope.Tool}");
    Console.WriteLine($"Provider: {envelope.Provider}");
    Console.WriteLine($"Model: {envelope.Model}");
    Console.WriteLine($"Content type: {envelope.ContentType}");
    Console.WriteLine($"Created at: {envelope.CreatedAt}");
}

// Extract the actual payload
var payload = incoming.GetPayload();
Console.WriteLine($"Payload at '{envelope?.PayloadPath}': {payload}");

```

### Shorthands available in `ToolResultEnvelopeExtensions`

- `root.WithEnvelope(envelope)`
- `root.EnsureEnvelope(tool, type, payloadPath)`
- `root.GetEnvelope()`
- `root.GetPayload()`
- `payload.WrapPayload(envelope, payloadKey)`

### Relation to `AddInteractionToolResult`

- Keep using `AIBody.AddInteractionToolResult(JObject, AIMetrics?, List<SHRuntimeMessage>?)`.
- The envelope and `AddInteractionToolResult` are complementary:
  - Envelope = metadata attached to the payload (`JObject`) under `"__envelope"`.
  - `AddInteractionToolResult` = how the wrapped payload is appended to the conversation/interaction stream.

### Extensibility guidelines

- Prefer adding new metadata fields inside `"__envelope"` rather than altering payload shape.
- Use `schemaRef` for linking a JSON Schema when structured output is expected.
- Use `tags` to add optional categorization or hints.
- Use `compat` for feature flags or migration aids when evolving consumers.

### Examples in repo

- `src/SmartHopper.Core.Grasshopper/AITools/text2text.cs`
- `src/SmartHopper.Core.Grasshopper/AITools/text2textlist.cs`
- `src/SmartHopper.Core.Grasshopper/AITools/text2img.cs`

---

## Architecture & Design

The envelope pattern is a classic non-breaking extension strategy:

- **Reserved root key**: `"__envelope"` is namespaced to avoid collisions with tool-specific payload keys.
- **Separation of data and metadata**: The payload remains at the path the tool author chooses, while metadata lives in a predictable, inspectable envelope object.
- **Content typing**: `ToolResultContentType` allows generic consumers to branch behavior without inspecting arbitrary payload shapes.
- **Traceability**: Every envelope captures `provider`, `model`, `toolCallId`, and `createdAt`, making it easy to audit and debug which AI invocation produced a given result.
- **Additive evolution**: New fields can be added to the envelope schema without invalidating existing consumers, because the envelope is optional and self-describing.
