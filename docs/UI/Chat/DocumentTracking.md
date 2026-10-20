# Chat Document Tracking

WebChat records which Grasshopper document the conversation is working on and what changed on its canvas between AI responses, as first-class conversation interactions.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Core.Grasshopper/Utils/Canvas/ChatDocumentTracker.cs`, `src/SmartHopper.ProviderSdk/AICall/Core/Interactions/AIInteractionDocumentEvent.cs` |
| **Since Version** | 2.0.0-dev |
| **Last Updated** | 2026-10-20 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

**You should read this if you:**

- Work on WebChat history, rendering, or what the model sees between turns
- Add a new interaction type that must be visible in the UI and sent to the model
- Debug "Active document", "Switched to", or "Canvas changed" cards

---

## End-User Guide

A chat dialog is not bound to one file: tools and context always use the **active** Grasshopper document. The chat shows small "Canvas" cards so both you and the AI know which document is in use and what changed outside the AI's knowledge:

- `Active document: **file.gh**` — when the chat opens (or when a document first becomes active).
- `Switched to **file.gh**` — when you send a message after switching documents. No diff is reported for the new document; the file context already reports its counts.
- `Canvas changed since the last AI response: …` — when the active document changed structurally since the last AI response, whoever made the change.

The change sentence always lists: components added / removed / modified, input modifiers changed, output modifiers changed, connections added / removed (a rewired connection is one removal plus one addition). Group changes are appended only when non-zero. Small GhPatches (≤ 4000 characters) are included as a collapsed JSON block; larger ones are replaced by a `gh_get` hint listing affected component GUIDs and removed components.

Ignored as noise: component positions (pivots), errors/warnings/remarks, runtime data, selection, and metadata.

---

## Developer Reference

### Data flow

```
WebChatDialog.InitializeNewConversationAsync ─▶ tracker.Start()           ─▶ "Active document" + baseline
WebChatDialog.ProcessAIInteraction (before user msg) ─▶ tracker.CollectChanges() ─▶ switch / change events
WebChatDialog.ProcessAIInteraction (finally)  ─▶ tracker.Rebaseline()    ─▶ baseline = canvas after AI response
events ─▶ ConversationSession.AddInteraction ─▶ history + WebChatObserver render + replay
history ─▶ provider Encode ─▶ InteractionModelProjection ─▶ user text "[Canvas] …"
```

### Contracts

- `AIInteractionDocumentEvent` (ProviderSdk): `Kind` (`ActiveDocument`, `SwitchedDocument`, `CanvasChanged`), `DocumentId`, `DocumentName`, `Content` (deterministic Markdown shared by UI and model), unique `Id` used for stream/dedup keys. `Agent` is fixed to `User`; it renders with the collapsible `system` card style and header "Canvas".
- `IAIModelProjectedInteraction` + `InteractionModelProjection.Project(...)` (ProviderSdk): projects such interactions into user `AIInteractionText` at encode time. Consecutive events are prepended to the next user text (providers such as DeepSeek reasoner reject consecutive user messages) or emitted standalone. Called from `AIProvider.MergeSystemAndSummary` (all OpenAI-compatible, Anthropic, Mistral, Ollama, DeepSeek, LocalAI providers) and from `GeminiProvider.Encode`.
- `IChatDocumentTracker` / `ChatDocumentTracking.TrackerFactory` (Core): WebChat's seam. `SmartHopperAssemblyPriority` registers `ChatDocumentTracker` (Core.Grasshopper), because GhJSON.Grasshopper serialization is not available in Core.

### Diffing

- Snapshot: `GhJsonGrasshopper.Serialize(doc.Objects, …)` with connections, groups and internalized data; without runtime data/messages, selection and metadata. Serialized on the Rhino UI thread.
- `GhJsonStableIdMap` rewrites sequential ids to ids stable per instance GUID for the tracked document, because GhPatch matches connections by endpoint ids (removing a component would otherwise report spurious connection changes).
- `CanvasChangeSummary.Build(...)` diffs with `IgnorePivots`, `IgnoreRuntimeMessages`, `IgnoreMetadata*` and no base checksum, drops metadata ops, and returns null when nothing structural changed.

### Storage

`ChatCanvasBaselineStore` keeps one compact GhJSON baseline per chat dialog in `%APPDATA%/Grasshopper/SmartHopper/ChatCanvasBaselines/<id>.ghjson` instead of RAM. Deleted when the dialog closes; files older than 3 days are pruned when a new store is created. All I/O is best-effort and never breaks the chat.

### Gotchas

- The baseline is updated right after a change is reported and again after every AI response, so AI-made edits are never reported back as user changes and failed turns do not repeat a summary.
- GhPatch component ids in the summary are tracking ids, not the sequential ids returned by `gh_get`; use the instance GUIDs to correlate.
- `GhJson.Diff` clears `InstanceGuid` on added components of the right-hand document in place, so `CanvasChangeSummary` diffs a JSON copy of the current snapshot and resolves added-component GUIDs from the original.

---

## Architecture & Design

### Why a dedicated interaction (not a context provider or a System message)?

- Context providers are re-injected every call and replace each other (`ContextInjectionRequestPolicy` strips `AIAgent.Context`), so they are not visible history.
- Request policies mutate the session request, so converting in a policy would erase the dedicated type from history.
- `System` interactions are hoisted into the system prompt by Anthropic/Gemini and `MergeSystemAndSummary` keeps only the last one, which would overwrite the real system prompt and lose timeline position.
