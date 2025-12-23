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
  - Loads initial HTML (CSS/JS inline) via `HtmlChatRenderer.GetInitialHtml()`.
  - Handles JS → host events (send/clear/cancel/clipboard) in `WebView.DocumentLoading`.
  - Calls session methods and injects UI updates back using `ExecuteScript(...)` (`addMessage`, `replaceLastMessageByRole`, `setStatus`, `setProcessing`).
- `WebChatObserver` implements `IConversationObserver` and bridges `ConversationSession` streaming/partials/tool events to the WebView DOM incrementally.

## Rendering Interaction Types

Supported via `HtmlChatRenderer.RenderInteraction(...)` and CSS classes:

- `AIInteractionText` → Markdown content (with optional reasoning panel)
- `AIInteractionToolCall` → compact card with tool name and arguments
- `AIInteractionToolResult` → result payload (e.g., JSON block)
- `AIInteractionImage` → `<img>` with caption/alt text
- `AIInteractionError` → styled error bubble

Each message uses role classes like `.message.user`, `.message.assistant`, `.message.system`, `.message.tool`, enabling distinct styling.

## Host ↔ JS API

For the complete and authoritative API between the WebView and host, see:

- `docs/UI/Chat/WebView-Bridge.md`

This Chat UI overview intentionally avoids duplicating the detailed function lists to keep a single source of truth.

## Bridge & Schemes

- See: [WebView ↔ Host Bridge](./WebView-Bridge.md)
  - Custom URL schemes: `sh://event`, `clipboard://copy`
  - Query encoding and routing
  - Threading rules and deferring actions from `DocumentLoading`

## Threading

All UI work (including `ExecuteScript`) is marshaled via `RhinoApp.InvokeOnUiThread(...)`. DOM updates are serialized by the dialog to avoid re-entrancy into the WebView’s script engine.

## Performance pipeline (high level)

- The host minimizes WebView re-entrancy by enqueueing DOM operations (`RunWhenWebViewReady(...)`) and draining them in small batches.
- The drain scheduling is debounced to coalesce bursts of updates into fewer WebView script injections.
- `ExecuteScript(...)` enforces a small concurrency gate to avoid piling scripts into the WebView.

On the WebView side (`chat-script.js`):

- Message mutations (`addMessage`, `upsertMessage`, `upsertMessageAfter`, `replaceLastMessageByRole`) are enqueued and flushed using `requestAnimationFrame` (with a small timeout fallback).
- Rendering work is reduced using:
  - Template cloning for repeated HTML.
  - A keyed LRU cache + sampled diff checks to skip redundant DOM updates.
  - Optional patch payloads (JSON `{ patch, html }`) to append/replace only message content during streaming instead of re-sending full bubbles.
  - A max message HTML length cap to prevent huge DOM inserts.
- Lightweight perf counters are sampled to keep overhead low; only outlier renders are logged.

These changes also mitigate issue #261 ("ui eventually freezes on opening webchat").

## Clipboard

`chat-script.js` uses `clipboard://copy?text=...` for code-block copy. The host intercepts it and sets system clipboard text, then shows a toast in the WebView.

## Consumers

- `CanvasButton` and `AIChatComponent` open/reuse the dialog via `WebChatUtils`. They receive incremental `ChatUpdated` snapshots to surface transcript/metrics in Grasshopper.

## Rendering contracts

For the canonical documentation of interaction contracts (`IAIRenderInteraction`, `IAIKeyedInteraction`, concrete interaction types), see:

- `docs/Providers/AICall/interactions.md`

This page references rendering at a high level and leaves type-level details to the Interactions documentation.

## Streaming lifecycle and re‑keying

- During streaming, assistant token deltas are surfaced via `IConversationObserver.OnDelta(...)` and aggregated under a constant stream key computed via `GetStreamKey()`; the UI updates the same DOM node using `upsertMessage(key, …)`.

- After streaming ends, `ConversationSession` persists a single stable snapshot and surfaces it via `OnInteractionCompleted(...)`/`OnFinal(...)`. The streaming bubble is replaced with a finalized message keyed by the interaction’s **dedup key** (`GetDedupKey()`):

  - This prevents new assistant turns from overwriting previous replies and ensures accurate history replay.

Notes:

- Non‑assistant interactions (tool calls/results, system, errors) are appended only once persisted by the session.

- DOM updates are serialized and marshaled to Rhino’s UI thread to avoid WebView re‑entrancy.
