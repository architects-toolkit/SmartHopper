---
agent: devin-local
created: 2026-09-10
status: implemented-unverified
branch: feature/2.1.0-agent-features
prerequisite: https://github.com/architects-toolkit/SmartHopper/pull/816
---

# SmartHopper agent-features plan

## Implementation status

Implemented on `feature/2.1.0-agent-features` after fast-forwarding PR #816's visual-diff branch. Compilation and automated tests were skipped at the user's direction because this environment has no installed .NET SDK; Roslyn syntax checks pass except expected source-generator partial-method diagnostics. Every concrete `AIMutatingTool` is wrapped by the Grasshopper undo coordinator, which verifies undo creation and merges multiple records from one call.

One conservative deviation remains: patch-to-canvas routes removals and placement through the same consent system as two sequential graphical reviews and aborts if removals are not fully accepted. It does not yet combine both stages into one atomic visual proposal because `gh_put` owns its accepted-subset placement pipeline. No deletion occurs without consent.

## 1. Objective

Turn WebChat into an opt-in agentic copilot without turning SmartHopper's generic tool catalog or MCP server into an agent runtime.

The copilot should:

1. Let the model call `plan_propose` when a user request benefits from a multi-step plan.
2. Pause that tool call until the user approves or rejects the proposed plan.
3. Treat plan approval as permission to continue reasoning only, not as permission for future canvas edits.
4. Pause every canvas-mutating tool call independently at a centralized `ConsentGate`.
5. Use the graphical, selectively applicable canvas diff from PR #816 for structural changes instead of a binary yes/no prompt.
6. Apply only the subchanges accepted in that graphical review.
7. Apply the same consent invariant to mutating Grasshopper components and direct component utilities even when no WebChat or `ConversationSession` exists.
8. Keep control tools such as `plan_propose` unavailable through MCP and batch execution.
9. Preserve existing MCP behavior for ordinary tools and avoid adding planning responsibilities to MCP.

## 2. Confirmed product decisions

- Agent surface: SmartHopper WebChat/copilot only.
- Planner entry point: model-initiated `plan_propose` AITool.
- Planning is optional: the model calls it when appropriate based on the user request and system guidance.
- Plan interaction: v2 async pause/resume. The tool remains pending while the user reviews it.
- Consent scope default: one tool call only.
- Plan approval does not grant consent to later mutating calls.
- Planning is not a prerequisite for mutation: if the model skips planning, the `ConsentGate` still reviews every mutating call.
- Every mutating call receives its own review.
- The two approval layers are deliberate: textual plan approval authorizes the approach, while graphical per-call review authorizes only the selected concrete canvas changes.
- Structural mutation approval uses PR #816's graphical diff and per-change selection.
- Control tools are excluded from MCP and batch processing.
- MCP remains a transport over ordinary AITools; this feature does not add an MCP-side agent runtime.
- Consent is not WebChat-specific: direct Grasshopper components and component utilities must use the same reviewed-operation path.
- Low-level mutation primitives do not open dialogs; their invoking operation prepares and obtains consent before applying.

## 3. Dependency on PR #816

This work should start after PR #816 (`feature/2.1.0-visual-diff`) lands in `main`, then this branch should be rebased or merged from the updated `main` before implementation.

PR #816 already introduces:

- `CanvasChangeReviewSession`
- `CanvasChangeReviewItem`
- `CanvasChangeReviewService`
- `CanvasChangePreviewOverlay`
- `CanvasChangeReviewDialog`
- `GhPutChangePlan`
- selective acceptance and dependency filtering
- staged review integrations for `gh_put`, `gh_remove`, `gh_clear`, `gh_connect`, `gh_disconnect`, `gh_move`, `gh_group`, and `gh_group_selected`

The agent feature must reuse and generalize these contracts rather than creating a second canvas approval UI. In this plan, “GhGraphDiff” refers to that graphical review workflow; PR #816 does not currently declare a type named `GhGraphDiff`.

Important findings from review of PR #816:

