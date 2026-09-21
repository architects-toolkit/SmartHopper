# Live re-assessment: gh_tidy_up after bounds-aware spacing (GhJSON 1.2.0)

**Status:** fixes applied (see addendum) — pending rebuild + live re-verification
**Date:** 2026-09-21
**Branch tested:** `feature/mcp-canvas-usability` (built + signed `2.0.0-dev.260921`)
**Method:** re-ran the original live test — `tidyup-test.ghjson` (19 components: math island with sliders/arithmetic/compare/panels + fan-out + skip edge; geometry island with sliders/Number param/panel input/Construct→Deconstruct Point/Sphere/panels), placed via `gh_put` (auto-approved), tidied via `gh_tidy_up`, evaluated with `canvas_hi-res_screenshot` (`scope=document`).
**Screenshots:** `tidyup-v2-before.png`, `tidyup-v2-after.png`, `tidyup-v2-after2.png` (repo root — delete before merging or move under docs).

This run validates which items of [260920-tidyup-mcp-overhaul](260920-tidyup-mcp-overhaul.md) landed and which gaps remain.

---

## What improved since the first assessment

| Area | Result |
| --- | --- |
| Density | Island-internal spacing is compact and readable (slider rows ~40px pitch, tight column flow). |
| Overlaps | Zero component overlaps in both islands. |
| Left-to-right flow | Clean layer progression (sources → operators → sinks). |
| Island separation | Islands are cleanly disjoint — no interleaving. |
| Unrelated objects | MCP Server component + Boolean Toggle were untouched (not in `guids`) — correct scoping. |
| Mixed reference points | Sliders (top-left pivot), Number param and components (center-ish pivots), Panels (top-left of 160×100) all align in the same input column — the `NodeSizeProvider` bounds work (A4) handles heterogeneous bounds correctly. |
| MCP flow | `gh_put` auto-approve (B1), native image blocks (B2), `gh_get detail=summary` (B6 read side) all work. |

Measured post-tidy coordinates (pivot):

```
Math island:    sliders C(972,471) A(972,525) B(972,565) → LT(1168,396) Add(1168,554)
                → GT?(1365,407) Mul(1365,544) → Sub(1557,534) → Result(1750,535)
Geometry:       X(200,1275) Y(200,1317) Z(200,1399) R(200,1533) → Pt(394,1305)
                → Dec(540,1112) Sph(540,1522) → py(732,899) pz(732,1347) sphere(732,1523)
```

## What still fails

### 1. Spurious moves on repeated runs — undo pollution (new finding)

Pass 2 and pass 3 each reported the same 6 components as `moved` (Add, Mul, Sub, LT, Dec, Sph) yet their final pivots are **bit-identical**. The layout is positionally converged after one pass, but the apply step moves nodes unconditionally instead of no-op'ing when `target == current`. Each extra `gh_tidy_up` pollutes the Grasshopper undo stack with no-op moves.

- **Fix:** in the apply path, skip `SetPivot` (and the undo record) when the computed pivot equals the current pivot within epsilon. Relates to plan item A3 — convergence is achieved in *position*, but not in *action*.

### 2. Fan-in port-order inversion → visible crossing (A2/A6 territory)

`LT` (Larger Than) sits at row 0 of the math island (y=396), above all three sliders. Its inputs come from `C` (y=471, above) and `A` (y=525, below), but `C` lands on `LT.B` (lower port) and `A` on `LT.A` (upper port) — the two wires cross visibly right before the inputs. Node-level row ordering can't express this; the ordering needs the target **port index**, not just the component.

### 3. Skip-edge wire passes through a component bounds

`A→Sub.A` (a 3-layer skip) runs at y≈530 across the whole island and passes through `Mul`'s bounds (Mul spans y522–566, x1334–1399; the wire crosses x1334–1399 at y≈528–530). The wire visually collides with `Mul`'s A-input port region — ambiguous which wire owns the port.

- **Fix direction:** during coordinate assignment, reserve a "wire corridor" row for skip edges (nodes with edge span >1 column get a dedicated Y channel between rows), or add a post-pass that detects wire-through-bounds and nudges the *source* or inserts spacing — node-level moves can't fix this cleanly; it's another argument for port-row-aware layout (A6).

### 4. Leaf panel floats far from its source row

`py` (fed by `Dec.Y`, output index 1) sits at y=899 — ~210px **above** Dec (y=1112), while `pz` (`Dec.Z`) sits at y=1346, ~230px below. The output column spreads panels across the full island height instead of aligning each to its source port row. Wires `Dec.Y→py` and `Dec.Z→pz` are long sweeping arcs. `sphere` (fed by `Sph` at y=1522 → panel at y=1523) shows the desired behavior: port-aligned, near-horizontal wire.

- **Fix:** A1+A2 — leaf sinks should align to their source's *output port* Y (param targets count as connection targets too).

### 5. Non-uniform visual gaps — uniform pivot pitch, not uniform gap

Column pitch is nearly constant (~192–197px pivot-to-pivot) regardless of column contents. Because slider/panel bounds are 160px wide and math components 65px, the *actual empty gap* varies wildly: slider→Add ≈ **5px**, Mul→Sub ≈ **127px**, Sub→Result ≈ **159px**. The 5px gap makes the A/B→Add wires nearly vertical stubs.

- **Fix:** pitch should be `maxLeftColWidth + spacing` *per column pair*, not one global pitch. The bounds are already available via `NodeSizeProvider` — the pitch just isn't consuming them per-column.

### 6. Islands not re-packed — diagonal arrangement wastes canvas

After tidy, math island occupies x972–1910/y374–634 and geometry x175–892/y898–1622 — diagonally opposed with a large empty L. Each island seems anchored near its original extent; islands aren't compacted relative to each other.

