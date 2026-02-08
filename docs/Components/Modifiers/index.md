# Component & Parameter Modifiers

**Last Updated**: November 2025

Comprehensive utilities for modifying Grasshopper components and their parameters programmatically.

---

## Overview

The modifier system provides three focused classes for different modification needs:

```
Utils/
├── Components/
│   ├── ScriptModifier        → Script component operations
│   └── ParameterModifier     → Parameter data settings
└── Canvas/
    └── ComponentManipulation → Component state (lock, preview)
```

---

## Quick Reference

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

## Detailed Documentation

- [ScriptModifier Reference](./script-modifier.md) - Script component operations
- [ParameterModifier Reference](./parameter-modifier.md) - Parameter data settings
- [ComponentManipulation Reference](./component-manipulation.md) - Component state

---

## Decision Tree

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

---

## Use Cases

### AI Tools Integration

The modifiers are designed for use in AI tools:

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

### Script Component Updates

```csharp
// Update script with new parameters
ScriptModifier.UpdateScript(
    scriptComponent,
    newCode: updatedPythonCode,
    newInputs: inputSettingsArray,
    newOutputs: outputSettingsArray);
```

### Batch Parameter Operations

```csharp
// Apply settings to all inputs
var allInputs = component.Params.Input;
ParameterModifier.BulkApply(
    allInputs,
    dataMapping: GH_DataMapping.Flatten,
    simplify: true);
```

---

## GhJSON Integration

The modifier system provides **perfect parity** with GhJSON serialization:

| Property | GhJSON | ScriptModifier | ParameterModifier |
|----------|--------|----------------|-------------------|
| Script Code | ✅ | ✅ UpdateCode() | ❌ |
| Type Hints | ✅ | ✅ SetInputTypeHint() | ❌ |
| Access Types | ✅ | ✅ SetInputAccess() | ❌ |
| Data Mapping | ✅ | ❌ | ✅ SetDataMapping() |
| Reverse | ✅ | ❌ | ✅ SetReverse() |
| Simplify | ✅ | ❌ | ✅ SetSimplify() |
| ShowStdOutput | ✅ | ✅ SetShowStandardOutput() | ❌ |
| Principal Input | ✅ | ✅ SetPrincipalInput() | ❌ |

**Result**: Everything that can be serialized can be modified programmatically.

---

## Best Practices

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

### Error Handling

```csharp
try
{
    ScriptModifier.AddInputParameter(script, "data", "object", "item");
}
catch (ArgumentNullException ex)
{
    Debug.WriteLine($"Invalid script component: {ex.Message}");
}
```

---

## Related Documentation

- [GhJSON Format](https://github.com/architects-toolkit/ghjson-dotnet)
- [AI Tools Overview](../AITools/index.md)
- [Component Base Classes](../ComponentBase/index.md)

---

## API Status

| Class | Status | Methods | Coverage |
|-------|--------|---------|----------|
| **ScriptModifier** | ✅ Stable | 19 | 100% GhJSON |
| **ParameterModifier** | ✅ Stable | 5 | Parameter data |
| **ComponentManipulation** | ✅ Stable | 3 | State only |

**Last Review**: November 2025
**Breaking Changes**: None planned
