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

## Flow

1. Component builds `AIBody` with tool filters/schemas as needed.
2. Provider formats tool definitions into its API shape.
3. Model may call a tool → tool executes → results appended to the interaction stream.
4. Components read normalized results from `AIReturn<T>`.
