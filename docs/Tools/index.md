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

## Available tools

### Instruction & Knowledge

| Tool | Description |
|------|-------------|
| [instruction_get](./instruction_get.md) | Returns detailed operational guidance for specific topics (canvas, scripting, knowledge, etc.) |
| file_to_md | Converts documents (PDF, DOCX, XLSX, PPTX, HTML, CSV, JSON, XML, TXT, etc.) to Markdown |
| web_generic_page_read | Reads and extracts content from web pages |
| mcneel_forum_search | Searches the McNeel forum for Rhino/Grasshopper content |
| mcneel_forum_topic | Retrieves specific forum topic content |
| mcneel_forum_post | Creates a new forum post |

### Text Generation & Processing

| Tool | Description |
|------|-------------|
| text_generate | Generates text based on a prompt with optional instructions |
| text_evaluate | Evaluates text against criteria and returns assessments |
| list_generate | Generates a list of items based on a prompt, count, and type (text/number/integer/boolean) |
| list_filter | Filters list items based on criteria |
| list_evaluate | Evaluates list items against criteria |

### Image Processing

| Tool | Description |
|------|-------------|
| [img_to_text](./img_to_text.md) | Describes or analyzes an image using a vision model |
| img_generate | Generates images from text prompts (e.g., DALL-E) |

### Grasshopper Canvas Operations

| Tool | Description |
|------|-------------|
| gh_get | Reads the Grasshopper file and returns GhJSON structure with optional filters |
| gh_put | Places components from GhJSON onto the canvas |
| gh_move | Moves components to new positions |
| gh_merge | Merges multiple GhJSON definitions |
| gh_group | Creates component groups |
| gh_tidy_up | Auto-organizes canvas layout |
| gh_list_categories | Lists available Grasshopper component categories |
| gh_list_components | Lists available components by category |
| gh_component_preview | Toggles component preview state on/off |
| gh_component_lock | Locks or unlocks components |
| _gh_generate | WIP: AI-powered canvas generation with GhJSON |
| _gh_connect | WIP: Connects components based on AI suggestions |
| _gh_parameter_modifier | WIP: Modifies parameter properties |

### Scripting Tools

| Tool | Description |
|------|-------------|
| script_generate | Generates new C# scripts for Grasshopper |
| script_edit | Edits existing C# scripts |
| script_review | Reviews and provides feedback on script code |
| _script_parameter_modifier | WIP: Modifies script parameter properties |
| ScriptCodeValidator | Validates script code for errors |

### Rhino 3DM Tools

| Tool | Description |
|------|-------------|
| _rhino_read_3dm | WIP: Reads 3DM file metadata |
| _rhino_get_geometry_3dm | WIP: Extracts geometry from 3DM files |

### Metadata & Envelope

| Tool/Convention | Description |
|-----------------|-------------|
| [ToolResultEnvelope](./ToolResultEnvelope.md) | Standard metadata envelope convention for all tool results (attached under `__envelope` key) |

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