- Review currently occurs inside each tool body.
- `CanvasChangeReviewService.ReviewAsync` uses a `TaskCompletionSource<bool>` and an Eto modal dialog on Rhino's UI thread.
- Review returns a binary “apply/cancel” result while the session independently records per-item selections.
- Cancellation tokens and late/zombie completion protection are not yet part of the review API.
- The proposal is painted without mutating the document.
- Accepted and rejected changes are reported back in tool results.
- Protected objects are filtered by each tool before review.

## 4. Non-goals

- No autonomous background execution.
- No long-term memory or cross-document memory in this phase.
- No MCP planner, MCP plan resource, or MCP consent protocol.
- No batch planning or batch control tools.
- No automatic approval of later calls based on an approved plan.
- No replacement of `ConversationSession`.
- No generic shell, filesystem, or unrestricted network agent tools.
- No hidden execution of rejected canvas changes.
- No multi-agent delegation in this phase.

## 5. Architecture

### 5.1 Responsibility split

| Concern | Owner |
| --- | --- |
| Tool metadata and callable delegate | `AITool` |
| Standard two-phase mutation contract | new `AIMutatingTool` |
| Pending consent lifecycle and one-call grants | new centralized `ConsentGate` |
| Generic consent request/decision contracts | `SmartHopper.Infrastructure` |
| Canvas proposal model and selective decisions | PR #816 classes in `SmartHopper.Core.Grasshopper` |
| Canvas visual presenter | adapter over `CanvasChangeReviewService` |
| Plan control tool | `SmartHopper.Core.Grasshopper/AITools/plan_propose.cs` |
| Plan approval UI | WebChat consent presenter/card |
| Multi-turn model/tool loop | existing `ConversationSession` |
| MCP filtering | `AIToolMcpAdapter` based on explicit tool surfaces |
| Batch filtering | batch interception/execution based on explicit tool surfaces |

### 5.2 Agent flow

```text
User request
  -> WebChat ConversationSession
  -> model optionally calls plan_propose
  -> plan_propose validates plan and requests plan review
  -> WebChat renders pending plan card
  -> tool call awaits user decision
      -> reject/cancel: tool result says rejected; model reports or replans
      -> approve: tool result says approved; model continues
  -> model calls a canvas-mutating tool
  -> AIMutatingTool prepares a non-mutating proposal
  -> ConsentGate registers a one-call pending request
  -> canvas presenter opens PR #816 graphical diff review
  -> user selects subchanges and applies or cancels
  -> ConsentGate atomically resolves the request
  -> AIMutatingTool applies only accepted subchanges
  -> ordinary tool result is appended
  -> ConversationSession continues to a stable assistant response
```

### 5.3 Why consent is centralized but presentation is not

`ConsentGate` should own the invariant and lifecycle:

- each mutation needs an explicit decision
- the default scope is one call
- decisions are consumed once
- cancellation/timeout closes the request
- late UI events cannot revive an expired request
- no mutation starts before the decision is resolved

The gate must not own Grasshopper UI types. Presentation stays behind an infrastructure interface so WebChat can present a plan card and Core.Grasshopper can present a canvas diff.

### 5.4 Consent without WebChat or `ConversationSession`

Consent is attached to a mutation invocation, not to a conversation. A `ConversationSession` is one possible source of invocation context, but must not be required.

```text
WebChat tool call -----------\
Direct AIToolCall ------------> MutatingOperationExecutor -> ConsentGate -> presenter -> apply
Grasshopper component -------/
Component utility command ---/
MCP mutating tool call ------/
```

Add a host-agnostic `MutationInvocationContext` with:

- unique invocation ID
- source (`WebChat`, `GrasshopperComponent`, `Mcp`, `DirectTool`, `Internal`)
- optional owner ID (conversation/session ID or Grasshopper component instance GUID)
- optional tool-call ID and tool name
- active Grasshopper document ID/fingerprint where relevant
- execution surface
- linked cancellation token

A missing conversation ID is valid. A missing invocation context at an externally reachable mutating boundary is not: the boundary must construct a fresh direct context or deny execution, never silently bypass review.

Direct integration examples:

