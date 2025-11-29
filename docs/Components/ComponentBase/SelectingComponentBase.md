# SelectingComponentBase

Base that adds a "Select Components" UI to pick Grasshopper objects directly from the canvas.

## Purpose

Provide a consistent, shared schema for components that need the user to select other Grasshopper objects as inputs.

## Key types

- **`SelectingComponentBase`**
  Non‑AI base: inherits `GH_Component` and implements `ISelectingComponent`.

- **`AISelectingStatefulAsyncComponentBase`**
  AI base: inherits `AIStatefulAsyncComponentBase` and implements `ISelectingComponent`. Uses the same selection pipeline as `SelectingComponentBase`.

- **`ISelectingComponent`**
  Small contract implemented by both bases:
  - `List<IGH_ActiveObject> SelectedObjects { get; }`
  - `void EnableSelectionMode()`

- **`SelectingComponentCore`** (internal)
  Shared helper that contains all selection logic:
  - Enters/leaves selection mode
  - Tracks selected objects on the canvas
  - Handles GUID‑based persistence and deferred restoration

- **`SelectingComponentAttributes`**
  Shared `GH_ComponentAttributes` that renders:
  - The "Select" button below the component
  - Dashed highlight rectangles around selected objects on hover
  - A connector line from the combined selection center to the "Select" button using the same color as dialog links
  It works with any `GH_Component` that also implements `ISelectingComponent`.

## Selection pipeline

- **1. User clicks "Select" button**
  `SelectingComponentAttributes` calls `ISelectingComponent.EnableSelectionMode()`.

- **2. Enter selection mode**
  `SelectingComponentCore.EnableSelectionMode()`:
  - Clears `SelectedObjects`
  - Sets an internal `inSelectionMode` flag
  - Hides the canvas context menu
  - Triggers a refresh to process current canvas selection

- **3. Collect selected objects**
  The core reads `Instances.ActiveCanvas.Document.SelectedObjects()` and filters to supported types (see below), populating `SelectedObjects` and updating the component message to `"N selected"`.

- **4. Persist selection**
  On `Write(...)` the core stores:
  - `SelectedObjectsCount`
  - `SelectedObject_0..N` as `InstanceGuid`s of each `IGH_DocumentObject`

- **5. Restore selection**
  On `Read(...)` and when the document finishes loading (`OnDocumentAdded`):
  - GUIDs are resolved back to document objects
  - Only existing objects are re‑added to `SelectedObjects`
  - Missing/deleted objects are skipped
  - The component `Message` is updated and the solution is expired when something is restored

All document‑load restoration is invoked on Rhino's UI thread via `RhinoApp.InvokeOnUiThread` to keep canvas access safe.

## What gets selected

`SelectingComponentCore` currently accepts:

- **`IGH_Component`** instances
- **`IGH_Param`** instances
- **Groups** (`Grasshopper.Kernel.Special.GH_Group`)
- **Scribbles** (types whose name contains `"Scribble"`)
- **Panels** (types whose name contains `"Panel"`)

Everything is stored as `IGH_ActiveObject` in `SelectedObjects` but must also be an `IGH_DocumentObject` to be persisted (so it has an `InstanceGuid`).

## When to derive

- **Use `SelectingComponentBase`** when:
  - You are building a non‑AI component in `SmartHopper.Components`.
  - You want a "Select" button + selection highlight and persistent `SelectedObjects`.

- **Use `AISelectingStatefulAsyncComponentBase`** when:
  - You also need the AI stateful async pipeline (`Run?`, metrics, provider/model selection).
  - You want the same selection UX integrated into an AI component.

In both cases, you typically:

- Read `SelectedObjects` in `SolveInstance` (or async worker) to drive your logic.
- Do **not** re‑implement any selection or persistence logic: this is fully handled by `SelectingComponentCore`.

## Related

- [StatefulAsyncComponentBase](./StatefulAsyncComponentBase.md)
- [AIStatefulAsyncComponentBase](./AIStatefulAsyncComponentBase.md)
- `ISelectingComponent`, `SelectingComponentCore`, and `SelectingComponentAttributes` in `src/SmartHopper.Core/ComponentBase`
