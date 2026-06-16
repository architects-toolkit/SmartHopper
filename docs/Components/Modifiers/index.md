# Component & Parameter Modifiers

Comprehensive utilities for modifying Grasshopper components and their parameters programmatically.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Core.Grasshopper/Utils/` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

This page provides a comprehensive guide to the modifier system, which enables programmatic modification of Grasshopper components and their parameters. This is essential for AI-powered automation and dynamic component configuration.

**You should read this if you:**

- Need to modify Grasshopper components programmatically
- Are building AI tools that interact with the Grasshopper canvas
- Want to understand how to update script components dynamically
- Need to manipulate parameter data settings (flatten, graft, reverse)

---

## End-User Guide

### Overview

The modifier system provides three focused classes for different modification needs:

```

Utils/
├── Components/
│   ├── ScriptModifier        → Script component operations
│   └── ParameterModifier     → Parameter data settings
└── Canvas/
    └── ComponentManipulation → Component state (lock, preview)

```

### Decision Tree

```

What do you need to modify?

├─ Script Component?
│  └─ Use ScriptModifier
│     ├─ Code updates
│     ├─ Parameter management
│     ├─ Type hints
│     └─ Access types
│
├─ Parameter Data Settings?
│  └─ Use ParameterModifier
│     ├─ Flatten/Graft
│     ├─ Reverse
│     └─ Simplify
│
└─ Component State?
   └─ Use ComponentManipulation
      ├─ Lock/unlock
      └─ Show/hide preview

```

### Common Questions

**Q: When should I use ScriptModifier vs ParameterModifier?**
A: Use `ScriptModifier` for script components (Python, C#, etc.) to manage their code, parameters, type hints, and access types. Use `ParameterModifier` for any component's parameter data settings like flatten, graft, reverse, and simplify.

**Q: Are these thread-safe?**
A: All modifications should occur on the UI thread using `RhinoApp.InvokeOnUiThread()`.

---

## Developer Reference

### ScriptModifier

**Location**: `SmartHopper.Core.Grasshopper.Utils.Components.ScriptModifier`
**Purpose**: Modify script components (Python, C#, VB, IronPython)

```csharp
using SmartHopper.Core.Grasshopper.Utils.Components;

// Update entire script
ScriptModifier.UpdateScript(scriptComp, newCode, inputs, outputs);

// Manage parameters
ScriptModifier.AddInputParameter(scriptComp, "points", "Point3d", "list");
ScriptModifier.SetInputTypeHint(scriptComp, 0, "Curve");
ScriptModifier.SetInputAccess(scriptComp, 0, GH_ParamAccess.list);

// Configure component
ScriptModifier.SetShowStandardOutput(scriptComp, true);
ScriptModifier.SetPrincipalInput(scriptComp, 0);

```

#### Code Example: Complete Script Update

```csharp
[AITool("script_generator")]
public static async Task<AIReturn> GenerateOrEditScript(
    Guid componentGuid,
    string instructions)
{
    var scriptComp = FindScriptComponent(componentGuid);

    // Get AI-generated changes
    var response = await GetAIResponse(instructions);

    // Apply on UI thread
    var tcs = new TaskCompletionSource<bool>();
    RhinoApp.InvokeOnUiThread(() =>
    {
        try
        {
            ScriptModifier.UpdateScript(
                scriptComp,
                response.Code,
                response.Inputs,
                response.Outputs);

            scriptComp.ExpireSolution(true);
            tcs.SetResult(true);
        }
        catch (Exception ex)
        {
            tcs.SetException(ex);
        }
    });

    await tcs.Task;
    return AIReturn.Success("Script generated or updated");
}

```

### ParameterModifier

**Location**: `SmartHopper.Core.Grasshopper.Utils.Components.ParameterModifier`
**Purpose**: Modify parameter data settings on non-script components

```csharp
using SmartHopper.Core.Grasshopper.Utils.Components;

