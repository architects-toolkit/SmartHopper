# GhJSON Format Specification

**Version**: 1.0  
**Status**: Draft  
**Last Updated**: 2026-01-12

---

## Table of Contents

1. [Introduction](#1-introduction)
2. [Document Structure](#2-document-structure)
3. [Components](#3-components)
4. [Connections](#4-connections)
5. [Groups](#5-groups)
6. [Data Types](#6-data-types)
7. [Component-Specific Formats](#7-component-specific-formats)
8. [Validation](#8-validation)
9. [Examples](#9-examples)
10. [Diff and Patch](#10-diff-and-patch)

---

## 1. Introduction

### 1.1 Purpose

GhJSON is a JSON-based format for representing Grasshopper definitions. It provides a human-readable, portable way to serialize and deserialize Grasshopper documents, enabling:

- **Version control**: Track changes to Grasshopper definitions in Git
- **AI integration**: Enable AI tools to read and generate Grasshopper definitions
- **Cross-platform sharing**: Share definitions without binary format dependencies
- **Automation**: Programmatically create and modify definitions

### 1.2 Design Principles

1. **Human-readable**: JSON format with meaningful property names
2. **Compact**: Optimized representations (e.g., compact pivot format)
3. **Complete**: Captures all necessary information to reconstruct a definition
4. **Extensible**: Supports custom properties and future enhancements
5. **Backward-compatible**: Legacy formats are supported during deserialization

### 1.3 File Extension

GhJSON files use the `.ghjson` extension.

### 1.4 Encoding

GhJSON files MUST be encoded in UTF-8 without BOM.

---

## 2. Document Structure

A GhJSON document is a JSON object with the following top-level structure:

```json
{
  "schema": "1.0",
  "metadata": { ... },
  "components": [ ... ],
  "connections": [ ... ],
  "groups": [ ... ]
}
```

### 2.1 Properties

| Property | Type | Required | Description |
| ---------- | ------ | ---------- | ------------- |
| `schema` | string | No | GhJSON schema version (default: "1.0") |
| `metadata` | object | No | Document metadata |
| `components` | array | **Yes** | List of components |
| `connections` | array | No | List of connections |
| `groups` | array | No | List of groups |

### 2.2 Metadata

The `metadata` object contains information about the document:

```json
{
  "metadata": {
    "description": "A parametric tower generator",
    "author": "Jane Smith",
    "version": "2",
    "created": "2026-01-10T14:30:00Z",
    "modified": "2026-01-11T09:15:00Z",
    "rhinoVersion": "8.24",
    "grasshopperVersion": "1.0.0007",
    "dependencies": ["Kangaroo2", "LunchBox"],
    "componentCount": 45,
    "connectionCount": 12,
    "groupCount": 3
  }
}
```

| Property | Type | Description |
| ---------- | ------ | ------------- |
| `description` | string | Description of the definition |
| `author` | string | Author name |
| `version` | string | Definition version |
| `created` | string | ISO 8601 creation timestamp |
| `modified` | string | ISO 8601 modification timestamp |
| `rhinoVersion` | string | Target Rhino version |
| `grasshopperVersion` | string | Target Grasshopper version |
| `dependencies` | string[] | Required plugin dependencies |
| `componentCount` | integer | Total component count. For paginated documents this is the total across all pages. |
| `connectionCount` | integer | Total connection count |
| `groupCount` | integer | Total group count |
| `pagination` | object | Pagination information when the document contains only a subset of components and spans more than one page (see [Pagination](#84-pagination)) |

---

## 3. Components

Components are the core elements of a GhJSON document. Each component represents a Grasshopper component or parameter on the canvas.

### 3.1 Component Structure

```json
{
  "name": "Addition",
  "library": "Maths",
  "nickName": "Add",
  "componentGuid": "a0d62394-a118-422d-abb3-6af115c75b25",
  "instanceGuid": "12345678-1234-1234-1234-123456789abc",
  "id": 1,
  "pivot": "100,200",
  "inputSettings": [ ... ],
  "outputSettings": [ ... ],
  "componentState": { ... }
}
```

### 3.2 Component Properties

| Property | Type | Required | Description |
| ---------- | ------ | ---------- | ------------- |
| `name` | string | Conditional | Display name of the component |
| `library` | string | No | Component library/category |
| `nickName` | string | No | Custom nickname |
| `componentGuid` | uuid | No | Component type GUID |
| `instanceGuid` | uuid | No | Instance GUID |
| `id` | integer | Conditional | Integer ID for references (required when referenced by connections/groups) |
| `pivot` | string or object | No | Canvas position (string: "X,Y" or object: `{ "x": 100, "y": 200 }`) |
| `inputSettings` | array | No | Input parameter settings |
| `outputSettings` | array | No | Output parameter settings |
| `componentState` | object | No | UI-specific state |
| `warnings` | string[] | No | Warning messages |
| `errors` | string[] | No | Error messages |
| `remarks` | string[] | No | Remark messages |

### 3.3 Pivot Format

The `pivot` property uses a compact string format:
```json
"pivot": "100.5,200.25"
```
Or an explicit object format:
```json
"pivot": { "x": 100.5, "y": 200.25 }
```

### 3.4 Component Identification

Components MUST have at least one identifier (`id` or `instanceGuid`). Library implementations MAY auto-assign sequential `id` values to components lacking identifiers during document construction.

Components can be identified with any of these combinations:

1. **By Name + ID**: `name` with compact integer `id`
2. **By Name + Instance GUID**: `name` plus `instanceGuid`
3. **By Component GUID + ID**: `componentGuid` plus `id`
4. **By Component GUID + Instance GUID**: `componentGuid` plus `instanceGuid`

You MAY include both `componentGuid` and `name` together. For reliable instantiation, include `componentGuid` and an `id` when the component will be referenced by connections or groups.

### 3.5 Integer IDs

The `id` property provides a compact integer reference for use in connections and groups. IDs:

- MUST be positive integers (≥ 1)
- MUST be unique within the document
- SHOULD be sequential (1, 2, 3, ...)

### 3.6 Parameter Settings (`inputSettings` / `outputSettings`)

The optional `inputSettings` and `outputSettings` arrays configure a component's parameters.

Each item is a **Parameter Settings** object:

| Property | Type | Required | Description |
| ---------- | ------ | ---------- | ------------- |
| `parameterName` | string | **Yes** | The parameter name |
| `nickName` | string | No | Custom nickname |
| `variableName` | string | No | Script parameter variable name |
| `description` | string | No | Parameter description |
| `dataMapping` | string | No | Mapping mode: `none`, `flatten`, `graft` |
| `expression` | string | No | Expression (presence implies an expression exists) |
| `access` | string | No | Script access: `item`, `list`, `tree` |
| `typeHint` | string | No | Script type hint (e.g., `double`, `Point3d`) |
| `isPrincipal` | boolean | No | Principal/master input |
| `isRequired` | boolean | No | Required parameter for variable parameter components |
| `isReparameterized` | boolean | No | Domain reparameterization |
| `isReversed` | boolean | No | Reverse modifier |
| `isSimplified` | boolean | No | Simplify modifier |
| `isInverted` | boolean | No | Invert modifier (boolean params) |
| `isUnitized` | boolean | No | Unitize modifier (vector params) |
| `internalizedData` | object | No | Internalized data tree |

---

## 4. Connections

Connections represent the wires between component parameters.

### 4.1 Connection Structure

```json
{
  "connections": [
    {
      "from": { "id": 1, "paramIndex": 0 },
      "to": { "id": 2, "paramIndex": 0 }
    }
  ]
}
```

### 4.2 Connection Properties

| Property | Type | Required | Description |
| ---------- | ------ | ---------- | ------------- |
| `from` | endpoint | **Yes** | Source endpoint (output) |
| `to` | endpoint | **Yes** | Target endpoint (input) |
| `boundary` | boolean | No | When `true`, one or both endpoints reference components not present in this document (e.g. because of pagination) |

### 4.3 Endpoint Properties

| Property | Type | Required | Description |
| ---------- | ------ | ---------- | ------------- |
| `id` | integer | **Yes** | Component integer ID |
| `paramName` | string | Conditional | Parameter name (required if `paramIndex` is not provided) |
| `paramIndex` | integer | Conditional | Zero-based parameter index (required if `paramName` is not provided) |

The `paramIndex` provides reliable parameter matching when parameter names may vary due to localization or custom nicknames.

---

## 5. Groups

Groups organize components visually on the canvas.

### 5.1 Group Structure

```json
{
  "groups": [
    {
      "instanceGuid": "abcd1234-5678-90ab-cdef-1234567890ab",
      "id": 1,
      "name": "Input Parameters",
      "color": "argb:255,200,220,255",
      "members": [1, 2, 3]
    }
  ]
}
```

### 5.2 Group Properties

| Property | Type | Required | Description |
| ---------- | ------ | ---------- | ------------- |
| `instanceGuid` | uuid | Conditional | Group instance GUID (required if `id` is not provided; both may be present) |
| `id` | integer | Conditional | Group integer ID (required if `instanceGuid` is not provided; both may be present) |
| `name` | string | No | Group name/nickname |
| `color` | string | No | ARGB color with prefix (`argb:A,R,G,B`), each channel 0–255 |
| `members` | integer[] | **Yes** | Component IDs in this group |

---

## 6. Data Types

GhJSON uses prefixed string formats for geometric and special data types.

### 6.1 Basic Types

| Type | Format | Example |
| ------ | -------- | --------- |
| Text | `text:value` | `"text:Hello World"` |
| Number | `number:value` | `"number:3.14159"` |
| Integer | `integer:value` | `"integer:42"` |
| Boolean | `boolean:value` | `"boolean:true"` |

### 6.2 Geometric Types

| Type | Format | Example |
| ------ | -------- | --------- |
| Point | `pointXYZ:x,y,z` | `"pointXYZ:1,2,3"` |
| Vector | `vectorXYZ:x,y,z` | `"vectorXYZ:0,0,1"` |
| Line | `line2p:x1,y1,z1;x2,y2,z2` | `"line2p:0,0,0;1,0,0"` |
| Plane | `planeOXY:ox,oy,oz;xx,xy,xz;yx,yy,yz` | `"planeOXY:0,0,0;1,0,0;0,1,0"` |
| Circle | `circleCNRS:cx,cy,cz;nx,ny,nz;r;sx,sy,sz` | `"circleCNRS:0,0,0;0,0,1;5;1,0,0"` |
| Arc | `arc3P:x1,y1,z1;x2,y2,z2;x3,y3,z3` | `"arc3P:0,0,0;1,1,0;2,0,0"` |
| Box | `boxOXY:ox,oy,oz;xx,xy,xz;yx,yy,yz;x0,x1;y0,y1;z0,z1` | See spec |
| Rectangle | `rectangleCXY:cx,cy,cz;xx,xy,xz;yx,yy,yz;w,h` | See spec |
| Interval | `interval:min<max` | `"interval:0<10"` |
| Bounds | `bounds:WxH` | `"bounds:150x100"` |
| Color | `argb:a,r,g,b` | `"argb:255,128,64,32"` |

---

## 7. Component-Specific Formats

### 7.1 Number Slider

Number sliders use a compact value format:

```json
{
  "name": "Number Slider",
  "componentState": {
    "extensions": {
      "gh.numberslider": {
        "value": "5.5<0~10>"
      }
    }
  }
}
```

Format: `value<min~max>` where:
- `value` is the current value
- `min` is the minimum value
- `max` is the maximum value
- Decimal precision is determined by the value with the most decimal places

Additional properties:
- `rounding`: Rounding mode ("Round", "None", "Even", "Odd")

### 7.2 Panel

```json
{
  "name": "Panel",
  "componentState": {
    "extensions": {
      "gh.panel": {
        "text": "Hello World",
        "multiline": true,
        "wrap": true,
        "drawIndices": false,
        "drawPaths": true,
        "alignment": "Left",
        "bounds": ["150x100"]
      }
    }
  }
}
```

### 7.3 Value List

```json
{
  "name": "Value List",
  "componentState": {
    "extensions": {
      "gh.valuelist": {
        "listMode": "DropDown",
        "items": [
          { "name": "Option A", "expression": "0", "selected": true },
          { "name": "Option B", "expression": "1" },
          { "name": "Option C", "expression": "2" }
        ]
      }
    }
  }
}
```

`listMode` is an implementation-defined string (the schema does not enforce a fixed set of values).

### 7.4 Script Components (C#, Python, IronPython)

Script components (C#, Python 3, IronPython 2) store their code and configuration in the `componentState.extensions` object:

```json
{
  "name": "C# Script",
  "inputSettings": [
    {
      "parameterName": "x",
      "variableName": "x",
      "typeHint": "double",
      "access": "item"
    }
  ],
  "outputSettings": [
    {
      "parameterName": "A",
      "variableName": "A",
      "typeHint": "object"
    }
  ],
  "componentState": {
    "extensions": {
      "gh.csharp": {
        "code": "A = x * 2;",
        "showStandardOutput": true,
        "avoidMarshalGuids": true,
        "avoidGraftOutputs": true,
        "avoidMarshalInputs": true,
        "outModifiers": {
          "isSimplified": true,
          "isReversed": false,
          "dataMapping": "Graft",
          "expression": "x * 2"
        }
      }
    }
  }
}
```

**Extension keys:**
- `gh.csharp` — C# Script component
- `gh.python` — Python 3 Script component  
- `gh.ironpython` — IronPython 2 Script component

**Extension properties:**

| Property | Type | Required | Description |
| ---------- | ------ | ---------- | ------------- |
| `code` | string | **Yes** | Script source code |
| `showStandardOutput` | boolean | No | Whether to show the "out" parameter (default: true) |
| `avoidMarshalGuids` | boolean | No | "Avoid Marshalling Output Guids" — `true` when enabled (default: `false`) |
| `avoidGraftOutputs` | boolean | No | "Avoid Grafting Output Lines" — `true` when enabled (default: `false`) |
| `avoidMarshalInputs` | boolean | No | "Avoid Marshalling Inputs" — `true` when enabled (default: `false`) |
| `outModifiers` | object | No | Modifiers for the "out" parameter (see below) |

**Out Parameter Modifiers:**

The `outModifiers` object captures modifiers for the "out" standard output parameter:

| Property | Type | Description |
| ---------- | ------ | ------------- |
| `isSimplified` | boolean | Simplify output data tree |
| `isReversed` | boolean | Reverse output data tree |
| `dataMapping` | string | Data mapping mode: `"None"`, `"Flatten"`, or `"Graft"` |
| `expression` | string | Expression applied to the output |

**Notes:**
- Marshalling options (`avoidMarshalGuids`, `avoidGraftOutputs`, `avoidMarshalInputs`) are only available on C#, Python 3, and IronPython 2 components (those implementing `IScriptComponent`)
- Default value for all marshalling options is `false` (marshalling active). Only store `true` values (when "Avoid" is enabled).
- The "out" parameter is only present when `showStandardOutput` is `true`

### 7.5 VB Script

VB Script components have three code sections:

```json
{
  "name": "VB Script",
  "componentState": {
    "extensions": {
      "gh.vbscript": {
        "vbCode": {
          "imports": "Imports System.Math",
          "script": "A = Sin(x)",
          "additional": "' Helper functions here"
        }
      }
    }
  }
}
```

### 7.6 GhPython (Old GhPython / Ladybug Tools)

Old GhPython script components (`ZuiPythonComponent` from `GhPython.dll`) store their code in the `componentState.extensions` object under the `gh.ghpython` key. This covers:

- The generic **GhPython Script** component from the Grasshopper toolbar.
- **Ladybug Tools** components (LB Hourly Plot, LB Sunpath, etc.).
- **Honeybee**, **Dragonfly**, and any other plugin that ships pre-configured `ZuiPythonComponent` instances.

Unlike Rhino 8 script components (C#, Python 3, IronPython 2) which implement `IScriptComponent`, old GhPython components do not expose that interface. Marshalling options (`avoidMarshalGuids`, `avoidGraftOutputs`, `avoidMarshalInputs`) are therefore **not applicable**.

```json
{
  "name": "LB Hourly Plot",
  "library": "Ladybug",
  "componentGuid": "410755b1-224a-4c1e-a407-bf32fb45ea7e",
  "id": 1,
  "pivot": "100,200",
  "inputSettings": [
    {
      "parameterName": "_data",
      "access": "list"
    },
    {
      "parameterName": "_base_pt_"
    }
  ],
  "outputSettings": [
    {
      "parameterName": "mesh"
    },
    {
      "parameterName": "legend"
    }
  ],
  "componentState": {
    "extensions": {
      "gh.ghpython": {
        "code": "# Ladybug: A Plugin for Environmental Analysis (GPL)\n# ...\ntry:\n    from ladybug_rhino.grasshopper import ...\nexcept ImportError as e:\n    raise ImportError(...)",
        "showStandardOutput": true
      }
    }
  }
}
```

**Extension key:** `gh.ghpython`

**Extension properties:**

| Property | Type | Required | Description |
| ---------- | ------ | ---------- | ------------- |
| `code` | string | **Yes** | Python (IronPython) script code |
| `showStandardOutput` | boolean | No | Whether the `out` standard output parameter is visible (default: true) |
| `outModifiers` | object | No | Modifiers for the `out` parameter (same shape as section 7.4) |

**Notes:**

- For Ladybug/Honeybee components the `code` property captures the embedded script for documentation and AI understanding. During deserialization on a machine where the plugin is installed, `AddedToDocument` may reinitialize the component from the plugin's own script files.
- The `inputSettings` and `outputSettings` arrays capture script parameter details (access mode, variable names, type hints) the same way as section 7.4.
- Marshalling options are **not available** on old GhPython components; do not include `avoidMarshalGuids`, `avoidGraftOutputs`, or `avoidMarshalInputs`.

### 7.7 Scribble

Scribble geometry is defined by three corner offsets relative to the component pivot: A (top-left origin), B (top-right), and D (bottom-left). Corner C is derived as `B + D - A` (parallelogram rule). This minimal representation preserves rotation.

```json
{
  "name": "Scribble",
  "componentState": {
    "extensions": {
      "gh.scribble": {
        "text": "Design Notes",
        "fontFamily": "Arial",
        "fontSize": 14,
        "fontStyle": "Bold",
        "corners": ["0,0", "200,0", "0,100"]
      }
    }
  }
}
```

| Property | Type | Required | Description |
| ---------- | ------ | ---------- | ------------- |
| `text` | string | **Yes** | Scribble text content |
| `corners` | array of 3 strings | **Yes** | Corner offsets relative to pivot as `"x,y"` pairs: `[A, B, D]`. Corner C = B + D − A. |
| `fontFamily` | string | No | Font family name (default: system default) |
| `fontSize` | number | No | Font size in points (default: 12) |
| `fontStyle` | string | No | Font style: `Regular`, `Bold`, `Italic`, `BoldItalic` (default: `Regular`) |

---

## 8. Validation

### 8.1 Schema Validation

GhJSON documents SHOULD validate against the JSON Schema at:
```text
https://architects-toolkit.github.io/ghjson-spec/schema/v1.0/ghjson.schema.json
```

### 8.2 Structural Validation

Beyond schema validation, implementations SHOULD verify:

1. **Unique IDs**: All component `id` values are unique
2. **Valid references**: All connection endpoint `id` values reference existing components
3. **Valid parameter names**: Connection `paramName` values exist on referenced components
4. **Group membership**: All group `members` reference existing component IDs

### 8.3 Grasshopper Validation

When deserializing, implementations MAY additionally verify:

1. **Component existence**: The `componentGuid` or `name` matches an installed component
2. **Type compatibility**: Connected parameter types are compatible
3. **Plugin dependencies**: Required plugins are installed

### 8.4 Pagination

GhJSON documents MAY represent a single page of a larger definition. When paginated, the document contains only a subset of components, but it SHOULD keep all connections that touch those components and mark cross-page connections with `boundary: true`.

Paginated documents that span more than one page MUST include `metadata.pagination`:

```json
{
  "metadata": {
    "componentCount": 118,
    "pagination": {
      "page": 1,
      "pageSize": 25,
      "totalPages": 5
    }
  }
}
```

| Property | Type | Description |
| ---------- | ------ | ------------- |
| `page` | integer | One-based page index |
| `pageSize` | integer | Number of components per page |
| `totalPages` | integer | Total number of pages available |

The `metadata.componentCount` field contains the total number of components across all pages. The number of components actually present in the document is the length of the `components` array. Pagination metadata is omitted when the document fits in a single page.

When placing a paginated document on a canvas, implementations SHOULD preserve internal connections and inform the user when a boundary connection cannot be created because its other endpoint is not present in the document.

---

## 9. Examples

### 9.1 Minimal Document

```json
{
  "components": [
    {
      "name": "Number Slider",
      "instanceGuid": "11111111-1111-1111-1111-111111111111",
      "id": 1,
      "pivot": "100,100",
      "componentState": {
        "extensions": {
          "gh.numberslider": {
            "value": "5<0~10>"
          }
        }
      }
    }
  ]
}
```

### 9.2 Simple Addition

```json
{
  "schema": "1.0",
  "components": [
    {
      "name": "Number Slider",
      "instanceGuid": "11111111-1111-1111-1111-111111111111",
      "id": 1,
      "pivot": "100,100",
      "componentState": {
        "extensions": {
          "gh.numberslider": {
            "value": "5<0~10>"
          }
        }
      }
    },
    {
      "name": "Number Slider",
      "instanceGuid": "22222222-2222-2222-2222-222222222222",
      "id": 2,
      "pivot": "100,150",
      "componentState": {
        "extensions": {
          "gh.numberslider": {
            "value": "3<0,10>"
          }
        }
      }
    },
    {
      "name": "Addition",
      "componentGuid": "a0d62394-a118-422d-abb3-6af115c75b25",
      "instanceGuid": "33333333-3333-3333-3333-333333333333",
      "id": 3,
      "pivot": "300,125"
    },
    {
      "name": "Panel",
      "instanceGuid": "44444444-4444-4444-4444-444444444444",
      "id": 4,
      "pivot": "500,125"
    }
  ],
  "connections": [
    { "from": { "id": 1, "paramName": "Number" }, "to": { "id": 3, "paramName": "A" } },
    { "from": { "id": 2, "paramName": "Number" }, "to": { "id": 3, "paramName": "B" } },
    { "from": { "id": 3, "paramName": "Result" }, "to": { "id": 4, "paramName": "Input" } }
  ]
}
```

### 9.3 With Groups and Metadata

```json
{
  "schema": "1.0",
  "metadata": {
    "description": "Simple addition example",
    "author": "GhJSON Team",
    "created": "2026-01-11T10:00:00Z"
  },
  "components": [
    {
      "name": "Number Slider",
      "instanceGuid": "11111111-1111-1111-1111-111111111111",
      "id": 1,
      "pivot": "100,100",
      "componentState": {
        "extensions": {
          "gh.numberslider": {
            "value": "5<0~10>"
          }
        }
      }
    },
    {
      "name": "Number Slider",
      "instanceGuid": "22222222-2222-2222-2222-222222222222",
      "id": 2,
      "pivot": "100,150",
      "componentState": {
        "extensions": {
          "gh.numberslider": {
            "value": "3<0~10>"
          }
        }
      }
    },
    {
      "name": "Addition",
      "instanceGuid": "33333333-3333-3333-3333-333333333333",
      "id": 3,
      "pivot": "300,125"
    }
  ],
  "connections": [
    { "from": { "id": 1, "paramName": "Number" }, "to": { "id": 3, "paramName": "A" } },
    { "from": { "id": 2, "paramName": "Number" }, "to": { "id": 3, "paramName": "B" } }
  ],
  "groups": [
    {
      "instanceGuid": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
      "name": "Inputs",
      "color": "argb:255,200,220,255",
      "members": [1, 2]
    }
  ]
}
```

---

## 10. Diff and Patch

GhJSON has a sibling **patch format** for representing differences between two `.ghjson` documents and for applying partial edits on top of an existing document. The patch format is defined separately to keep the base GhJSON schema small and stable.

- File extension: `.ghpatch`
- JSON Schema: [`schema/v1.0/ghpatch.schema.json`](../schema/v1.0/ghpatch.schema.json)
- Full specification: [`docs/ghpatch.md`](ghpatch.md)

Highlights:

- A patch is a JSON document with `kind: "ghpatch"` and a `patch` body containing `metadata`, `components`, `connections`, and `groups` operations.
- Operations are keyed by **GhJSON identity** (`instanceGuid`, `id`) — not by JSON Pointer or array index. Reordering arrays in the base document does not invalidate a patch.
- There is no `move` operation. Component/connection/group order in the GhJSON arrays has no semantic meaning.
- Patches MAY include a base-document checksum so implementations can refuse to apply on a different base.
- Conflicts are surfaced at apply time as a structured list, not encoded in the patch.

See [`docs/ghpatch.md`](ghpatch.md) for the full operation grammar, identity rules, apply semantics, and conflict taxonomy.

---

## Appendix A: MIME Type

The recommended MIME type for GhJSON files is:

```text
application/vnd.grasshopper.ghjson+json
```

## Appendix B: Schema Registry

The GhJSON schema is registered at:

- **GitHub**: https://github.com/architects-toolkit/ghjson-spec
- **Schema URLs**:
  - https://architects-toolkit.github.io/ghjson-spec/schema/v1.0/ghjson.schema.json
  - https://architects-toolkit.github.io/ghjson-spec/schema/v1.0/ghpatch.schema.json
