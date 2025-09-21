# Interactions

Covers `IAIInteraction` and concrete message types.

## IAIInteraction
 
- File: `IAIInteraction.cs`
- Properties:
  - `DateTime Time`
  - `AIAgent Agent`
  - `AIMetrics Metrics`

## AIInteractionText
 
- File: `AIInteractionText.cs`
- Purpose: text output with optional reasoning.
- Fields: `Content`, `Reasoning`
- Methods: `SetResult(AIAgent agent, string content, string? reasoning)`; `ToString()` formats `<think>reasoning</think>content`.

## AIInteractionImage
 
- File: `AIInteractionImage.cs`
- Purpose: image generation results and prompts
- Fields: `ImageUrl`, `ImageData`, `RevisedPrompt`, `OriginalPrompt`, `ImageSize` (default `1024x1024`), `ImageQuality` (default `standard`), `ImageStyle` (default `vivid`)
- Methods:
  - `CreateRequest(prompt, size?, quality?, style?)` records desired parameters
  - `SetResult(imageUrl?, imageData?, revisedPrompt?)` (one of url/data required)

## AIInteractionToolCall
  
- File: `AIInteractionToolCall.cs`
- Purpose: model asks to invoke a tool
- Fields: `Id`, `Name`, `Arguments` (JObject)
- Methods: `ToString()` pretty prints name, id and JSON args
- Agent defaults to `ToolCall`
- See also: [Tools](./tools.md) for how pending tool calls are executed and orchestrated.

## AIInteractionToolResult
  
- File: `AIInteractionToolResult.cs`
- Purpose: result of executing a tool
- Inherits: `AIInteractionToolCall`
- Adds field: `Result` (JObject)
- Overrides agent to `ToolResult`
- See also: [Tools](./tools.md) for result aggregation and message handling.

## AIAgent

- File: `AIAgent.cs`
- Enum roles: `Context`, `System`, `User`, `Assistant`, `ToolCall`, `ToolResult`, `Unknown`
- Extension helpers: `.ToString()`, `.ToDescription()`, `FromString(string)`

## IAIRenderInteraction

- File: `src/SmartHopper.Infrastructure/AICall/Core/Interactions/IAIRenderInteraction.cs`
- Purpose: eliminate type switches in UI rendering by letting each interaction define how it should be displayed.
- Methods:
  - `GetRoleClassForRender()` → returns the CSS role class (e.g., `assistant`, `user`, `tool`, `error`).
  - `GetDisplayNameForRender()` → the display label used in the message header.
  - `GetRawContentForRender()` → raw markdown content (converted to HTML by `ChatResourceManager`).
  - `GetRawReasoningForRender()` → optional reasoning; supports `<think>…</think>` and is rendered as a collapsible panel in the UI.

Consumption:
- Used by `ChatResourceManager.CreateMessageHtml(...)` and `HtmlChatRenderer.RenderInteraction(...)` to build the final HTML message without casting on interaction type.

## IAIKeyedInteraction

- File: `src/SmartHopper.Infrastructure/AICall/Core/Interactions/IAIKeyedInteraction.cs`
- Purpose: provide stable identity keys to aggregate streaming updates and to de‑duplicate persisted messages.
- Methods:
  - `GetStreamKey()` → stable grouping key for streaming (multiple deltas update a single bubble in UI).
  - `GetDedupKey()` → stable identity for persisted messages (used for history hydration and to avoid duplicates).

Consumption:
- Used by the chat UI observer (`WebChatObserver`) to:
  - Upsert streaming content via `GetStreamKey()`.
  - On finalization, re‑key the assistant bubble from stream key → `GetDedupKey()` so later assistant turns don’t overwrite previous ones.
