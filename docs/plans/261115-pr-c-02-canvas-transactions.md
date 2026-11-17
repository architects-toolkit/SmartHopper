<!-- docs-validation: ignore -->

# PR-C 2: Canvas transactions and GhJSON 1.3.7

Parent: [handoff](261115-pr-c-implementation-handoff.md). Prerequisite: [execution contracts](261115-pr-c-01-execution-contracts.md). Implements D37, D12, D62 and shared endpoint services needed by F5.

## Existing Entry Points

SmartHopper:

- `src/SmartHopper.Core.Grasshopper/AITools/{gh_put,gh_set_value,gh_component_set_provider,gh_connect,gh_disconnect}.cs`
- `Utils/Canvas/{ComponentManipulation,CanvasNewComponentLayout,CanvasChangeReview,CanvasChangeReviewService,GrasshopperMutationUndoCoordinator}.cs` under the same project.
- `src/SmartHopper.Core.Grasshopper/AITools/GhPutChangePlan.cs`: reuse its accepted-fragment/external-wire logic rather than rewriting it.
- `AIProviderComponentBase.SetSelectedProviderName` delegates to `ProviderSelectionCore.SetCurrentProvider`, which changes pending selection without solving. Do not call `CommitChange` from a canvas edit.

GhJSON paths below are relative to sibling `../ghjson-dotnet`:

- `src/GhJSON.Grasshopper/{GhJsonGrasshopper.cs,PutOperations/CanvasPlacer.cs,PutOperations/PutOptions.cs,PutOperations/CanvasBoundsCalculator.cs}`
- `Deserialization/ComponentInstantiator.cs`, `ConnectionOperations/{CanvasConnector,ConnectionHelper}.cs`, `LayoutRefinements/*`, `Shared/UiThreadDispatcher.cs`.
- `src/GhJSON.Core/{NameResolution,LayoutRefinements,SchemaModels}` for host-independent contracts/algorithms.

## A. D37: Finite Change-Set API

### Public Tool Contract

Add `gh_apply_changes` as an `AIMutatingTool`, available on normal non-batch tool surfaces. It accepts one complete request; no cross-RPC begin/end state. Register it through the existing first-party discovery path and retain the manager's authorization/consent context.

Arguments:

```json
{
  "requestId": "client-retry-key",
  "operations": [
    {
      "opId": "provider",
      "kind": "set_provider",
      "target": { "instanceGuid": "EXISTING-INSTANCE-GUID" },
      "provider": "TestProvider"
    },
    {
      "opId": "text",
      "kind": "set_value",
      "target": { "instanceGuid": "EXISTING-PANEL-GUID" },
      "value": "new input"
    }
  ]
}
```

The example's GUID strings are placeholders, not valid runnable input. Use real live IDs in certification.

Define a closed discriminated schema, not `{toolName, arguments}`:

| Kind | Fields besides unique nonempty `opId`/`kind` | Meaning |
| --- | --- | --- |
| `put` | `ghjson` string; optional `editMode`, `autoOffset` | Existing `gh_put` semantics; at most one, first in the array |
| `set_value` | `target`, `value` and/or `panelMode`; optional `param` | Existing supported direct/persistent-value semantics |
| `set_provider` | `target`; at least one of `provider`, `model` | Existing component-scoped provider/model behavior |
| `connect` / `disconnect` | `from`, `to` | Explicit endpoints; no global tidy-up or implicit clearing of unrelated wires |

Target union: exactly one of `{"instanceGuid":"..."}` or `{"id":2}`. An integer ID refers only to a component in this request's single `put` fragment, never a canvas list index. Endpoint adds `paramName` and/or `paramIndex` to that identity. New connections require an unambiguous port; floating parameters resolve to their sole port. No arbitrary script/network/file/provider-execution operation kinds.

- `operations` must be nonempty, with at most 256 entries; total bytes remain bounded by the existing transport. A `put` can contain more than 256 components; operation count is not graph size.
- Reject unknown kinds/operation fields and invalid unions before doing any work. `requestId` is optional for ordinary execution, but requires the session contract when used for replay.
- Return overall mutation status, per-op results keyed by `opId`, created ID-to-instance-GUID mappings and execution metadata. Acknowledging Applied does not assert downstream completion.

### Shared Implementation, Not Nested Tool Calls

