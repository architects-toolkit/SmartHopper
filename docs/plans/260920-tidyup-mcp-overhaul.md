# Plan: Layout quality overhaul + MCP/canvas tooling usability

**Status:** planned — not yet implemented
**Date:** 2026-09-20
**Branches:**

- `architects-toolkit/SmartHopper` → `feature/mcp-canvas-usability` (branched from `feature/2.1.0-agent-features`)
- `architects-toolkit/ghjson-dotnet` → `feature/port-aware-layout` (branched from `main`)

**Origin:** live assessment of `gh_tidy_up` on a two-island test definition (math + geometry islands, 19 components incl. sliders, panels, a Number param, fan-out, multi-port sources, a 3-layer skip edge). Screenshots: `tidyup-before.png`, `tidyup-after.png`, `tidyup-after-island1.png`, `tidyup-after-island2.png` (repo root — delete before merging or move under docs).

**Design rule for all items below:** prefer *general rules* over per-component-type fixes. If a fix only works for panels or sliders specifically, it is the wrong fix.

---

## Part A — ghjson-dotnet: layout engine

### A1. Port-align param targets, not just components (small, high impact)

`PortAlignment.AlignToPorts` skips every connection whose target is not `IGH_Component`, so wires into panels/params get zero alignment contribution. Fix: for `IGH_Param` targets, use the param's own input hotspot bounds (its left-edge input region) the same way component input ports are used.

- **Rule:** every connection contributes a desired source Y — component targets and param targets alike.
- File: `src/GhJSON.Grasshopper/LayoutRefinements/PortAlignment.cs`.

### A2. Align port-to-port, not center-to-port (small, high impact)

`AlignToPorts` aligns the *source component's center* to the target input port. For multi-output sources (Dec X/Y/Z, LT >/>=) no center position can make all wires horizontal. Fix: `desiredSourceY = targetPos.Y + targetPortDelta − sourcePortDelta`, where `sourcePortDelta` is the source's *output port* center offset from its own center (`conn.From.ParamIndex`).

- **Rule:** wires are port-to-port; alignment math must use both port offsets.
- File: same as A1.

### A3. Converge alignment and collision resolution (small-medium)

Today `AlignToPorts` sets precise Ys, then `CollisionResolver.AvoidCollisions` pushes colliding nodes down, silently re-breaking alignment. Fix: iterate `AlignToPorts` ↔ `AvoidCollisions` up to N=3 passes or until no node moves more than a small epsilon.

- **Rule:** the final layout must satisfy *both* constraints simultaneously; post-passes must converge, not overwrite each other.
- Files: `LayoutRefinementEngine.cs`, `CollisionResolver.cs`.

### A4. Feed real bounds into the core layout (medium, high impact)

`LayoutEngine` assigns every node `DefaultNodeWidth/Height` (100×60) regardless of real size; `BoundsAwareSpacing` then remaps the grid post-hoc by grouping on `(int)position` truncation — correct only because upstream emits exact integers.

- Add a bounds provider to `LayoutOptions` (`Func<Guid, SizeF?>` or a pre-populated `Dictionary<Guid, SizeF>`). `gh_tidy_up`/`gh_put` supply live `Attributes.Bounds`; fallback stays 100×60 when unknown.
- `CoordinateAssigner` then computes per-column real widths and **per-row real heights** (row height = tallest node in that row, not global max) — removing most of `BoundsAwareSpacing`'s job.
- Replace `(int)` grouping keys in `BoundsAwareSpacing`/`CollisionResolver` with tolerance-based clustering (positions within ε px = same column/row).
- **Rule:** the graph model must carry real geometry; refinements adjust, never reconstruct.

### A5. Compactness (trivial config + relies on A4)

Current gaps are far too generous: `SpacingX=200` is the *empty gap* between a column's right edge and the next column's left edge; `SpacingY=100` between row bands; `IslandSpacingY=150`.

- New defaults: `SpacingX ≈ 80`, `SpacingY ≈ 28`, `IslandSpacingY ≈ 100` — just enough to distinguish wires and read port labels. Validate visually on the test definition; adjust within ±20.
- With A4's per-row heights, tall panels only inflate *their* row, not every row.
- **Rule:** spacing exists to make wires legible — size it for wire readability, not component padding.

### A6. Port-aware layout model (the large refactor)

Replace node-to-node ordering/coordinates with **port-row-aware** layout inside the existing Sugiyama pipeline:

- `LayoutNode` gains ordered input/output port slots (from `conn.*.ParamIndex` + real port heights via A4's bounds provider).
- Crossing counting and `CrossingMinimizer` order port *rows*, not component centers — fan-out edges get distinct rows and stop competing for one median.
- `CoordinateAssigner` places each component so its port rows land on their assigned slots: `pivot.Y = slotY − portCenterOffset`.
- Result: A1–A3 largely dissolve into the core model; the Grasshopper refinements shrink to a final validation/collision pass.
- Sinks stay in their natural longest-path layer (explicitly decided: **no** sink-to-maxLayer push).

### A7. Component resolution: obsolete must never win by name (small)

`ComponentTypeResolver` already penalizes obsolete types (−100) in `FindProxyByName` *exact-name ties*, but the fuzzy pool (`ComponentNameResolver.Resolve` over distinct names) is name-only — an obsolete component whose *name* fuzzy-matches best can still win.

- Exclude obsolete proxies from the fuzzy candidate set entirely, and prefer checking `IGH_ObjectProxy`/descriptor-level obsolete markers over the `Activator.CreateInstance` fallback where possible.
- **Rule:** an obsolete component is only ever instantiated when requested by explicit GUID (round-tripping old files must still work).

---

## Part B — SmartHopper: MCP & canvas tooling

### B1. Auto-approve policy for MCP mutations (medium)

Every mutating call pops the review dialog — correct default for chat, painful for scripted MCP flows. `MutationInvocationContext.Surface` already carries `AIToolSurface.Mcp`, and `ConsentGate` resolves a presenter per call — a clean seam.

- Add a consent policy: when `Surface == Mcp` **and** auto-approve is enabled → return approved decision without a dialog.
- Enable via a boolean input/parameter on the **MCP Server component** ("Auto-approve AI changes", default `false`) — visible on canvas where the server lives; user explicitly opts in per document. (Alternative: global setting; component input preferred for visibility.)
- Keep recording undo events regardless — auto-approve affects review, not undoability.
- Files: `Consent/ConsentContracts.cs` (context), `ConsentGate`/`MutatingOperationExecutor` (policy hook), MCP server component (input), `CanvasChangeReviewService` (skip path).

### B2. Native MCP image results (small-medium)

`canvas_screenshot`/`viewport_screenshot` return `imageBase64` inside the JSON result — clients must decode it manually (I did exactly that this session). The MCP spec has a native `image` content block.

- In the MCP bridge, detect `{imageBase64, mimeType}` tool results and emit proper `{"type":"image","data":…,"mimeType":"image/png"}` content blocks.
- Also add optional `savePath` parameter to both screenshot tools to write the PNG to disk directly.
- **Rule:** tools returning binary payloads use MCP-native content types, never base64-in-JSON.

### B3. Viewport control tool (small)

No zoom/pan exists — I had to ask the user to hand-zoom twice. `CanvasAccess` already writes `Viewport.MidPoint`.

- New `canvas_view` tool: `action: zoomExtents | frameGuids | setZoom | setCenter`, with `guids`, `zoom`, `x`, `y` args. Read-only-ish (viewport only, no document mutation — no consent needed).
- File: `Utils/Canvas/CanvasAccess.cs` (viewport helpers), new AITool provider.

### B4. Selection tool (small)

`gh_tidy_up_selected` exists but nothing can *select* via MCP.

- New `gh_select` tool: `guids`, `mode: set | add | remove | clear`. Mutating (goes through the same consent path — and is the natural consumer of B1's auto-approve).

### B5. Idempotent mutating calls (medium)

My first `gh_put` reported "failed to connect" yet had actually executed; the retry placed a second copy of all 19 components on identical pivots.

- Add optional `requestId` to mutating tool args; cache `{requestId → result}` for ~10 min in the executor layer; a retried call returns the cached result instead of re-executing.
- **Rule:** idempotency lives once at the `MutatingOperationExecutor`/tool layer, not per tool.

### B6. Compact errors and reads (small)

- On argument-validation failure the MCP layer re-sent the entire tool catalog (thousands of lines). Return `{error, expectedSchema}` for the called tool only.
- `gh_get` dumps full ghjson + names + guids + serialization quality every call. Add `detail: summary | full` (default `summary` = guid + name + pivot + bounds only).
- **Rule:** tool responses are sized for the request, not for debugging.

---

## Cross-repo sequencing

1. **ghjson first:** A1–A3, A5 (defaults), A7 are independent and shippable as `GhJSON.* 1.2.0`. A4 is the prerequisite for A6.
2. SmartHopper consumes the new package version (bump `GhJSON.Core`/`GhJSON.Grasshopper` in `SmartHopper.Core.Grasshopper.csproj`, or use the existing local-source switch during development).
3. SmartHopper B-items are independent of ghjson and can land in any order; B1 unblocks hands-free MCP testing of everything else.
4. **Re-run the same test** (`tidyup-test.ghjson`) after each phase; expected end state: zero visible crossings on both islands, all wires into panels near-horizontal, ~half the current whitespace, no review clicks required.

## Explicitly out of scope

- Pushing sinks to the last layer (rejected — sinks stay at natural depth).
- Per-component-type cosmetic tweaks (panels-only offsets, slider-specific hacks).
