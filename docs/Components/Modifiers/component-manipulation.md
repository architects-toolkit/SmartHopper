# ComponentManipulation

Static utility class for manipulating Grasshopper component state on the canvas, including preview visibility, lock state, and geometric bounds.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Core.Grasshopper/Utils/Canvas/ComponentManipulation.cs` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-13 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

This utility class provides programmatic control over Grasshopper component state that is normally only accessible through the UI. Use it when building tools that need to modify component behavior automatically.

**You should read this if you:**

- Are building a tool that modifies component state programmatically
- Need to control preview visibility or lock state from code
- Want to query component positions on the canvas

---

## End-User Guide

### What Does This Do?

ComponentManipulation lets other SmartHopper tools control Grasshopper components automatically:

- **Preview control**: Show or hide geometry previews on components
- **Lock control**: Lock components to prevent them from solving
- **Bounds queries**: Get component positions for UI operations

### When to Use It

These operations are typically used by:

- The [ScriptModifier](./index.md) for script component operations
- The [ParameterModifier](./index.md) for parameter modifications
- Custom tools that need to control component state

<!-- PLACEHOLDER: Screenshot showing component right-click menu with preview/lock options -->

---

## Developer Reference

### API Overview

```csharp
public static class ComponentManipulation
{
    public static void SetComponentPreview(Guid guid, bool previewOn, bool redraw = true);
    public static void SetComponentLock(Guid guid, bool locked, bool redraw = true);
    public static RectangleF GetComponentBounds(Guid guid);
}

```

### SetComponentPreview

```csharp
public static void SetComponentPreview(Guid guid, bool previewOn, bool redraw = true)

```

Sets the preview state of a Grasshopper component or parameter by GUID.

**Parameters:**

- `guid` — GUID of the component to modify
- `previewOn` — `true` to show preview, `false` to hide
- `redraw` — `true` to redraw canvas immediately (default: `true`)

**Behavior:**

- Works with both `GH_Component` and `IGH_Param` objects
- Only affects objects where `IsPreviewCapable` is `true`
- Records an undo event for the operation
- Automatically redraws the canvas if `redraw` is true

**Example:**

```csharp
// Hide preview for a component
ComponentManipulation.SetComponentPreview(componentGuid, previewOn: false);

```

---

### SetComponentLock

```csharp
public static void SetComponentLock(Guid guid, bool locked, bool redraw = true)

```

Sets the lock state of a Grasshopper component or parameter by GUID.

**Parameters:**

- `guid` — GUID of the component to modify
- `locked` — `true` to lock (disable), `false` to unlock (enable)
- `redraw` — `true` to redraw canvas immediately (default: `true`)

**Behavior:**

- Works with both `GH_Component` and `IGH_Param` objects
- Locked components are disabled and won't solve
- Records an undo event for the operation
- Automatically redraws the canvas if `redraw` is true

**Example:**

```csharp
// Lock a component to prevent it from solving
ComponentManipulation.SetComponentLock(componentGuid, locked: true);

```

---

### GetComponentBounds

```csharp
public static RectangleF GetComponentBounds(Guid guid)

```

Gets the bounding rectangle of a Grasshopper component or parameter on the canvas.

**Parameters:**

- `guid` — GUID of the component or parameter

**Returns:**

- `RectangleF` — The bounding rectangle in canvas coordinates
- `RectangleF.Empty` — If the object is not found

**Example:**

```csharp
var bounds = ComponentManipulation.GetComponentBounds(componentGuid);
if (bounds != RectangleF.Empty)
{
    Console.WriteLine($"Component at ({bounds.X}, {bounds.Y}), size {bounds.Width}x{bounds.Height}");
}

```

---

## Architecture & Design

### Design Rationale

**Problem**: Need to programmatically control component state that is normally UI-only.

**Approach**: Static utility methods that wrap Grasshopper's internal APIs with undo support.

**Trade-offs**:

- **Benefit**: Simple API for common operations
- **Benefit**: Automatic undo recording
- **Cost**: Requires knowing the component's GUID

### System Relationships

```text
[ScriptModifier] ──uses──> [ComponentManipulation] <──uses── [ParameterModifier]
                                   │
                                   v
                         [Grasshopper Canvas API]

```

### Related Documentation

- [Modifiers Index](./index.md) -- overview of all modifier utilities
- [ScriptModifier](./index.md) -- script component operations
- [ParameterModifier](./index.md) -- parameter data settings
