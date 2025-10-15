# GhJSON Format Roadmap

## Vision

Transform GhJSON into a robust, AI-optimized serialization format that balances completeness with efficiency, enabling advanced AI-powered Grasshopper workflows while maintaining reliability and consistency.

---

## Phase 1: Enhanced Schema and Metadata

**Goal**: Improve GhJSON schema structure with essential metadata, better component organization, and more comprehensive information capture.

### 1.1 Document-Level Metadata

**Current Gap**: No top-level metadata for document description, versioning, or authorship.

**Proposed Schema**:

```json
{
  "schemaVersion": "1",
  "metadata": {
    "description": "Parametric tower with adaptive facade",
    "version": "1",
    "created": "2025-01-15T10:30:00Z",
    "modified": "2025-01-15T14:45:00Z",
    "author": "SmartHopper AI",
    "rhinoVersion": "8.0",
    "grasshopperVersion": "1.0.0007",
    "dependencies": []
  },
  "components": [...],
  "connections": [...],
  "groups": [...]
}
```

**Implementation**:

- [x] **Description**: Top-level document description
- [x] **Version**: GhJSON format version for schema evolution
- [x] **Timestamps**: Creation and modification dates (ISO 8601)
- [x] **Author**: Creator information
- [x] **Environment**: Rhino/Grasshopper version information
- [x] **Dependencies**: Required plugins and versions

### 1.2 Groups Support (Full Format Only) ✅

**Status**: Implemented - Groups are always serialized.

**Implemented Schema**:

```json
{
  "groups": [
    {
      "id": 1,
      "name": "Input Parameters",
      "color": {"r": 255, "g": 200, "b": 100, "a": 255},
      "members": [1, 2, 3],
      "position": {"x": 100.0, "y": 100.0}
    }
  ]
}
```

**Implementation**:

- [ ] **Group Metadata**: Name, color, description
- [ ] **Member Tracking**: Component IDs belonging to group (uses integer `id` not GUIDs)
- [ ] **Nested Groups**: Support for group hierarchies
- [ ] **Position**: Group bounding box or anchor point

### 1.3 Component Schema Improvements

**Current Limitations**: Component properties are stored in a single unified dictionary, making it difficult to distinguish between input/output settings and component state.

**Proposed Changes**:

#### Input/Output Settings Separation

**Current**: Unified `properties` dictionary  
**Proposed**: Separate `inputSettings` and `outputSettings` arrays

```json
{
  "inputSettings": [
    {
      "parameterName": "A",
      "dataMapping": "None",
      "isReparameterized": false,
      "hasExpression": false,
      "expression": null,
      "additionalSettings": {
        "Optional": false,
        "Reverse": false,
        "Simplify": false,
        "Locked": false
      }
    }
  ],
  "outputSettings": [...]
}
```

#### Component State Object

**Current**: Mixed with properties  
**Proposed**: Dedicated `componentState` object for UI-specific data

```json
{
  "componentState": {
    "multiline": true,
    "wrap": true,
    "color": {"r": 255, "g": 255, "b": 255, "a": 255}
  }
}
```

#### Position Naming

**Current**: `pivot` with `X`/`Y` (capitalized)  
**Proposed**: `position` with `x`/`y` (lowercase) for consistency

**Implementation**:

- [ ] **Separate I/O Settings**: Split properties into input/output parameter arrays
- [ ] **Parameter Metadata**: Access mode, optional status, type hints, expressions
- [ ] **Component State**: Dedicated object for UI state (colors, multiline, etc.)
- [ ] **Position Standardization**: Use lowercase `x`/`y` coordinates
- [ ] **Params Object**: Simple key-value pairs for basic properties

### 1.4 Data Type Serialization

**Current Gap**: No standardized serialization for Grasshopper/Rhino data types (Color, Point3d, Vector3d, Line, Plane, Circle, Arc, BoundingBox, etc.).

**Problem**:

- Persistent data and component values contain complex types
- No consistent encoding/decoding strategy
- AI cannot easily interpret or generate these values
- Manual string parsing is error-prone

**Proposed Solution**: Unified data type serialization system with human-readable string encoding.

#### Core Data Types

