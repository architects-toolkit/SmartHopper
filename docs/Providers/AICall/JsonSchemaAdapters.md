# JSON Schema Adapters

Centralized service that normalizes JSON schemas for provider structured-output requests and unwraps model responses before extraction.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.ProviderSdk/AICall/JsonSchemas/` |
| **Since Version** | ? |
| **Last Updated** | 2026-09-01 |
| **Documentation Maintainer** | Devin AI |

---

## Why Read This?

This document explains how SmartHopper components request a JSON output schema and how each provider transforms it before a request and cleans the response after the model returns. Understanding this is essential when adding a new provider or a component that depends on structured JSON output.

**You should read this if you:**

- Need to know how a non-object schema is converted into the object-root format many providers require.
- Want to implement, extend, or register a custom `IJsonSchemaAdapter`.
- Need to debug why a structured output was not extracted into the expected Grasshopper data.

---

## End-User Guide

### What Do Schema Adapters Do?

Some AI providers require the JSON schema used for structured output to start with an `object` type at the root. If a SmartHopper component requests a primitive or array schema, the adapter wraps it into an object before sending the request. When the model finishes, the adapter (or the shared service) extracts the original value from that wrapper before it is returned to the Grasshopper canvas.

This gives consistent results across providers while still letting each provider declare its own quirks.

### How Does This Affect Components?

Most end users do not interact with the adapter directly. The component supplies a target schema; SmartHopper:

1. Parses and validates the schema.
2. Wraps it for the active provider.
3. Sends the request with the provider-specific encoding.
4. Receives the response.
5. Unwraps the wrapped property before surfacing it.

When a provider like DeepSeek sends an unusual response shape, the adapter's `Unwrap` step cleans it up before extraction.

### Common Questions

**Q: Why does my array output appear under an `items` property?**
A: Providers that require object-root schemas receive `{ "items": [ ... ] }`. SmartHopper unwraps `items` before returning the array to the component.

**Q: Does every provider need a custom adapter?**
A: No. Most OpenAI-compatible providers use the shared `OpenAICompatibleJsonSchemaAdapter`. Only providers with response-cleanup quirks need to override it.

---

## Developer Reference

### Adapter Contract

`IJsonSchemaAdapter` in `SmartHopper.ProviderSdk.AICall.JsonSchemas` is the provider-specific plug-in:

- `ProviderName` — the adapter's provider key.
- `Wrap(JObject schema)` — returns a transformed schema plus `SchemaWrapperInfo` describing whether and how the schema was wrapped.
- `Unwrap(string content, SchemaWrapperInfo info)` — optional response cleanup before `JsonSchemaService` extracts the wrapped property.

`JsonSchemaService` is the centralized entry point:

- `WrapForProvider(schema, providerName)` — looks up the provider adapter, calls `Wrap`, and stores `SchemaWrapperInfo` in an `AsyncLocal` context.
- `Unwrap(content, info)` — lets the provider adapter pre-process the response, then extracts the wrapped property using `info.PropertyName`.

### Key Types

| Type | File | Purpose |
| --- | --- | --- |
| `IJsonSchemaAdapter` | `ProviderSchemaAdapters.cs` | Provider-specific schema transform and cleanup contract. |
| `JsonSchemaAdapterRegistry` | `ProviderSchemaAdapters.cs` | Thread-safe registry that adapters register against at runtime. |
| `JsonSchemaService` | `JsonSchemaService.cs` | Default `IJsonSchemaService` implementation; wraps and unwraps schemas. |
| `OpenAICompatibleJsonSchemaAdapter` | `OpenAICompatibleJsonSchemaAdapter.cs` | Shared adapter for OpenAI-compatible providers. |
| `SchemaWrapperInfo` | `IJsonSchemaService.cs` | Metadata describing a wrapped schema. |

### Shared Base Behavior

`OpenAICompatibleJsonSchemaAdapter` is the default implementation for providers that require an object-root schema. It applies the following convention:

| Input `type` | Wrapped shape | `WrapperType` | `PropertyName` |
| --- | --- | --- | --- |
| `object` | returned as-is | `null` | `null` |
| `array` | `{ "type": "object", "properties": { "items": schema }, "required": ["items"] }` | `array` | `items` |
| `string`, `number`, `integer`, `boolean` | `{ "type": "object", "properties": { "value": schema }, "required": ["value"] }` | the input type | `value` |
| anything else | `{ "type": "object", "properties": { "data": schema }, "required": ["data"] }` | `unknown` | `data` |

OpenAI, MistralAI, Ollama, and LocalAI register the shared adapter directly:

```csharp
JsonSchemaAdapterRegistry.Register(new OpenAICompatibleJsonSchemaAdapter(this.Name));
```

DeepSeek inherits the base and overrides `Unwrap` to clean up malformed `enum` arrays that some responses produce.

### Default Fallback

`JsonSchemaAdapterRegistry.Default` is an `OpenAICompatibleJsonSchemaAdapter` with the reserved name `__default__`. Providers that do not register a custom adapter (e.g., Anthropic through the default path, OpenRouter before registration, or third-party providers) fall back to this base behavior.

### Provider-Specific Request Encoding

The adapter only normalizes the schema object. How the wrapped schema is inserted into the provider request body is still provider-specific and is **not** part of the adapter contract:

- OpenAI: `response_format: { type: "json_schema", json_schema: { name, strict, schema } }` (Chat Completions) or `text.format: { type: "json_schema", ... }` (Responses).
- MistralAI: `response_format: { type: "json_object" }` plus a system message containing the wrapped schema, or native `json_schema` mode.
- Ollama / LocalAI: `response_format: { type: "json_object" }` plus a system message with the wrapped schema.
- OpenRouter: `response_format: { type: "json_schema", json_schema: { ... } }` and `structured_outputs: true`.
- DeepSeek: `response_format: { type: "json_object" }` plus a system message, with `Unwrap` cleanup.
- Anthropic: `output_config.format: { type: "json_schema", schema }` plus a system instruction.
- Gemini: uses the native `responseJsonSchema` field directly and does not call `JsonSchemaService`.

### Code Examples

#### Register a New Provider Adapter

```csharp
// In the provider constructor or initialization path
JsonSchemaAdapterRegistry.Register(new OpenAICompatibleJsonSchemaAdapter("OpenAI"));
```

#### Wrap and Unwrap a Schema at Runtime

```csharp
var schemaService = JsonSchemaService.Instance;

