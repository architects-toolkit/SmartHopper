---
trigger: model_decision
description: Information about AICall, AIRequest, AIToolCall, AIBody, AIAgent, AIInteraction*, AIReturn... Related with AI response generation logic
---

# AICall infrastructure

## Location

- `src/SmartHopper.Infrastructure/AICall/`
- Detailed docs: `docs/Providers/AICall/`

## Purpose

- Provide a provider-agnostic foundation to build, validate, execute, stream, and capture AI calls.
- Normalize provider-specific behavior into common request, interaction, return, metrics, status, and runtime-message models.

## Core concepts

- `AIAgent`: Context, System, User, Assistant, ToolCall, ToolResult.
- `AICallStatus`: Idle, Processing, Streaming, CallingTools, Finished.
- `IAIInteraction`: Common metadata for every interaction.
- `AIRequestCall`: Provider/model/capability/body plus HTTP details for one provider call.
- `AIBody`: Conversation history plus optional JSON schema, context filter, and tool filter. Context injection is non-mutating.
- `AIReturn`: Normalized result with body, raw provider payload, metrics, status, diagnostics, and errors.
- `AIToolCall`: Executes one tool call through the tool manager.
- `ConversationSession`: Orchestrates multi-turn calls, tool loops, streaming, observer callbacks, and final stable results.

## Execution guidance

1. Build an `AIBody` with interactions and optional context/tool/schema filters.
2. Create an `AIRequestCall` for a single provider turn.
3. Use `AIRequestCall.Exec()` for one provider call only.
4. Use `ConversationSession` when a workflow needs tool processing, bounded turns, streaming, observers, cancellation, or stable history persistence.
5. Use `AIToolCall.Exec()` or `AIToolManager` for exactly one tool call.

## Streaming guidance

- Provider streaming support is exposed through `IStreamingAdapter`.
- `ConversationSession.Stream(...)` gates streaming by provider/model/settings capabilities and falls back to non-streaming when appropriate.
- Streaming deltas should be emitted promptly, honor cancellation, and avoid unbounded buffering.
- UI consumers should prefer `IConversationObserver` callbacks for incremental rendering.

## Design priorities

- Keep provider-specific encoding/decoding in provider projects.
- Keep orchestration in `ConversationSession`, not components or providers.
- Attach structured diagnostics with `AIReturn.AddRuntimeMessage(...)` instead of raw log-only errors.
- Preserve metrics and raw payloads for debugging.
