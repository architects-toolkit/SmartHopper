<!-- docs-validation: ignore -->

# PR-C implementation handoff

Status: proposed implementation specification, not implemented. Writing this handoff does not itself authorize production edits. When instructed to implement PR-C, follow this plan and record each checkpoint's actual verification.

## Start Here

This is the entry point for an implementing agent. Read this file first, then the numbered work packages in order. The packages intentionally fit separate working contexts.

1. [Execution contracts](261115-pr-c-01-execution-contracts.md): F2, F8, D35, cancellation, replay, and ownership.
2. [Canvas transactions](261115-pr-c-02-canvas-transactions.md): D37, D12, D62 and GhJSON 1.3.7.
3. [Focused tools](261115-pr-c-03-focused-tools.md): F4, F5, F16.
4. [Validation and delivery](261115-pr-c-04-validation.md): test ownership, live procedures, release sequence, and stop conditions.

All new type/method names in these packages are **proposed**, not existing APIs. Existing files/symbols are identified explicitly. Preserve the contracts if local naming changes are necessary; do not invent compatibility wrappers or duplicate workflows.

## Verified Baseline

- SmartHopper branch: `fix/2.0.0-mcp-robustness`; production baseline includes PR 104 (`ab943240`). Investigation commits through `93ecc31b` contain documentation only.
- Sibling `../ghjson-dotnet`: observed checkout `feature/data-tree-editing` at `71adf8c`; its production source was checked against released 1.3.6 (`origin/main`, `abc51ee`). Recheck branches/worktrees before editing; never reset or switch someone else's dirty worktree.
- Intended library release: GhJSON 1.3.7. Use a dedicated branch from the verified 1.3.6 release/main baseline, not the historical feature branch merely because it is checked out.
- SmartHopper currently defaults to sibling source references when present; package fallback in `SmartHopper.Core.Grasshopper.csproj` is 1.3.6. Both modes must be verified before shipping.
- The build-generated `Directory.Build.props` public-key change is not part of this work. Do not stage keys, certificates, local settings, temporary rigs, or raw logs.

Evidence: [D12/F8 live reproduction](../Reviews/261115%20PR-C%20D12%20F8%20Live%20Reproduction.md) and [live report](../Reviews/261110%20MCP%20Live%20Test%20Report.md).

## Scope And Completion Contract

| Item | Concrete outcome | Package |
| --- | --- | --- |
| D37 | A finite typed `gh_apply_changes` request applies related edits without intermediate solves; one review and undo group | 2 |
| D12 | Applied identities/undo completion no longer wait for downstream solving; retries cannot double-apply an in-flight mutation | 1 + 2 |
| D62 | GhJSON owns one bounds-aware unanchored placement policy used by preview and apply | 2 |
| D35 | Successful MCP results carry warnings; Error diagnostics cannot be hidden by a payload | 1 |
| F2 | Control/discovery/cancellation do not wait behind document execution; real writes remain serialized | 1 |
| F8 | Busy UI produces bounded admission failure and no abandoned queued action executes later | 1 |
| F16 | Viewport responses reflect recomputed, applied projection state, not stale cached coordinates | 3 |
| F4 | Discovery matches names, nicknames, and keywords with deterministic ranking before limiting | 3 |
| F5 | Generation emits no component identity GUIDs/pivots and validates/canonicalizes endpoint references without destructive repair | 2 + 3 |

The old 395-object/300-second threshold is **not reproduced**: 395 inert objects/395 wires complete in 1.056 s. A controlled slow solver does reproduce placement-after-timeout. Forty workers/eighty intercepted 30-second failures do **not** reproduce the historical minutes-long provider freeze; an independent busy-UI waiter does reproduce protocol blocking. Do not optimize against an unverified cause.

## Corrections Important For Implementation

