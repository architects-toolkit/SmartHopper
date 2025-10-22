# GhJSON Property Management System

## Overview

SmartHopper uses an advanced property management system for component serialization. The system provides flexible, context-aware filtering to optimize GhJSON output for different use cases:

- **Reduce payload size**: Exclude irrelevant or redundant data based on context
- **Improve AI comprehension**: Focus on actionable properties with AI-optimized contexts
- **Ensure consistency**: Standardized serialization across components with clear rules
- **Prevent errors**: Avoid circular references and non-serializable data
- **Easy maintenance**: Centralized configuration with component categories

## Property Management Architecture

The new system uses a **three-layer approach**:

1. **Property Filtering** (`PropertyFilter`) - Determines which properties to include/exclude
2. **Property Handling** (`PropertyHandler`) - Manages extraction and application of specific property types  
3. **Property Management** (`PropertyManagerV2`) - Orchestrates the entire process

### Serialization Contexts

Different contexts optimize property selection for specific use cases:

| Context | Purpose | Properties Included | Size Reduction |
|---------|---------|-------------------|----------------|
| `AIOptimized` | Clean structure for AI processing | Core + Parameters + Essential UI | ~60% |
| `FullSerialization` | Maximum fidelity preservation | All properties except runtime-only | ~10% |
| `CompactSerialization` | Minimal data for storage efficiency | Core properties only | ~80% |
| `ParametersOnly` | Parameter-focused extraction | Core + Parameter properties | ~70% |

## Current Schema Structure

The modern GhJSON format organizes properties into distinct sections:

### Component-Level Structure

```json
{
  "name": "Addition",
  "componentGuid": "a0d62394-a118-422d-abb3-6af115c75b25",
  "instanceGuid": "f8e7d6c5-b4a3-9281-7065-43e1f2a9b8c7",
  "id": 1,
  "pivot": "100.0,200.0",
  "params": {...},               // Simple key-value properties (NickName, etc.)
  "inputSettings": [...],        // Input parameter configuration
  "outputSettings": [...],       // Output parameter configuration
  "componentState": {...},       // UI state and universal value
  "selected": false,
  "warnings": [],
  "errors": []
}
```

**Key Features:**
- `pivot` is a compact string `"X,Y"` format
- `id` field is auto-generated for connections and group references
- No `type` or `objectType` fields (redundant with `componentGuid`)
- No `properties` dictionary (removed - use `componentState.value` instead)
- All component values stored in `componentState.value`

### Parameter Settings Structure

```json
"inputSettings": [{
  "parameterName": "A",
  "dataMapping": "None",
  "expression": "x * 2",
  "variableName": "myVar",
  "isReparameterized": false,
  "additionalSettings": {
    "reverse": false,
    "simplify": false,
    "invert": false,
    "isPrincipal": true,
    "locked": false
  }
}]
```

**Note**: `expression` field replaces the redundant `hasExpression` flag. If `expression` is present and non-null, the parameter has an expression.

### Component State Structure

```json
"componentState": {
  "locked": false,
  "hidden": false,
  "script": "print('hello')",
  "marshInputs": true,
  "marshOutputs": false,
  "listItems": [...],
  "listMode": "DropDown",
  "font": {...},
  "corners": [...]
}
```

**Universal Value Property (✅ IMPLEMENTED):**

All components store their primary value in `componentState.value`:

**Number Slider:**
```json
"componentState": {
  "value": "5.0<0.0,10.0>"  // Format: "value<min,max>"
}
```

**Panel / Scribble:**
```json
"componentState": {
  "value": "Hello World"  // Plain text
}
```

**Script Components:**
```json
"componentState": {
  "value": "import math\nprint(x)",  // Script code
  "marshInputs": true,
  "marshOutputs": false
}
```

**Value List:**
```json
"componentState": {
  "value": [{"Name": "A", "Expression": "0"}],  // Array of items
  "listMode": "DropDown"
}
```

This unified approach provides:
- ✅ Consistent API for all component types
- ✅ Simpler for AI to understand and generate
- ✅ Single source of truth for component values
- ✅ No component-specific property names needed

---

## Whitelisted Properties

> **Important**: The `properties` dictionary has been **removed**. All component values are now stored in `componentState.value`.

### General Component Properties

| Property | Type | Location | Description | Components |
|----------|------|----------|-------------|------------|
| `Locked` | Boolean | `componentState` | Whether component is locked (disabled) | All components |
| `NickName` | String | `params` | Custom nickname for the component | All |
| `DisplayName` | String | `params` | Display name shown on canvas | All |

### Parameter Properties

