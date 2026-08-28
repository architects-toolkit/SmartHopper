# Interactions

Covers `IAIInteraction` and concrete message types.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.ProviderSdk/AICall/Core/Interactions/` |
| **Since Version** | ? |
| **Last Updated** | 2026-08-28 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

Interactions are the fundamental message units exchanged between users, AI models, tools, and the UI layer. This page catalogs every interaction type, their fields, provider encoding rules, and the rendering contracts that power the chat interface.

**You should read this if you:**

- Need to create, inspect, or extend interaction types
- Are implementing a new provider and need to know how interactions map to provider APIs
- Are working on UI rendering or streaming aggregation logic

---

## End-User Guide

This section provides an overview of all interaction types available in the AICall system.

### IAIInteraction

- File: `IAIInteraction.cs`
- Properties:
  - `string TurnId`
  - `DateTime Time`
  - `AIAgent Agent`
  - `AIMetrics Metrics`

### AIInteractionText

- File: `AIInteractionText.cs`
- Purpose: text output with optional reasoning.
- Fields: `Content`, `Reasoning` (both `init`-only).
- Copy-returning methods:
  - `WithResult(AIAgent agent, string content, string? reasoning)` — returns a new interaction with the given content and reasoning.
  - `WithDeltaMetrics(AIMetrics metricsDelta)` — returns a new interaction with metrics combined into the existing metrics.
- `ToString()` formats `reasoning\n\ncontent`.
- Streaming: use the nested `AIInteractionText.Builder` to accumulate deltas, then call `Build()` to emit the immutable snapshot.

### AIInteractionImage

- File: `AIInteractionImage.cs`
- Purpose: image generation results, prompts, and vision input (image understanding)
- Fields: `ImageUrl`, `ImageData`, `RevisedPrompt`, `OriginalPrompt`, `ImageSize` (default `1024x1024`), `ImageQuality` (default `standard`), `ImageStyle` (default `vivid`), `AspectRatio`, `MimeType` (for vision base64 input)
- Copy-returning methods:
  - `WithRequest(prompt, size?, quality?, style?, aspectRatio?)` records desired image generation parameters.
  - `WithResult(imageUrl?, imageData?, revisedPrompt?)` (one of url/data required).
  - `WithVisionInput(Uri imageUrl)` creates a vision input from a URL.
  - `WithVisionInput(string imageUrl)` creates a vision input from a URL string.
  - `WithVisionInputFromBase64(string base64Data, string mimeType = "image/png")` creates a vision input from base64-encoded image data.

### AIInteractionToolCall

- File: `AIInteractionToolCall.cs`
- Purpose: model asks to invoke a tool
- Fields: `Id`, `Name`, `Arguments` (JObject)
- Methods: `ToString()` pretty prints name, id and JSON args
- Agent defaults to `ToolCall`
- See also: [Tools](./tools.md) for how pending tool calls are executed and orchestrated.

### AIInteractionToolResult

- File: `AIInteractionToolResult.cs`
- Purpose: result of executing a tool
- Inherits: `AIInteractionToolCall`
- Adds field: `Result` (JObject)
- Overrides agent to `ToolResult`
- See also: [Tools](./tools.md) for result aggregation and message handling.

### AIInteractionRuntimeMessage

- File: `AIInteractionRuntimeMessage.cs`
- Purpose: unified UI-only diagnostic interaction carrying structured runtime message metadata (severity, origin, code, surfaceable flag, content)
- Replaces previous four distinct interaction types (Debug, Info, Warning, Error) with severity modeled as data rather than type
- **Critical**: Providers must skip all instances of this class during request encoding — these entries are for UI/diagnostics only and must never be sent to the AI model
- Fields:
  - `Severity` (SHRuntimeMessageSeverity: Debug/Info/Warning/Error) — determines effective Agent, CSS role class, and display name
  - `Code` (SHMessageCode) — machine-readable diagnostic code; defaults to Unknown
  - `Origin` (SHRuntimeMessageOrigin: Request/Return/Provider/Tool/Network/Validation/Worker) — who emitted this diagnostic
  - `Surfaceable` (bool) — whether this diagnostic should be surfaced to end users; defaults to true for Info/Warning/Error, false for Debug
  - `Content` (string) — human-readable diagnostic text
- Methods:
  - `WithResult(string content, AIMetrics? metrics = null)` — returns a new diagnostic interaction with the given content and metrics.
  - `ToRuntimeMessage()` — project into an equivalent `SHRuntimeMessage`.
  - `FromRuntimeMessage(SHRuntimeMessage message)` — static factory creating an interaction from a runtime message.
  - `CreateDebug(string content, AIMetrics? metrics = null)` — static factory for a debug-level diagnostic (non-surfaceable by default).
  - `GetRoleClassForRender()` — returns CSS role class based on severity
  - `GetDisplayNameForRender()` — returns display label for UI
  - `GetRawContentForRender()` — returns diagnostic content for rendering
  - `GetRawReasoningForRender()` — returns empty string (diagnostics have no reasoning)
  - `GetStreamKey()` — stable grouping key for streaming
  - `GetDedupKey()` — stable identity for persisted messages
- Implements: `IAIKeyedInteraction`, `IAIRenderInteraction`

### AIInteractionAudio

- File: `AIInteractionAudio.cs`
- Purpose: audio interaction for speech-to-text or text-to-speech operations
- Fields:
  - `Data` (byte[]) — audio data as a byte array; either Data or FilePath should be set, not both
  - `FilePath` (string) — file path to the audio file; either Data or FilePath should be set, not both
  - `MimeType` (string) — MIME type of the audio (e.g., "audio/wav", "audio/mp3", "audio/mpeg")
  - `LanguageHint` (string) — optional language hint for speech-to-text operations; ISO 639-1 language code format (e.g., "en", "es", "fr")
- Methods:
  - `GetAudioSize()` — returns the size of the audio data in bytes; handles both in-memory and file-based audio
  - `GetStreamKey()` — returns a stable stream grouping key using file path when available, otherwise a short hash of audio data
  - `GetDedupKey()` — returns a stable de-duplication key including stream key and MIME type to distinguish similar audio files
  - `ToString()` — returns a formatted string containing audio metadata (MIME type, source, size, language hint)
- Implements: `IAIKeyedInteraction`

---

## Developer Reference

### AIAgent

- File: `AIAgent.cs`
- Enum roles: `Context`, `System`, `User`, `Assistant`, `ToolCall`, `ToolResult`, `Unknown`
- Extension helpers: `.ToString()`, `.ToDescription()`, `FromString(string)`

### IAIRenderInteraction

- File: `src/SmartHopper.ProviderSdk/AICall/Core/Interactions/IAIRenderInteraction.cs`
- Purpose: eliminate type switches in UI rendering by letting each interaction define how it should be displayed.
- Methods:
  - `GetRoleClassForRender()` → returns the CSS role class (e.g., `assistant`, `user`, `tool`, `error`).
  - `GetDisplayNameForRender()` → the display label used in the message header.
  - `GetRawContentForRender()` → raw markdown content (converted to HTML by `ChatResourceManager`).
  - `GetRawReasoningForRender()` → optional reasoning; supports `reasoning` and is rendered as a collapsible panel in the UI.

Consumption:

- Used by `ChatResourceManager.CreateMessageHtml(...)` and `HtmlChatRenderer.RenderInteraction(...)` to build the final HTML message without casting on interaction type.

### IAIKeyedInteraction

- File: `src/SmartHopper.ProviderSdk/AICall/Core/Interactions/IAIKeyedInteraction.cs`
- Purpose: provide stable identity keys to aggregate streaming updates and to de-duplicate persisted messages.
- Methods:
  - `GetStreamKey()` → stable grouping key for streaming (multiple deltas update a single bubble in UI).
  - `GetDedupKey()` → stable identity for persisted messages (used for history hydration and to avoid duplicates).

Consumption:

- Used by the chat UI observer (`WebChatObserver`) to:
  - Upsert streaming content via `GetStreamKey()`.
  - On finalization, re-key the assistant bubble from stream key → `GetDedupKey()` so later assistant turns don't overwrite previous ones.

### Code Examples

```csharp
// Creating a text interaction
var textInteraction = new AIInteractionText
{
    Agent = AIAgent.Assistant,
    Content = "Hello, world!",
    Reasoning = "Reasoning text here",
};