- `GhPutComponents` already creates and executes an `AIToolCall` for `gh_put`; mark it as `GrasshopperComponent`, set the owner to the component instance GUID, and pass its worker cancellation token to `Exec(token)`. It then follows the same `AIMutatingTool` and GhGraphDiff path as WebChat.
- `GhPatchApplyToCanvasComponents` computes a patch, directly deletes removed components, then calls `gh_put`. Replace that split mutation with one prepared patch-to-canvas operation containing removals, modifications, additions, wires, and groups; review it once and apply the accepted coherent subset under one undo record.
- Component-base `CallAIToolAsync` must create a component/direct invocation context. It must not impersonate WebChat merely to access a tool.
- MCP creates an `Mcp` invocation context. Ordinary mutating tools may still use the local graphical canvas presenter when exposed, but Chat-only control tools remain unavailable.

### 5.5 Low-level mutation primitives

`ScriptModifier`, `CanvasAccess`, and `GhJsonGrasshopper` calls are apply primitives, not consent presenters. Making every low-level setter asynchronous or opening modal UI from these helpers would couple reusable code to policy and create nested prompts.

Introduce a shared execution layer:

- `IConsentControlledOperation` / `MutatingOperation<TPreparation, TResult>`: prepare without side effects, then apply an accepted decision.
- `MutatingOperationExecutor`: the only public prepare -> consent -> apply coordinator; uses `ConsentGate`.
- `AIMutatingTool`: adapts an operation to `AITool`/`AIReturn`.
- Direct components: invoke the same operation through `MutatingOperationExecutor` or, where already appropriate, through `AIToolCall`.
- Low-level helpers: apply only after authorization and never request consent themselves.

For stronger bypass resistance, mutation utilities used only by SmartHopper-controlled operations should become `internal` where compatibility allows. If a public helper must remain public, document it as a raw primitive and ensure no AI-facing or user-triggered SmartHopper path calls it outside a reviewed operation.

For `ScriptModifier`, create a `ScriptModificationOperation` that:

1. resolves and validates the target script component
2. rejects protected targets
3. serializes the current component state
4. creates the proposed script/parameter state off-canvas (prefer GhJSON/GhPatch representation)
5. builds a graphical review proposal showing code/parameter/state changes
6. revalidates target identity and document fingerprint after review
7. records undo
8. invokes `ScriptModifier` only during apply

If a script parameter change cannot yet be represented faithfully in GhJSON, use a specialized script-change proposal rendered by the same review dialog/presenter contract. Do not silently fall back to immediate mutation.

## 6. Core contracts

Names are proposed and may be refined during implementation, but responsibilities should remain stable.

### 6.1 Explicit tool surfaces

Add a flags enum rather than using free-form tags as access control:

```csharp
[Flags]
public enum AIToolSurface
{
    None = 0,
    Chat = 1,
    Direct = 2,
    Batch = 4,
    Mcp = 8,
    All = Chat | Direct | Batch | Mcp,
}
```

- Existing tools default to `All` for compatibility.
- `plan_propose` uses `Chat` only.
- `AIMutatingTool` defaults to `Chat | Direct | Mcp`; batch is excluded unless a future mutating tool explicitly supports a meaningful batch proposal flow.
- `Tags` remain descriptive and must not enforce exposure.
- `Enabled` remains the global master switch.

Because provider formatting occurs in `SmartHopper.ProviderSdk`, add the surface to the SDK-side tool projection (`ProviderToolDefinition`) and carry the current tool surface on the request/session execution context. Preserve compatibility overloads where possible.

Required enforcement points:

1. Provider tool formatting: only expose definitions valid for the current request surface.
2. `AIToolMcpAdapter.IsExposed`: reject tools without `Mcp` even if allow-listed.
3. Batch interception: reject tools without `Batch`; do not silently fall through to synchronous execution.
4. `AIToolManager.ExecuteTool`: enforce the declared surface again at execution time.

### 6.2 Consent contracts

Add provider-agnostic contracts under `SmartHopper.Infrastructure/Consent/`:

- `IConsentGate`
- `IConsentPresenter`
- `IConsentProposal`
- `ConsentContext`
- `ConsentDecision`
- `ConsentDecisionStatus` (`Approved`, `PartiallyApproved`, `Rejected`, `Cancelled`, `Expired`, `Unavailable`)
- `PendingConsentRequest`