1. Extract typed operation parsers/handlers from the five existing tools. They support validation, preparation, review projection, commit and compensation. None calls `Exec` on another public tool to implement an operation.
2. Existing single tools translate their current schemas into a one-operation request, then translate the shared result back to their existing payload keys. Preserve the misleading legacy `gh_component_set_provider.componentGuid` field as an **instance GUID**; the new union uses `instanceGuid`.
3. Prepare the final graph of all operations before layout/review, including wires from later `connect` entries. Do not place disconnected roots and wire them afterward in separate commit calls.
4. New `gh_apply_changes` uses all-or-nothing approval. Display the whole group clearly; rejecting any constituent row rejects the group with no apply. Add an explicit review-session acceptance policy if necessary; existing standalone `gh_put` keeps independent row selection and its accepted-fragment dependency checks.
5. Re-review rather than silently changing an approved plan when a relevant live target, settings wire, parameter schema, protection status, or placement obstacle has changed.
6. Commit the accepted plan in one UI turn with all required preconditions rechecked, one undo group, and only non-recomputing invalidation until the final scheduled solve.

### Important Behavior And Conflicts

- Apply `put` then remaining operations in their declared order inside the commit. Preparation still sees the complete final wire topology for layout/validation.
- Reject multiple writes to the same logical field or mutually contradictory connect/disconnect pairs. Coalesce exact duplicate wires/no-ops explicitly. Do not invent last-writer-wins semantics for ambiguous plans.
- `set_provider(model=...)` preserves the existing behavior: create a model Panel and replace the target Settings sources. Review must show that replacement and the added panel. A same-plan explicit Settings connection/write conflicts with that implicit replacement; reject and ask the caller to represent its intended panel/wires explicitly in `put` instead.
- Merely setting a provider never changes global provider preferences. Validate against the current registered providers, normalize `Default` using the existing provider-selection contract, and preserve pending-run snapshots.
- `set_value(param=...)` changes persistent data, not upstream wired data. Preserve existing conversion/range/panel-mode behavior; warn when connected sources mask a persistent default. Never remove those sources implicitly.
- Preserve existing no-op handling; an empty/no-op group requests no solve and records no misleading undo action.
- A component already running asynchronous work may retain its run snapshot and see the final edit as pending, per PR-B. Do not abort/re-submit its batch, reset its epoch, or force a new Run edge.
- A synchronous Grasshopper solve (`SolutionDepth > 0`) is different: do not mutate from inside it. Use bounded admission/rejection, not `doc.Enabled=false`, global solution disabling, nested `NewSolution`, or `DoEvents`.
- Do not force Run false/true as a transaction mechanism. Requested Run edits use the same typed handler and become visible only with the final configuration's solve.

## B. GhJSON: Genuine Preparation, Then Apply

Add facade-level prepared placement operations, e.g. `GhJsonGrasshopper.PreparePut` and `ApplyPreparedPut`, and a disposable `PreparedPut` handle. Names are proposed; facade ownership and these semantics are required.

Preparation:

- Bind a specific `GH_Document`; the old ambient active-document lookup is insufficient across approval awaits.
- Parse/validate schema and identity/reference integrity before creating objects. Resolve types/ports and instantiate **new off-document prototypes**, never call current `Put` as preparation.
- For existing updates, prepare deltas/prototypes and preconditions; do not invoke `ObjectHandlerOrchestrator.Deserialize` against a live existing object. The current `CanvasPlacer` phase 1 does exactly that and must be split.
- Produce one immutable review/layout projection with measured bounds, canonical endpoints and proposed pivots. Reuse measurements for review rather than deserializing the same fragment again.
- Keep prepared resources UI-owned; dispose them after refusal/cancellation/failure or transfer ownership on apply. Do not cache mutable GH objects globally or carry them across documents.
- Dynamic/script parameters require metadata after their state has been applied to prototypes. If this cannot be measured safely without solving, return a precise unsupported/ambiguous preparation error; never run an unapproved script to discover its ports.

Apply:

- Check bound document and all preconditions before touching anything. Replacement types must instantiate successfully before removing originals.
- Use strict preparation/apply for the atomic plan: failed components/endpoints are not silently skipped. Do not globally change the existing standalone `SkipInvalidComponents` default.
- Preserve same-type update identity/state semantics; different-type replacement keeps its instance GUID and external wires where resolvable. Do not convert every update into remove-and-add.
- Preserve supplied pivots; a matching existing instance without a pivot retains its position.
- Reuse existing handlers for component state/internalized data, groups and post-placement fixups. Do not lose them by writing a new minimal importer.
- Expose an optional `PutOptions.RequestSolution` (default true) or an equivalent facade composition option. SmartHopper's host-managed apply selects false. Existing standalone Put behavior remains the default.
- All child wire/value/state helpers used during commit must be non-solving. Adding this flag only to `Put` while leaving external-wire restoration calling `NewSolution` does not fix D12.

### Undo And Failure Ownership

The current undo coordinator only counts/merges records. Replace/widen the affected-tool commit service so the actual record/journal belongs to the commit, not the whole tool invocation.