// Or copy from a blank instance
textInteraction = new AIInteractionText().WithResult(AIAgent.Assistant, "Hello, world!", "Reasoning text here");

// Building a streamed text interaction
var builder = new AIInteractionText.Builder();
builder.WithResult(AIAgent.Assistant, string.Empty)
       .AppendContent("Hello")
       .AppendContent(", world!");
var streamedText = builder.Build();

// Creating a tool call interaction
var toolCall = new AIInteractionToolCall
{
    Id = "call_123",
    Name = "GetWeather",
    Arguments = JObject.Parse("{ \"city\": \"Barcelona\" }")
};

```

```csharp
// Creating a vision input from a URL
var visionInput = new AIInteractionImage().WithVisionInput("<https://example.com/image.png">);

// Creating a runtime message for diagnostics
var runtimeMsg = AIInteractionRuntimeMessage.FromRuntimeMessage(
    new SHRuntimeMessage { Content = "Connection restored", Severity = SHRuntimeMessageSeverity.Info }
);

// Updating an image generation request
var imageRequest = new AIInteractionImage().WithRequest(
    prompt: "A futuristic building in Barcelona",
    size: "1024x1024");

```

---

## Architecture & Design

### Immutability

- All interaction records are immutable after construction.
- Use object initializers, `with` expressions, or the copy-returning `With...` helpers to create variants.
- Streaming and incremental aggregation use the nested `Builder` classes (for example, `AIInteractionText.Builder`) to accumulate mutable local state and then emit an immutable snapshot with `Build()`.
- `AIBody` and `AIReturn` are immutable snapshots of a request/response turn; they contain `IReadOnlyList<T>` collections and have no post-construction mutation methods.

### Provider Encoding

- **OpenAI**: `image_url` content block with URL or `data:{mime};base64,{data}` data URI
- **Anthropic**: `image` content block with `base64` or `url` source type
- **MistralAI**: OpenAI-compatible `image_url` content block with data URI support
- **DeepSeek**: falls back to `OriginalPrompt` text (no `ImageInput` capability)


