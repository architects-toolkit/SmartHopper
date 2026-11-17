<!-- docs-validation: ignore -->

# PR-C 3: Discovery, generation and viewport

Parent: [handoff](261115-pr-c-implementation-handoff.md). Use packages [1](261115-pr-c-01-execution-contracts.md) and [2](261115-pr-c-02-canvas-transactions.md); do not add separate cancellation/layout implementations here.

## A. F4: Metadata Search

Existing source: `src/SmartHopper.Core.Grasshopper/AITools/gh_list_components.cs`, particularly filtering before `Take(maxResults)`. Keywords already appear in result metadata but not the query predicate.

### Implementation Steps

1. Snapshot proxy metadata safely on the UI thread: type GUID, Name, NickName, keywords, Category/SubCategory and display fields. Background ranking works on immutable DTOs, not live proxy objects.
2. Apply the existing include/exclude category parser first. Preserve `query` and the existing `nameFilter` fallback; a nonempty explicit query wins.
3. Normalize query by trimming/collapsing whitespace and use ordinal case-insensitive comparisons. Treat a multiword query as a phrase, not a new token/fuzzy language.
4. Match Name, NickName, and each keyword. Do not add a Settings-specific alias or match descriptions by default.
5. Rank by the first matching tier: exact Name, exact NickName, exact keyword, Name prefix, NickName prefix, keyword prefix, Name substring, NickName substring, keyword substring.
6. Break ties by Name (ordinal-ignore-case), NickName, then type GUID (ordinal stable representation). Apply `maxResults` only after filtering/ranking. Preserve default 100 and existing `<=0` unlimited behavior unless separately approved.
7. Deduplicate proxy entries by type GUID, not display name. Two different components with the same name remain discoverable.
8. Instantiate only the selected limited results and only when inputs/outputs were requested (`includeDetails` empty still means full details). Keep the current broken-proxy isolation; one failing constructor must not break a metadata search.
9. Preserve result keys, `includeDetails` meaning, and count/list relationship. An internal metadata cache, if needed, must invalidate on catalogue/plugin changes; do not introduce an unrefreshable global snapshot.

### Edge Cases / Tests

- `"AI Settings"` matches the Settings keyword even without `"keywords"` in `includeDetails`.
- Null/empty keyword arrays, non-ASCII display names, whitespace-only queries, duplicate names and shuffled registry enumeration produce valid deterministic output.
- With `maxResults=1`, the best exact match wins even when it is last in registry order.
- Category exclusions still override matches. Metadata-only requests make zero `CreateInstance` calls.
- Host-independent ranking tests use fake metadata; actual proxy reads/introspection use a test component/live MCP.

## B. F5: Generation Is Not Import Repair

Existing files:

- `src/SmartHopper.Core.Grasshopper/AITools/gh_generate.cs`: `GenerateGhJsonAsync` currently validates, parses, calls default `GhJson.Fix`, then removes pivots.
- `src/SmartHopper.Core.Grasshopper/Utils/Parsing/GhJsonGenerationPolicy.cs` and existing policy tests.
- GhJSON `src/GhJSON.Core/FixOperations/DocumentFixer.cs`, `NameResolution/{ComponentTypeResolver,ParameterNameResolver}.cs`.
- GhJSON `src/GhJSON.Grasshopper/ConnectionOperations/ConnectionHelper.cs`: `ResolveEndpointParam` and its existing index/name/nickname/fuzzy logic.

### Generation-Only Contract

Generated component definitions use names plus unique integer IDs. No component `pivot`, `componentGuid` or `instanceGuid`; no group `instanceGuid`. Generic imported/read-back documents preserve those identities and positions.

1. Extend the **generation-only** normalization policy; do not alter generic fixer defaults or recursively delete any key named "guid" anywhere in arbitrary extension/script data.
2. Before removing type GUIDs, resolve an available canonical component type/name. A model-supplied type GUID may be used internally to identify the type, but it must not leak into the generated output.
3. Before removing instance GUIDs, ensure all internal references have unambiguous integer IDs and convert supported GUID-based local references through a unique mapping. Reject unresolved/external canvas references in newly generated definitions; `gh_put`/explicit connect operations handle existing canvas identities.
4. Keep valid integer IDs stable. If absent IDs can be assigned without guessing references, assign deterministically; otherwise feed precise errors into the existing bounded generation retry loop. Do not silently renumber duplicates whose references are ambiguous.
5. Preserve component state, scripts, internalized data, groups/members and extension content. Normalize only documented identity/reference fields; UUID literals in a script/text value are not component identities.
6. Do not call default `GhJson.Fix` on this path. If reusing a fixer with explicit options, disable GUID generation/regeneration and all cleanup that drops connections/groups or changes meaning. A named generation policy is safer than a scattered options list.
7. After normalization and semantic validation, serialize and validate the final output again. A document valid before a transformation need not be valid afterward. No later helper may regenerate GUIDs.
8. Reuse the existing `MaxValidationRetries=2` policy and cancellation/metrics aggregation. Do not add an unbounded repair loop or spend real provider credits in tests.

### One Semantic Endpoint Resolver

Extend GhJSON's existing resolver behind its facade to return a typed resolution result: owner ID, direction, actual index/full Name, match kind, and unresolved/ambiguous diagnostics. SmartHopper must not implement a second resolver from strings.

