---
trigger: glob
globs: **/SmartHopper.Core.Grasshopper/AITools/*.cs
---

# Tool result envelope

AI tools that return JSON should attach a metadata envelope under the reserved root key `__envelope`.

## Pattern

1. Build a root `JObject`.
2. Place the payload at a predictable key such as `result`, `list`, `json`, `description`, `image`, `components`, or another documented domain-specific key.
3. Attach `ToolResultEnvelope` with `WithEnvelope(...)` or the extension helpers.
4. Add the wrapped payload to the interaction stream with `AIBodyBuilder.AddToolResult(...)`/the current body-builder API, or return it through `AIReturn` when the caller wraps it.

## Envelope content

Include useful metadata when available:

- Tool name.
- Provider and model.
- Tool-call identifier.
- Content type.
- Payload path.
- Schema reference or compatibility keys for structured outputs.

## Compatibility

- Do not move existing payload keys only to add metadata.
- Keep `__envelope` additive and non-breaking.
- Consumers that do not understand the envelope must still be able to read the payload.

See `docs/Tools/ToolResultEnvelope.md`.
