# Property Management (V2)

This page documents the modern Property Management system used by GhJSON and where properties live in the current schema.

- Params: simple key-value pairs on `component.properties` → now `component.params`
- Parameter settings: `component.inputSettings[]` and `component.outputSettings[]`
- Component UI/state: `component.componentState{ ... }`
- Universal value: `component.componentState.value` for sliders, panels, scribbles, value lists, scripts

Legacy `properties` dictionary is deprecated. See notes in the format-specification for legacy reference only.

---

## Complete Property Reference

#### Parameter/Input/Output Properties

| Property | Full Format | Lite Format | Data Type | Values/Examples | Purpose | Status | Notes |
|----------|-------------|-------------|-----------|-----------------|---------|--------|-------|
| `parameterName`  | ✅ | ✅ | string | Parameter name | Identifies the parameter | ✅ Implemented | |
| `dataMapping`  | ✅ | ✅ | string | `"None"`, `"Flatten"`, `"Graft"` | Tree structure manipulation | ✅ Implemented | |
| `simplify`  | ✅ | ✅ | boolean | `true`, `false` | Simplifies data tree paths | ✅ Implemented | |
| `reverse`  | ✅ | ✅ | boolean | `true`, `false` | Reverses list order | ✅ Implemented | |
| `invert`  | ✅ | ✅ | boolean | `true`, `false` | Inverts boolean/numeric values | ✅ Implemented | |
| `expression`  | ✅ | ✅ | string | `"x * 2"`, `"Math.Sin(x)"` | Parameter expression | ✅ Implemented | |
| `persistentData`  | ✅ | ✅ | object | Data tree structure | Internalized parameter data | ✅ Implemented | |
| `isPrincipal`  | ✅ | ✅ | boolean | `true`, `false` | Parameter matching behavior | ✅ Implemented | Only in `additionalSettings` |
| `expressionContent`  | ❌ | ❌ | string | Expression code | Separate expression storage | 🗑️ **ToRemove** | Redundant with `expression` |
| `variableName`  | ✅ | ✅ | string | Variable name | Script parameter variable | ✅ Implemented | Script components only |
| **Properties to Remove** |
| `dataType`  | ❌ | ❌ | string | `"remote"`, `"void"`, `"local"` | Redundant (inferred) | 🗑️ **ToRemove** | Inferred from connections/persistentData |
| `volatileData`  | ❌ | ❌ | object | Runtime data | Runtime-only | 🗑️ **ToRemove** | Not persistent |
| **Properties Excluded** |
| `access`  | ❌ | ❌ | string | `"item"`, `"list"`, `"tree"` | Implicit from component type | ❌ Excluded | |
| `description`  | ❌ | ❌ | string | Text | Implicit from component definition | ❌ Excluded | |
| `optional`  | ❌ | ❌ | boolean | `true`, `false` | Redundant information | ❌ Excluded | |
| `isReparameterized`  | ✅ | ✅ | boolean | `true`, `false` | Domain reparameterization | 🔨 **TODO** | Model exists, extraction/application not implemented |

#### Component Properties

