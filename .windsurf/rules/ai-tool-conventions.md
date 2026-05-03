---
trigger: glob
globs: **/SmartHopper.Core.Grasshopper/AITools/*.cs
---

# AI Tool conventions

- AI tools live in `src/SmartHopper.Core.Grasshopper/AITools/` and implement `IAIToolProvider`.
- Check similar tools before adding or changing behavior.
- File names use `scope_action.cs`, for example `gh_get.cs`, `text_generate.cs`, `list_filter.cs`, or `script_review.cs`.
- Each tool provider exposes tools through `GetTools()` with:
  - Stable tool name.
  - Clear description and category.
  - JSON parameter schema.
  - Required capabilities when model/provider support matters.
  - Async execution delegate.
- Tools are discovered by `AIToolManager`; do not hand-register duplicates.
- Execute tools through `AIToolCall.Exec()` or `AIToolManager.ExecuteTool(...)`; do not call an `AITool.Execute` delegate directly from unrelated code.
- Validate and sanitize tool arguments from `AIInteractionToolCall.Arguments` before side effects.
- For JSON tool results, keep payloads at predictable keys (`result`, `list`, or domain-specific keys) and attach `ToolResultEnvelope` metadata under `__envelope`.
- For canvas mutations, follow the Grasshopper undo rule before changing objects, wires, positions, preview, locks, or parameters.
- Keep long-running or UI-thread-sensitive work off background threads unless explicitly marshaled to the Rhino UI thread.