| Type | Format | Example | Notes |
|------|--------|---------|-------|
| **Color** | `a,r,g,b` | `"argb:255,128,64,255"` | ARGB values 0-255 |
| **Point** | `x,y,z` | `"pointXYZ:10.5,20.0,30.5"` | 3D coordinates |
| **Vector** | `x,y,z` | `"vectorXYZ:1.0,0.0,0.0"` | 3D direction vector |
| **Line** | `x1,y1,z1;x2,y2,z2` | `"line2p:0,0,0;10,10,10"` | Start and end points |
| **Plane** | `ox,oy,oz;xx,xy,xz;yx,yy,yz` | `"planeOXY:0,0,0;1,0,0;0,1,0"` | Origin + X/Y axes |
| **Circle** | `cx,cy,cz;nx,ny,nz;r` | `"circleCNR:0,0,0;0,0,1;5.0"` | Center + normal + radius |
| **Arc** | `cx,cy,cz;nx,ny,nz;r;a1;a2` | `"arcCNRAB:0,0,0;0,0,1;5.0;0;1.57"` | Circle + start/end angles |
| **BoundingBox** | `x1,y1,z1;x2,y2,z2` | `"box2p:0,0,0;10,10,10"` | Min and max corners |
| **Domain** | `min,max` | `"domain:0.0<10.0"` | Domain/range |
| **Rectangle** | `cx,cy,cz;nx,ny,nz;w,h` | `"rectangleCNWH:0,0,0;0,0,1;10,5"` | Corner + normal + width/height |

#### Persistent Data Encoding

**Current**: Data trees stored as nested JSON with type information  
**Proposed**: Add type-aware serialization for leaf values

```json
{
  "persistentData": {
    "{0}": {
      "0": {
        "type": "Point3d",
        "value": "10.0,20.0,30.0"
      },
      "1": {
        "type": "Color",
        "value": "255,128,64,255"
      }
    }
  }
}
```

#### Implementation Requirements

- [ ] **DataTypeSerializer Class**: Centralized encoding/decoding for all types
- [ ] **Type Registry**: Map type names to serializers
- [ ] **Validation**: Ensure valid format on deserialization
- [ ] **Error Handling**: Graceful fallback for unknown types
- [ ] **Documentation**: Clear format specification for each type
- [ ] **AI Context**: Include type format reference in tool descriptions

#### Benefits

- **AI-Friendly**: LLMs can generate valid data type strings
- **Human-Readable**: Easy to inspect and debug
- **Consistent**: Single encoding strategy across all types
- **Extensible**: Easy to add new types
- **Compact**: More efficient than full JSON object representation

### 1.5 Advanced Component Support

**Current Gaps:**

- Limited support for special component types
- No support for custom components

**Additions:**

#### Cluster Support

- [ ] **Cluster Contents**: Serialize internal cluster structure
- [ ] **Cluster I/O**: Input/output parameter mapping
- [ ] **Cluster Metadata**: Version, author, description

#### Custom Components

- [ ] **Plugin Information**: Plugin name, version, GUID
- [ ] **Custom Properties**: Plugin-specific property serialization
- [ ] **Dependency Tracking**: Required plugins and versions

### 1.5 Full Format Example

**Complete example** showing all proposed Phase 1 enhancements:

> **Note**: This example demonstrates the **proposed** Phase 1 format. It includes features not yet in the current format specification:
> - `metadata` object (Phase 1.1)
> - `groups` array (Phase 1.2)
> - `position` with lowercase `x`/`y` instead of `pivot` with `X`/`Y` (Phase 1.3)
> - `params` object separate from `properties` (Phase 1.3)
> - `inputSettings`/`outputSettings` arrays (Phase 1.3)
> - `componentState` object (Phase 1.3)
> - Integer `id` field on components (Phase 1.2)

