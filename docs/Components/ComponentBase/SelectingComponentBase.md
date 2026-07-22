# SelectingComponentBase / SelectingStatefulComponentBase / AISelectingStatefulAsyncComponentBase

This page documents the family of bases that add a *Select Components* button to a Grasshopper component for picking other canvas objects as inputs.

## The three bases

| Base | Inherits | Use when |
| --- | --- | --- |
| `SelectingComponentBase` | `GH_Component` | Plain non-async, non-stateful component that just needs a Select button. |
| `SelectingStatefulComponentBase` | [StatefulComponentBase](./StatefulComponentBase.md) | Selection + state machine + persistent outputs, no AI. |
| `AISelectingStatefulAsyncComponentBase` | [AIStatefulAsyncComponentBase](./AIStatefulAsyncComponentBase.md) | Selection + AI request orchestration. |

All three implement `ISelectingComponent` and delegate the actual logic to the shared `SelectingComponentCore` helper. None of them re-implements selection or persistence.

## `ISelectingComponent`

```csharp
public interface ISelectingComponent
{
    List<IGH_DocumentObject> SelectedObjects { get; }
    void EnableSelectionMode();
}
```

> `SelectedObjects` exposes `IGH_DocumentObject`, not `IGH_ActiveObject`, so types like scribbles (which do not implement `IGH_ActiveObject`) are supported.

## `SelectingComponentCore` (internal helper)

Contains every piece of selection logic. Created by each base in its constructor with a `SubscribeToDocumentEvents()` call:

- `EnableSelectionMode()` — clears `SelectedObjects`, enters selection mode, hides the canvas context menu, refreshes the canvas to consume the current selection.
- Reads `Instances.ActiveCanvas.Document.SelectedObjects()` and filters to **components**, **params**, **groups**, **scribbles** (type name contains `Scribble`) and **panels** (type name contains `Panel`).
- `Write(GH_IWriter)` / `Read(GH_IReader)` — persists `SelectedObjectsCount` and `SelectedObject_0..N` as `InstanceGuid`s.
- `OnDocumentAdded` — restores GUIDs once the document is fully loaded; missing objects are skipped, the message is updated and `ExpireSolution` is called.
- `PruneDeletedSelections(...)` — invoked from each `SelectedObjects` getter to drop dead references.
- `RenderSelectButton` / `RenderSelectionOverlay` / `BuildSelectedBounds` / `Restart`+`StopSelectDisplayTimer` — drawing primitives shared between the plain and AI custom-attributes classes.

All canvas/UI work is marshalled to Rhino's UI thread via `RhinoApp.InvokeOnUiThread`.

## Custom attributes

- `SelectingComponentAttributes` (used by `SelectingComponentBase` and `SelectingStatefulComponentBase`) extends `GH_ComponentAttributes` and renders the Select button below the component plus a dashed-rectangle highlight around hovered selections.
- `AISelectingComponentAttributes` (used by `AISelectingStatefulAsyncComponentBase`) extends [`ComponentBadgesAttributes`](#related), so the AI variant keeps provider/model badges and adds the Select button. It also defers tooltip rendering so the tooltip stays above the Select overlay.

Both classes share a 5 s auto-hide timer for the dashed highlight when hovering the Select button.

- Are building a component that needs to read or react to other canvas objects (components, params, groups, scribbles, panels).
- Want to understand how selection persistence works across copy/paste and file re-open.
- Need to choose between the three selection-enabled base classes.

1. User clicks Select → attributes call `ISelectingComponent.EnableSelectionMode()`.
2. Core enters selection mode, clears the list, refreshes canvas.
3. Core reads currently-selected canvas objects, filters and stores them, sets `Message = "N selected"`.
4. On `Write` the core stores `InstanceGuid`s.
5. On `Read` and on `OnDocumentAdded` GUIDs are resolved back to live `IGH_DocumentObject` instances; missing ones are skipped.

## Design criteria

- **One source of truth.** All selection logic lives in `SelectingComponentCore`; the three bases are thin pass-throughs.
- **Persist GUIDs, not objects.** Documents survive copy/paste and re-open without dangling references.
- **UI thread for canvas work.** Restoration is dispatched through `RhinoApp.InvokeOnUiThread`.
- **`IGH_DocumentObject`** as the public type so scribbles and other non-`IGH_ActiveObject` items can be selected.

## Related

- `ISelectingComponent`, `SelectingComponentCore`, `SelectingComponentAttributes`, `AISelectingComponentAttributes` in `src/SmartHopper.Core/ComponentBase/`
- `ComponentBadgesAttributes` for the AI variant.