`ConsentContext` should contain at minimum:

- unique request/invocation ID
- invocation source
- optional owner ID (conversation/session ID or component instance GUID)
- optional tool-call ID and tool name
- execution surface
- optional active-document identity/fingerprint
- cancellation token linkage
- creation time

Do not require a conversation/session ID: direct components must be first-class consent callers.

`ConsentDecision` should contain:

- terminal status
- accepted item keys (empty for rejection/cancellation)
- optional user-facing reason
- decision timestamp

The gate registry must be concurrency-safe and keyed by the unique consent request ID, not only by tool name or turn ID.

### 6.3 `AIMutatingTool`

Add `AIMutatingTool : AITool` in Infrastructure. It must do more than set `MutatesCanvas = true`; it standardizes two-phase execution:

1. Validate and prepare a proposal without mutating state.
2. Ask `ConsentGate` for a one-call decision.
3. Apply only the accepted subset.
4. Return a normal `AIInteractionToolResult` describing applied, rejected, protected, and failed changes.

Suggested delegates/contracts:

- `PrepareMutationAsync(AIToolCall, CancellationToken) -> MutationPreparation`
- `ApplyMutationAsync(AIToolCall, MutationPreparation, ConsentDecision, CancellationToken) -> AIReturn`

`MutationPreparation` should carry an `IConsentProposal` and any immutable prepared data required for apply. It must not contain unvalidated live mutation state that can go stale unnoticed.

The two-phase implementation should delegate to the shared `MutatingOperationExecutor`; `AIMutatingTool` is an adapter, not the only route into consent-controlled execution. This lets direct components use exactly the same operation without constructing a fake conversation.

`AIMutatingTool` should force:

- `MutatesCanvas = true`
- no batch surface by default
- no `BuildRequest` by default
- a consent requirement that cannot be disabled by changing tags or descriptions

Do not put Rhino, Grasshopper, Eto, or GhJSON references in Infrastructure.

### 6.4 Canvas adapter over PR #816

Add a Core.Grasshopper adapter implementing `IConsentProposal` and an `IConsentPresenter` implementation that delegates to the existing graphical review infrastructure.

Possible types:

- `CanvasMutationConsentProposal`
- `CanvasChangeConsentPresenter`

The proposal wraps the PR #816 `CanvasChangeReviewSession`. The presenter:

1. starts the overlay
2. opens the review dialog
3. lets the user accept/reject individual changes
4. returns accepted item keys
5. ends the overlay in `finally`
6. handles cancellation by closing the pending dialog and resolving exactly once

Do not reduce the decision to a plain yes/no. `Apply selected` returns `Approved` or `PartiallyApproved` based on effective selections. `Cancel` returns `Cancelled`; selecting no items and applying returns `Rejected` or an explicit no-op decision.

### 6.5 Plan proposal

Add `plan_propose` as a read-only, control-category AITool with `AIToolSurface.Chat` only.

Suggested input schema:

```json
{
  "goal": "string",
  "summary": "string",
  "steps": [
    {
      "id": "stable step id",
      "description": "user-facing action",
      "tool": "optional registered tool name"
    }
  ],
  "assumptions": ["string"],
  "successCriteria": ["string"]
}
```

Server-side validation must:

- require a bounded number of steps
- reject duplicate/empty step IDs
- validate referenced tool names against `AIToolManager`
- derive mutability and exposure from registered tool metadata rather than trusting model-supplied booleans
- reject references to tools unavailable on the Chat surface
- enforce payload size limits
- sanitize all content before rendering in WebChat

Execution:

1. Build an immutable plan proposal.
2. Send it through `ConsentGate` to the WebChat presenter.
3. Await the user decision.
4. Return an ordinary tool result with `planId`, status, and normalized steps.
5. Let `ConversationSession` append the result and resume the provider turn normally.

Plan approval means “continue with this approach.” It does not create consent tokens for listed mutating steps.

### 6.6 WebChat plan presenter

