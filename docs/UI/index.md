# User Interface

Documentation for SmartHopper's user-facing UI: the WebView chat, canvas overlays and review dialogs, and the icon set design brief.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Core/UI/` and `src/SmartHopper.Core.Grasshopper/Utils/Canvas/` |
| **Since Version** | ? |
| **Last Updated** | 2026-09-07 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

SmartHopper surfaces AI capabilities through two main UI layers: the WebView-based chat and canvas-integrated overlays/dialogs. This index links the detailed pages and explains how they relate.

**You should read this if you:**

- Are working on the chat dialog, canvas overlays, or Eto dialogs
- Need to find where a UI surface is implemented
- Want to add a new user-facing review or visualization feature

---

## End-User Guide

### UI Surfaces

| Surface | Description | Documentation |
| --- | --- | --- |
| Chat UI | Full WebView chat interface bridged to the Rhino host | [Chat UI](Chat/index.md) |
| AI Canvas Change Review | Staged visual review of AI-proposed canvas changes | [AI Canvas Change Review](ai-change-review.md) |
| Icon Set | Component and brand asset design brief | [Icon Set Brief](icon-set-brief.md) |

### Common Questions

**Q: Where does the chat window live?**
A: The WebView chat is hosted in `SmartHopper.Core` under `UI/Chat/`, with conversation state in `SmartHopper.Infrastructure`.

**Q: Where are canvas overlays implemented?**
A: Overlays live in `SmartHopper.Core.Grasshopper` under `Utils/Canvas/` and render via `GH_Canvas` paint callbacks.

---

## Developer Reference

### Entry Points

| Concern | Location |
| --- | --- |
| Chat dialog and renderer | `src/SmartHopper.Core/UI/Chat/` |
| Canvas overlays and review | `src/SmartHopper.Core.Grasshopper/Utils/Canvas/` |
| Assembly-level canvas registration | `src/SmartHopper.Components/SmartHopperAssemblyPriority.cs` |

### Code Examples

#### Registering a Canvas Overlay

```csharp
// Overlays are initialized once from the assembly priority load hook.
public override GH_LoadingInstruction PriorityLoad()
{
    CanvasProtectionOverlay.Initialize();
    CanvasChangePreviewOverlay.Initialize();
    return GH_LoadingInstruction.Proceed;
}
```

#### Showing a Staged Review

```csharp
// Tools hand a prepared session to the shared review service.
var session = CanvasChangeReviewService.CreateMoveSession("gh_move", targets, relative: false);
var applied = await CanvasChangeReviewService.ReviewAsync(session);
```

---

## Architecture & Design

### Design Rationale

**Problem**: SmartHopper needs both a rich conversational UI and lightweight, in-context canvas feedback without duplicating UI frameworks.

**Approach**: The chat uses a WebView hosted in Eto for rich HTML rendering; canvas feedback uses `GH_Canvas` paint callbacks plus small Eto dialogs. Both are owned by `SmartHopper.Core`/`SmartHopper.Core.Grasshopper` so provider and component projects stay UI-free.

### Related Documentation

- [Chat UI](Chat/index.md)
- [AI Canvas Change Review](ai-change-review.md)
- [Icon Set Brief](icon-set-brief.md)
- [Documentation Hub](../index.md)