| Property | Full Format | Lite Format | Data Type | Values/Examples | Purpose | Status | Notes |
|----------|-------------|-------------|-----------|-----------------|---------|--------|-------|
| **General Component Properties** |
| `nickName`  | ✅ | ❌ | string | Custom name | Component nickname | ✅ Implemented | |
| `displayName`  | ✅ | ❌ | string | Display name | Component display name | ✅ Implemented | |
| `locked`  | ✅ | ✅ | boolean | `true`, `false` | Parameter/component locked state | ✅ Implemented | In `additionalSettings` for parameters, `componentState` for components |
| `hidden`  | ✅ | ✅ | boolean | `true`, `false` | Preview visibility state | ✅ Implemented | |
| `value`  | ✅ | ✅ | various | Component value | **Universal value property** | 💡 **Consolidate** | See mapping table below |
| `humanReadable`  | ❌ | ❌ | string | Human-readable value | Debug/display helper | 🗑️ **ToRemove** | Not necessary if `value` is properly serialized |
| **Number Slider** |
| `currentValue`  | ✅ | ✅ | string | `"5.0<0.0,10.0>"` | Slider value with range | 🗑️ **ToRemove** | Maps to `value` |
| `minimum`  | ❌ | ❌ | number | Min value | Slider minimum | 🗑️ **ToRemove** | Redundant (in currentValue) |
| `maximum`  | ❌ | ❌ | number | Max value | Slider maximum | 🗑️ **ToRemove** | Redundant (in currentValue) |
| `decimals`  | ❌ | ❌ | integer | Decimal places | Slider precision | 🗑️ **ToRemove** | Redundant (in currentValue) |
| `range`  | ❌ | ❌ | object | Range config | Slider range | 🗑️ **ToRemove** | Redundant (in currentValue) |
| `limit`  | ❌ | ❌ | object | Limit config | Slider limits | 🗑️ **ToRemove** | Redundant (in currentValue) |
| `displayFormat`  | ❌ | ❌ | string | Format string | Display format | 🗑️ **ToRemove** | Redundant (in currentValue) |
| **Panel** |
| `userText`  | ✅ | ✅ | string | Panel text | Panel content | 🗑️ **ToRemove** | Maps to `value` |
| `properties`  | ✅ | ❌ | object | Nested properties | Panel properties | ✅ Implemented | UI formatting |
| **Scribble** |
| `text`  | ✅ | ✅ | string | Scribble text | Scribble content | 🗑️ **ToRemove** | Maps to `value` |
| `font`  | ✅ | ❌ | object | Font config | Font settings | ✅ Implemented | UI formatting |
| `corners`  | ✅ | ❌ | array | Corner points | Scribble bounds | ✅ Implemented | UI positioning |
| **Value List** |
| `listMode`  | ✅ | ✅ | string | Selection mode | List mode | ✅ Implemented | |
| `listItems`  | ✅ | ✅ | array | List items | Selectable items | 🗑️ **ToRemove** | Maps to `value` |
| **Multidimensional Slider** |
| `sliderMode`  | ✅ | ❌ | string | Slider mode | Mode config | ✅ Implemented | |
| `xInterval`  | ❌ | ❌ | object | X interval | X-axis range | 🗑️ **ToRemove** | Redundant (in value) |
| `yInterval`  | ❌ | ❌ | object | Y interval | Y-axis range | 🗑️ **ToRemove** | Redundant (in value) |
| `zInterval`  | ❌ | ❌ | object | Z interval | Z-axis range | 🗑️ **ToRemove** | Redundant (in value) |
| `x`  | ❌ | ❌ | number | X value | Current X | 🗑️ **ToRemove** | Maps to `value` (consolidate) |
| `y`  | ❌ | ❌ | number | Y value | Current Y | 🗑️ **ToRemove** | Maps to `value` (consolidate) |
| `z`  | ❌ | ❌ | number | Z value | Current Z | 🗑️ **ToRemove** | Maps to `value` (consolidate) |
| **Script Component** |
| `script`  | ✅ | ✅ | string | Script code | Script content | 🗑️ **ToRemove** | Maps to `value` |
| **Geometry Pipeline** |
| `layerFilter`  | ✅ | ❌ | string | Layer filter | Filter pattern | ✅ Implemented | |
| `nameFilter`  | ✅ | ❌ | string | Name filter | Filter pattern | ✅ Implemented | |
| `typeFilter`  | ✅ | ❌ | string | Type filter | Filter pattern | ✅ Implemented | |
| `includeLocked`  | ✅ | ❌ | boolean | Include locked | Filter option | ✅ Implemented | |
| `includeHidden`  | ✅ | ❌ | boolean | Include hidden | Filter option | ✅ Implemented | |
| `groupByLayer`  | ✅ | ❌ | boolean | Group by layer | Grouping option | ✅ Implemented | |
| `groupByType`  | ✅ | ❌ | boolean | Group by type | Grouping option | ✅ Implemented | |
| **Other Components** |
| `graphType`  | ✅ | ❌ | string | Graph type | Graph Mapper type | ✅ Implemented | |
| `lexers`  | ✅ | ❌ | array | Path lexers | Path Mapper lexers | ✅ Implemented | |
| `state`  | ✅ | ❌ | object | Color state | Color Wheel state | ✅ Implemented | |
| `dataLimit`  | ✅ | ❌ | integer | Data limit | Data Recorder limit | ✅ Implemented | |
| `recordData`  | ✅ | ❌ | boolean | Recording state | Data Recorder active | ✅ Implemented | |
| `treePath`  | ✅ | ❌ | string | Tree path | Item Picker path | ✅ Implemented | |
| `treeIndex`  | ✅ | ❌ | integer | Tree index | Item Picker index | ✅ Implemented | |
| `expressionNormal`  | ✅ | ❌ | string | Normal expression | Button normal state | ✅ Implemented | |
| `expressionPressed`  | ✅ | ❌ | string | Pressed expression | Button pressed state | ✅ Implemented | |

---

## Core Data Types

| Type | Format | Example | Notes |
|------|--------|---------|-------|
| **Text** | `value` | `"text:Hello World"` | String values |
| **Number** | `value` | `"number:3.14159"` | Double precision floating-point |
| **Integer** | `value` | `"integer:42"` | 32-bit signed integer |
| **Boolean** | `true/false` | `"boolean:true"` | Boolean values (lowercase) |
| **Color** | `a,r,g,b` | `"argb:255,128,64,255"` | ARGB values 0-255 |
| **Point** | `x,y,z` | `"pointXYZ:10.5,20.0,30.5"` | 3D coordinates |
| **Vector** | `x,y,z` | `"vectorXYZ:1.0,0.0,0.0"` | 3D direction vector |
| **Line** | `x1,y1,z1;x2,y2,z2` | `"line2p:0,0,0;10,10,10"` | Start and end points |
| **Plane** | `ox,oy,oz;xx,xy,xz;yx,yy,yz` | `"planeOXY:0,0,0;1,0,0;0,1,0"` | Origin + X/Y axes |
| **Circle** | `cx,cy,cz;nx,ny,nz;r;sx,sy,sz` | `"circleCNRS:0,0,0;0,0,1;5.0;5,0,0"` | Center + normal + radius + start point |
| **Arc** | `x1,y1,z1;x2,y2,z2;x3,y3,z3` | `"arc3P:0,0,0;5,5,0;10,0,0"` | Three points defining the arc |
| **Box** | `ox,oy,oz;xx,xy,xz;yx,yy,yz;x0,x1;y0,y1;z0,z1` | `"boxOXY:0,0,0;1,0,0;0,1,0;-5,5;-5,5;0,10"` | Origin + X-axis + Y-axis + 3 intervals |
| **Interval (Domain)** | `min,max` | `"interval:0.0<10.0"` | Domain/range/interval |
| **Rectangle** | `cx,cy,cz;xx,xy,xz;yx,yy,yz;w,h` | `"rectangleCXY:0,0,0;1,0,0;0,1,0;10,5"` | Center + X-axis + Y-axis + dimensions |

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

---

## Notes

- Prefer `SerializationContext` presets and `PropertyManagerV2` to control what gets serialized.
- Legacy `properties` section is retained in docs for historical context only; generation should use `params`, `inputSettings`, `outputSettings`, and `componentState`.