| Property | Type | Description | Components |
|----------|------|-------------|------------|
| `Simplify` | Boolean | Simplify data structure | Parameters (in additionalSettings) |
| `Reverse` | Boolean | Reverse data order | Parameters (in additionalSettings) |
| `Invert` | Boolean | Invert boolean/numeric values | Parameters (in additionalSettings) |
| `IsPrincipal` | Boolean | Parameter matching behavior | Parameters (in additionalSettings only) |
| `Locked` | Boolean | Parameter locked state | Parameters (in additionalSettings) |
| `DataMapping` | String | Data mapping mode (None/Flatten/Graft) | Parameters |
| `Expression` | String | Parameter expression | Parameters |
| `VariableName` | String | Script parameter variable name | Script parameters |
| `PersistentData` | Object | Internalized data in parameters | Parameters |

### Number Slider Properties

| Property | Type | Description |
|----------|------|-------------|
| `CurrentValue` | String | Current slider value in format `value<min,max>` |
| `Minimum` | Number | Minimum slider value |
| `Maximum` | Number | Maximum slider value |
| `Range` | Object | Value range |
| `Decimals` | Integer | Number of decimal places |
| `Limit` | Object | Limit configuration |
| `DisplayFormat` | String | Display format string |

### Multidimensional Slider Properties

| Property | Type | Description |
|----------|------|-------------|
| `SliderMode` | String | Slider mode configuration |
| `XInterval` | Object | X-axis interval |
| `YInterval` | Object | Y-axis interval |
| `ZInterval` | Object | Z-axis interval |
| `X` | Number | Current X value |
| `Y` | Number | Current Y value |
| `Z` | Number | Current Z value |

### Script Component Properties

| Property | Type | Description |
|----------|------|-------------|
| `Script` | String | Script code content (in componentState) |
| `MarshInputs` | Boolean | Marshal input values (in componentState) |
| `MarshOutputs` | Boolean | Marshal output values (in componentState) |

### Panel Properties

| Property | Type | Description |
|----------|------|-------------|
| `UserText` | String | Text content in panel |
| `Font` | Object | Font configuration (in componentState) |
| `Alignment` | String | Text alignment (in componentState) |

### Scribble Properties

| Property | Type | Description |
|----------|------|-------------|
| `Text` | String | Scribble text content |
| `Font` | Object | Font configuration (in componentState) |
| `Corners` | Array | Corner points (in componentState) |

### Value List Properties

| Property | Type | Description |
|----------|------|-------------|
| `ListMode` | String | List selection mode (in componentState) |
| `ListItems` | Array | List of selectable items (in componentState) |

### Component State Properties

| Property | Type | Description |
|----------|------|-------------|
| `Locked` | Boolean | Component locked state |
| `Hidden` | Boolean | Preview visibility state |
| `Value` | Various | Universal value property |
| `CurrentValue` | String | Current value (sliders, etc.) |
| `Multiline` | Boolean | Multiline mode enabled |
| `Wrap` | Boolean | Text wrapping enabled |
| `Color` | Object | Component color (RGBA) |

### Geometry Pipeline Properties

| Property | Type | Description |
|----------|------|-------------|
| `LayerFilter` | String | Layer filter pattern |
| `NameFilter` | String | Name filter pattern |
| `TypeFilter` | String | Type filter pattern |
| `IncludeLocked` | Boolean | Include locked objects |
| `IncludeHidden` | Boolean | Include hidden objects |
| `GroupByLayer` | Boolean | Group by layer |
| `GroupByType` | Boolean | Group by type |

### Graph Mapper Properties

| Property | Type | Description |
|----------|------|-------------|
| `GraphType` | String | Type of graph curve |

### Path Mapper Properties

| Property | Type | Description |
|----------|------|-------------|
| `Lexers` | Array | Path mapping lexers |

### Color Wheel Properties

| Property | Type | Description |
|----------|------|-------------|
| `State` | Object | Color wheel state |

### Data Recorder Properties

| Property | Type | Description |
|----------|------|-------------|
| `DataLimit` | Integer | Maximum data records |
| `RecordData` | Boolean | Whether recording is active |

### Item Picker Properties

| Property | Type | Description |
|----------|------|-------------|
| `TreePath` | String | Selected tree path |
| `TreeIndex` | Integer | Selected tree index |

### Button Properties

| Property | Type | Description |
|----------|------|-------------|
| `ExpressionNormal` | String | Expression when not pressed |
| `ExpressionPressed` | String | Expression when pressed |

### Control Knob Properties

| Property | Type | Description |
|----------|------|-------------|
| `Value` | Number | Current knob value |

---

## Component Categories

Properties are organized by component categories for easy management:

