---
trigger: glob
globs: src/SmartHopper.Core/UI/Chat/**
---

# WebChat architecture

The chat UI is a full WebView interface. Preserve the existing separation of concerns:

- `ConversationSession`: conversation history, provider calls, tool loops, streaming, cancellation, and stable final results.
- `HtmlChatRenderer`: converts interactions to HTML.
- `WebChatDialog`: hosts the WebView, intercepts JS-to-host URL schemes, and injects serialized DOM updates.
- `WebChatObserver`: maps `IConversationObserver` events to incremental DOM updates.
- `Resources/`: local HTML, CSS, JavaScript, templates, and third-party assets.

## Bridge rules

- JavaScript sends actions through intercepted URL schemes:
  - `sh://event?type=send&text=...`
  - `sh://event?type=clear`
  - `sh://event?type=cancel`
  - `clipboard://copy?text=...`
- URL query keys and values must be encoded in JavaScript and decoded in C#.
- Host actions triggered from `DocumentLoading` must be deferred to the next UI tick to avoid WebView re-entrancy.
- All UI updates must run on Rhino's UI thread via `RhinoApp.InvokeOnUiThread(...)`.
- Serialize and batch DOM updates; avoid one WebView script injection per streaming token.

## Dependencies

Host third-party WebChat JavaScript and CSS locally. Do not use external CDNs.

See `docs/UI/Chat/index.md` and `docs/UI/Chat/WebView-Bridge.md`.
