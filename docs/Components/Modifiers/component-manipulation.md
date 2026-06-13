# ComponentManipulation

`src/SmartHopper.Core.Grasshopper/Utils/Canvas/ComponentManipulation.cs`

Static utility class for manipulating Grasshopper component and parameter state on the canvas (preview visibility, lock state, bounds).

## Purpose

Provides programmatic control over component visualization and interaction state without modifying the component's data or logic.

## Public API

```csharp
public static class ComponentManipulation
{
    public static void SetComponentPreview(Guid guid, bool previewOn, bool redraw = true);
    public static void SetComponentLock(Guid guid, bool locked, bool redraw = true);
    public static RectangleF GetComponentBounds(Guid guid);
}
```

## Methods

### SetComponentPreview

- **Purpose**: Show or hide the preview of a component or parameter by GUID.
- **Parameters**:
  - `guid` — GUID of the component or parameter
  - `previewOn` — `true` to show preview, `false` to hide
  - `redraw` — `true` to redraw the canvas immediately (default: `true`)
- **Behavior**:
  - Works on both `GH_Component` and `IGH_Param` objects
  - Checks `IsPreviewCapable` before applying the change
  - Records an undo event for user-initiated changes
  - Redraws the canvas if requested

### SetComponentLock

- **Purpose**: Lock or unlock a component by GUID.
- **Parameters**:
  - `guid` — GUID of the component
  - `locked` — `true` to lock, `false` to unlock
  - `redraw` — `true` to redraw the canvas immediately (default: `true`)
- **Behavior**:
  - Prevents the user from editing the component when locked
  - Records an undo event
  - Redraws the canvas if requested

### GetComponentBounds

- **Purpose**: Retrieve the bounding rectangle of a component on the canvas.
- **Parameters**:
  - `guid` — GUID of the component
- **Returns**: `RectangleF` representing the component's bounds, or an empty rectangle if not found

## Usage example

```csharp
using SmartHopper.Core.Grasshopper.Utils.Canvas;

// Hide preview of a component
ComponentManipulation.SetComponentPreview(componentGuid, previewOn: false);

// Lock a component to prevent editing
ComponentManipulation.SetComponentLock(componentGuid, locked: true);

// Get component bounds
var bounds = ComponentManipulation.GetComponentBounds(componentGuid);
Console.WriteLine($"Component bounds: {bounds}");
```

## Related

- [ParameterModifier](./parameter-modifier.md) — modify parameter data settings
- [ScriptModifier](./script-modifier.md) — modify script component code and parameters
