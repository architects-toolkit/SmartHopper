# Tools

Tools are callable operations the AI can invoke (function/tool calling) and utilities exposed to Grasshopper.

## Purpose

- Provide discrete actions (e.g., create geometry, tidy canvas, generate text/images) with JSON schemas.
- Enable provider-agnostic tool calling: tools are formatted by providers and executed by components.

## Key locations

- `src/SmartHopper.Core.Grasshopper/AITools/` — tool definitions (schema, validation, execution)
- `src/SmartHopper.Infrastructure/AICall/` — request/response plumbing (`AIBody`, `AIRequestCall`, `AIReturn<T>`)

## Tool contract (typical)

- Name, description, category
- JSON Schema for inputs (validated before execution)
- Execute handler that performs the action and returns structured results

## Conventions

- Naming: `gh_*`, `text_*`, `list_*`, `img_*`, `web_*`, etc.
- Tools should return consistent keys (e.g., `list` for list_generate) and clear error messages.
- Use provider/model capability checks via the model registry when needed.

## Tool-as-Documentation

- Some tools exist primarily to provide detailed operational guidance to the agent without bloating the system prompt.
- This keeps prompts short, reduces per-turn token usage, and centralizes workflows in one place.
- See `docs/Tools/instruction_get.md`.

## Tool Result Envelope

- Tools should attach a metadata envelope to their JSON result under the reserved root key `"__envelope"`.
- The actual payload remains at predictable keys (e.g., `result`, `list`).
- See `docs/Tools/ToolResultEnvelope.md` for schema, rationale, and examples.

## Flow
1. Component builds `AIBody` with tool filters/schemas as needed.
2. Provider formats tool definitions into its API shape.
3. If a tool is executed: the tool builds a payload, attaches `ToolResultEnvelope` to the root, and appends it via `AIBody.AddInteractionToolResult(...)` to the interaction stream.
4. Components read normalized results (including the envelope) from `AIReturn<T>`.