```json
{
  "metadata": {
    "description": "Parametric facade with adaptive panels based on sun analysis",
    "version": "1.0",
    "created": "2025-01-15T10:30:00Z",
    "modified": "2025-01-15T14:45:00Z",
    "author": "SmartHopper AI",
    "rhinoVersion": "8.0",
    "grasshopperVersion": "1.0.0007",
    "dependencies": ["Ladybug", "Pufferfish"]
  },
  "components": [
    {
      "id": 1,
      "name": "Number Slider",
      "type": "IGH_Param",
      "objectType": "Grasshopper.Kernel.Special.GH_NumberSlider",
      "componentGuid": "57da07bd-ecab-415d-9d86-af36d7073abc",
      "instanceGuid": "a1111111-1111-1111-1111-111111111111",
      "selected": true,
      "position": {"x": 100.0, "y": 100.0},
      "params": {
        "NickName": "Panel Count"
      },
      "inputSettings": [],
      "outputSettings": [
        {
          "parameterName": "Number Slider",
          "dataMapping": "None",
          "isReparameterized": false,
          "hasExpression": false,
          "expressionContent": null,
          "additionalSettings": {
            "Hidden": false,
            "Optional": false,
            "Reverse": false,
            "Simplify": false,
            "Locked": false
          }
        }
      ],
      "componentState": {
        "currentValue": "12.0<5.0,50.0>",
        "slider": {
          "min": 5.0,
          "max": 50.0,
          "value": 12.0
        }
      }
    },
    {
      "id": 2,
      "name": "Python Script",
      "type": "IGH_Component",
      "objectType": "RhinoCodePlatform.GH.Components.PythonScriptComponent",
      "componentGuid": "410755b1-224a-4c1e-a407-bf32fb45ea7e",
      "instanceGuid": "b2222222-2222-2222-2222-222222222222",
      "selected": false,
      "position": {"x": 300.0, "y": 100.0},
      "params": {},
      "inputSettings": [
        {
          "parameterName": "count",
          "dataMapping": "None",
          "isReparameterized": false,
          "hasExpression": false,
          "expressionContent": null,
          "additionalSettings": {
            "Optional": false,
            "Reverse": false,
            "Simplify": false,
            "Locked": false
          }
        }
      ],
      "outputSettings": [
        {
          "parameterName": "panels",
          "dataMapping": "None",
          "isReparameterized": false,
          "hasExpression": false,
          "expressionContent": null,
          "additionalSettings": {
            "Hidden": false,
            "Optional": false,
            "Reverse": false,
            "Simplify": false,
            "Locked": false
          }
        }
      ],
      "componentState": {
        "script": "import rhinoscriptsyntax as rs\\n\\npanels = []\\nfor i in range(int(count)):\\n    panels.append(create_panel(i))",
        "marshInputs": true,
        "marshOutputs": true
      },
      "warnings": [],
      "errors": []
    },
    {
      "id": 3,
      "name": "Panel",
      "type": "IGH_Param",
      "objectType": "Grasshopper.Kernel.Special.GH_Panel",
      "componentGuid": "59e0b89a-e487-49f8-bab8-b5bab16be14c",
      "instanceGuid": "c3333333-3333-3333-3333-333333333333",
      "selected": false,
      "position": {"x": 500.0, "y": 100.0},
      "params": {
        "UserText": "Output results"
      },
      "inputSettings": [],
      "outputSettings": [
        {
          "parameterName": "Panel",
          "dataMapping": "None",
          "isReparameterized": false,
          "hasExpression": false,
          "expressionContent": null,
          "additionalSettings": {
            "Hidden": false,
            "Optional": false,
            "Reverse": false,
            "Simplify": false,
            "Locked": false
          }
        }
      ],
      "componentState": {
        "multiline": true,
        "wrap": true,
        "color": {"r": 255, "g": 255, "b": 255, "a": 255}
      }
    }
  ],
  "connections": [
    {
      "from": {"id": 1, "output": "Number Slider"},
      "to": {"id": 2, "input": "count"}
    },
    {
      "from": {"id": 2, "output": "panels"},
      "to": {"id": 3, "input": "Panel"}
    }
  ],
  "groups": [
    {
      "id": 1,
      "name": "Input Parameters",
      "color": {"r": 150, "g": 200, "b": 255, "a": 100},
      "members": [1],
      "position": {"x": 80.0, "y": 80.0}
    },
    {
      "id": 2,
      "name": "Processing",
      "color": {"r": 255, "g": 200, "b": 150, "a": 100},
      "members": [2],
      "position": {"x": 280.0, "y": 80.0}
    }
  ]
}
```