Add a pending plan card through the existing WebView host bridge:

- `Approve plan`
- `Reject`
- `Cancel run`

The initial phase should not support editing plan text inline; rejection lets the user send correction instructions in the next message. Inline editing can be added later without changing consent contracts.

The card should display:

- goal and summary
- ordered steps
- which steps name mutating tools (derived server-side)
- assumptions
- success criteria
- clear statement that each actual canvas mutation will still receive a graphical diff review

The presenter owns only UI completion. `ConsentGate` owns request state and prevents duplicate/late resolution.

Pending requests are runtime state. The final `plan_propose` tool result remains in normal conversation history and records the user's decision for audit/replay. A rehydrated historical plan must render as resolved, never as an active approval control.

## 7. Execution timeout and cancellation

This is a required safety refactor.

Current `AIToolCall.Exec` wraps all of `AIToolManager.ExecuteTool` in `Task.WhenAny` with a delay. If consent waits inside that task, the timeout can return while the original execution continues; a late approval could then mutate the canvas after the caller saw a timeout.

Refactor execution into explicit phases:

1. resolve and validate tool
2. verify tool surface
3. prepare mutation proposal (for `AIMutatingTool`)
4. await consent using the session cancellation token; execution timeout does not run while a human is reviewing
5. atomically consume the one-call decision
6. start the tool execution timeout
7. apply with a linked cancellation token
8. reject any late completion after cancellation/expiry

The existing timeout behavior for ordinary tools should remain functionally compatible, but use a cancellable linked token rather than an unobserved task that can continue with side effects.

Required cancellation paths:

- WebChat Cancel button
- conversation/session cancellation
- dialog close
- Rhino/Grasshopper shutdown
- active document replacement or closure
- tool timeout after approval

All paths must remove the pending request, close/detach UI, end the overlay, and append a valid failed/cancelled tool result so provider history remains valid.

## 8. Migration of canvas tools

After PR #816 lands, migrate covered structural tools from ad hoc calls to `CanvasChangeReviewService.ReviewAsync` into `AIMutatingTool` preparation/apply delegates:

- `gh_put`
- `gh_remove`
- `gh_clear`
- `gh_connect`
- `gh_disconnect`
- `gh_move`
- `gh_group`
- `gh_group_selected`

Preserve PR #816 behavior:

- proposal rendered on the live canvas
- no temporary document objects
- protected objects filtered before they become selectable
- dependency-aware effective acceptance
- accepted/rejected counts in results
- cancellation is a no-op
- replacements validated before removal
- external connections preserved
- undo recorded for applied changes

Do not force non-structural runtime actions into GhGraphDiff. Inventory and classify separately:

- `button_click`
- parameter modifiers
- component preview/lock tools
- `set_ai_provider_and_model`
- script replacement tools
- `gh_tidy_up`
- `gh_smart_connect`
- document save

For each, choose one of:

1. adopt a meaningful structural proposal and graphical review
2. use a specialized one-call consent presenter
3. remain outside `AIMutatingTool` only with documented rationale

Also migrate non-WebChat mutation entry points:

- `GhPutComponents`: retain its existing `gh_put` reuse, but propagate `GrasshopperComponent` invocation context and cancellation.
- `GhPatchApplyToCanvasComponents`: replace direct pre-review deletion plus later `gh_put` with one atomic, reviewable patch-to-canvas operation.
- `_script_parameter_modifier` tools: convert to `AIMutatingTool`/`ScriptModificationOperation` before enabling them.
- `ScriptModifier`: keep as an apply primitive; route every SmartHopper AI-facing caller through `ScriptModificationOperation` and tighten raw method visibility where possible.
- Any component that calls `CanvasAccess`, `GhJsonGrasshopper`, or modifier helpers to mutate another canvas object must use `MutatingOperationExecutor` unless the action is the component's ordinary self-maintenance rather than an AI/user-requested external mutation.

The initial feature is not complete until every existing `MutatesCanvas=true` tool and every SmartHopper component that externally mutates other canvas objects is inventoried and explicitly classified.

## 9. Tool results and model behavior

Consent rejection/cancellation is an expected tool outcome, not an exception.

