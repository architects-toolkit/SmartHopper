<!-- docs-validation: ignore -->

# PR-C 1: Execution contracts

Parent: [handoff](261115-pr-c-implementation-handoff.md). Implements D35, F2, F8 and the replay/ownership prerequisite for D12/D37. This is a specification, not a claim that these types already exist.

## Existing Code To Read

- `src/SmartHopper.Infrastructure/Mcp/{McpServer,JsonRpcDispatcher,AIToolMcpAdapter,McpToolCallResult,McpToolCallCache,McpServerOptions}.cs`
- `src/SmartHopper.Infrastructure/AICall/Tools/AIToolCall.cs`: `Exec` already cancels/bounds the observer with `Task.WhenAny`.
- `src/SmartHopper.Infrastructure/AITools/{ToolManager,MutationUndoCoordinator,AIMutatingTool}.cs`
- `src/SmartHopper.Infrastructure/Consent/ConsentContracts.cs`: `MutationInvocationContext`; do not build a parallel consent context.
- `src/SmartHopper.Core.Grasshopper/Utils/Canvas/{CanvasAccess,UiThreadMarshal,CanvasNewComponentLayout,GrasshopperMutationUndoCoordinator}.cs`
- GhJSON `src/GhJSON.Grasshopper/Shared/UiThreadDispatcher.cs` already has a queued-start timeout guard and waits for started actions. Preserve that safety.

## A. D35: One Diagnostic Contract

### Implementation

1. Add typed immutable diagnostic projections to `McpToolCallResult`, built from canonical `AIReturn.Messages`. Use existing `SHRuntimeMessage` severity/origin/code meanings; allow-list serializable fields rather than serializing exception/request objects.
2. Correct `AIToolMcpAdapter.BuildResult` order: null result -> error; `!result.Success` -> error even if a tool-result payload exists; otherwise return the payload and diagnostics. `AIReturn.Success` already means no Error-severity message. Warning/Info are not errors.
3. Keep an available partial payload on errors; do not replace it with success. Include a safe error summary and retain validation `expectedSchema`. A null body with an Error still produces a meaningful error; a legitimate empty successful tool result remains supported.
4. Preserve the existing first payload text block, `structuredContent` shape, typed image blocks/order, binary-stripping behavior, and `_meta["smarthopper/helperUsage"]`.
5. Put projected diagnostics in `_meta["smarthopper/diagnostics"]`. Also append one model-visible text content block containing `{"diagnostics":[...]}` when nonempty. Metadata alone is insufficient because an LLM may never see it. Do not inject fields into every domain payload or violate tool output schemas.
6. Projection fields: `severity`, `origin`, `code`, `message`; enum names use the existing enum spelling, e.g. `Warning` and `Tool`. Include Info/Warning/Error; Debug stays private unless the existing host debug policy explicitly exposes it.
7. Bound diagnostic output centrally (64 entries, 2048 characters per message); indicate truncation with one final diagnostic. Reuse the project's scrubbing policy; never echo arguments, keys/credentials, raw exceptions, or provider bodies as a diagnostic workaround.

### Edge Cases / Tests

- Payload + Warning stays `isError:false`; payload + Error becomes `isError:true`.
- Errors from every origin count, not only Tool; multiple messages retain order and codes.
- No double-copy from both a tool-result interaction and `AIReturn.Messages`.
- Images, metrics, empty/scalar/array payloads, `expectedSchema`, and legacy screenshots keep their established encoding.
- Replay must clone every new field, images and metrics as well as JSON. Mutating a replay response must not modify the stored result.
- Live test the existing unknown-argument probe through actual MCP `tools/call`, with no provider call.

## B. Request Identity Is Not Mutation Idempotency

There are two identifiers: JSON-RPC `id` identifies an observer request; tool argument `requestId` identifies an intended repeatable mutation. They must not be conflated.

### Session And Request Registry

The baseline transport has no logical-client session registry. A TCP connection is not a session because each request may use a new connection.