// Parse the target schema
if (!schemaService.TryParseSchema(userSchemaJson, out var schema, out var error))
{
    throw new InvalidOperationException(error);
}

// Wrap for the active provider
var (wrapped, info) = schemaService.WrapForProvider(schema, "OpenAI");

// Later, after the model returns a JSON string
var extracted = schemaService.Unwrap(modelResponse, info);
```

#### Override Unwrap for a Custom Adapter

```csharp
public class CustomJsonSchemaAdapter : OpenAICompatibleJsonSchemaAdapter
{
    public CustomJsonSchemaAdapter(string providerName) : base(providerName) { }

    public override string Unwrap(string content, SchemaWrapperInfo info)
    {
        // Pre-process provider-specific quirks before default extraction
        if (info.WrapperType == "enum")
        {
            content = content.Replace("\"", string.Empty);
        }

        return base.Unwrap(content, info);
    }
}
```

### When to Override

Override `OpenAICompatibleJsonSchemaAdapter` only when a provider needs response cleanup that the default extraction does not handle. DeepSeek is currently the only built-in override.

---

## Architecture & Design

### Design Rationale

**Problem**: Different providers accept or require different schema shapes for structured outputs. Hard-coding provider-specific wrapping inside each request builder would scatter logic and make adding new providers error-prone.

**Decision**: Introduce a small `IJsonSchemaAdapter` contract, a global `JsonSchemaAdapterRegistry`, and a centralized `JsonSchemaService`.

- Each provider registers its adapter at runtime, so `Infrastructure` and `ProviderSdk` do not need to reference every provider project.
- The service keeps `SchemaWrapperInfo` in an `AsyncLocal` context, so downstream response mappers can unwrap without threading the info through every call.

### Data Flow

```text
Component Schema
      │
      ▼
JsonSchemaService.TryParseSchema
      │
      ▼
IJsonSchemaAdapter.Wrap —► provider-specific request body
      │
      ▼
    AI model
      │
      ▼
IJsonSchemaAdapter.Unwrap (optional cleanup)
      │
      ▼
JsonSchemaService.Unwrap (extract wrapped property)
      │
      ▼
Component output
```

### Key Design Decisions

- **Non-object roots are wrapped, object roots are passed through.** This satisfies OpenAI-like providers without changing the original schema for providers that accept any shape.
- **Wrapper metadata is provider-scoped.** `SchemaWrapperInfo.ProviderName` lets the service find the same adapter during unwrapping.
- **Fallback to a default adapter.** Unknown or unregistered providers still get the OpenAI-compatible object-root wrapping.

### Related Documentation

- [AICall Overview](./index.md)
- [Messages](./messages.md)
- [OpenAI Provider](../OpenAI.md)
- [DeepSeek Provider](../DeepSeek.md)