- **Fix:** after per-island layout, pack islands with a bin-packing/stacking pass (e.g., stack vertically at `IslandSpacingY`, or sort islands left-to-right then top-to-bottom within the selection's original bounding box).

### 7. `canvas_view` regression — cross-thread crash (tooling)

`canvas_view` (`zoomExtents`, tried twice) fails with `Cross-thread operation not valid: Control 'Canvas' accessed from a thread other than the thread it was created on.` The tool touches the GH canvas control off the UI thread. `gh_put`/`gh_tidy_up` marshal correctly, so this is specific to the viewport path (B3 shipped but crashes — needs a `Rhino.RhinoApp.InvokeOnUiThread` wrap or equivalent marshaling used by the mutating tools).

### 8. Error responses still dump the whole tool catalog (B6 partial)

An argument error (`gh_get_by_guid` wanted `guidFilter`, not `guids`) returned the new compact `{expectedSchema, error}` payload — good — **but** still appended the full `Available tools` catalog (~4000 lines). The compact error is produced; the catalog append needs removing from the error path.

## Verdict vs the 260920 plan

| Plan item | Status after this test |
| --- | --- |
| A1 param-target port alignment | **Not effective** — panel wires still unaligned (finding 4) |
| A2 port-to-port alignment | **Not effective** — LT fan-in crossing (finding 2) |
| A3 align↔collision convergence | **Partial** — positions converge, but moves aren't no-op'ed (finding 1) |
| A4 real bounds via NodeSizeProvider | **Landed** — heterogeneous pivots handled, no overlaps |
| A5 tighter spacing | **Landed** — dense and readable |
| A6 port-row-aware layout | Not implemented — findings 2/3/4 all point back to it |
| B1 auto-approve | Landed |
| B2 native image blocks + savePath | Landed |
| B3 canvas_view | **Broken** — cross-thread crash (finding 7) |
| B6 compact errors | **Partial** — schema hint added, catalog dump remains (finding 8) |
| — island packing | **New finding** — not in plan (finding 6) |
| — per-column pitch | **New finding** — spacing is pivot-uniform, not gap-uniform (finding 5) |

**Bottom line:** the bounds-aware spacing pass (A4+A5) visibly improved density and eliminated overlaps, and the MCP tooling layer (auto-approve, image blocks, summary reads) works. The remaining layout defects — input-order crossings, wire-through-bounds, floating leaf panels — all trace back to the same root cause identified originally: the layout model reasons about *component centers*, while Grasshopper wires are *port-to-port*. A6 remains the right fix. The newly discovered spurious-move and island-packing issues are small, independent wins worth taking first.

---

## Addendum — fixes applied after the second user review (2026-09-21)

The second review surfaced five more issues; investigation found they shared a single root cause plus independent small bugs.

### Root cause found: pivot-semantics mismatch

The whole pipeline (core `CoordinateAssigner`, `BoundsAwareSpacing`, `PortAlignment`) works in **bounds-center** coordinates, but the positions were written straight to `Attributes.Pivot` — which is top-left for sliders, panels and floating params, center for components. One write-time semantic mismatch explained three observations: panels top-aligned next to center-aligned components, the 5px slider→Add gap (finding 5), and part of the island drift (finding 6).

### Fixes

| Finding | Fix | Location |
| --- | --- | --- |
| Pivot-vs-center | New `PivotSemantics.CenterToPivot`/`BoundsCenter` (per-object `Pivot − BoundsCenter` offset; no per-type switch). Applied in `CanvasPlacer` and `gh_tidy_up` (targets + origin anchor both in center space) | `GhJSON.Grasshopper/Shared/PivotSemantics.cs`, `gh_tidy_up.cs` |
| Island packing (6) | Islands stack **vertically with a shared left edge**; normalization is by bounds top-left so edges truly align; ordering follows original canvas position (top→bottom) when pivots exist, size-desc otherwise. `IslandWrapWidth` removed | `LayoutEngine.cs` |
| Panels too big | `PanelHandler` sizes bounds from text/font/multiline/wrap when `bounds` is absent | `PanelHandler.cs` |
| Spurious moves (1) | `MoveInstance` no-ops sub-pixel (<0.5) deltas and records undo only when actually moving (was recording before the check) | `CanvasAccess.cs` |
| `canvas_view` crash (7) | Viewport access marshaled via new `CanvasAccess.RunOnUiThread` | `canvas_view.cs`, `CanvasAccess.cs` |
| Bonus: bounds math | `CanvasBoundsCalculator` now uses `Attributes.Bounds` edges directly (was `Pivot+size`, wrong for center-pivot objects) | `CanvasBoundsCalculator.cs` |
| Obsolete resolution | Rhino 8 renamed "Deconstruct Point" → "Deconstruct"; legacy `670fcdba` is obsolete so exact-name refused and fuzzy fell back to `Construct Point`. New name aliases (`deconstructpoint`, `pointdeconstruct`, `pointcoordinates`, `pdecon`) → `Deconstruct`. Test file `tidyup-test.ghjson` updated to modern GUIDs | `ComponentNameResolver.cs` |

### Verification status

- `GhJSON.Core.Tests`: 572/572 pass (one origin-semantics test updated to bounds-edge contract).
- `SmartHopper.Core.Grasshopper` builds clean.
- **Live MCP re-verification pending**: requires rebuild + Rhino restart (running instance has old assemblies; MCP server was down at the end of the session).

### Still open (require layout-model work, not boundary fixes)

- Fan-in port-order crossing (finding 2) — needs target-port-index ordering.
- Wire-through-bounds on skip edges (finding 3) — needs corridor reservation or routing awareness.
- Leaf-panel port alignment for multi-output sources (finding 4).
- Error responses still append the full tool catalog (finding 8).