- Add bounded in-memory MCP sessions at the transport boundary using standard `Mcp-Session-Id`: initialize allocates an opaque cryptographically random ID and returns it as an HTTP response header; subsequent requests present it. A session is not authentication: retain bearer checks on every request when configured.
- Keep ordinary stateless calls without a session supported. Stateful features (`requestId` replay and cross-request cancellation) require the initialized session. A mutation containing `requestId` without it returns `session_required` before execution. This is an intentional documented pre-alpha tightening of the previous global-cache behavior.
- Proposed limits: 128 sessions per server generation; 30-minute idle expiry; never evict a session with owned running operations. Reject excess session creation rather than evicting active ownership.
- Key in-flight JSON-RPC requests by server generation + session + typed ID. String `"1"` is not numeric `1`. Reject concurrent duplicate request IDs within a session; do not overwrite their CTS. IDs are reusable after completion.
- Legacy stateless requests still receive a request-local deadline/server-stop token, but cannot cancel another HTTP request by guessing its ID.
- Implement `notifications/cancelled` before any document admission wait. It finds only the matching session's request, requests cancellation, and returns no JSON-RPC response (HTTP 204). Unknown/already-finished IDs are harmless; malformed notifications must not become error responses containing a fake ID.
- Dispose registrations/CTS only after their real execution/observers release them. Session/server shutdown prevents queued starts; it does not fabricate completion for an already-started commit.

### In-Flight Mutation Reservation

Replace `McpToolCallCache`'s check-then-execute-then-store pattern with atomic reservation:

```text
key = (server generation, session, bound document identity, tool name, requestId)
entry = normalized-argument fingerprint + owned completion task + lifecycle + result
```

- Resolve/bind the document on the UI thread before reserving; never use an unsafely read ambient document as the key. Once reserved, retries can find that recorded binding without needing the busy UI again.
- Same key + same arguments joins/observes the existing operation; it never starts another review, import, paid generation, or commit.
- Same key + different arguments returns `idempotency_conflict`; retain the original entry.
- Fingerprint canonical JSON after removing transport-only `requestId`; retain meaningful differences, array order and scalar types. Do not log the raw arguments or use a plaintext fingerprint as a substitute for scrubbing.
- Extract `requestId` into invocation metadata and remove it from the business-argument clone before schema/unknown-argument validation. Augment mutating MCP input descriptors with this optional transport field centrally; a legitimate replay key must not produce D35's ignored-argument warning. Do not mutate the caller's JObject.
- Binding cannot drift to a newly active document. A retry while another document is active must replay the recorded outcome or return `document_changed`, never apply to that other document.
- Maintain a unique lookup from `(server generation, session, tool, requestId)` to its first reserved document binding for the retention period. This makes retry lookup possible before busy-UI admission and prevents accidentally creating a second document-scoped entry for the same intended retry.
- Reserve before execution, and cache the real terminal result even if the HTTP writer has already failed. Use immutable/deep-cloned result projections.
- Keep completed outcomes for the existing 10-minute lifetime; bound completed entries (proposed 512/session) and use bounded admission for active entries. Never TTL-evict or capacity-evict an in-flight write.
- Bound observer waits. An operation still owned after an observer deadline returns `operation_in_progress`, not "nothing was applied." Add execution metadata with `operationId`, `state`, and `applied` (`false`, `true`, or unknown). The same key can retrieve the final result later. Do not cache that observer timeout as the operation's terminal result.
- Pre-start cancellation, validation failure, refusal, success, compensated failure, and honest partial failure are terminal outcomes for that key. A caller wanting a different operation uses a new key. After expiry/restart, replay guarantees end; document this rather than claiming durable exactly-once execution.

## C. Actual Execution Versus Waiting For It

Use one host-independent lifecycle object carried with the existing invocation context. Suggested name: `ToolExecutionLifecycle`.

```text
Preparing -> Reviewing -> Queued -> Applying -> Applied
       \          \         \          \-> FailedCompensated / FailedPartial
        \----------\---------\-> CancelledNotStarted / Rejected / Invalid
```

- The atomic `Queued -> Applying` transition owns the commit. Queued cancellation/deadline races compete with that transition; only one wins.
- Before Applying, cancellation can terminate without side effects. During Applying, stop the observer if necessary, but retain the operation task, document gate, undo ownership, and result publisher until actual completion/compensation.
- Refactor `AIToolCall.Exec`'s existing timeout path for mutation-owning executions: it must not return/cache an ordinary final failure while its manager task still writes. The outer MCP observer may return `operation_in_progress`; the owned execution still publishes its eventual real outcome. Observe all task exceptions.
- Ordinary non-mutating tools retain bounded execution waits and cooperative cancellation. No new global timeout value, timeout increase, force-abort, or retry loop.
- Cancellation after Applied does not undo the graph automatically. The response remains Applied, possibly with a solve-scheduling diagnostic.

