# AI Canvas Change Review

Structural AI canvas changes are staged as a visual proposal on the actual Grasshopper canvas and applied only after explicit user acceptance.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Core.Grasshopper/Utils/Canvas/` |
| **Since Version** | 2.1.0 |
| **Last Updated** | 2026-09-07 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

AI tools can add, modify, move, connect, group, and delete Grasshopper objects. Without a review step, every proposed mutation becomes part of the document immediately. This feature stages structural changes first, paints them over the live canvas, and applies only what the user accepts.

**You should read this if you:**

- Want to review or partially accept AI-proposed canvas changes
- Are integrating a canvas-mutating AI tool with the shared review flow
- Need to understand how SmartHopper splits responsibilities with `ghjson-dotnet`
- Are debugging overlay rendering, proposal filtering, or undo behavior

---

## End-User Guide

### What Is This?

When an AI tool wants to change the canvas, SmartHopper shows a review dialog next to the Grasshopper window and paints the proposal directly on the canvas. Nothing is added to or removed from the document until the user clicks **Apply**.

### Overlay Color Legend

| Color | Meaning |
| --- | --- |
| Green | Component additions |
| Amber | Modifications and proposed move targets |
| Red | Removals and disconnections |
| Blue | New connections |
| Purple | Groups |

### Step-by-Step

1. Run an AI request that mutates the canvas (for example `gh_put`, `gh_move`, or `gh_disconnect`).
2. Inspect the painted proposal on the canvas and the matching rows in the review dialog.
3. Uncheck any change you do not want, or cancel to apply nothing.
4. Click **Apply** to execute only the effectively accepted changes.
5. The tool reports accepted and rejected counts back to the AI so it can continue without assuming rejected changes were applied.

### Common Questions

**Q: Does the preview create temporary components on the canvas?**
A: No. The preview is painted during `CanvasPostPaintOverlay`. It never creates a duplicate `GH_Document` or temporary `IGH_DocumentObject` instances.

**Q: What happens when I cancel the dialog?**
A: The proposal is discarded and the document is left untouched.

**Q: Can protected components be changed by the AI?**
A: No. Protected canvas objects are filtered out before review and cannot be accepted indirectly through dependent changes.

---

## Developer Reference

### API Overview

```csharp
// Review entry point: shows the dialog and overlay, returns true when applied.
public static class CanvasChangeReviewService
{
    public static Task<bool> ReviewAsync(CanvasChangeReviewSession session);
    public static CanvasChangeReviewSession CreateRemovalSession(string source, IEnumerable<Guid> instanceGuids);
    public static CanvasChangeReviewSession CreateMoveSession(string source, IReadOnlyDictionary<Guid, PointF> targets, bool relative);
    public static CanvasChangeReviewSession CreateConnectionSession(string source, IReadOnlyList<CanvasConnectionReviewProposal> proposals, CanvasChangeKind kind);
    public static IReadOnlySet<Guid> GetAcceptedComponentGuids(CanvasChangeReviewSession session);
    public static IReadOnlyList<Guid> GetAcceptedRemovalGuids(CanvasChangeReviewSession session);
    public static IReadOnlySet<int> GetAcceptedConnectionProposalIndexes(CanvasChangeReviewSession session);
}
```

### Key Types

| Type | Purpose |
| --- | --- |
| `CanvasChangeReviewItem` | One independently selectable structural change, with optional dependencies via `RequiredItemKeys` |
| `CanvasChangeReviewSession` | Immutable proposal (`GhJsonDocument`) plus mutable user selections |
| `CanvasChangeReviewService` | UI-thread review entry point and proposal factories for removals, moves, and connections |
| `CanvasChangePreviewOverlay` | Actual-canvas renderer hooked on `CanvasPostPaintOverlay`; never mutates `GH_Document` |
| `CanvasChangeReviewDialog` | Eto dialog with per-change accept/reject checkboxes, positioned beside the canvas |
| `GhPutChangePlan` | Compares an incoming GhJSON fragment with serialized live components and filters rejected components, connections, and groups |
| `CanvasChangeKind` | Visual category: `ComponentAdded`, `ComponentModified`, `ComponentRemoved`, `ConnectionAdded`, `ConnectionRemoved`, `GroupAdded`, `GroupModified`, `GroupRemoved` |

### Code Examples

#### Building and Reviewing a Proposal

```csharp
// A tool builds review items, shows the session, and applies only accepted changes.
var session = new CanvasChangeReviewSession(
    "Review AI changes",
    "gh_put",
    proposedDocument,
    items);

