# GhJSON Format Specification

> **📊 Implementation Status**: This specification describes the current implemented format. See [implementation-status.md](./implementation-status.md) for feature completion status and [roadmap.md](./roadmap.md) for planned enhancements.

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
  "componentGuid": "a0d62394-a118-422d-abb3-6af115c75b25",
  "instanceGuid": "f8e7d6c5-b4a3-9281-7065-43e1f2a9b8c7",
  "id": 1,
  "pivot": "150.0,200.0",
  "properties": {},
  "selected": false,
  "warnings": [],
  "errors": []
}
```

### Component Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `name` | String | Yes | Display name of the component |
| `componentGuid` | GUID | No | Unique identifier for the component type |
| `instanceGuid` | GUID | Yes | Unique identifier for this specific instance |
| `id` | Integer | No | Sequential integer ID for connections and group references |
| `pivot` | String | No | Canvas position in compact "X,Y" format (e.g., "150.0,200.0") |
| `properties` | Object | No | Dictionary of component-specific properties (legacy) |
| `params` | Object | No | Simple key-value pairs for basic properties |
| `inputSettings` | Array | No | Input parameter configuration |
| `outputSettings` | Array | No | Output parameter configuration |
| `componentState` | Object | No | Component-specific UI state |
| `selected` | Boolean | No | Whether the component is currently selected |
| `warnings` | Array | No | List of warning messages |
| `errors` | Array | No | List of error messages |

### Notes

- **Integer IDs**: Components are assigned sequential integer IDs (1, 2, 3...) used for connections and group member references
- **Compact Position**: The `pivot` property uses a compact string format "X,Y" instead of object format for efficiency
- **Property Organization**: Properties are organized into `properties` (legacy), `params` (simple values), `inputSettings`/`outputSettings` (parameter config), and `componentState` (UI state)

---

## Properties Schema

The `properties` object contains component-specific configuration.

### Property Structure

```json
"properties": {
  "Locked": {
    "value": false
  },
  "CurrentValue": {
    "value": "5.0<0.0,10.0>"
  }
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `value` | Any | Yes | The actual property value (type inferred from JSON) |

---

## Connection Schema

Connections define wiring between component parameters.

### Connection Structure

```json
{
  "from": {
    "id": 1,
    "paramName": "Result"
  },
  "to": {
    "id": 2,
    "paramName": "A"
  }
}
```

### Connection Endpoint

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `id` | Integer | Yes | Integer ID of the component (references component.id) |
| `paramName` | String | Yes | Name of the parameter |

**Note**: Connections use integer IDs instead of GUIDs for compact representation. The IDs reference the `id` field in components.

---

## Validation Rules

### Required Fields

**Components:**
- `name` (string, non-empty)
- `instanceGuid` (valid GUID)

**Connections:**
- `from` and `to` objects
- `id` (integer > 0) in both endpoints
- `paramName` (string, non-empty) in both endpoints

### Warnings

- Invalid or missing `componentGuid`
- Non-GUID `instanceGuid` values
- Missing `id` field in components (auto-assigned during extraction)

### Information

- Missing `type` or `objectType` fields
- Incomplete pivot positions

---

## ID Handling

### Integer ID System

GhJSON uses integer IDs for connections and group references:

**Component ID Assignment:**
```json
{
  "name": "Addition",
  "instanceGuid": "f8e7d6c5-b4a3-9281-7065-43e1f2a9b8c7",
  "id": 1
}
```

**Connection References:**
```json
{
  "from": {"id": 1, "paramName": "Result"},
  "to": {"id": 2, "paramName": "A"}
}
```

**Group Member References:**
```json
{
  "instanceGuid": "...",
  "name": "Input Group",
  "members": [1, 2, 3]
}
```

**AI-Generated Content:**
AI can use simple integer strings as `instanceGuid` values (e.g., `"1"`, `"2"`), which are automatically converted to proper GUIDs during deserialization.

---

## Related Documentation

- [Implementation Status](./implementation-status.md) - Current progress and pending tasks
- [GhJSON Roadmap](./roadmap.md) - Complete feature roadmap
- [Property Management V2](./property-management-v2.md) - Advanced property system
- [Property Whitelist](./property-whitelist.md) - Legacy property filtering
- [Examples](./examples.md) - Usage examples
