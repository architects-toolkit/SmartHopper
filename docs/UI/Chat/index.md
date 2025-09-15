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
  - `replaceLastMessageByRole(role, html)`
  - `clearMessages()`
  - `setStatus(text)`
  - `setProcessing(isProcessing)`
  - `showToast(message)`
- Host functions (C# in `WebChatDialog.cs` / `WebChatObserver.cs`):
  - `AddInteractionToWebView(IAIInteraction)`
  - `ReplaceLastMessageByRole(AIAgent, IAIInteraction)`
  - `ExecuteScript(string)` (UI-thread marshaled)

## Bridge & Schemes

- See: [WebView ↔ Host Bridge](./WebView-Bridge.md)
  - Custom URL schemes: `sh://event`, `clipboard://copy`
  - Query encoding and routing
  - Threading rules and deferring actions from `DocumentLoading`

## Threading

All UI work (including `ExecuteScript`) is marshaled via `RhinoApp.InvokeOnUiThread(...)`. DOM updates are serialized by the dialog to avoid re-entrancy into the WebView’s script engine.

## Styling (minimal additions)

The CSS now includes minimal styles for:

- `#status-bar` and `#status-text`
- `.spinner` and `.hidden`
- `#input-bar` (+ buttons and textarea)

## Clipboard

`chat-script.js` uses `clipboard://copy?text=...` for code-block copy. The host intercepts it and sets system clipboard text, then shows a toast in the WebView.

## Consumers

- `CanvasButton` and `AIChatComponent` continue to open/reuse the dialog via `WebChatUtils`. They receive incremental `ChatUpdated` snapshots to surface transcript/metrics in Grasshopper.
