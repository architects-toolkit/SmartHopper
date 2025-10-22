# Script Component Serialization Fix

## Issues Summary

Script components (Python 3, C#, VB, etc.) had multiple critical issues after `gh_get` → `gh_put` round-trip:

1. **Parameters lost completely** - All input/output parameters disappeared
2. **Access modes lost** - Tree/List access reverted to Item access
3. **Type hints lost** - Strongly-typed parameters (DataTree, List<Curve>, etc.) became generic `object`
4. **Marsh settings changed** - marshInputs/marshOutputs flags not preserved
5. **C# compilation errors** - Missing type information caused syntax errors

## Root Cause

**Critical misunderstanding of `VariableParameterMaintenance()` behavior:**

`VariableParameterMaintenance()` does **NOT** parse the script code to create parameters. Instead, it **regenerates the script code** based on already-registered parameters. 

### Previous (Broken) Logic:
1. Clear all existing parameters
2. Set script code with parameter declarations (e.g., `def RunScript(x2, y, z)`)
3. Call `VariableParameterMaintenance()` expecting it to create parameters from script
4. **Result**: Parameters stripped from script because no parameters were registered

### Correct Logic (like `script_edit.cs`):

1. Clear all existing parameters
2. **Manually create and register parameters** from `inputSettings`/`outputSettings`
3. Set script code
4. Call `VariableParameterMaintenance()` to sync script with registered parameters
5. **Result**: Parameters preserved in script

## Solution Implemented

Modified `GhJsonPlacer.cs` script component handling:

```csharp
// CRITICAL: Create parameters BEFORE setting script code
// VariableParameterMaintenance() regenerates script code based on registered parameters,
// so we must register parameters first to prevent parameter declarations from being stripped

// Create input parameters from inputSettings
if (component.InputSettings != null)
{
    foreach (var inputSetting in component.InputSettings)
    {
        var variableName = inputSetting.VariableName ?? inputSetting.ParameterName ?? "input";
        var param = new RhinoCodePluginGH.Parameters.ScriptVariableParam(variableName)
        {
            Name = variableName,
            NickName = variableName,
            Description = string.Empty,
            Access = GH_ParamAccess.item
        };
        param.CreateAttributes();
        ghComp.Params.RegisterInputParam(param);
    }
}

// Create output parameters from outputSettings
if (component.OutputSettings != null)
{
    foreach (var outputSetting in component.OutputSettings)
    {
        var variableName = outputSetting.VariableName ?? outputSetting.ParameterName ?? "output";
        var param = new RhinoCodePluginGH.Parameters.ScriptVariableParam(variableName)
        {
            Name = variableName,
            NickName = variableName,
            Description = string.Empty,
            Access = GH_ParamAccess.item
        };
        param.CreateAttributes();
        ghComp.Params.RegisterOutputParam(param);
    }
}

// NOW set script code - VariableParameterMaintenance will maintain parameter declarations
scriptComp.Text = scriptCode;
((dynamic)scriptComp).VariableParameterMaintenance();
```

## Additional Fixes

### 1. VariableName Extraction for All Script Parameters

**Problem**: Output parameters of script components that are NOT `ScriptVariableParam` (e.g., `Param_String`) were not having their `variableName` extracted during `gh_get`.

**Root Cause**: The extraction logic only checked parameter type, not whether the parameter belonged to a script component.

**Solution**: Simplified approach - check if the owning component is an `IScriptComponent`. If yes, extract `variableName` from `NickName` for ALL parameters, regardless of their type.

**Implementation**:

```csharp
// Extract variable name and type hint for script component parameters
if (component is IScriptComponent)
{
    settings.VariableName = param.NickName;
    // Also extract TypeHint via reflection if available
}
```

**Files Modified**:
- `DocumentIntrospectionV2.cs`: Pass component to `CreateParameterSettings()` and check `component is IScriptComponent`

### 2. Access Mode and Type Hints

Added `Access` and `TypeHint` properties to `ParameterSettings` model to preserve parameter metadata:

- **Extraction** (`DocumentIntrospectionV2.cs`): Captures access mode (item/list/tree) and type hints via reflection
- **Deserialization** (`GhJsonPlacer.cs`): Applies access mode and type hints when creating parameters

### 3. Marsh Settings

Apply `marshInputs` and `marshOutputs` directly in inline script component handling to preserve data marshalling preferences.

### 4. Skip Duplicate Processing

Added check in `ApplySchemaProperties()` to skip script components since they're handled inline.

### 5. Enhanced Debugging

Added DEBUG logging to show created parameters with Name/NickName properties and access modes.

### 6. Fallback Parameter Lookup

Search by both `Name` and `NickName` when applying parameter settings for robustness.

## Files Modified

- `src/SmartHopper.Core/Models/Components/ParameterSettings.cs` - Added Access and TypeHint properties
- `src/SmartHopper.Core.Grasshopper/Utils/Serialization/DocumentIntrospectionV2.cs` - Extract access mode and type hints
- `src/SmartHopper.Core.Grasshopper/Utils/Internal/GhJsonPlacer.cs` - Apply all metadata during deserialization

## Testing Results

After all fixes, script components correctly preserve:

- ✅ Input/output parameters (names, counts)
- ✅ Access modes (item/list/tree)
- ✅ Type hints (DataTree, List<Curve>, Interval, etc.)
- ✅ Marsh settings (marshInputs/marshOutputs)
- ✅ Script code with correct parameter declarations
- ✅ Parameter settings (Reverse, Simplify, DataMapping, etc.)
- ✅ Hidden state

## Examples

### Python 3 Script

**Original JSON (after gh_get)**:

```json
{
  "inputSettings": [
    {"parameterName": "x2", "variableName": "x2", "access": "tree", "typeHint": "DataTree"}
  ],
  "componentState": {
    "marshInputs": true,
    "marshOutputs": true,
    "hidden": true
  }
}
```

**After gh_put → gh_get**: All properties preserved correctly.

### C# Script

**Original**: `DataTree<object> x2, List<Curve> y, Interval z`  
**After Fix**: Type hints correctly applied, no compilation errors.

## Related Code

Similar pattern used in `src/SmartHopper.Core.Grasshopper/AITools/script_edit.cs` (lines 320-355).

Both now follow the same pattern: **register parameters → set code → call VariableParameterMaintenance()**.