1. `AIToolCall.Exec` **already has a tool timeout**, linked cancellation, and `Task.WhenAny`. The defect is not an absent timeout: it can return an error while ignored-token work continues. Fix ownership/outcome handling; do not add another competing timeout layer.
2. `GrasshopperMutationUndoCoordinator` counts and merges undo records. It is **not** a before-state snapshot, transaction journal, or rollback implementation.
3. GhJSON `CanvasPlacer.Put` can update existing objects during its so-called phase 1. Calling it to "prepare" would mutate the canvas before approval.
4. `McpToolCallCache` is completed-result caching, not in-flight deduplication. It is globally keyed only by tool/name ID, and its clone loses images/metrics. Replace that ownership model before allowing concurrent mutation requests.
5. F16's stale state is not proof of a 300 ms viewport animation. The inspected Rhino 8 `GH_Viewport` setters change target/zoom while `VisibleRegion` remains cached until `ComputeProjection`. `CanvasAccess`'s 300 ms movement code animates **objects**, not this viewport API.
6. Explicit MCP `EnabledTools` allow-lists currently override mutation/view/external filters by documented policy and tests. Do not silently change that policy. Still enforce tool enabled/surface authorization, consent, and protection at execution time; the new plan must not become an arbitrary nested-tool bypass.

## Non-Negotiable Invariants

- Persisted batch lifecycle, PR-A epochs/import behavior, and PR-B Run/input/provider snapshots are out of scope to redesign.
- No provider submission, solve, live object mutation, or undo record during preparation/review.
- All Grasshopper object access/instantiation/layout and viewport access stay on the host UI thread. Do not put Rhino calls in `Task.Run` to make a timeout test pass.
- Bind the intended document before preparing; revalidate document identity, target state, protection, and authorization immediately before commit. Never silently retarget a switched canvas.
- Queued cancellation means no work started. Started mutation ownership lasts through actual completion/compensation, even if the HTTP observer times out.
- Prepare/review hold no document mutation lock. Apply + undo finalization share one exclusive UI scope. Downstream solve is scheduled afterward.
- Imported GUIDs/pivots/state retain their meaning. Generation-only normalization never runs on `gh_get`, generic `gh_put`, imports, patching, or result restoration.
- Supplied pivots and unchanged live objects never move during automatic placement. Later `gh_connect` remains wire-only.
- All normal tool calls go through `AIToolCall.Exec`/`AIToolManager`. Extract shared operation handlers instead of calling a tool's delegate directly or nesting public tools to construct a transaction.
- Do not promise rollback of arbitrary third-party side effects. Reject unsupported atomic operations before writing; expose partial failure honestly if compensation itself fails.

## Dependency Order / Checkpoints

| Checkpoint | Deliverable | Gate before proceeding |
| --- | --- | --- |
| C0 | Reconfirm baselines; add deterministic ownership/UI-post/review/clock seams and focused failing tests | Tests describe the identified defects; no paid network |
| C1 | Typed result diagnostics and corrected error precedence | D35 result, image, metrics, and replay-copy tests pass |
| C2 | Session/request identity, in-flight replay reservation, execution ownership, bounded UI executor | Cancellation/duplicate/replay races pass with a fake dispatcher |
| C3 | GhJSON prepared placement, strict endpoint-resolution result, shared origin policy, optional deferred solve | Pure tests + host test components; existing standalone Put defaults preserved |
| C4 | Shared host commit service, affected legacy-tool adapters, `gh_apply_changes` | D37 and undo/compensation tests; D12 short-deadline live test |
| C5 | Enable independent dispatcher/control admission only after C2/C4 ownership is safe | F2/F8 concurrency and saturation live tests |
| C6 | F4/F5/F16 using shared services | Focused tests and live scenarios pass |
| C7 | Package/source integration, full required checks, docs/changelog, final live certification | All package-4 exit criteria recorded |

Do not remove the global dispatcher gate as the first edit. Do not release/publish 1.3.7 before its consumer integration has been verified.

## Handoff Instructions

For each checkpoint, report files changed, tests/commands and results, live evidence where required, limitations, and next checkpoint. Preserve unrelated worktree edits. If a required invariant cannot be met with the listed boundaries, stop with concrete evidence rather than adding sleeps, retries, silent repair, or undocumented behavior.

Suggested implementing-agent prompt:

```text
Implement PR-C following docs/plans/261115-pr-c-implementation-handoff.md
and its four linked work packages. This plan defines the intended contracts.
Start at the first incomplete checkpoint; do not treat historical allegations
as proven defects or claim unrun live certification. Keep actual progress and
evidence in the live report. Ask before destructive actions or scope changes.
```
