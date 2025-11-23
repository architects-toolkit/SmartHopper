# GhJSON Documentation

## Overview

**GhJSON** (Grasshopper JSON) is SmartHopper's serialization format for representing Grasshopper definitions as structured JSON documents. It enables AI-powered tools to read, analyze, modify, and generate Grasshopper component networks programmatically.

---

## Documentation

### Core Documentation

- **[Format Specification](./format-specification.md)**
  Complete schema reference for GhJSON format including component structure, connections, groups, and validation rules.

- **[Property Management (V2)](./property-management.md)**
  Modern property system with contexts and categories, including the Complete Property Reference and Core Data Types tables.

- **[Examples](./examples.md)**
  Practical examples of GhJSON format for common Grasshopper patterns and component types.

---

## Quick Start

### Reading Components

Use the `gh_get` AI tool to retrieve components from the current Grasshopper canvas:

```json
// Get all components
gh_get()

// Get selected components only
gh_get_selected()

// Get components with errors
gh_get_errors()

// Get specific components by GUID
gh_get_by_guid(["guid1", "guid2"])
```

### Writing Components

Use the `gh_put` AI tool to place components on the canvas from GhJSON:

```json
gh_put({
  "json": "{\"components\": [...], \"connections\": [...]}"
})
```

### Validation

Validate GhJSON before placement using `GhJsonValidator.Validate()`:

```csharp
bool isValid = GhJsonValidator.Validate(json, out string errorMessage);
if (!isValid)
{
    Console.WriteLine(errorMessage);
}
```

---

## Status

- Value consolidation: component values are stored in `componentState.value` (sliders, panels, scribbles, scripts, value lists).
- Property Management V2: implemented and used by extraction.
- Groups and document metadata: supported in models and extraction.
- GhJSON-Lite, advanced validation levels, and diff utilities: not implemented.

---

## Key Features

### Component Serialization

- **Comprehensive Metadata**: Captures component type, GUID, position, properties, and runtime messages
- **Property Whitelist**: Only relevant properties are serialized to reduce payload size
- **Special Component Support**: Script components, sliders, panels, value lists, and more
- **Error Tracking**: Runtime errors and warnings included in serialization

### Connection Management

- **Directional Wiring**: Connections stored from output to input perspective
- **Parameter Naming**: Uses actual parameter names for reliable reconnection
- **Validation**: Ensures all connection references point to existing components

### AI Optimization

- **Integer ID Support**: AI can use simple integer IDs (1, 2, 3) which are auto-converted to GUIDs
- **Flexible Validation**: Permissive mode auto-fixes common issues
- **Structured Output**: Consistent schema enables reliable AI generation

### Reliability

- **Round-Trip Fidelity**: Serialize → Deserialize → Serialize yields identical results
- **Error Recovery**: Graceful handling of invalid data with detailed error messages
- **Type Safety**: Strong typing with .NET model classes

---

## Architecture

### Core Classes

**Document Models** (`SmartHopper.Core.Models.Document`):
- `GrasshopperDocument`: Root document container
- `ComponentProperties`: Component metadata and properties
- `ConnectionPairing`: Connection between two components

**Serialization** (`SmartHopper.Core.Models.Serialization`):
- `GHJsonConverter`: Serialization/deserialization utilities
- `GHJsonAnalyzer`: Core validation logic
- `GHJsonFixer`: Auto-repair for common issues

**Grasshopper Integration** (`SmartHopper.Core.Grasshopper.Utils.Serialization`):
- `DocumentIntrospectionV2`: Extract GhJSON from live Grasshopper objects
- `GhJsonValidator`: Grasshopper-specific validation (component existence, type compatibility)
- `PropertyManagerV2`: Context-aware property extraction and filtering

**Placement** (`SmartHopper.Core.Grasshopper.Utils.Internal`):
- `GhJsonPlacer`: Place deserialized components on canvas with positioning and wiring

### AI Tools

**Retrieval Tools**:
- `gh_get`: Generic component retrieval with filters
- `gh_get_selected`: Selected components only
- `gh_get_errors`: Components with errors
- `gh_get_locked`: Locked/disabled components
- `gh_get_hidden`: Components with preview off
- `gh_get_visible`: Components with preview on
- `gh_get_by_guid`: Specific components by GUID

**Placement Tools**:
- `gh_put`: Place components from GhJSON

---

## Use Cases

### AI-Powered Component Generation

AI can generate complete Grasshopper definitions from natural language descriptions:

```
User: "Create a parametric tower with 10 floors"
AI: Uses gh_put to place components for tower generation
```

### Component Analysis

Analyze existing definitions to understand structure and identify issues:

```
User: "What components have errors?"
AI: Uses gh_get_errors to retrieve and analyze problematic components
```

### Definition Modification

Modify existing definitions programmatically:

```
User: "Unlock all locked components"
AI: Uses gh_get_locked to find components, then modifies and replaces them
```

### Documentation

Extract component usage patterns for documentation or analytics:

```
User: "What components are used most frequently?"
AI: Uses gh_get to retrieve all components and analyze distribution
```

---

## Future Development

Planned enhancements (high level):

- **Phase 2: GhJSON-Lite**
- [ ] 2.1 Lite converter (structure-only)
- [ ] 2.2 Type dictionary optimization
- [ ] 2.3 Diff generation utility

- **Phase 3: Reliability and Consistency**
- [ ] 3.1 Validation framework (levels + JSON Schema)
- [ ] 3.2 Error handling and recovery (graceful degradation, auto-repair)
- [ ] 3.3 Consistency guarantees (deterministic ordering, idempotency)
- [ ] 3.4 Undo support to gh_put

---

## Related Documentation

- [Format Specification](./format-specification.md)
- [Property Management (V2)](./property-management.md)
- [Examples](./examples.md)
- [AI Tools Documentation](../Tools/index.md)
- [Component Base Classes](../Components/index.md)
- [Architecture Overview](../Architecture.md)