**Key Features Demonstrated:**

- **Document metadata** (Phase 1.1): Description, versioning, dependencies
- **Component diversity**: Slider, script, panel with full configuration
- **Input/Output settings** (Phase 1.3): Detailed parameter configuration in separate arrays
- **Component state** (Phase 1.3): Type-specific data in dedicated `componentState` object
- **Groups** (Phase 1.2): Logical organization with colors and integer ID-based member references
- **Connections**: ID-based dataflow (uses integer `id` field from Phase 1.2)
- **Position naming** (Phase 1.3): Lowercase `x`/`y` instead of current `X`/`Y`
- **Params object** (Phase 1.3): Simple key-value pairs separate from `properties`
- **Selection state**: `selected` flag for AI context
- **Error tracking**: Empty arrays ready for runtime messages

---

## Phase 2: GhJSON-Lite Format

**Goal**: Create a lightweight variant optimizing for specific use cases.

### 2.1 Use Cases for GhJSON-Lite

**Target Scenarios:**

- Bulk component retrieval with `gh_get`
- Diff generation and comparison
- AI context windows (token optimization)

### 3.2 Lite Format Design

**Principles:**

- **Minimal**: Only essential information
- **Consistent**: Same schema, fewer properties
- **Reversible**: Can be expanded back to full GhJSON
- **Configurable**: Customizable reduction levels

**Reduction Strategies:**

#### Level 1: Structure-Only Format (AI-Readable)

**Purpose**: Lightweight format for AI consumption, analysis, and documentation. Maintains full readability without canvas-specific data.

**Example** (same definition as Full Format example in 1.5):

```json
{
  "metadata": {
    "description": "Parametric facade with adaptive panels based on sun analysis",
    "version": "1.0"
  },
  "components": [
    {
      "id": 1,
      "name": "Number Slider",
      "type": "Grasshopper.Kernel.Special.GH_NumberSlider",
      "position": {"x": 100.0, "y": 100.0},
      "params": {
        "NickName": "Panel Count",
        "value": "12.0<5.0,50.0>"
      },
      "outputs": [
        {
          "name": "Number Slider",
          "dataMapping": "Flatten",
          "hasExpression": true,
          "expressionContent": "x * 2"
        }
      ]
    },
    {
      "id": 2,
      "name": "Python Script",
      "type": "RhinoCodePlatform.GH.Components.PythonScriptComponent",
      "position": {"x": 300.0, "y": 100.0},
      "params": {
        "script": "import rhinoscriptsyntax as rs\\n\\npanels = []\\nfor i in range(int(count)):\\n    panels.append(create_panel(i))"
      },
      "inputs": [
        {
          "name": "count",
          "simplify": true
        }
      ],
      "outputs": [
        {
          "name": "panels",
          "dataMapping": "Graft"
        }
      ]
    },
    {
      "id": 3,
      "name": "Panel",
      "type": "Grasshopper.Kernel.Special.GH_Panel",
      "position": {"x": 500.0, "y": 100.0},
      "params": {
        "UserText": "Output results"
      }
    }
  ],
  "connections": [
    {
      "from": {"id": 1, "output": "Number Slider"},
      "to": {"id": 2, "input": "count"}
    },
    {
      "from": {"id": 2, "output": "panels"},
      "to": {"id": 3, "input": "Panel"}
    }
  ],
  "groups": [
    {
      "id": 1,
      "name": "Input Parameters",
      "members": [1]
    },
    {
      "id": 2,
      "name": "Processing",
      "members": [2]
    }
  ]
}
```

**Key Differences from Full Format:**

- **ID-based references**: Use integer `id` instead of GUIDs for all references (components, connections, groups)
- **Single type field**: Use `type` (objectType) instead of separate `componentGuid`, `instanceGuid`, `objectType`
- **Removed canvas state**: No `selected`, `locked`, `hidden` (runtime/UI state)
- **Simplified params**: Only essential component properties
- **Simplified I/O settings**: Include only data tree modifiers (`dataMapping`: Flatten/Graft/None, `simplify`, `reverse`, `invert`, `isPrincipal`) and expressions when applied
- **No componentState**: Omit UI-specific state (colors, multiline, etc.)

**Retained for AI Context:**