// Single parameter
ParameterModifier.SetDataMapping(param, GH_DataMapping.Flatten);
ParameterModifier.SetReverse(param, true);
ParameterModifier.SetSimplify(param, true);

// Bulk operations
ParameterModifier.BulkApply(parameters,
    dataMapping: GH_DataMapping.Graft,
    reverse: true);

```

#### Code Example: AI Tool Integration

```csharp
// In an AI tool
[AITool("gh_parameter_flatten")]
public static AIReturn FlattenParameter(Guid componentGuid, int paramIndex)
{
    var obj = CanvasAccess.FindInstance(componentGuid);
    if (obj is IGH_Component comp && paramIndex < comp.Params.Input.Count)
    {
        ParameterModifier.SetDataMapping(
            comp.Params.Input[paramIndex],
            GH_DataMapping.Flatten);
        return AIReturn.Success("Parameter flattened");
    }
    return AIReturn.Failure("Component or parameter not found");
}

```

#### Code Example: Batch Parameter Operations

```csharp
// Apply settings to all inputs
var allInputs = component.Params.Input;
ParameterModifier.BulkApply(
    allInputs,
    dataMapping: GH_DataMapping.Flatten,
    simplify: true);

```

### ComponentManipulation

**Location**: `SmartHopper.Core.Grasshopper.Utils.Canvas.ComponentManipulation`
**Purpose**: Component state operations

```csharp
using SmartHopper.Core.Grasshopper.Utils.Canvas;

// Preview and lock
ComponentManipulation.SetComponentPreview(guid, previewOn: false);
ComponentManipulation.SetComponentLock(guid, locked: true);
var bounds = ComponentManipulation.GetComponentBounds(guid);

```

---

## Architecture & Design

### Design Rationale

The modifier system was designed to provide **100% parity** with GhJSON serialization capabilities, enabling programmatic modification of everything that can be serialized.

| Property | GhJSON | ScriptModifier | ParameterModifier |
| --- | --- | --- |----------------|-------------------|
| Script Code | ✅ | ✅ UpdateCode() | ❌ |
| Type Hints | ✅ | ✅ SetInputTypeHint() | ❌ |
| Access Types | ✅ | ✅ SetInputAccess() | ❌ |
| Data Mapping | ✅ | ❌ | ✅ SetDataMapping() |
| Reverse | ✅ | ❌ | ✅ SetReverse() |
| Simplify | ✅ | ❌ | ✅ SetSimplify() |
| ShowStdOutput | ✅ | ✅ SetShowStandardOutput() | ❌ |
| Principal Input | ✅ | ✅ SetPrincipalInput() | ❌ |

**Result**: Everything that can be serialized can be modified programmatically.

### Thread Safety

All modifications should occur on the UI thread:

```csharp
RhinoApp.InvokeOnUiThread(() =>
{
    ParameterModifier.SetDataMapping(param, GH_DataMapping.Flatten);
    Instances.RedrawCanvas();
});

```

### Undo Support

Use Grasshopper's undo system when modifying from UI:

```csharp
component.RecordUndoEvent("[SH] Flatten Parameter");
ParameterModifier.SetDataMapping(param, GH_DataMapping.Flatten);

```

### API Status

| Class | Status | Methods | Coverage |
| --- | --- | --- |---------|----------|
| **ScriptModifier** | ✅ Stable | 19 | 100% GhJSON |
| **ParameterModifier** | ✅ Stable | 5 | Parameter data |
| **ComponentManipulation** | ✅ Stable | 3 | State only |

**Breaking Changes**: None planned

### Related Documentation

- [ScriptModifier Reference](./script-modifier.md) - Script component operations
- [ParameterModifier Reference](./parameter-modifier.md) - Parameter data settings
- [ComponentManipulation Reference](./component-manipulation.md) - Component state
- [AI Components](../AI/index.md) - AI integration examples
