# GhJSON Property Whitelist

## Overview

SmartHopper uses a whitelist approach for component property serialization. Only explicitly whitelisted properties are included in GhJSON output to:

- **Reduce payload size**: Exclude irrelevant or redundant data
- **Improve AI comprehension**: Focus on actionable properties
- **Ensure consistency**: Standardize serialization across components
- **Prevent errors**: Avoid circular references and non-serializable data

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

## Omitted Properties

The following properties are explicitly omitted from serialization:

| Property | Reason |
|----------|--------|
| `VolatileData` | Runtime-only, not persistent |
| `DataType` | Redundant with type information |
| `Properties` | Circular reference risk |
| `Params` | Circular reference (handled separately) |

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

The whitelist is defined in `PropertyManager.cs`:

```csharp
private static Dictionary<string, List<string>> PropertiesWhitelist { get; } = new Dictionary<string, List<string>>
{
    { "Value", null },
    { "Locked", null },
    { "Simplify", null },
    // ... additional properties
};
```

Properties with `null` values are leaf properties. Properties with list values support nested property access.

---

## Related Documentation

- [GhJSON Format Specification](./format-specification.md)
- [GhJSON Roadmap](./roadmap.md)