- Existing generic runtime resolution stays compatible. Expose a stricter generation/preflight mode instead of removing nickname/index/fuzzy support globally.
- Build metadata after prototype component/script state has established its actual variable ports, not from bare stock component names alone. Never solve/execute a script merely to discover a port.
- A valid explicit index identifies a port. A supplied name plus index must agree under strict generation rules; contradictory references are errors, not silent precedence changes.
- Without an index: unique exact Name, unique nickname/alias, then only a uniquely justified fuzzy candidate under the existing resolver's distance policy. A fuzzy tie is an error, not "take first". The built-in alias dictionary is a hint evaluated against that component's ports.
- Emit the actual full Name when unique and stable; use the validated index when names are duplicate/unsuitable. Floating parameters use their sole endpoint (index 0). Do not invent a canonical label from a global nickname dictionary.
- Missing, negative/out-of-range, ambiguous or wrong-direction endpoints fail generation/preflight with component/direction/candidate details. Never drop a wire or default an unresolved multi-port endpoint to zero.
- Preserve every valid connection, including multiple wires between the same component pair on different ports. Cycles are not automatically invalid; respect existing GhJSON/Grasshopper graph rules.

If an optional plugin is unavailable or port metadata cannot be established safely, fail with an actionable diagnostic/retry result rather than returning `success:true` for an unverified repaired graph.

### Wrapper And Test Requirements

- `gh_generate_and_place_on_canvas` must use the same normalization and shared mutation commit path, propagate cancellation/context, and avoid a second undo lock. Capture generation once per owned replay operation; retrying placement must not re-run paid generation.
- Preserve the existing wrapper's payload keys and report generate/put partial failure honestly. Source wrappers currently include direct helper calls; refactor to trusted shared handlers, not delegate bypass.
- Pure tests: normalization idempotence; removal of model-supplied and fixer-generated identities; preserved state/wiring/groups; malformed/duplicate references; no imported-data normalization; strict resolver ambiguity/conflict; final schema validity.
- Live tests: dynamic/script IO, standalone parameters, canonical names/nicknames/indices, unavailable type failure, and generated-and-placed graph. Use deterministic response fixtures/local provider only.

## C. F16: Applied Viewport Projection

Existing source: `src/SmartHopper.Core.Grasshopper/AITools/canvas_view.cs`, especially `ApplyViewAction` and `ViewportPayload`. Use a shared viewport helper for other framing/screenshot callers as applicable.

### Root Cause Precision

The inspected installed Rhino 8 `GH_Viewport` implementation exposes `Target`, `Zoom` and `ComputeProjection()`. `Focus`/`MidPoint` setters update the target, but `MidPoint` getter derives from cached `VisibleRegion`. `ComputeProjection()` updates projection/inverse/visible rectangle. A synchronous `Refresh()` after building the payload is too late.

Prefer a deterministic **nonanimated** MCP action and explicit projection recomputation, not a timer, arbitrary sleep, polling "stable twice", private reflection, or an imagined animation-completion event. Verify the public methods against the minimum referenced Rhino 8 API during compilation. The object-movement animation in `CanvasAccess` is unrelated.

### Implementation Steps

1. Use package 1's bounded UI executor, capturing document/canvas identity. Resolve/validate the complete action before changing view state.
2. Read current projection coherently. `setCenter` preserves zoom. `setZoom` clamps to the existing tool range (0.01..32) and preserves the current world-space center.
3. For `frameGuids`/`zoomExtents`, union valid rendered bounds, calculate a zoom fitting both dimensions with 24 control-pixel padding on each side, clamp zoom, and use the bounds center. This makes "frame" real framing rather than blindly calling Focus without fitting.
4. Apply a coherent target transform using public API. For the inspected world-to-control projection: `targetX = round(viewWidth/2 - centerX*zoom)`, likewise Y. Validate finite values and target integer range before modifying anything; use at least 1 canvas unit for degenerate bounds dimensions.
5. Assign Zoom/Target, call `ComputeProjection()`, request refresh, then build `zoom`, `center`, `visibleRegion` from that **same** applied viewport in the same UI turn.
6. `framedGuids` identifies resolved distinct targets. Retain valid missing GUIDs in `missingGuids`; malformed GUID tokens receive a warning, not a fabricated Guid.Empty target. If no target resolves, return an error without changing the viewport.
7. No document undo/consent is needed for view-only actions; existing view-control exposure policy still applies. The response describes the instant the action was applied, not a lock against future user panning.

### Edge Cases / Tests

- Repeating frame on the same component returns a consistent center/zoom. Test at non-unit zoom (including about 0.4), far positive/negative coordinates and the original stale-center scenario.
- After setZoom, the old world center remains within rounding tolerance. After setCenter, zoom is unchanged.
- Allowed center error comes from integer target pixels: approximately `0.5/zoom` world units per axis, plus normal floating-point tolerance; do not assert exact float equality.
- Empty document, no active canvas, disposed/switched canvas, zero-sized/minimized view, nonfinite inputs, overflow-scale centers, partial/all missing targets and simultaneous queued views have explicit outcomes.
- Queued timeout/cancellation applies no late view. Success followed immediately by `canvas_screenshot` observes the applied projection. Do not use a Rhino perspective screenshot as canvas evidence.
- Put projection/fit math in a pure testable helper. Actual viewport behavior belongs in `SmartHopper.Components.Test`/live certification, not new Rhino-dependent unit tests.