- Component names and types (for understanding)
- Positions (for spatial reasoning)
- Essential parameters (slider values, panel text, script code)
- **Data tree modifiers**: `dataMapping` (Flatten/Graft/None), `simplify`, `reverse`, `invert`, `isPrincipal` (critical for behavior)
- **Expressions**: Parameter expressions that transform data (e.g., `x * 2`, `Math.Sin(x)`)
- Connections (for dataflow understanding)
- Groups (for logical organization)
- Errors/warnings (if present)

### Complete Property Reference

#### Parameter/Input/Output Properties

| Property | Full Format | Lite Format | Data Type | Values/Examples | Purpose | Status | Notes |
|----------|-------------|-------------|-----------|-----------------|---------|--------|-------|
| `parameterName` | ✅ | ✅ | string | Parameter name | Identifies the parameter | ✅ Implemented | |
| `dataMapping` | ✅ | ✅ | string | `"None"`, `"Flatten"`, `"Graft"` | Tree structure manipulation | ✅ Implemented | |
| `simplify` | ✅ | ✅ | boolean | `true`, `false` | Simplifies data tree paths | ✅ Implemented | |
| `reverse` | ✅ | ✅ | boolean | `true`, `false` | Reverses list order | ✅ Implemented | |
| `invert` | ✅ | ✅ | boolean | `true`, `false` | Inverts boolean/numeric values | ✅ Implemented | |
| `expression` | ✅ | ✅ | string | `"x * 2"`, `"Math.Sin(x)"` | Parameter expression | ✅ Implemented | |
| `persistentData` | ✅ | ✅ | object | Data tree structure | Internalized parameter data | ✅ Implemented | |
| `isPrincipal` | ✅ | ✅ | string | `"IsNotPrincipal"`, `"IsPrincipal"` | Parameter matching behavior | 🔨 **TODO** | |
| `expressionContent` | ❌ | ❌ | string | Expression code | Separate expression storage | 🗑️ **ToRemove** | Redundant with `expression` |
| `variableName` | ✅ | ✅ | string | Variable name | Script parameter variable | ✅ Implemented | Script components only |
| **Properties to Remove** |
| `dataType` | ✅ | ❌ | string | `"remote"`, `"void"`, `"local"` | Redundant (inferred) | 🗑️ **ToRemove** | Inferred from connections/persistentData |
| `volatileData` | ✅ | ❌ | object | Runtime data | Runtime-only | 🗑️ **ToRemove** | Not persistent |
| **Properties Excluded** |
| `access` | ❌ | ❌ | string | `"item"`, `"list"`, `"tree"` | Implicit from component type | ❌ Excluded | |
| `description` | ❌ | ❌ | string | Text | Implicit from component definition | ❌ Excluded | |
| `optional` | ❌ | ❌ | boolean | `true`, `false` | Redundant information | ❌ Excluded | |
| `isReparameterized` | ✅ | ❌ | boolean | `true`, `false` | Domain reparameterization | 🔨 **TODO** | |

#### Component Properties

