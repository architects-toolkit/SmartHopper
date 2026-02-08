# instruction_get

`instruction_get` is an AI tool that returns **detailed operational guidance** to the agent on demand. It enables a *tool-as-documentation* pattern where the chat system prompt stays short and stable, while domain-specific workflows and edge-case constraints are pulled only when needed.

## Location

- Tool implementation: `src/SmartHopper.Core.Grasshopper/AITools/instruction_get.cs`

## When to use

Call `instruction_get` **before** executing domain-specific workflows, especially when:

- You need the canonical sequence of tools for a given task.
- You need safety/UX constraints (edge cases) without bloating the system prompt.
- You want the assistant to follow consistent internal conventions.

This is particularly valuable for:

- Canvas inspection and modifications.
- GhJSON read/modify workflows.
- Scripting workflows (creation/editing/review, and constraints like avoiding huge pasted scripts).
- Knowledge searches (forum/web reading and summarization flow).

## Parameters

- `topic` (string, required): which instruction bundle to return.

Supported topics are declared in the tool schema. Examples include:

- `canvas`
- `discovery`
- `scripting`
- `knowledge`
- (project-specific subtopics such as `ghjson`, `selected`, `errors`, `locks`, `visibility` may be routed to a common bundle; for GhJSON format details see [ghjson-dotnet](https://github.com/architects-toolkit/ghjson-dotnet))

## Output

The tool returns a tool-result payload containing:

- `topic` (string)
- `instructions` (string): a markdown-formatted instruction block intended for the agent.

The response is returned as a tool result interaction (so it becomes part of the chat/tool trace).

## Tool-as-Documentation pattern

### Why

- Keeps system prompts short and easier to iterate on.
- Reduces token usage on every turn.
- Centralizes “how we operate” knowledge into a single authoritative tool.

### Recommended usage pattern

1. Identify which domain the user’s request falls into.
2. Call `instruction_get` with the relevant topic.
3. Follow the returned steps as the authoritative workflow.

## Notes

- This tool is intentionally **local** and does not require provider/model metrics.
- Instruction text may include edge-case constraints (for example, scripting-specific “do not paste entire scripts”).
