# GhJSON Documentation

## Overview

**GhJSON** (Grasshopper JSON) is SmartHopper's serialization format for representing Grasshopper definitions as structured JSON documents. It enables AI-powered tools to read, analyze, modify, and generate Grasshopper component networks programmatically.

---

## Documentation

### Core Documentation

- **[Format Specification](./format-specification.md)**  
  Complete schema reference for GhJSON format including component structure, properties, connections, and validation rules.

- **[Property Management System](./property-whitelist.md)**  
  Advanced property management with context-aware filtering, component categories, and flexible configuration system.

- **[Property Management V2 - Implementation Status](./property-management-v2.md)**  
  Detailed status of the new property management system implementation and alignment with roadmap goals.

- **[Examples](./examples.md)**  
  Practical examples of GhJSON format for common Grasshopper patterns and component types.

- **[Roadmap](./roadmap.md)**  
  Development roadmap with Phase 1 completed (Enhanced Schema and Metadata) and future plans.

- **[Practical Implementation Plan](./practical-plan.md)**  
  Step-by-step implementation plan with current status and next phases.

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
- `DocumentIntrospection`: Extract GhJSON from live Grasshopper objects
- `GhJsonValidator`: Grasshopper-specific validation (component existence, type compatibility)
- `PropertyManager`: Property whitelist and serialization logic

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

See the [Roadmap](./roadmap.md) for planned enhancements:

1. **Enhanced Metadata**: Component state, categories, descriptions
2. **Wolf Format Integration**: Learn from Wolf Community Scripts dataset
3. **GhJSON-Lite**: Lightweight variant for bulk operations and AI context optimization
4. **Reliability Improvements**: Enhanced validation, error recovery, and testing

---

## Related Documentation

- [Implementation Status](./implementation-status.md) - Current implementation progress and pending tasks
- [GhJSON Roadmap](./roadmap.md) - Complete feature roadmap and vision
- [Format Specification](./format-specification.md) - Current format specification
- [Property Management V2](./property-management-v2.md) - Advanced property filtering system
- [Property Whitelist](./property-whitelist.md) - Legacy property filtering (deprecated)
- [Examples](./examples.md) - Usage examples
- [AI Tools Documentation](../Tools/index.md)
- [Component Base Classes](../Components/index.md)
- [Architecture Overview](../Architecture.md)
