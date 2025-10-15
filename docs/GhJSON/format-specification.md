# GhJSON Format Specification

## Overview

**GhJSON** (Grasshopper JSON) is SmartHopper's serialization format for representing Grasshopper definitions as structured JSON documents. It enables AI-powered tools to read, analyze, modify, and generate Grasshopper component networks programmatically.

## Purpose

GhJSON serves multiple purposes:
- **AI Tool Integration**: Enables AI to understand and manipulate Grasshopper definitions
- **Component Analysis**: Allows inspection of component properties, connections, and states
- **Definition Generation**: Supports programmatic creation of Grasshopper networks
- **Validation**: Provides schema-based validation of component structures
- **Serialization**: Enables persistence and transmission of Grasshopper definitions

## Core Schema

### Document Root

The root structure contains two main arrays:

```json
{
  "components": [...],
  "connections": [...]
}
```

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `components` | Array | Yes | List of all components in the document |
| `connections` | Array | Yes | List of all connections between components |

---

## Component Schema

Each component in the `components` array represents a Grasshopper object (component or parameter).

### Basic Structure

```json
{
  "name": "Addition",
  "type": "IGH_Component",
  "objectType": "Grasshopper.Kernel.Components.GH_Addition",
  "componentGuid": "a0d62394-a118-422d-abb3-6af115c75b25",
  "instanceGuid": "f8e7d6c5-b4a3-9281-7065-43e1f2a9b8c7",
  "selected": false,
  "pivot": {
    "X": 150.0,
    "Y": 200.0
  },
  "properties": {},
  "warnings": [],
  "errors": []
}
```

### Component Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `name` | String | Yes | Display name of the component |
| `type` | String | No | Component type classification |
| `objectType` | String | No | Full .NET type name of the component |
| `componentGuid` | GUID | No | Unique identifier for the component type |
| `instanceGuid` | GUID | Yes | Unique identifier for this specific instance |
| `selected` | Boolean | No | Whether the component is currently selected |
| `pivot` | Object | No | Canvas position with X and Y coordinates |
| `properties` | Object | No | Dictionary of component-specific properties |
| `warnings` | Array | No | List of warning messages |
| `errors` | Array | No | List of error messages |

### Component Types

- **IGH_Component**: Standard Grasshopper components
- **IGH_Param**: Parameter objects
- **other**: Other Grasshopper objects

---

## Properties Schema

The `properties` object contains component-specific configuration.

### Property Structure

```json
"properties": {
  "Locked": {
    "value": false,
    "type": "Boolean"
  },
  "CurrentValue": {
    "value": "5.0<0.0,10.0>",
    "type": "String",
    "humanReadable": "5.0"
  }
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `value` | Any | Yes | The actual property value |
| `type` | String | Yes | .NET type name of the value |
| `humanReadable` | String | No | Human-readable representation |

---

## Connection Schema

Connections define wiring between component parameters.

### Connection Structure

```json
{
  "from": {
    "instanceId": "f8e7d6c5-b4a3-9281-7065-43e1f2a9b8c7",
    "paramName": "Result"
  },
  "to": {
    "instanceId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "paramName": "A"
  }
}
```

### Connection Endpoint

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `instanceId` | GUID | Yes | Instance GUID of the component |
| `paramName` | String | Yes | Name of the parameter |

---

## Validation Rules

### Required Fields

**Components:**
- `name` (string, non-empty)
- `instanceGuid` (valid GUID)

**Connections:**
- `from` and `to` objects
- `instanceId` in both endpoints
- `paramName` in both endpoints

### Warnings

- Invalid or missing `componentGuid`
- Non-GUID `instanceGuid` values

### Information

- Missing `type` or `objectType` fields
- Incomplete pivot positions

---

## ID Handling

### Integer ID Support

GhJSON supports integer IDs for AI-generated content:

```json
{
  "instanceGuid": "1",
  "componentGuid": "a0d62394-a118-422d-abb3-6af115c75b25"
}
```

Integer IDs are automatically converted to proper GUIDs during deserialization.

---

## Related Documentation

- [GhJSON Roadmap](./roadmap.md)
- [Property Whitelist](./property-whitelist.md)
- [Examples](./examples.md)