| Category | Components | Key Properties |
|----------|------------|----------------|
| `Panel` | GH_Panel | `UserText`, `Font`, `Alignment` |
| `Scribble` | GH_Scribble | `Text`, `Font`, `Corners` |
| `Slider` | GH_NumberSlider | `CurrentValue`, `Minimum`, `Maximum`, `Range` |
| `MultidimensionalSlider` | GH_MultiDimensionalSlider | `SliderMode`, `XInterval`, `YInterval`, `ZInterval` |
| `ValueList` | GH_ValueList | `ListMode`, `ListItems` |
| `Script` | IScriptComponent | `Script`, `MarshInputs`, `MarshOutputs` |
| `GeometryPipeline` | GH_GeometryPipeline | `LayerFilter`, `NameFilter`, `TypeFilter` |
| `Essential` | Panel + Scribble + Slider + ValueList + Script | Combined essential components |
| `UI` | Panel + Scribble + Button + ColorWheel | UI-focused components |

### Category Usage

```csharp
// Include only essential component categories
var essentialManager = PropertyManagerFactory.CreateWithCategories(ComponentCategory.Essential);

// Include UI components only
var uiManager = PropertyManagerFactory.CreateWithCategories(ComponentCategory.UI);

// Custom combination
var customManager = PropertyFilterBuilder.Create()
    .WithCategories(ComponentCategory.Slider | ComponentCategory.Script)
    .BuildManager();
```

## Omitted Properties

The following properties are globally blacklisted from all serialization contexts:

| Property | Reason |
|----------|--------|
| `VolatileData` | Runtime-only, not persistent |
| `IsValid` | Runtime validation state |
| `IsValidWhyNot` | Runtime validation messages |
| `TypeDescription` | Redundant with component type |
| `TypeName` | Redundant with component type |
| `Boundingbox` | Runtime geometry bounds |
| `ClippingBox` | Runtime geometry bounds |
| `ReferenceID` | Internal framework property |
| `IsReferencedGeometry` | Runtime geometry state |
| `IsGeometryLoaded` | Runtime geometry state |
| `QC_Type` | Internal quality control type |
| `humanReadable` | Redundant metadata |
| `Properties` | Legacy properties dictionary (replaced by structured schema) |

---

## Special Handling

### PersistentData

Internalized parameter data is serialized as a nested structure:

```json
"PersistentData": {
  "value": {
    "{0}": {
      "0": {"value": 42},
      "1": {"value": 43}
    }
  },
  "type": "JObject"
}
```

### Number Slider CurrentValue

Slider values are formatted as `value<min,max>`:

```json
"CurrentValue": {
  "value": "5.0<0.0,10.0>",
  "type": "String",
  "humanReadable": "5.0"
}
```

### Script Inputs/Outputs

Script parameters are serialized as structured arrays:

```json
"ScriptInputs": [
  {
    "variableName": "x",
    "name": "X Input",
    "description": "X coordinate value",
    "access": "item",
    "simplify": false,
    "reverse": false,
    "dataMapping": "None"
  }
]
```

---

## Implementation

The new property management system is implemented across multiple components:

### PropertyFilterConfig.cs

Central configuration defining property rules:

```csharp
// Global blacklist - never serialize these
public static readonly HashSet<string> GlobalBlacklist = new()
{
    "VolatileData", "IsValid", "TypeDescription", "Boundingbox", // ...
};

// Core properties - essential for all objects  
public static readonly HashSet<string> CoreProperties = new()
{
    "NickName", "Locked", "PersistentData"
};

// Category-specific properties
public static readonly Dictionary<ComponentCategory, HashSet<string>> CategoryProperties = new()
{
    [ComponentCategory.Panel] = new() { "UserText", "Font", "Alignment" },
    [ComponentCategory.Slider] = new() { "CurrentValue", "Minimum", "Maximum" },
    // ...
};
```

### PropertyManagerV2.cs

Main orchestrator providing high-level API:

```csharp
// Use predefined contexts
var aiManager = PropertyManagerFactory.CreateForAI();
var properties = aiManager.ExtractProperties(grasshopperObject);

// Custom configuration
var customManager = PropertyFilterBuilder.Create()
    .WithCore(true)
    .WithCategories(ComponentCategory.Essential)
    .Include("CustomProperty")
    .BuildManager();
```

### Migration from Old System

The old `PropertyManager` is deprecated but maintained for compatibility:

```csharp
// OLD WAY (deprecated)
var isAllowed = PropertyManager.IsPropertyInWhitelist("CurrentValue");

// NEW WAY
var manager = PropertyManagerFactory.CreateForAI();
var isAllowed = manager.ShouldIncludeProperty("CurrentValue", grasshopperObject);
```

---

## Related Documentation

- [GhJSON Format Specification](./format-specification.md)
- [GhJSON Roadmap](./roadmap.md)
