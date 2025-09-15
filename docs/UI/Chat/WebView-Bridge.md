# WebView ↔ Host Bridge

This document specifies the conventions used by the Chat UI WebView to communicate with the C# host, and the host-side threading/serialization rules that must be followed to keep Rhino and Eto.Forms responsive and stable.

- Host file: `src/SmartHopper.Core/UI/Chat/WebChatDialog.cs`
- JS file: `src/SmartHopper.Core/UI/Chat/Resources/js/chat-script.js`
- Rendering: `src/SmartHopper.Core/UI/Chat/HtmlChatRenderer.cs`

## Custom URL Schemes

The WebView sends actions to the host by navigating to custom URL schemes (navigation is intercepted and canceled by the host):

- sh://event?type=send&text=...
- sh://event?type=clear
- sh://event?type=cancel
- clipboard://copy?text=...

The C# host handles them in `WebChatDialog.WebView_DocumentLoading(...)`.

### Query-string rules

- Keys and values are URL-encoded. The host runs `Uri.UnescapeDataString(...)` on both keys and values.
- The `text` payload can contain newlines and markdown; it must be URL-encoded from JS before navigation.
- Unknown keys are ignored; unrecognized `type` values log a warning and no action is performed.

### Example (JS)

```js
// Send message (Enter key, Shift+Enter inserts newline)
const raw = input.value;                    // raw text from textarea
const encoded = encodeURIComponent(raw);    // URL-encode for transport
window.location.href = `sh://event?type=send&text=${encoded}`;
```

```js
// Clear transcript
window.location.href = `sh://event?type=clear`;
```

```js
// Cancel current run
window.location.href = `sh://event?type=cancel`;
```

```js
// Clipboard copy (used by code blocks)
const payload = encodeURIComponent(textToCopy);
window.location.href = `clipboard://copy?text=${payload}`;
```

## Host-side interception flow

- The host subscribes to `WebView.DocumentLoading`.
- For `sh://` and `clipboard://`, it sets `e.Cancel = true` to prevent actual navigation.
- It parses the query via a local helper: `ParseQueryString(uri.Query)`.
- It routes to: `SendMessage(text)`, `ClearChat()`, `CancelChat()`, or clipboard handling.

Important: To avoid re-entrancy and deadlocks in WebView during `DocumentLoading`, actions are deferred to the next UI tick:

```csharp
Application.Instance.AsyncInvoke(() => SendMessage(text));
```

This ensures scripts (e.g., `ExecuteScript(...)`) are not executed inside `DocumentLoading`.

## Threading and serialization rules

- All UI updates must run on Rhino's main UI thread using `Rhino.RhinoApp.InvokeOnUiThread(...)`.
- The dialog serializes DOM updates to avoid nested WebView script execution:
  - `RunWhenWebViewReady(Action)` queues and executes actions with a re-entrancy guard.
  - `ExecuteScript(string)` always marshals to the UI thread and is called only after the document is fully loaded.
- After deferring from `DocumentLoading`, host methods may safely call `ExecuteScript(...)` (e.g., `addMessage`, `setStatus`, `setProcessing`).

## JS ↔ Host API

JavaScript functions (in `chat-script.js`):

- `addMessage(html)` — Append a pre-rendered HTML message bubble.
- `replaceLastMessageByRole(role, html)` — Replace the last message with a given role.
- `clearMessages()` — Clear transcript area.
- `setStatus(text)` — Update status bar text.
- `setProcessing(isProcessing)` — Show/hide spinner and disable input.
- `showToast(message)` — Temporary notification.

Host functions (in `WebChatDialog.cs` / `WebChatObserver.cs`):

- `AddInteractionToWebView(IAIInteraction)`
- `ReplaceLastMessageByRole(AIAgent, IAIInteraction)`
- `ExecuteScript(string)`
- Observer callbacks: `OnStart`, `OnDelta`, `OnPartial`, `OnFinal`, `OnError`, `OnToolCall`, `OnToolResult` (drive incremental updates during streaming)

## Event types and lifecycle

- send
  - JS → host: `sh://event?type=send&text=...`
  - Host appends user bubble immediately, then schedules `ProcessAIInteraction()` on a background task.
  - Observer updates status/spinner and replaces the temporary loading bubble with streamed or final content.
- clear
  - JS → host: `sh://event?type=clear`
  - Host cancels any run and clears transcript in place (`clearMessages()`), emits reset snapshot.
- cancel
  - JS → host: `sh://event?type=cancel`
  - Host cancels current CTS and session (`ConversationSession.Cancel()`).
- clipboard
  - JS → host: `clipboard://copy?text=...`
  - Host sets system clipboard and calls `showToast('Copied to clipboard')`.

## Error handling

- If an exception occurs during processing, the host:
  - Appends a system error bubble
  - Sets `setStatus('Error')`, `setProcessing(false)`
  - Emits a snapshot (`ChatUpdated`)
- Validation failures when attempting streaming are logged and the host falls back to non-streaming execution.

## Testing checklist

- Enter-to-send works; Shift+Enter inserts newline.
- `send`, `clear`, `cancel` all execute when buttons are clicked or shortcuts are used.
- Clipboard copy creates a toast and the data is in the OS clipboard.
- While a run is in progress, spinner is visible and input is disabled.
- Streaming updates incrementally replace the loading bubble; fallback to non-streaming displays final answer.

## Security considerations

- Only `sh://event` and `clipboard://copy` are handled. All other schemes are ignored or allowed to navigate normally.
- The host never executes arbitrary JS received from the page; it only injects known safe JS functions under our control.
- User message text is treated as data and rendered as HTML via `HtmlChatRenderer` with Markdown conversion (avoid raw HTML injection from user content).
