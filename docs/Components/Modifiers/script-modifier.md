# ScriptModifier Reference

**Namespace**: `SmartHopper.Core.Grasshopper.Utils.Components`
**Purpose**: Comprehensive modification of script components (Python, C#, VB, IronPython)

---

## Overview

`ScriptModifier` provides a complete API for modifying script components programmatically, with **100% parity** with GhJSON serialization capabilities.

**Key Features**:

- ✅ Update script code
- ✅ Manage input/output parameters
- ✅ Set type hints (Point3d, Curve, etc.)
- ✅ Configure access types (item/list/tree)
- ✅ Control standard output visibility
- ✅ Set principal input parameter
- ✅ Manage parameter properties (optional/required, descriptions)

---

## API Reference

### Script Updates

#### UpdateScript

Complete script update with code and parameters.

```csharp
public static void UpdateScript(
    IScriptComponent scriptComp,
    string newCode,
    JArray newInputs,
    JArray newOutputs)
```

**Example**:

```csharp
ScriptModifier.UpdateScript(
    scriptComp,
    newCode: "print('Hello, Rhino!')",
    newInputs: inputsJArray,
    newOutputs: outputsJArray);
```

#### UpdateCode

Update only the script code.

```csharp
public static void UpdateCode(IScriptComponent scriptComp, string newCode)
```

**Example**:

```csharp
ScriptModifier.UpdateCode(scriptComp, "import rhinoscriptsyntax as rs");
```

#### UpdateParameters

Update only the parameters, preserving the code.

```csharp
public static void UpdateParameters(
    IScriptComponent scriptComp,
    List<ParameterSettings> newInputs,
    List<ParameterSettings> newOutputs)
```

---

### Parameter Management

#### AddInputParameter

Add a new input parameter to the script.

```csharp
public static void AddInputParameter(
    IScriptComponent scriptComp,
    string name,
    string typeHint = "object",
    string access = "item",
    string description = "",
    bool optional = true)
```

**Example**:

```csharp
ScriptModifier.AddInputParameter(
    scriptComp,
    name: "curves",
    typeHint: "Curve",
    access: "list",
    description: "Curves to process",
    optional: false);
```

#### AddOutputParameter

Add a new output parameter to the script.

```csharp
public static void AddOutputParameter(
    IScriptComponent scriptComp,
    string name,
    string typeHint = "object",
    string description = "")
```

**Example**:

```csharp
ScriptModifier.AddOutputParameter(
    scriptComp,
    name: "points",
    typeHint: "Point3d",
    description: "Extracted points");
```

#### RemoveInputParameter / RemoveOutputParameter

Remove parameters by index.

```csharp
public static void RemoveInputParameter(IScriptComponent scriptComp, int index)
public static void RemoveOutputParameter(IScriptComponent scriptComp, int index)
```

---

### Type Hints

Type hints provide IntelliSense and type checking in script components.

#### SetInputTypeHint / SetOutputTypeHint

Set the type hint for a parameter.

```csharp
public static void SetInputTypeHint(
    IScriptComponent scriptComp,
    int index,
    string typeHint)

public static void SetOutputTypeHint(
    IScriptComponent scriptComp,
    int index,
    string typeHint)
```

**Common Type Hints**:

- `Point3d`, `Vector3d`, `Plane`
- `Curve`, `Surface`, `Brep`, `Mesh`
- `Line`, `Circle`, `Arc`, `Rectangle3d`
- `int`, `double`, `bool`, `string`
- `Guid`

**Example**:

```csharp
ScriptModifier.SetInputTypeHint(scriptComp, 0, "Curve");
ScriptModifier.SetOutputTypeHint(scriptComp, 0, "Point3d");
```

---

### Access Types

#### SetInputAccess

Configure how a parameter handles data (item, list, or tree).

```csharp
public static void SetInputAccess(
    IScriptComponent scriptComp,
    int index,
    GH_ParamAccess access)
```

**Access Types**:

- `GH_ParamAccess.item` - Single item
- `GH_ParamAccess.list` - List of items
- `GH_ParamAccess.tree` - Data tree

**Example**:

```csharp
ScriptModifier.SetInputAccess(scriptComp, 0, GH_ParamAccess.list);
```

---

### Script Component State

#### SetShowStandardOutput

Control visibility of the standard output parameter ("out").

```csharp
public static void SetShowStandardOutput(IScriptComponent scriptComp, bool show)
```

**Example**:

```csharp
// Show the "out" parameter for debugging
ScriptModifier.SetShowStandardOutput(scriptComp, true);

// Hide it for cleaner outputs
ScriptModifier.SetShowStandardOutput(scriptComp, false);
```

#### SetPrincipalInput

Set which input parameter drives the component's iteration.

```csharp
public static void SetPrincipalInput(IScriptComponent scriptComp, int index)
```

**Example**:

```csharp
// Make the second input the principal parameter
ScriptModifier.SetPrincipalInput(scriptComp, 1);
```

---

### Parameter Properties

#### SetInputOptional

Mark an input parameter as optional or required.

```csharp
public static void SetInputOptional(
    IScriptComponent scriptComp,
    int index,
    bool optional)
```

**Example**:

```csharp
// Make first input required
ScriptModifier.SetInputOptional(scriptComp, 0, false);

// Make second input optional
ScriptModifier.SetInputOptional(scriptComp, 1, true);
```

#### SetInputDescription / SetOutputDescription

Set parameter descriptions (shown in tooltips).

```csharp
public static void SetInputDescription(
    IScriptComponent scriptComp,
    int index,
    string description)

public static void SetOutputDescription(
    IScriptComponent scriptComp,
    int index,
    string description)
```

#### RenameInput / RenameOutput

Rename parameters.

```csharp
public static void RenameInput(IScriptComponent scriptComp, int index, string newName)
public static void RenameOutput(IScriptComponent scriptComp, int index, string newName)
```

---

## Usage Patterns

### Complete Script Update (AI Tools)

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

### Incremental Parameter Addition

```csharp
// Start with basic script
ScriptModifier.UpdateCode(scriptComp, "# Python script");

// Add parameters as needed
ScriptModifier.AddInputParameter(
    scriptComp,
    "geometry",
    typeHint: "GeometryBase",
    access: "list");

ScriptModifier.AddInputParameter(
    scriptComp,
    "tolerance",
    typeHint: "double",
    access: "item",
    optional: true);

ScriptModifier.AddOutputParameter(
    scriptComp,
    "result",
    typeHint: "Brep");
```

### Type Hint Updates

```csharp
// Initially generic
ScriptModifier.SetInputTypeHint(scriptComp, 0, "object");

// Later specify type based on analysis
ScriptModifier.SetInputTypeHint(scriptComp, 0, "Curve");
```

---

## GhJSON Integration

ScriptModifier provides complete modification capabilities for all GhJSON script properties:

| GhJSON Property | ScriptModifier Method |
|-----------------|----------------------|
| `componentState.value` (code) | `UpdateCode()` |
| `inputSettings[].VariableName` | `RenameInput()` |
| `inputSettings[].TypeHint` | `SetInputTypeHint()` |
| `inputSettings[].Access` | `SetInputAccess()` |
| `inputSettings[].Description` | `SetInputDescription()` |
| `inputSettings[].Required` | `SetInputOptional()` |
| `inputSettings[].IsPrincipal` | `SetPrincipalInput()` |
| `componentState.ShowStandardOutput` | `SetShowStandardOutput()` |

---

## Thread Safety

All ScriptModifier methods must be called on the Rhino UI thread:

```csharp
RhinoApp.InvokeOnUiThread(() =>
{
    ScriptModifier.UpdateCode(scriptComp, newCode);
    scriptComp.ExpireSolution(true);
    Instances.RedrawCanvas();
});
```

---

## Best Practices

### 1. Always Refresh After Changes

```csharp
ScriptModifier.UpdateScript(scriptComp, code, inputs, outputs);
scriptComp.ExpireSolution(true); // Force recalculation
```

### 2. Validate Indices

```csharp
var inputCount = ((IGH_Component)scriptComp).Params.Input.Count;
if (index >= 0 && index < inputCount)
{
    ScriptModifier.SetInputTypeHint(scriptComp, index, "Curve");
}
```

### 3. Use Undo Events

```csharp
((IGH_Component)scriptComp).RecordUndoEvent("[SH] Update Script");
ScriptModifier.UpdateCode(scriptComp, newCode);
```

### 4. Combine with ParameterModifier

```csharp
// First set up script structure
ScriptModifier.AddInputParameter(scriptComp, "data", "object", "list");

// Then apply data settings
var param = ((IGH_Component)scriptComp).Params.Input[0];
ParameterModifier.SetDataMapping(param, GH_DataMapping.Flatten);
ParameterModifier.SetSimplify(param, true);
```

---

## Related

- [ParameterModifier](./parameter-modifier.md) - For parameter data settings
- [GhJSON Script Components](../../GhJSON/script-components.md)
- [AI Tools](../AITools/index.md)
