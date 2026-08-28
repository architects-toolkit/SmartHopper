# JSON Schema Adapters

## Purpose

SmartHopper components can request a JSON output schema. Some providers require the schema to be an object at the root; others accept a raw JSON schema. `JsonSchemaService` and `IJsonSchemaAdapter` bridge this by letting each provider declare how to transform the schema before the request and how to clean up the response before extraction.

## The adapter contract

`IJsonSchemaAdapter` in `SmartHopper.ProviderSdk.AICall.JsonSchemas` exposes:

- `ProviderName` — the adapter's provider key.
- `Wrap(JObject schema)` — returns a transformed schema plus `SchemaWrapperInfo` describing whether and how the schema was wrapped.
- `Unwrap(string content, SchemaWrapperInfo info)` — optional response cleanup before `JsonSchemaService` extracts the wrapped property.

`JsonSchemaService` is the centralized entry point:

- `WrapForProvider(schema, providerName)` — looks up the provider adapter, calls `Wrap`, and stores `SchemaWrapperInfo` in an `AsyncLocal` context.
- `Unwrap(content, info)` — lets the provider adapter pre-process the response, then extracts the wrapped property using `info.PropertyName`.

## Shared base for OpenAI-compatible providers

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

## Default fallback

`JsonSchemaAdapterRegistry.Default` is an `OpenAICompatibleJsonSchemaAdapter` with the reserved name `__default__`. Providers that do not register a custom adapter (e.g., Anthropic through the default path, OpenRouter before registration, or third-party providers) fall back to this base behavior.

## Provider-specific request encoding

The adapter only normalizes the schema object. How the wrapped schema is inserted into the provider request body is still provider-specific and is **not** part of the adapter contract:

- OpenAI: `response_format: { type: "json_schema", json_schema: { name, strict, schema } }` (Chat Completions) or `text.format: { type: "json_schema", ... }` (Responses).
- MistralAI: `response_format: { type: "json_object" }` plus a system message containing the wrapped schema, or native `json_schema` mode.
- Ollama / LocalAI: `response_format: { type: "json_object" }` plus a system message with the wrapped schema.
- OpenRouter: `response_format: { type: "json_schema", json_schema: { ... } }` and `structured_outputs: true`.
- DeepSeek: `response_format: { type: "json_object" }` plus a system message, with `Unwrap` cleanup.
- Anthropic: `output_config.format: { type: "json_schema", schema }` plus a system instruction.
- Gemini: uses the native `responseJsonSchema` field directly and does not call `JsonSchemaService`.

## When to override

Override `OpenAICompatibleJsonSchemaAdapter` only when a provider needs response cleanup that the default extraction does not handle. DeepSeek is currently the only built-in override.
