# SelectingComponentBase

Base that adds a "Select Components" UI to pick Grasshopper objects directly from the canvas.

## Purpose

Provide a consistent UX for components that need the user to select other GH objects as inputs.

## Key features

- Renders a "Select Components" button via custom attributes.
- Manages selection state and notifies when it changes.
- **Persists selection across document saves/loads** using GUID-based serialization.
- Safe interaction with the GH canvas and document.
- Graceful handling of missing objects (deleted after selection).

## Usage

- Derive when your logic depends on user‑selected GH objects (components/params/wires, depending on your implementation).
- Handle selection changes and validate that the chosen targets are compatible.
- Keep canvas operations on the UI thread and respect Grasshopper undo/redo rules.

## Related

- This is an independent base; it does not depend on any other component base.