## D. F8: Bounded UI Executor

Introduce one asynchronous host UI invocation primitive through the GhJSON facade/shared host bridge; keep the ownership state machine testable without Rhino. SmartHopper's `CanvasAccess`/`UiThreadMarshal` become adapters to this contract, not rival implementations.

1. Queue using a genuine nonblocking host UI post. Do not use `Task.Run(action)` for UI work.
2. Use `TaskCompletionSource` with `RunContinuationsAsynchronously`; the callback atomically claims start before touching UI or a captured document.
3. Proposed queued-start budget: 1 second, or the remaining request budget if shorter. Already-cancelled requests do not post. Cancellation/timeout before start completes `canvas_busy`/cancelled-not-started and makes the later callback a no-op.
4. Once started, ownership follows C. Do not dispose a wait handle still used by a started callback, release a gate early, or let `TrySetCanceled` impersonate a cancelled mutation.
5. Keep cancellation registration alive until completion; fix the existing `CanvasNewComponentLayout.ApplyAsync` pattern that returns a Task after its method-scoped registration has been disposed.
6. Reentrant UI-thread callers run inline only if their document/ownership checks pass. Reject a fresh write during `SolutionDepth > 0`; do not introduce nested solves or `Application.DoEvents`.
7. Document closed/switched, canvas disposed, server restarted, callback exception, and cancellation-after-post all have explicit terminal handling. No uncancelled "fire-and-forget" mutation.
8. Synchronous compatibility methods may block while a started operation finishes, but MCP uses the asynchronous path. Existing standalone GhJSON defaults remain safe; no public timeout/solver flags on every SmartHopper tool.

## E. F2: Remove The Wrong Gate, Keep The Right Ones

- Only after B-D and package 2's commit ownership pass tests, remove `JsonRpcDispatcher.gate` as an all-method lock.
- Ping, initialization, cancellation, and immutable `tools/list` must not acquire a document gate. Snapshot the tool registry safely and build descriptors from immutable data.
- Do not assume every resources/prompts call is UI-independent: static catalogues may be concurrent; dynamic callbacks must use the appropriate bounded executor. Never infer UI safety from `readOnlyHint` (viewport control is a counterexample).
- Serialize document commits by **bound document identity**, using the same coordinator for MCP, direct tools and chat. Do not create an MCP-only write lock.
- Scope the new affected-tool undo handling to commit, not approval/AI generation. Audit all existing mutating tools before changing the manager's automatic undo wrapper.
- A safe migration uses an explicit internal mutation-boundary classification: legacy whole-invocation handling, prepared commit, or composite wrapper. Default existing tools to guarded legacy ownership; migrated tools use exactly one prepared commit; composites delegate to children without owning a second non-reentrant undo gate. Do not remove safety for unrelated tools to finish the nine items.
- This classification is internal registration metadata, not a user-exposed `skipUndo` or `skipApproval` argument. Nested trusted children propagate context; do not call a registered delegate directly or permit parallel child writes to share one unsynchronized scope.
- Preserve request size, HTTP parsing, authentication, bind address, I/O deadlines, and overload protection. The current 16 transport slots alone are insufficient: bound waiting UI/tool work separately (proposed 12 slots) so at least four slots remain available to reach control methods.
- Separate limited pre-parse I/O capacity from admitted long-running tool capacity; use short parsing deadlines and reject excess tool work promptly. Do not give "control" requests an unlimited unauthenticated bypass. Test saturation with all tool slots occupied, not just one waiter.
- Retain documented explicit tool allow-list overrides. Disabled tools, wrong surfaces, changed registration and protected targets are still rejected at actual execution/commit; session/replay must never return another caller's data or bypass revoked authorization.

## Required Tests

Use `SmartHopper.Infrastructure.Tests/Mcp` and fake clocks/UI posts/commit tasks: string-vs-number IDs; two sessions with the same IDs; duplicate JSON-RPC ID; notification cancellation without a response; queued start/cancel races; started-write timeout and eventual outcome; shutdown; same-key concurrent join; argument conflict; closed/switched documents; bounded eviction; full result cloning; UI saturation with responsive control; and no late callback after `canvas_busy`.

Use host test components/live scenarios for actual Rhino UI behavior, not new Rhino-dependent unit tests. The existing forty-worker negative control is a regression baseline, not justification for a provider concurrency cap.