- Capture old values, provider selection, settings sources, affected object state/positions, external wires and group membership **before** their writes. Add/remove actions require the corresponding Grasshopper undo actions.
- Seal exactly one real undo group before requesting a solution. Do not hold the group across approval or wait for a second UI callback after solving starts.
- Use existing Grasshopper undo actions/targeted inverse operations for compensation. Do not clear unrelated undo history or call an ambient generic Undo that might undo a user's intervening edit.
- If apply fails after a partial write, compensate within the still-owned UI commit, with no intermediate solve. Report `FailedCompensated` only after verifying original supported state and disposing added objects.
- If compensation fails, retain a usable undo record, report `FailedPartial` and actual affected IDs, and suppress automatic solve. Never report cancellation/no-op/success or automatically retry.
- Third-party constructors/handlers may have effects outside the canvas; do not promise general ACID rollback. Initially support only the listed finite operations whose canvas inverses can be captured. Stop if a particular handler cannot meet this contract.
- Keep a guarded legacy invocation path for unrelated mutating tools until migrated, as package 1 specifies. Avoid double wrapping or reacquiring a non-reentrant lock through composite tools.

## C. D12: Acknowledgement Before Downstream Solve

The new boundary is:

```text
prepare -> review -> revalidate -> apply + seal undo + capture result
        -> enqueue one solution without solving -> publish Applied result
        -> respond without any further UI-dependent finalization
```

- Mark the internal lifecycle Applied immediately after the successful commit. Finalize the immutable replay result only after the scheduling attempt, so it truthfully includes scheduled/not-scheduled status. All of this happens before leaving the UI pass; it never awaits the downstream solution or a later UI finalizer.
- Schedule via `GH_Document.ScheduleSolution`, not immediate `NewSolution(true)`. The callback expires affected objects without requesting another nested solution.
- Track a document-lifetime/generation token; a closed/changed document cancels an obsolete scheduled callback. Multiple pending dirty sets may coalesce into one scheduled solution; no missing edits and no duplicate full-document solves.
- A valid no-op requests zero solves. A refusal/cancelled-before-start/compensated or partial failure requests zero automatic solves.
- If scheduling fails after commit, retain Applied and add a warning/status identifying that solving was not scheduled. Rolling back a completed graph because a UI refresh failed would be misleading.
- Ensure `ToolManager`'s finalization and composite wrappers cannot wait for another UI undo pass after the scheduled slow solve begins. Package 1's ownership tracker, not the HTTP connection, publishes the final result.
- Replay the same client key after disconnect/in-progress timeout. Do not replay a cached transient timeout as the final mutation result.

Instrument queue, parse/validate, instantiate/measure, layout, review wait, commit, undo seal, and solve-request separately. Record IDs/counts/durations safely, not full GhJSON, prompts or credentials. Profiling the separate cold-preparation cost is not permission for unconditional placement chunking or a promise that every cold plugin constructor runs under one second.

## D. D62: Exactly One Placement-Origin Policy

Implement this policy in GhJSON, then make `CanvasNewComponentLayout` a thin preparation/review adapter. Both direct library placement and SmartHopper preview must use it:

1. Preserve authored/fixed pivots (including `(0,0)`) and existing live objects without requested moves.
2. Connected pivotless nodes use the existing fixed-anchor/constraint-group pipeline. Include planned external edges as read-only anchor context; never emit those live anchors as additions.
3. Only disconnected unanchored content uses the free-block policy. With `AutoOffset=true` and valid existing rendered bounds, align the new free block's **rendered left edge** to existing `LeftmostX` and put its **rendered top edge** at existing bottom (`LowestY`) + existing spacing (default 100).
4. Compute translation from measured free-block bounds: `dx = existing.LeftmostX - free.Left`, `dy = existing.LowestY + spacing - free.Top`. Do not simply pass `LeftmostX` as an additive offset and double-count the block's own origin.
5. Empty canvas uses normal compact origin. `AutoOffset=false` does not introduce a new offset. Explicit `PutOptions.Offset` retains its documented explicit-translation semantics.
6. Translate all free islands as one compact pack, preserving their internal spacing/order, not each island onto the same origin. Keep existing collision handling for movable nodes only.

Use a single rendered obstacle set, excluding planned replacements/removals. Preserve the current active-object convention; do not suddenly make enclosing group decoration a huge duplicate obstacle. Reject/ignore invalid nonfinite rectangles with diagnostics rather than propagating infinities from an empty bounds sentinel. Negative world coordinates and different pivot conventions are valid.

Do not fix X=0 separately in both callers. Do not move existing components on later ordinary `gh_connect` or run automatic `gh_tidy_up`. Do not change the shared port-alignment heuristic to solve an origin bug.

## Required Verification

Pure tests cover plan unions/dependencies/conflicts, approval grouping, origin math and deterministic translations. Real-host test components cover prepared-state immutability, mixed port/pivot geometry, rollback/undo, dynamic parameters, protected objects, document switching, existing-instance updates/replacements and the D37/D12 live scenarios in package 4.
