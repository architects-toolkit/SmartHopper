# Agentic Copilot and Consent

SmartHopper WebChat can propose explicit plans and all supported AI-driven canvas mutations pass through a shared, invocation-scoped consent gate.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Infrastructure/Consent/`, `src/SmartHopper.Infrastructure/AICall/Subagents/`, `src/SmartHopper.Core/UI/Chat/`, `src/SmartHopper.Core.Grasshopper/AITools/` |
| **Since Version** | 2.1.0 |
| **Last Updated** | 2026-11-01 |
| **Documentation Maintainer** | Devin AI |

---

## Why Read This?

Read this when changing plan approval, tool exposure, graphical canvas review, or any path that lets AI tools and components modify another Grasshopper object.

## End-User Guide

For non-trivial multi-step work, the assistant may show a plan card. Approving it permits the assistant to continue with that approach; it does not approve later canvas edits.

While working, the assistant can maintain a live task plan as a native card in the transcript, listing each task as pending, in progress, or completed with a progress indicator. The card updates in place as work advances and keeps its final state until replaced or reset.

The assistant may also ask the user a blocking question mid-run (`ask_user`), showing 2–4 answer options plus a free-text field in a question card, and may point at components or canvas regions (`canvas_point`) — a replay action on the transcript row pans, zooms and highlights the target.

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
| `TaskPlan` | Full-state copilot task list snapshot |
| `ITaskPlanPresenter` | UI adapter that renders task plan updates without a user decision |
| `UserQuestionRequest`/`UserQuestionAnswer` | Blocking question + terminal answer for `ask_user` |
| `IUserQuestionPresenter` | UI adapter that renders the question card and awaits the answer |
| `CanvasPointerRequest` | Replayable pan/zoom/highlight target for `canvas_point` |
| `ICanvasPointerPresenter` | UI adapter that renders the pointer card (canvas owned by Core.Grasshopper) |
| `CanvasPointerBridge` | Process-wide handler seam letting chat replay a pointer without Grasshopper references |
| `ISubagentHost` | Per-session conductor contract carried on `MutationInvocationContext` |
| `SubagentHost` | Owns the session's subagents; launches child `ConversationSession` runs and raises `RunFinished` |
| `Subagent`/`SubagentRun` | Worker record plus one run per instruction; `Delivered` tracks whether the outcome reached the conductor |
| `SubagentRunReport` | Terminal-run payload handed to the chat host (evidence line + wake trigger) |

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
- `plan_propose`, `plan_tasks`, `ask_user`, and the four conductor tools (`spawn_subagent`, `send_to_subagent`, `wait_subagents`, `list_subagents`) are Chat-only and are rejected by MCP and batch execution.
- `AIMutatingTool` excludes Batch by default.
- Surface checks occur both when formatting/discovering tools and immediately before execution.

### Cancellation

Consent waits use the invocation cancellation token. Cancelling WebChat, a component worker, or the graphical dialog resolves the request without applying changes. Tool execution uses linked timeout cancellation so an operation cannot complete later as an unobserved mutation after reporting timeout.

## Architecture & Design

Every `AIMutatingTool` is wrapped by the host undo coordinator. Existing operation-specific APIs still record the correct pivot, wire, object, state, add, and remove actions; the coordinator verifies that a successful call produced an undo record and merges multiple records from one tool call into one Ctrl+Z step.

Consent is invocation-scoped rather than conversation-scoped because SmartHopper has several mutation callers. UI is separated through presenters: WebChat renders plan cards, while Core.Grasshopper owns GhJSON-aware canvas overlays. Low-level helpers such as `ScriptModifier`, `CanvasAccess`, and `GhJsonGrasshopper` remain apply primitives and must not open consent UI themselves.

Task plan visualization rides the same invocation-context presenter seam as consent but never blocks on a decision. `plan_tasks` sends a complete `TaskPlan` snapshot on every call; the WebChat presenter renders it as a native `card:task_plan` interaction (id `sh-task-plan`, upserted in place) inside the vendored chat-ui view — structured plugin surfaces travel as canonical parts/cards rather than DOM injections, so progress stays visible without leaving the transcript. Because no consent is involved, the tool call returns immediately after the update is queued.

`ask_user` uses the same seam in the blocking direction: the tool awaits the presenter's `Task` until the user picks an option, submits free text, or the run's cancellation token fires — the card is a native `card:question`. `canvas_point` is the Grasshopper-side counterpart — the tool executes the pan/zoom/highlight via `CanvasPointerService` on the Rhino UI thread, registers the request by pointer id, and the transcript row's `canvas_pointer` action replays it through `CanvasPointerBridge` so `SmartHopper.Core` never references Grasshopper APIs. Plan consent renders as a native `card:approval`.

Subagents ride the same invocation-context channel without a presenter: `MutationInvocationContext.SubagentHost` carries the session's `ISubagentHost` into every tool call, so the conductor tools resolve their owning host the way consent proposals resolve their presenter. Each `spawn_subagent` clones the parent request (provider, model, endpoint, tool filter minus the conductor tools — a depth cap of 1) into a child `ConversationSession` that receives **no presenters and no subagent host**: consent-gated canvas mutations fail closed (`Unavailable`) and workers cannot spawn recursively. A finished run raises `SubagentHost.RunFinished`; WebChat then appends a visible evidence line and, when no turn is in flight, drives a wake turn whose transcript keeps a terse user marker while the full worker report rides as a system interaction. `wait_subagents` marks a run `Delivered` when its outcome is collected inline, which is what suppresses the automatic wake. Runs are in-memory (they do not survive a Rhino restart), bounded to `MaxSubagents`/`MaxConcurrentRuns` (8 each), and cancelled when the chat closes.

`send_to_subagent` no longer fails while the worker is mid-run: `mode: "queue"` (the default) parks the instruction in the worker's `PendingInstructions` FIFO, and `KickNext` dequeues the head into a new run the moment the current run settles — results still flow through the same `RunFinished` → evidence → wake pipeline. `mode: "on-the-fly"` steers the live run instead: the instruction is appended to the child session when no tool call is pending (a user message must not split a call/result pair), so the worker reads it at its next provider call; unsafe sends fall back to queue semantics. A steer that lands inside the last provider call's window is guaranteed a continuation run at settle — the host detects any trailing user text the finished run did not produce itself. Pending instructions surface in the chat's outgoing queue panel (`PendingItems` → `meta.agent` rows, `PendingChanged` re-renders) and accept the shared row actions via `TryApplyPendingAction` (remove/edit/promote). `send_to_subagent` results report `delivery` (`started`/`queued`/`steered`) and `pending_count` so the conductor sees where its instruction landed.

Plan approval and mutation approval are deliberately separate. A textual plan cannot authorize a later concrete graph whose arguments or affected objects may differ. Plans that contain canvas-mutating steps therefore skip the blocking approval card entirely — `plan_propose` emits a non-blocking `TaskPlan` checklist via `TaskPlanPresenter` and leaves the staged canvas review as the single consent gate, avoiding a double prompt for the same concrete change.

New-component placement always runs the shared GhJSON tidy-up layout (`CanvasNewComponentLayout` → `GhJsonGrasshopper.ComputeLayout`: dependency-graph order, measured bounds, port alignment, wire corridors, collision resolution) before review and before `Put`, with `PutOptions.AutoOffset` disabled once pivots are baked — so staged preview ghosts render at the final proposed location and match what is applied on accept.

Category and tag labels (e.g. `Planning`, `planning`) are descriptive only. `AIToolSurface` is the access-control contract used for MCP and batch isolation.

## Related Documentation

- [AI Canvas Change Review](../UI/ai-change-review.md)
- [Chat UI](../UI/Chat/index.md)
- [AICall Tools](../Providers/AICall/tools.md)
- [MCP Server](mcp-server.md)