Normalize mutating-tool results to include where applicable:

- `status`
- `appliedChanges`
- `rejectedChanges`
- `protectedChanges`
- `failedChanges`
- `analysis` or messages

The model must receive enough information not to claim rejected changes were applied. It may propose a revised action but must not immediately repeat the same rejected mutation without a new user instruction or materially different proposal.

Update `assistant-core.md` to state:

- use `plan_propose` for non-trivial multi-step tasks
- do not plan trivial read-only questions
- plan approval is not mutation approval
- every canvas change is reviewed per call
- honor partial/rejected decisions
- verify the resulting canvas after applied mutations

## 10. Implementation phases

### Phase 0 — integrate prerequisite

1. Wait for PR #816 to merge.
2. Rebase/merge this branch onto updated `main`.
3. Run PR #816's focused tests/build before agent changes.
4. Record any interface differences from this plan.

### Phase 1 — surfaces and consent core

1. Add `AIToolSurface` and compatibility defaults.
2. Carry execution surface through request/tool-call context.
3. Enforce surfaces in provider formatting, MCP, batch, and execution.
4. Add `MutationInvocationContext`, consent contracts, and concurrency-safe `ConsentGate`.
5. Add the host-agnostic `MutatingOperationExecutor` used by tools and components.
6. Add fake presenters/gates for tests.
7. Refactor cancellation/timeout boundaries before any waiting consent is enabled.

### Phase 2 — mutating-tool standardization

1. Add `AIMutatingTool` two-phase execution.
2. Add canvas proposal/presenter adapter over PR #816.
3. Migrate the eight PR #816 structural tool entries.
4. Remove their direct review orchestration from tool bodies.
5. Preserve tool-specific proposal building and accepted-subset application.
6. Propagate component invocation context and cancellation through `GhPutComponents` and component-base `CallAIToolAsync`.
7. Replace `GhPatchApplyToCanvasComponents`' split direct-delete/`gh_put` mutation with one reviewed operation.
8. Add `ScriptModificationOperation` and route script modifier callers through it.
9. Inventory and classify remaining mutating tools and non-tool component mutation entry points.

### Phase 3 — `plan_propose`

1. Add Chat-only control tool and schema.
2. Validate plans against live tool metadata.
3. Add WebChat plan presenter/card and host bridge events.
4. Await approval/rejection through `ConsentGate`.
5. Resume the existing `ConversationSession` tool loop with the tool result.
6. Ensure historical resolved plans do not become interactive again.

### Phase 4 — guidance, observability, and verification

1. Update embedded assistant guidance and canonical workflows.
2. Add structured diagnostics for requested, approved, partially approved, rejected, cancelled, expired, and unavailable consent.
3. Avoid logging plan contents, GhJSON payloads, screenshots, or sensitive canvas data by default.
4. Ensure the assistant performs existing `validate_change`-style checks after applied mutations through prompt/workflow guidance; a dedicated critic turn remains a later enhancement.

### Phase 5 — documentation and changelog

1. Update `/docs/Architecture.md` with the copilot flow.
2. Add `/docs/Architecture/agent-copilot.md` and link it from the architecture index/entry point.
3. Update `/docs/UI/Chat/index.md` for plan cards and pending consent lifecycle.
4. Update PR #816's `/docs/UI/ai-change-review.md` with `ConsentGate` and `AIMutatingTool` ownership.
5. Update `/docs/Tools/index.md`, MCP docs, batch docs, and AICall tool/session docs.
6. Add `[Unreleased]` changelog entries under Added/Changed/Security as appropriate.

## 11. Expected files and modules

Exact names can change, but likely areas are:

### Provider SDK

- `SmartHopper.ProviderSdk/Hosting/IToolRegistryHost.cs`
- request/tool-surface execution context
- provider tool formatting compatibility overloads

### Infrastructure

- `AITools/AITool.cs`
- new `AITools/AIMutatingTool.cs`
- `AITools/ToolManager.cs`
- `AICall/Tools/AIToolCall.cs`
- new `Consent/*`
- new host-agnostic mutating-operation contracts/executor
- `Mcp/AIToolMcpAdapter.cs`
- `Hosting/ProviderSdkHostAdapters.cs`
- `AICall/Sessions/ConversationSession*` only for propagating session/surface/cancellation context, not for absorbing consent UI logic