var applied = await CanvasChangeReviewService.ReviewAsync(session);
if (!applied)
{
    // Cancelled: apply nothing.
}
```

#### Declaring Item Dependencies

```csharp
// A connection item can require its endpoint component items to be accepted.
var wire = new CanvasChangeReviewItem(
    "connection:0",
    CanvasChangeKind.ConnectionAdded,
    "Connect Number Slider -> Panel",
    "New wire");
wire.RequiredItemKeys.Add("component:42");
```

### Error Handling

| Error | Cause | Solution |
| --- | --- | --- |
| Proposal appears empty | All items were filtered as protected or invalid | Check the tool result; rejected/protected items are reported to the AI |
| Overlay not visible | Canvas was recreated or the session was cleared | Re-run the tool; overlays detach automatically on session end |
| Rejected dependency still applied | Item keys were not declared in `RequiredItemKeys` | Add dependency keys so dependent changes are rejected together |

---

## Architecture & Design

### Design Rationale

**Problem**: AI canvas mutations were applied immediately, giving the user no chance to inspect or partially accept them before they became persistent document changes.

**Approach**: Document staging. Tools build a proposed `GhJsonDocument` off-canvas, the overlay paints the proposal on the live canvas, and the final mutation runs only after user acceptance — ideally as a single undoable action.

**Trade-offs**:

- True staging (safe, honest preview) vs approximated ghost bounds for not-yet-instantiated components
- Shared review contracts (consistent UX across tools) vs a small per-tool adapter to describe proposals

### Ownership

- **GhJSON.Core** owns portable GhJSON/GhPatch models, diffing, patching, checksums, and conflict handling.
- **GhJSON.Grasshopper** owns generic canvas serialization, placement, connection, deletion, and undo integration.
- **SmartHopper.Core.Grasshopper** owns AI proposal policy, user acceptance, Eto UI, and canvas overlays.

This keeps GhJSON reusable by non-AI clients while avoiding SmartHopper-specific approval concepts in the format libraries.

### Data Flow

```text
AI tool/component → build proposal (GhJsonDocument + review items)
        → ConsentGate (one invocation, one decision)
        → CanvasChangeReviewService.ReviewAsync
        → CanvasChangePreviewOverlay paints on live canvas
        → CanvasChangeReviewDialog collects accept/reject
        → apply accepted subset only → tool result reports rejected items
```

### Tool Coverage

The shared review flow currently gates:

- `gh_put` additions, replacements, connections, and groups
- `gh_remove` and `gh_clear` removals
- `gh_connect` and `gh_disconnect` wire changes
- `gh_move` target positions
- `gh_group` and `gh_group_selected` group creation

Runtime actions such as button clicks and script execution are not represented as structural diffs. They retain their existing execution semantics.

### Gotchas

- Added-component ghosts approximate component bounds because exact attributes are only available after Grasshopper instantiation.
- Proposals without pivots use GhJSON dependency-graph layout for preview; Grasshopper-aware final layout may differ slightly.
- Non-edit `gh_put` can auto-offset the accepted network to avoid live objects, so its final global offset can differ from the proposal coordinates.
- Specialized structural tools (`gh_tidy_up`, `gh_smart_connect`, preview/lock changes, parameter modifiers) still use their existing execution paths and can adopt the same contracts later.

### Related Documentation

- [User Interface](./index.md)
- [Chat UI](./Chat/index.md)
- [Tools](../Tools/index.md)