| Property | Full Format | Lite Format | Data Type | Values/Examples | Purpose | Status | Notes |
|----------|-------------|-------------|-----------|-----------------|---------|--------|-------|
| **General Component Properties** |
| `nickName` | ✅ | ❌ | string | Custom name | Component nickname | ✅ Implemented | |
| `displayName` | ✅ | ❌ | string | Display name | Component display name | ✅ Implemented | |
| `locked` | ✅ | ✅ | boolean | `true`, `false` | Component locked state | ✅ Implemented | |
| `hidden` | ✅ | ✅ | boolean | `true`, `false` | Preview visibility state | ✅ Implemented | |
| `value` | ✅ | ✅ | various | Component value | **Universal value property** | 💡 **Consolidate** | See mapping table below |
| `humanReadable` | ❌ | ❌ | string | Human-readable value | Debug/display helper | 🗑️ **ToRemove** | Not necessary if `value` is properly serialized |
| **Number Slider** |
| `currentValue` | ✅ | ✅ | string | `"5.0<0.0,10.0>"` | Slider value with range | 🗑️ **ToRemove** | Maps to `value` |
| `minimum` | ✅ | ❌ | number | Min value | Slider minimum | 🗑️ **ToRemove** | Redundant (in currentValue) |
| `maximum` | ✅ | ❌ | number | Max value | Slider maximum | 🗑️ **ToRemove** | Redundant (in currentValue) |
| `decimals` | ✅ | ❌ | integer | Decimal places | Slider precision | 🗑️ **ToRemove** | Redundant (in currentValue) |
| `range` | ✅ | ❌ | object | Range config | Slider range | 🗑️ **ToRemove** | Redundant (in currentValue) |
| `limit` | ✅ | ❌ | object | Limit config | Slider limits | 🗑️ **ToRemove** | Redundant (in currentValue) |
| `displayFormat` | ✅ | ❌ | string | Format string | Display format | 🗑️ **ToRemove** | Redundant (in currentValue) |
| **Panel** |
| `userText` | ✅ | ✅ | string | Panel text | Panel content | 🗑️ **ToRemove** | Maps to `value` |
| `properties` | ✅ | ❌ | object | Nested properties | Panel properties | ✅ Implemented | UI formatting |
| **Scribble** |
| `text` | ✅ | ✅ | string | Scribble text | Scribble content | 🗑️ **ToRemove** | Maps to `value` |
| `font` | ✅ | ❌ | object | Font config | Font settings | ✅ Implemented | UI formatting |
| `corners` | ✅ | ❌ | array | Corner points | Scribble bounds | ✅ Implemented | UI positioning |
| **Value List** |
| `listMode` | ✅ | ✅ | string | Selection mode | List mode | ✅ Implemented | |
| `listItems` | ✅ | ✅ | array | List items | Selectable items | 🗑️ **ToRemove** | Maps to `value` |
| **Multidimensional Slider** |
| `sliderMode` | ✅ | ❌ | string | Slider mode | Mode config | ✅ Implemented | |
| `xInterval` | ✅ | ❌ | object | X interval | X-axis range | 🗑️ **ToRemove** | Redundant (in value) |
| `yInterval` | ✅ | ❌ | object | Y interval | Y-axis range | 🗑️ **ToRemove** | Redundant (in value) |
| `zInterval` | ✅ | ❌ | object | Z interval | Z-axis range | 🗑️ **ToRemove** | Redundant (in value) |
| `x` | ✅ | ❌ | number | X value | Current X | 🗑️ **ToRemove** | Maps to `value` (consolidate) |
| `y` | ✅ | ❌ | number | Y value | Current Y | 🗑️ **ToRemove** | Maps to `value` (consolidate) |
| `z` | ✅ | ❌ | number | Z value | Current Z | 🗑️ **ToRemove** | Maps to `value` (consolidate) |
| **Script Component** |
| `script` | ✅ | ✅ | string | Script code | Script content | 🗑️ **ToRemove** | Maps to `value` |
| **Geometry Pipeline** |
| `layerFilter` | ✅ | ❌ | string | Layer filter | Filter pattern | ✅ Implemented | |
| `nameFilter` | ✅ | ❌ | string | Name filter | Filter pattern | ✅ Implemented | |
| `typeFilter` | ✅ | ❌ | string | Type filter | Filter pattern | ✅ Implemented | |
| `includeLocked` | ✅ | ❌ | boolean | Include locked | Filter option | ✅ Implemented | |
| `includeHidden` | ✅ | ❌ | boolean | Include hidden | Filter option | ✅ Implemented | |
| `groupByLayer` | ✅ | ❌ | boolean | Group by layer | Grouping option | ✅ Implemented | |
| `groupByType` | ✅ | ❌ | boolean | Group by type | Grouping option | ✅ Implemented | |
| **Other Components** |
| `graphType` | ✅ | ❌ | string | Graph type | Graph Mapper type | ✅ Implemented | |
| `lexers` | ✅ | ❌ | array | Path lexers | Path Mapper lexers | ✅ Implemented | |
| `state` | ✅ | ❌ | object | Color state | Color Wheel state | ✅ Implemented | |
| `dataLimit` | ✅ | ❌ | integer | Data limit | Data Recorder limit | ✅ Implemented | |
| `recordData` | ✅ | ❌ | boolean | Recording state | Data Recorder active | ✅ Implemented | |
| `treePath` | ✅ | ❌ | string | Tree path | Item Picker path | ✅ Implemented | |
| `treeIndex` | ✅ | ❌ | integer | Tree index | Item Picker index | ✅ Implemented | |
| `expressionNormal` | ✅ | ❌ | string | Normal expression | Button normal state | ✅ Implemented | |
| `expressionPressed` | ✅ | ❌ | string | Pressed expression | Button pressed state | ✅ Implemented | |