### Core / Core.Grasshopper

- WebChat request creation marks Chat surface
- WebChat bridge and resources for plan cards
- canvas consent presenter adapter
- `AITools/plan_propose.cs`
- migrations of canvas mutation tools after PR #816
- `GhPutComponents` invocation-context propagation
- patch-to-canvas reviewed operation replacing direct deletion
- `ScriptModificationOperation` around `ScriptModifier`
- embedded `assistant-core.md` and workflows

### Tests

- `SmartHopper.Infrastructure.Tests` for gate, surfaces, execution, cancellation, and MCP filtering
- `SmartHopper.Core.Tests` for WebChat-safe non-Rhino plan state/serialization where possible
- `SmartHopper.Core.Grasshopper.Tests` for plan validation and proposal filtering that does not require Rhino runtime
- `SmartHopper.Components.Test` for graphical overlay/dialog/tool mutation flows requiring Rhino/Grasshopper

## 12. Test plan

### Infrastructure unit tests

- one-call decision is consumed exactly once
- duplicate approval/rejection resolves only once
- cancellation removes pending consent
- late approval after cancellation cannot call apply
- consent waiting does not consume tool execution timeout
- tool timeout after approval cancels apply and cannot produce a later mutation
- concurrent sessions and direct components do not share consent
- direct component invocation works without a conversation/session ID
- missing context creates a fresh direct context or denies; it never bypasses consent
- rejected/partial decisions become valid tool results
- `plan_propose` is Chat-only
- MCP descriptors and calls reject control tools even if allow-listed
- batch rejects control tools rather than falling through synchronously
- execution-time surface enforcement prevents bypass
- existing tools retain compatibility defaults

### Plan-tool tests

- valid plan accepted/rejected/cancelled flows
- unknown tool references rejected
- duplicate step IDs rejected
- model-supplied mutability cannot override registered metadata
- plan size and step count bounded
- rendered text escaped/sanitized
- historical resolved plan cannot resolve a new pending request

### Canvas proposal tests without Rhino runtime

- partial selection filters components/connections/groups correctly
- dependency rejection prevents orphan connections
- zero selected changes produces a no-op
- protected targets never appear as approvable
- accepted item keys map deterministically to applied changes

### Rhino/Grasshopper testing components

- proposal overlay appears without mutating the live document
- Apply selected changes only
- Cancel changes nothing
- WebChat Cancel closes the graphical review and changes nothing
- document close/replacement cancels pending review
- undo restores an applied call as one coherent user action where supported
- `gh_put` replacement preserves external connections
- sequential mutating calls each require a separate review
- `GhPutComponents` shows the same graphical review without WebChat and worker/component cancellation closes the pending review
- patch-to-canvas shows one coherent proposal and never deletes objects before approval
- script parameter/code modifications show the current/proposed change and do not call `ScriptModifier` before approval
- direct and tool-driven execution of the same operation produce equivalent accepted-subset behavior

### Verification commands

- Focused unsigned CI-safe tests where applicable:
  - `dotnet test src/SmartHopper.Infrastructure.Tests/SmartHopper.Infrastructure.Tests.csproj -p:SignAssembly=false`
  - `dotnet test src/SmartHopper.ProviderSdk.Tests/SmartHopper.ProviderSdk.Tests.csproj -p:SignAssembly=false`
  - focused Core/Core.Grasshopper tests if they build without Rhino activation
- Official compile verification uses `./tools/Build-Solution.ps1` in Developer PowerShell for Visual Studio, following the repository signing workflow.
- Rhino-dependent approval/overlay behavior is verified with testing components, not unit tests requiring Rhino runtime.

## 13. Threat review

This feature controls model-triggered side effects and therefore must use deny-by-default behavior when context is missing.

Threats and mitigations:

| Threat | Mitigation |
| --- | --- |
| Tool or direct component bypasses planning or consent | Shared mutating-operation executor plus enforcement at every AI-facing/user-triggered mutation boundary, not only WebChat |
| Low-level helper opens nested UI or mutates during preparation | Helpers remain apply-only; proposal preparation is side-effect free and UI stays in presenters |
| Model marks a mutation read-only | Mutability derived from registered tool type/metadata |
| Late click executes after timeout/cancel | Atomic terminal decision, linked cancellation, pending-request removal, apply cannot start after terminal state |
| Wrong conversation approves another call | consent keyed by session ID + tool-call ID + unique request ID |
| Partial rejection leaves invalid graph | PR #816 dependency-aware accepted-subset filtering |
| Protected component changed indirectly | protection filtering before presentation plus apply-time revalidation |
| Canvas changes between preview and apply | proposal captures a document/version fingerprint; revalidate targets before apply and require re-review if stale |
| Prompt/HTML injection in plan card | encode/sanitize all model-provided text; no raw HTML execution |
| MCP or batch reaches control tool | explicit surface filtering at discovery and execution |
| Sensitive design data logged | structured event metadata only; no plan/GhJSON/screenshot payload logging by default |
| Repeated rejected operation | return explicit rejection and instruct model not to retry unchanged proposal |

A document/version fingerprint is strongly recommended. Consent should authorize the reviewed proposal against the canvas state that produced it, not an arbitrary later state.

## 14. Acceptance criteria

- WebChat's model can call `plan_propose` and the tool call pauses until the user decides.
- Approving a plan resumes the existing `ConversationSession`; rejecting/cancelling returns a valid tool result.
- Plan approval grants no future mutation consent.
- Every migrated mutating call receives a distinct one-call consent decision, whether invoked from WebChat, MCP, a direct AITool call, or a Grasshopper component.
- `GhPutComponents` and patch-to-canvas are consent-controlled without requiring a `ConversationSession`.
- Script modifications are prepared and reviewed before `ScriptModifier` applies them.
- Structural canvas proposals use PR #816's overlay/checklist and support partial acceptance.
- No accepted subset is applied before review completion.
- Cancellation, timeout, document closure, and late UI events cannot cause zombie mutations.
- `plan_propose` is absent from MCP `tools/list`, rejected by MCP `tools/call`, and rejected in batch execution.
- MCP behavior for ordinary tools remains unchanged.
- Existing non-agent components do not gain planner behavior.
- All `MutatesCanvas=true` tools are inventoried and classified.
- Applied changes remain undoable according to Grasshopper conventions.
- CI-safe tests pass, and Rhino-dependent flows are covered by test components/manual checks.
- Architecture docs and changelog are synchronized.

## 15. Alternatives considered

### Put all approval in `ConversationSession`

Rejected. It would protect WebChat but not direct tool execution and would mix UI/security policy into conversation orchestration.

### Keep review calls inside each tool

Rejected as the final architecture. PR #816 proves the graphical UX, but ad hoc invocation does not standardize cancellation, execution surfaces, one-call scope, or bypass prevention.

### Make plan approval grant all listed mutations

Rejected. The user chose one-call consent, and a textual plan cannot faithfully preview the eventual GhGraphDiff or exact tool arguments.

### Expose `plan_propose` through MCP

Rejected. External MCP clients may have their own planners, and SmartHopper's control tool depends on WebChat-specific presentation and pause/resume semantics.

### Use tags for access control

Rejected. Tags are free-form metadata. Explicit surfaces are needed for reliable MCP, batch, and execution enforcement.

### Add planning directly as a special turn

Deferred. Special turns remain suitable for future host-forced planning or critic/verification turns, but model-initiated planning maps naturally to an AITool and the existing tool loop.

## 16. Follow-up opportunities, out of this implementation

- Host-forced “always plan” mode using a special turn.
- Editable plan cards.
- Dedicated post-action critic/verification special turn.
- Session-scoped low-risk consent modes (default remains one call).
- Persistent user preferences and project memory.
- Richer graphical diffs for specialized mutating tools.
- Parallel tool consent queue and multi-request review.
- Optional MCP-specific consent protocol if a future MCP specification/client flow supports it cleanly.
