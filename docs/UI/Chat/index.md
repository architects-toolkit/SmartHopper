# Chat UI (Full WebView)

This document describes the chat UI architecture and its interactions after migrating to a fully WebView-based interface.

- Paths
  - C# host: `src/SmartHopper.Core/UI/Chat/WebChatDialog.cs`
  - Observer: `src/SmartHopper.Core/UI/Chat/WebChatObserver.cs`
  - Rendering: `src/SmartHopper.Core/UI/Chat/HtmlChatRenderer.cs`
  - Resources: `src/SmartHopper.Core/UI/Chat/Resources/`
    - CSS: `css/chat-styles.css`
    - JS: `js/chat-script.js`
    - HTML templates: `templates/chat-template.html`, `templates/message-template.html`, `templates/error-template.html`
  - Conversation/session: `src/SmartHopper.Infrastructure/AICall/Sessions/ConversationSession.cs`
  - Utilities/lifecycle: `src/SmartHopper.Core/UI/Chat/WebChatUtils.cs`

## Overview

- The chat window is now a **full WebView UI** (no Eto input controls). The HTML page includes a status bar, the chat transcript, and an input bar with Send, Clear, and Cancel buttons.
- User actions originate in the WebView’s JavaScript and are bridged to C# via a **custom URL scheme** handled in the host:
  - `sh://event?type=send&text=...`
  - `sh://event?type=clear`
  - `sh://event?type=cancel`
  - Clipboard helper: `clipboard://copy?text=...`
- C# intercepts these URLs using `WebView.DocumentLoading` and cancels navigation, then routes the action to the appropriate host method.

## Separation of Concerns

- `ConversationSession` is the single source of truth for conversation history and for orchestrating provider calls and tool passes.
- `HtmlChatRenderer` (+ `ChatResourceManager`) converts `IAIInteraction` instances to HTML message bubbles. Markdig is used for Markdown → HTML.
- `WebChatDialog` is a thin host adapter:
  - Loads initial HTML (CSS/JS inline) via `ChatResourceManager.GetCompleteHtml()`.
  - Handles JS → host events (send/clear/cancel/clipboard) in `WebView.DocumentLoading`.
  - Calls session methods and injects UI updates back using `ExecuteScript(...)` (`addMessage`, `replaceLastMessageByRole`, `setStatus`, `setProcessing`).
- `WebChatObserver` implements `IConversationObserver` and bridges `ConversationSession` streaming/partials/tool events to the WebView DOM incrementally.

## Rendering Interaction Types

Supported via `ChatResourceManager.CreateMessageHtml(...)` and CSS classes:

- `AIInteractionText` → Markdown content (with optional reasoning panel)
- `AIInteractionToolCall` → compact card with tool name and arguments
- `AIInteractionToolResult` → result payload (e.g., JSON block)
- `AIInteractionImage` → `<img>` with caption/alt text
- `AIInteractionError` → styled error bubble

Each message uses role classes like `.message.user`, `.message.assistant`, `.message.system`, `.message.tool`, enabling distinct styling.

## Host ↔ JS API

- JS functions (defined in `js/chat-script.js`):
  - `addMessage(html)`
  - `upsertMessage(key, html)`
  - `replaceLastMessageByRole(role, html)`
  - `addLoadingMessage(role, text)` / `removeThinkingMessage()`
  - `clearMessages()`
  - `setStatus(text)`
  - `setProcessing(isProcessing)`
  - `showToast(message)`
- Host functions (C# in `WebChatDialog.cs` / `WebChatObserver.cs`):
  - `AddInteractionToWebView(IAIInteraction)`
  - `UpsertMessageByKey(string domKey, IAIInteraction)`
  - `ReplaceLastMessageByRole(AIAgent, IAIInteraction)`
  - `ExecuteScript(string)` (UI-thread marshaled)

## Bridge & Schemes

- See: [WebView ↔ Host Bridge](./WebView-Bridge.md)
  - Custom URL schemes: `sh://event`, `clipboard://copy`
  - Query encoding and routing
  - Threading rules and deferring actions from `DocumentLoading`

## Threading

All UI work (including `ExecuteScript`) is marshaled via `RhinoApp.InvokeOnUiThread(...)`. DOM updates are serialized by the dialog to avoid re-entrancy into the WebView’s script engine.

## Clipboard

`chat-script.js` uses `clipboard://copy?text=...` for code-block copy. The host intercepts it and sets system clipboard text, then shows a toast in the WebView.

## Consumers

- `CanvasButton` and `AIChatComponent` open/reuse the dialog via `WebChatUtils`. They receive incremental `ChatUpdated` snapshots to surface transcript/metrics in Grasshopper.

## Rendering contracts

To eliminate type switches in the renderer and observer layers, interactions implement explicit contracts:

- **`IAIRenderInteraction`** (`src/SmartHopper.Infrastructure/AICall/Core/Interactions/IAIRenderInteraction.cs`)
  - `GetRoleClassForRender()` → returns the CSS role class (e.g., `assistant`, `user`, `tool`, `error`).
  - `GetDisplayNameForRender()` → friendly label for the message header.
  - `GetRawContentForRender()` → raw markdown content (converted to HTML by `ChatResourceManager`).
  - `GetRawReasoningForRender()` → optional reasoning content (supports `<think>…</think>`); rendered as a collapsible panel.

- **`IAIKeyedInteraction`** (`src/SmartHopper.Infrastructure/AICall/Core/Interactions/IAIKeyedInteraction.cs`)
  - `GetStreamKey()` → stable grouping key for streaming aggregation (multiple deltas update one bubble).
  - `GetDedupKey()` → stable identity for persisted/history messages (prevents duplicates and supports hydration).

## Streaming lifecycle and re‑keying

- During streaming, assistant text is aggregated under a constant stream key computed via `GetStreamKey()`; the UI updates the same DOM node using `upsertMessage(key, …)`.
- On finalization (`WebChatObserver.OnFinal`), the streaming bubble is replaced with a finalized message keyed by the interaction’s **dedup key** (`GetDedupKey()`):
  - This prevents new assistant turns from overwriting previous replies and ensures accurate history replay.

Notes:
- Non‑assistant interactions (tool calls/results, system, errors) are appended only once persisted by the session.
- DOM updates are serialized and marshaled to Rhino’s UI thread to avoid WebView re‑entrancy.