#### Value Property Mapping (Proposed Consolidation)

| Component Type | Current Property | Proposed `value` Format | Example | Notes |
|----------------|------------------|------------------------|---------|-------|
| Number Slider | `currentValue` | `"value<min,max>"` | `"5.0<0.0,10.0>"` | Already implemented |
| Panel | `userText` | Plain text | `"Hello World"` | Direct mapping |
| Scribble | `text` | Plain text | `"Note: Check this"` | Direct mapping |
| Value List | `listItems` | Array of items | `[{"name":"A","value":"1"}]` | Keep as array |
| Multidimensional Slider | `x`, `y`, `z` | `"x,y,z"` or object | `"1.0,2.0,3.0"` | See Phase 1.4 |
| Script | `script` | Script code | `"import math\nprint(x)"` | Direct mapping |
| Parameter | `persistentData` | Data tree | See Phase 1.4 | Complex types use DataTypeSerializer |

**Note**: For complex data types (Color, Point3d, Line, Plane, etc.), see **Phase 1.4: Data Type Serialization** for standardized encoding formats.

**Legend:**

- ✅ **Included**: Property is serialized in this format
- ❌ **Excluded**: Property is omitted to reduce size/complexity

#### Level 2: Type Dictionary Optimization (Internal Use)

**Purpose**: Further compression for internal storage, caching, diff generation. **Not intended for AI consumption.**

```json
{
  "types": {
    "1": "Grasshopper.Kernel.Special.GH_NumberSlider",
    "2": "Grasshopper.Kernel.Components.GH_Addition"
  },
  "components": [
    {"id": 1, "name": "Number Slider", "type": "1", "position": {"x": 100, "y": 100}},
    {"id": 2, "name": "Addition", "type": "2", "position": {"x": 300, "y": 100}}
  ],
  "connections": [{"from": {"id": 1, "output": "Number Slider"}, "to": {"id": 2, "input": "A"}}]
}
```

**Optimizations:**

- Type dictionary for common component types
- Reference-based deduplication
- Omit empty arrays/objects
- Compact number formatting (100 instead of 100.0)

### 2.3 Lite Format API

```csharp
// Conversion API
GhJsonLite.FromFull(fullGhJson, level: CompressionLevel.Medium);
GhJsonLite.ToFull(liteGhJson);

// Configurable reduction
var config = new LiteConfig
{
    IncludePositions = false,
    IncludeErrors = true,
    PropertyWhitelist = new[] { "Locked", "CurrentValue" }
};
GhJsonLite.FromFull(fullGhJson, config);
```

### 2.4 Use Case Implementation

#### Diff Generation

**Example Diff Format:**

```json
{
  "metadata": {
    "diffType": "component-change",
    "timestamp": "2025-01-15T15:30:00Z",
    "fromVersion": "1.0",
    "toVersion": "1.1"
  },
  "changes": {
    "components": {
      "modified": [
        {
          "id": 1,
          "name": "Number Slider",
          "changes": {
            "params.value": {
              "old": "12.0<5.0,50.0>",
              "new": "25.0<5.0,50.0>"
            }
          }
        },
        {
          "id": 2,
          "name": "Python Script",
          "changes": {
            "params.script": {
              "old": "import rhinoscriptsyntax as rs\n\npanels = []\nfor i in range(int(count)):\n    panels.append(create_panel(i))",
              "new": "import rhinoscriptsyntax as rs\nimport math\n\npanels = []\nfor i in range(int(count)):\n    angle = math.radians(i * 15)\n    panels.append(create_rotated_panel(i, angle))"
            }
          }
        }
      ],
      "added": [
        {
          "id": 4,
          "name": "Rotate",
          "type": "Grasshopper.Kernel.Components.GH_Rotate",
          "position": {"x": 400.0, "y": 150.0}
        }
      ],
      "removed": []
    },
    "connections": {
      "added": [
        {
          "from": {"id": 2, "output": "panels"},
          "to": {"id": 4, "input": "Geometry"}
        },
        {
          "from": {"id": 4, "output": "Geometry"},
          "to": {"id": 3, "input": "Panel"}
        }
      ],
      "removed": [
        {
          "from": {"id": 2, "output": "panels"},
          "to": {"id": 3, "input": "Panel"}
        }
      ]
    },
    "groups": {
      "modified": [
        {
          "id": 2,
          "name": "Processing",
          "changes": {
            "members": {
              "old": [2],
              "new": [2, 4]
            }
          }
        }
      ]
    }
  },
  "summary": {
    "componentsModified": 2,
    "componentsAdded": 1,
    "componentsRemoved": 0,
    "connectionsAdded": 2,
    "connectionsRemoved": 1,
    "groupsModified": 1
  }
}
```

