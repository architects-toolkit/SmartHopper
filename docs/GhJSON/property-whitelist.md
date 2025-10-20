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

## Whitelisted Properties

### General Component Properties

| Property | Type | Description | Components |
|----------|------|-------------|------------|
| `Locked` | Boolean | Whether component is locked (disabled) | All components |
| `NickName` | String | Custom nickname for the component | All |
| `DisplayName` | String | Display name shown on canvas | All |

### Parameter Properties

| Property | Type | Description | Components |
|----------|------|-------------|------------|
| `Simplify` | Boolean | Simplify data structure | Parameters |
| `Reverse` | Boolean | Reverse data order | Parameters |
| `DataMapping` | String | Data mapping mode (None/Flatten/Graft) | Parameters |
| `DataType` | String | Data type (remote/void/local) | Parameters |
| `PersistentData` | Object | Internalized data in parameters | Parameters |
| `VolatileData` | Object | Current runtime data | Parameters |

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
| `Script` | String | Script code content |
| `ScriptInputs` | Array | Input parameter definitions |
| `ScriptOutputs` | Array | Output parameter definitions |
| `MarshInputs` | Boolean | Marshal input values |
| `MarshOutputs` | Boolean | Marshal output values |
| `MarshGuids` | Boolean | Marshal GUID values |

### Panel Properties

| Property | Type | Description |
|----------|------|-------------|
| `UserText` | String | Text content in panel |
| `Properties` | Object | Panel properties (nested) |

### Scribble Properties

| Property | Type | Description |
|----------|------|-------------|
| `Text` | String | Scribble text content |
| `Font` | Object | Font configuration |
| `Corners` | Array | Corner points |

### Value List Properties

| Property | Type | Description |
|----------|------|-------------|
| `ListMode` | String | List selection mode |
| `ListItems` | Array | List of selectable items |

### Expression Properties

| Property | Type | Description |
|----------|------|-------------|
| `Expression` | String | Mathematical expression |
| `Invert` | Boolean | Invert expression result |

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
