# ParameterModifier Reference

**Namespace**: `SmartHopper.Core.Grasshopper.Utils.Components`  
**Purpose**: Modify parameter data settings on non-script components

---

## Overview

`ParameterModifier` provides focused operations for parameter data manipulation settings. Use this for modifying how parameters handle data flow (flatten, graft, reverse, simplify).

**Scope**: Non-script component parameters only  
**For Script Components**: Use `ScriptModifier` first to set up parameters, then `ParameterModifier` for data settings

---

## API Reference

### Individual Parameter Operations

#### SetDataMapping

Set how the parameter organizes data (None, Flatten, Graft).

```csharp
public static void SetDataMapping(IGH_Param param, GH_DataMapping dataMapping)
```

**Data Mapping Options**:

- `GH_DataMapping.None` - No transformation
- `GH_DataMapping.Flatten` - Flatten data tree to single list
- `GH_DataMapping.Graft` - Add an extra branch level

**Example**:

```csharp
var param = component.Params.Input[0];
ParameterModifier.SetDataMapping(param, GH_DataMapping.Flatten);
```

#### SetReverse

Reverse the order of list items.

```csharp
public static void SetReverse(IGH_Param param, bool reverse)
```

**Example**:

```csharp
ParameterModifier.SetReverse(param, true); // Reverse list
ParameterModifier.SetReverse(param, false); // Normal order
```

#### SetSimplify

Simplify geometry (remove redundant control points, etc.).

```csharp
public static void SetSimplify(IGH_Param param, bool simplify)
```

**Example**:

```csharp
ParameterModifier.SetSimplify(param, true); // Simplify geometry
```

---

### Bulk Operations

#### BulkApply

Apply settings to multiple parameters at once.

```csharp
public static void BulkApply(
    IEnumerable<IGH_Param> parameters,
    GH_DataMapping? dataMapping = null,
    bool? reverse = null,
    bool? simplify = null)
```

**Example**:

```csharp
// Flatten all inputs
var allInputs = component.Params.Input;
ParameterModifier.BulkApply(
    allInputs,
    dataMapping: GH_DataMapping.Flatten);

// Reverse and simplify all outputs
var allOutputs = component.Params.Output;
ParameterModifier.BulkApply(
    allOutputs,
    reverse: true,
    simplify: true);
```

#### BatchModify

Apply custom modifications to multiple parameters.

```csharp
public static void BatchModify(
    IEnumerable<IGH_Param> parameters,
    Action<IGH_Param> modificationAction)
```

**Example**:

```csharp
// Custom logic for each parameter
ParameterModifier.BatchModify(
    component.Params.Input,
    param => {
        if (param.Name.Contains("curve"))
        {
            param.Simplify = true;
            param.DataMapping = GH_DataMapping.Flatten;
        }
    });
```

---

## Common Use Cases

### Flatten All Inputs

```csharp
[AITool("gh_parameter_flatten_all")]
public static AIReturn FlattenAllInputs(Guid componentGuid)
{
    var component = CanvasAccess.FindInstance(componentGuid) as IGH_Component;
    if (component == null)
        return AIReturn.Failure("Component not found");

    ParameterModifier.BulkApply(
        component.Params.Input,
        dataMapping: GH_DataMapping.Flatten);

    component.ExpireSolution(true);
    return AIReturn.Success($"Flattened {component.Params.Input.Count} inputs");
}
```

### Graft Specific Parameter

```csharp
[AITool("gh_parameter_graft")]
public static AIReturn GraftParameter(Guid componentGuid, int paramIndex)
{
    var component = CanvasAccess.FindInstance(componentGuid) as IGH_Component;
    if (component == null || paramIndex < 0 || paramIndex >= component.Params.Input.Count)
        return AIReturn.Failure("Invalid component or parameter index");

    var param = component.Params.Input[paramIndex];
    ParameterModifier.SetDataMapping(param, GH_DataMapping.Graft);

    component.ExpireSolution(true);
    return AIReturn.Success($"Grafted parameter '{param.Name}'");
}
```

### Reverse Parameter Data

```csharp
[AITool("gh_parameter_reverse")]
public static AIReturn ReverseParameter(Guid componentGuid, int paramIndex)
{
    var component = CanvasAccess.FindInstance(componentGuid) as IGH_Component;
    if (component == null || paramIndex < 0 || paramIndex >= component.Params.Input.Count)
        return AIReturn.Failure("Invalid component or parameter index");

    var param = component.Params.Input[paramIndex];
    ParameterModifier.SetReverse(param, true);

    component.ExpireSolution(true);
    return AIReturn.Success($"Reversed parameter '{param.Name}'");
}
```

### Simplify Geometry Output

```csharp
[AITool("gh_parameter_simplify")]
public static AIReturn SimplifyParameter(Guid componentGuid, int paramIndex)
{
    var component = CanvasAccess.FindInstance(componentGuid) as IGH_Component;
    if (component == null || paramIndex < 0 || paramIndex >= component.Params.Output.Count)
        return AIReturn.Failure("Invalid component or parameter index");

    var param = component.Params.Output[paramIndex];
    ParameterModifier.SetSimplify(param, true);

    component.ExpireSolution(true);
    return AIReturn.Success($"Simplified parameter '{param.Name}'");
}
```

---

## Integration with ScriptModifier

For script components, use both modifiers together:

```csharp
// Step 1: Set up script structure with ScriptModifier
ScriptModifier.AddInputParameter(
    scriptComp,
    "curves",
    typeHint: "Curve",
    access: "list");

// Step 2: Apply data settings with ParameterModifier
var param = ((IGH_Component)scriptComp).Params.Input[0];
ParameterModifier.SetDataMapping(param, GH_DataMapping.Flatten);
ParameterModifier.SetSimplify(param, true);
```

---

## Thread Safety

Always execute on UI thread:

```csharp
RhinoApp.InvokeOnUiThread(() =>
{
    ParameterModifier.SetDataMapping(param, GH_DataMapping.Flatten);
    component.ExpireSolution(true);
    Instances.RedrawCanvas();
});
```

---

## Best Practices

### 1. Check Parameter Type

Not all parameters support all settings:

```csharp
// Simplify only makes sense for geometry
if (param.Name.Contains("geo") || param.Name.Contains("curve"))
{
    ParameterModifier.SetSimplify(param, true);
}
```

### 2. Expire Solution After Changes

```csharp
ParameterModifier.SetReverse(param, true);
component.ExpireSolution(true); // Recalculate with new settings
```

### 3. Use Undo Support

```csharp
component.RecordUndoEvent("[SH] Flatten Parameter");
ParameterModifier.SetDataMapping(param, GH_DataMapping.Flatten);
```

### 4. Validate Indices

```csharp
if (paramIndex >= 0 && paramIndex < component.Params.Input.Count)
{
    var param = component.Params.Input[paramIndex];
    ParameterModifier.SetReverse(param, true);
}
```

---

## Visual Reference

### Data Mapping Visualized

```text
Original Data:
{0;0} → [A, B]
{0;1} → [C, D]

FLATTEN:
{0} → [A, B, C, D]

GRAFT:
{0;0;0} → [A]
{0;0;1} → [B]
{0;1;0} → [C]
{0;1;1} → [D]
```

### Reverse Visualized

```text
Original: [A, B, C, D]
Reverse:  [D, C, B, A]
```

---

## Related

- [ScriptModifier](./script-modifier.md) - Script component operations
- [ComponentManipulation](./component-manipulation.md) - Component state
- [Data Tree Operations](../../DataTree/index.md)