**Key Features:**

- **Change tracking**: Separate sections for modified/added/removed items
- **Property-level diffs**: Show old vs new values for changed properties
- **Connection changes**: Track dataflow modifications
- **Summary statistics**: Quick overview of changes
- **Metadata**: Timestamps and version tracking

**Implementation Tasks:**

- [ ] **Minimal Diff**: Only changed components
- [ ] **Property-Level Diff**: Changed properties only
- [ ] **Connection Diff**: Added/removed connections
- [ ] **Diff API**: Generate diffs between two GhJSON documents
- [ ] **Diff Visualization**: Human-readable diff output

#### AI Context Optimization

- [ ] **Smart Truncation**: Prioritize relevant components
- [ ] **Summarization**: High-level structure description

---

## Phase 3: Reliability and Consistency

**Goal**: Make serialization/deserialization robust, consistent, and trustworthy.

### 3.1 Validation Framework

**Current State:**

- Basic JSON schema validation
- Component existence checking
- Connection reference validation

**Enhancements:**

#### Schema Validation

- [ ] **JSON Schema**: Formal JSON Schema definition
- [ ] **Schema Versioning**: Support multiple schema versions
- [ ] **Strict Mode**: Reject invalid documents
- [ ] **Permissive Mode**: Auto-fix common issues

#### Semantic Validation

- [ ] **Type Compatibility**: Validate connection data types
- [ ] **Parameter Constraints**: Validate parameter values
- [ ] **Circular Dependencies**: Detect circular references
- [ ] **Orphaned Connections**: Detect invalid connections

#### Validation Levels

```csharp
public enum ValidationLevel
{
    None,           // No validation
    Syntax,         // JSON syntax only
    Schema,         // Schema compliance
    Semantic,       // Logical consistency
    Strict          // Full validation with errors
}
```

### 3.2 Error Handling and Recovery

**Robust Deserialization:**

- [ ] **Graceful Degradation**: Continue on non-critical errors
- [ ] **Error Collection**: Aggregate all errors, not just first
- [ ] **Auto-Repair**: Fix common issues automatically
- [ ] **Fallback Values**: Use defaults for missing properties
- [ ] **Partial Loading**: Load valid components, skip invalid

**Error Reporting:**
```json
{
  "success": false,
  "errors": [
    {
      "severity": "error",
      "code": "INVALID_GUID",
      "message": "Component GUID 'xyz' is invalid",
      "path": "components[3].componentGuid",
      "suggestion": "Use a valid GUID or omit for auto-generation"
    }
  ],
  "warnings": [...],
  "partialResult": {...}
}
```

### 3.3 Consistency Guarantees

**Serialization Consistency:**

- [ ] **Deterministic Output**: Same input always produces same output
- [ ] **Stable Ordering**: Consistent component/connection ordering
- [ ] **Canonical Form**: Single canonical representation
- [ ] **Idempotency**: Serialize → Deserialize → Serialize yields same result

### 3.4 Undo Support

- [ ] **Undo Support**: Support undo/redo for gh_put

---

## Related Documentation

- [GhJSON Format Specification](./format-specification.md)
- [Property Whitelist](./property-whitelist.md)
- [Examples](./examples.md)
