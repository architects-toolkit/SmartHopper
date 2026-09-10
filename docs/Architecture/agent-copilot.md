# Agentic Copilot and Consent

SmartHopper WebChat can propose explicit plans and all supported AI-driven canvas mutations pass through a shared, invocation-scoped consent gate.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Infrastructure/Consent/`, `src/SmartHopper.Core/UI/Chat/`, `src/SmartHopper.Core.Grasshopper/AITools/` |
| **Since Version** | 2.1.0 |
| **Last Updated** | 2026-09-10 |
| **Documentation Maintainer** | Devin AI |

---

## Why Read This?

Read this when changing plan approval, tool exposure, graphical canvas review, or any path that lets AI tools and components modify another Grasshopper object.

## End-User Guide

For non-trivial multi-step work, the assistant may show a plan card. Approving it permits the assistant to continue with that approach; it does not approve later canvas edits.

Each concrete canvas mutation is reviewed separately. Structural and component-state changes are painted on the live canvas and listed as selectable changes. Apply accepts the selected subset; Reject or Cancel leaves the rejected subset unchanged.

The same graphical review applies when a mutating tool is triggered by WebChat, MCP, another AI tool, or a direct SmartHopper Grasshopper component such as Place GhJSON.

## Developer Reference

### Core contracts

| Type | Responsibility |
| --- | --- |
| `AIToolSurface` | Explicit Chat, Direct, Batch, and MCP exposure policy |
| `AIMutatingTool` | Standard metadata for non-batch canvas-mutating tools |
| `MutationInvocationContext` | Identifies the invocation independently of a conversation |
| `IConsentProposal` | Immutable proposal presented for one invocation |
| `IConsentPresenter` | UI adapter for a proposal kind |
| `ConsentGate` | Tracks one pending request per invocation and consumes one terminal decision |
| `MutationUndoCoordinator` | Host seam that verifies and coalesces undo records for every `AIMutatingTool` |
| `GrasshopperMutationUndoCoordinator` | Grasshopper implementation using the active document undo server |
| `CanvasMutationConsentProposal` | Adapts `CanvasChangeReviewSession` to consent |
| `CanvasChangeConsentPresenter` | Uses the graphical canvas review dialog and overlay |
| `PlanConsentProposal` | Structured WebChat plan |

### Execution flow

```text
caller -> AIToolCall / mutation operation
       -> validate tool surface
       -> prepare proposal without canvas changes
       -> ConsentGate -> matching presenter
       -> approved selection
       -> revalidate and apply
       -> normal tool result
```

`ConversationSession` propagates the WebChat presenter and Chat surface but does not own consent policy. Direct components create a `GrasshopperComponent` invocation context and can use the same gate without a conversation.

### Tool surfaces

- Existing ordinary tools default to all surfaces for compatibility.
- `plan_propose` is Chat-only and is rejected by MCP and batch execution.
- `AIMutatingTool` excludes Batch by default.
- Surface checks occur both when formatting/discovering tools and immediately before execution.

### Cancellation

Consent waits use the invocation cancellation token. Cancelling WebChat, a component worker, or the graphical dialog resolves the request without applying changes. Tool execution uses linked timeout cancellation so an operation cannot complete later as an unobserved mutation after reporting timeout.

## Architecture & Design

Every `AIMutatingTool` is wrapped by the host undo coordinator. Existing operation-specific APIs still record the correct pivot, wire, object, state, add, and remove actions; the coordinator verifies that a successful call produced an undo record and merges multiple records from one tool call into one Ctrl+Z step.

Consent is invocation-scoped rather than conversation-scoped because SmartHopper has several mutation callers. UI is separated through presenters: WebChat renders plan cards, while Core.Grasshopper owns GhJSON-aware canvas overlays. Low-level helpers such as `ScriptModifier`, `CanvasAccess`, and `GhJsonGrasshopper` remain apply primitives and must not open consent UI themselves.

Plan approval and mutation approval are deliberately separate. A textual plan cannot authorize a later concrete graph whose arguments or affected objects may differ.

Control tags are descriptive only. `AIToolSurface` is the access-control contract used for MCP and batch isolation.

## Related Documentation

- [AI Canvas Change Review](../UI/ai-change-review.md)
- [Chat UI](../UI/Chat/index.md)
- [AICall Tools](../Providers/AICall/tools.md)
- [MCP Server](mcp-server.md)
