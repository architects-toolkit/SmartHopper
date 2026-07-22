# Streaming in AICall and Providers

SmartHopper supports provider-agnostic streaming of incremental AI results with coalescing, cancellation, and tool-call awareness.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Infrastructure/AICall/Streaming/` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

Streaming enables UIs to render AI tokens as they arrive, show tool calls promptly, and cancel responsively. This documentation explains how the streaming pipeline works end-to-end, from provider adapters to WebView rendering.

**You should read this if you:**

- Are implementing a new provider and need to add streaming support
- Are building UI that consumes live AI responses and tool events
- Need to debug cancellation, coalescing, or fallback behavior in streaming paths

---

## End-User Guide

SmartHopper supports provider-agnostic streaming of incremental results. When enabled, UI can render tokens as they arrive, show tool calls promptly, and cancel responsively.

- Conversation API: `ConversationSession.Stream(SessionOptions, StreamingOptions)` yields `AIReturn` deltas.
- Provider integration: providers may expose a `GetStreamingAdapter()` returning an `IStreamingAdapter`.
- Validation: streaming is gated by model/provider capability and provider settings.

### Buffering, coalescing and cancellation

- `StreamingOptions` enables UI-friendly coalescing to limit UI updates frequency/size.
- Cancellation token flows from `ConversationSession.Stream(...)` to adapters and HTTP calls.
- During streaming, UI consumption should prefer `OnDelta(...)` for live updates; placeholders are optional and provider-specific.

### Fallback behavior

If no adapter is available or streaming is disabled/unsupported, `ConversationSession.Stream(...)` performs a non-streaming provider call for the turn, merges/persists interactions, optionally processes tools, yields the non-streaming result, and finalizes when stable.

---

## Developer Reference

### IStreamingAdapter contract

```csharp
public interface IStreamingAdapter
{
    IAsyncEnumerable<AIReturn> StreamAsync(
        AIRequestCall request,
        StreamingOptions options,
        CancellationToken cancellationToken = default);
}

```

`StreamingOptions` controls basic backpressure:

- `CoalesceTokens` (bool, default true)
- `CoalesceDelayMs` (int ms, default 40)
- `PreferredChunkSize` (int chars, default 64)

Adapters must:

- Honor cancellation promptly.
- Avoid unbounded buffering and emit deltas regularly.
- Use `AICallStatus.Processing` -> `Streaming` -> `Finished` appropriately.

### Base utilities for adapters

Derive from `AIProviderStreamingAdapter` to reuse common functionality:

- HTTP client creation and auth headers
- SSE POST helper: `CreateSsePost(url, body, contentType)`
- SSE reader: `ReadSseDataAsync(response, ct)` yields `data:` payload lines

This reduces boilerplate while keeping API-specific parsing in the provider adapter.

### UI consumption pattern

```csharp
var session = new ConversationSession(request, observer);
var sessionOpts = new SessionOptions { ProcessTools = true, MaxTurns = 3, MaxToolPasses = 2 };
var streamOpts = new StreamingOptions { CoalesceTokens = true, CoalesceDelayMs = 40, PreferredChunkSize = 64 };

await foreach (var delta in session.Stream(sessionOpts, streamOpts, ct))
{
    // UI can append delta text, handle tool calls/results, etc.
}

```

Use an `IConversationObserver` implementation to update UI consistently:

- `OnStart` -- create UI placeholders
- `OnInteractionCompleted` -- update text progressively
- `OnToolCall`/`OnToolResult` -- show tool activity
- `OnFinal` -- persist final content and metrics
- `OnError` -- surface failures

### Implementing a provider adapter

- Expose a parameterless `GetStreamingAdapter()` method on the provider returning an implementation of `IStreamingAdapter`.
- Use provider's `PreCall(...)` to set endpoint/method/auth; add `stream=true` or equivalent as required by the API.
- Parse streaming protocol:
  - OpenAI: SSE from `/v1/chat/completions` with `choices[].delta.content`.
  - MistralAI: chunked JSON with `chunk` entries.
  - DeepSeek: chunked JSON with `choices[].delta`.
- Produce `AIReturn` deltas with `AIInteractionText` (assistant) and optional tool call interactions. Tool call deltas may be surfaced during stream for UI awareness; the session persists the latest tool_calls snapshot once after streaming ends.

Tip: see `OpenAIProvider.OpenAIStreamingAdapter` for a complete example.

---

## Architecture & Design

- Contract: `src/SmartHopper.Infrastructure/Streaming/IStreamingAdapter.cs`
- Base adapter utils: `src/SmartHopper.Infrastructure/AIProviders/AIProviderStreamingAdapter.cs`
- Session integration: `src/SmartHopper.Infrastructure/AICall/Sessions/ConversationSession.cs`
- Example: `src/SmartHopper.Providers.OpenAI/OpenAIProvider.cs` (nested `OpenAIStreamingAdapter`, `GetStreamingAdapter()`)

### ConversationSession.Stream flow

- Sets `Request.WantsStreaming = true` and validates (model supports streaming, provider settings allow it). If invalid, yields an error `AIReturn` and ends.
- Probes provider for a streaming adapter (via the provider executor's `TryGetStreamingAdapter(request)`).
  - If found, iterates `adapter.StreamAsync(...)` and forwards token/text deltas via `OnDelta(...)` as they arrive; deltas are also yielded to the caller.
  - If not found, falls back to a non-streaming single provider call for the turn.
- After streaming ends, the session persists a single stable snapshot into history (final assistant text and latest tool_calls snapshot), updates `_lastReturn`, and emits a partial snapshot via `OnInteractionCompleted(...)` followed by `OnFinal(...)` when appropriate.
- In non-streaming fallback, the session merges new interactions immediately and emits `OnInteractionCompleted(...)` for the single-shot result.
- Executes pending tool calls when `SessionOptions.ProcessTools` is true (both in non-streaming fallback and after stream persistence), emitting `OnToolCall`/`OnToolResult` as tool passes proceed.

### Migration and testing

- Providers can add streaming incrementally without affecting non-streaming paths.
- Add unit/integration tests to validate:
  - Capability gating and provider setting toggles
  - Correct status transitions and observer callbacks
  - Coalescing behavior and cancellation responsiveness
