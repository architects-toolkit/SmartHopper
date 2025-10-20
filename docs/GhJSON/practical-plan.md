# GhJSON Roadmap: Practical Implementation Plan

## Implementation Status Checklist

- **Phase 1: Enhanced Schema and Metadata** ✅ **COMPLETED**
- [x] 1.1 Document-level metadata (schemaVersion + metadata in `GrasshopperDocument`, metadata population in `DocumentIntrospection`, `includeMetadata` in `gh_get`, UI wiring in `GhGetComponents`)
- [x] 1.2 Groups support (groups array in `GrasshopperDocument`, `GroupInfo` model, extraction in `DocumentIntrospection`, recreation in `GhJsonPlacer` - always included, no flag needed)
- [x] 1.3 Data type serialization (core serializers + `PropertyManager`/`DataTreeConverter` integration)
- [x] 1.4 Component schema improvements (`params`, `inputSettings`/`outputSettings`, `componentState` - keeping legacy `pivot` for compactness)
- [x] 1.5 Property Management System V2: Advanced property management with context-aware filtering, component categories, and flexible configuration

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

## Executive Summary

This document provides a concrete, step-by-step implementation plan for evolving the GhJSON format from its current state to the enhanced schema outlined in the roadmap. The plan is organized into discrete, testable phases that can be implemented incrementally without breaking existing functionality.

---

## Current Architecture Analysis

### Serialization Flow (gh_get)
```
CanvasAccess.GetCurrentObjects()
    ↓
DocumentIntrospection.GetObjectsDetails()
    ↓
PropertyManager (whitelist-based property extraction)
    ↓
GrasshopperDocument model
    ↓
GHJsonConverter.SerializeToJson()
    ↓
JSON string output
```

### Deserialization Flow (gh_put)
```
JSON string input
    ↓
GhJsonValidator.Validate() (validation)
    ↓
GHJsonConverter.DeserializeFromJson()
    ↓
GHJsonFixer (GUID fixing, pivot removal)
    ↓
GrasshopperDocument model
    ↓
GhJsonPlacer.PutObjectsOnCanvas()
    ↓
Components placed on canvas
```

### Key Files
- **Models**: `GrasshopperDocument.cs`, `ComponentProperties.cs`, `ComponentProperty.cs`, `ConnectionPairing.cs`
- **Serialization**: `DocumentIntrospection.cs`, `PropertyManager.cs`, `GHJsonConverter.cs`
- **Validation**: `GhJsonValidator.cs`, `GHJsonAnalyzer.cs`, `GHJsonFixer.cs`
- **Placement**: `GhJsonPlacer.cs`
- **Tools**: `gh_get.cs`, `gh_put.cs`

---

## Implementation Phases Overview

### Phase 1: Enhanced Schema and Metadata (5-8 weeks)
- 1.1: Document-level metadata
- 1.2: Groups support
- 1.3: Data type serialization (Color, Point3d, Line, Plane, etc.)
- 1.4: Component schema improvements (position, params, inputSettings, outputSettings, componentState)

### Phase 2: GhJSON-Lite Format (3-4 weeks)
- 2.1: Lite format converter (Level 1: Structure-only)
- 2.2: Type dictionary optimization (Level 2)
- 2.3: Diff generation utility

### Phase 3: Reliability and Consistency (3-4 weeks)
- 3.1: Validation framework
- 3.2: Error handling and recovery
- 3.3: Consistency guarantees

**Total Estimated Time**: 11-16 weeks

---

## Phase 1: Enhanced Schema and Metadata

### 1.1: Document-Level Metadata (1-2 weeks)

**Objective**: Add top-level metadata and schema version to GrasshopperDocument.

**Files to Create**:
- `src/SmartHopper.Core/Models/Document/DocumentMetadata.cs`

**Files to Modify**:
- `src/SmartHopper.Core/Models/Document/GrasshopperDocument.cs`
- `src/SmartHopper.Core.Grasshopper/Utils/Serialization/DocumentIntrospection.cs`
- `src/SmartHopper.Core.Grasshopper/AITools/gh_get.cs`

**Implementation Tasks**:
1. Create `DocumentMetadata` model with properties: schemaVersion, description, version, created, modified, author, rhinoVersion, grasshopperVersion, dependencies
2. Add `Metadata` property to `GrasshopperDocument` (nullable, omit if null)
3. Implement metadata population in `DocumentIntrospection.GetObjectsDetails()`:
   - Detect Rhino/Grasshopper versions
   - Scan components for plugin dependencies
   - Set timestamps
4. Add `includeMetadata` parameter to `gh_get` tool
5. Write unit tests for metadata serialization/deserialization

**Backward Compatibility**: Metadata is optional; old JSON without metadata will deserialize correctly.

---

### 1.2: Groups Support (2 weeks)

**Objective**: Serialize and deserialize Grasshopper groups with integer ID-based member references.

**Files to Create**:
- `src/SmartHopper.Core/Models/Groups/GroupProperties.cs`
- `src/SmartHopper.Core/Models/Groups/ColorRGBA.cs`

**Files to Modify**:
- `src/SmartHopper.Core/Models/Components/ComponentProperties.cs` (add `Id` property)
- `src/SmartHopper.Core/Models/Document/GrasshopperDocument.cs` (add `Groups` property)
- `src/SmartHopper.Core.Grasshopper/Utils/Serialization/DocumentIntrospection.cs`
- `src/SmartHopper.Core.Grasshopper/Utils/Internal/GhJsonPlacer.cs`
- `src/SmartHopper.Core.Grasshopper/AITools/gh_get.cs`

**Implementation Tasks**:
1. Create `GroupProperties` model with id, name, color, members (int[]), position
2. Create `ColorRGBA` model for color representation
3. Add nullable `Id` property to `ComponentProperties` (sequential integer)
4. Add nullable `Groups` list to `GrasshopperDocument`
5. Implement group extraction in `DocumentIntrospection`:
   - Assign sequential IDs to components
   - Build GUID-to-ID mapping
   - Extract groups from canvas
   - Map group members to component IDs
6. Implement group recreation in `GhJsonPlacer`:
   - Build ID-to-GUID mapping from placed components
   - Create GH_Group objects
   - Add members by GUID
   - Apply colors and names
7. Add `includeGroups` parameter to `gh_get` tool
8. Write unit tests for group serialization/deserialization

**Backward Compatibility**: Groups and component IDs are optional; old JSON will deserialize correctly.

---

### 1.3: Data Type Serialization (1-2 weeks)

**Objective**: Create unified serialization system for Grasshopper/Rhino data types (Color, Point3d, Line, Plane, etc.).

**Files to Create**:
- `src/SmartHopper.Core/Serialization/DataTypes/IDataTypeSerializer.cs`
- `src/SmartHopper.Core/Serialization/DataTypes/DataTypeSerializer.cs`
- `src/SmartHopper.Core/Serialization/DataTypes/Serializers/ColorSerializer.cs`
- `src/SmartHopper.Core/Serialization/DataTypes/Serializers/Point3dSerializer.cs`
- `src/SmartHopper.Core/Serialization/DataTypes/Serializers/LineSerializer.cs`
- `src/SmartHopper.Core/Serialization/DataTypes/Serializers/PlaneSerializer.cs`
- `src/SmartHopper.Core/Serialization/DataTypes/Serializers/CircleSerializer.cs`
- `src/SmartHopper.Core/Serialization/DataTypes/Serializers/ArcSerializer.cs`
- `src/SmartHopper.Core/Serialization/DataTypes/Serializers/BoundingBoxSerializer.cs`
- `src/SmartHopper.Core/Serialization/DataTypes/Serializers/IntervalSerializer.cs`
- `src/SmartHopper.Core/Serialization/DataTypes/DataTypeRegistry.cs`

**Files to Modify**:
- `src/SmartHopper.Core.Grasshopper/Utils/Serialization/DataTreeConverter.cs`
- `src/SmartHopper.Core.Grasshopper/Utils/Serialization/PropertyManager.cs`
- `src/SmartHopper.Core.Grasshopper/AITools/gh_get.cs` (add type format reference to tool description)

**Implementation Tasks**:

1. **Create IDataTypeSerializer Interface**:
```csharp
public interface IDataTypeSerializer
{
    string TypeName { get; }
    Type TargetType { get; }
    string Serialize(object value);
    object Deserialize(string value);
    bool Validate(string value);
}
```

2. **Implement Core Serializers**:
   - **ColorSerializer**: `"r,g,b,a"` format (e.g., `"255,128,64,255"`)
   - **Point3dSerializer**: `"x,y,z"` format (e.g., `"10.5,20.0,30.5"`)
   - **Vector3dSerializer**: `"x,y,z"` format (e.g., `"1.0,0.0,0.0"`)
   - **LineSerializer**: `"x1,y1,z1,x2,y2,z2"` format (e.g., `"0,0,0,10,10,10"`)
   - **PlaneSerializer**: `"ox,oy,oz,xx,xy,xz,yx,yy,yz"` format (origin + X/Y axes)
   - **CircleSerializer**: `"cx,cy,cz,nx,ny,nz,r"` format (center + normal + radius)
   - **ArcSerializer**: `"cx,cy,cz,nx,ny,nz,r,a1,a2"` format (circle + angles)
   - **BoundingBoxSerializer**: `"x1,y1,z1,x2,y2,z2"` format (min/max corners)
   - **IntervalSerializer**: `"min,max"` format (e.g., `"0.0,10.0"`)

3. **Create DataTypeRegistry**:
   - Static registry mapping type names to serializers
   - Auto-registration via reflection or explicit registration
   - Lookup by Type or type name string
   - Thread-safe singleton pattern

4. **Create DataTypeSerializer Facade**:
```csharp
public static class DataTypeSerializer
{
    public static string Serialize(object value)
    {
        var serializer = DataTypeRegistry.GetSerializer(value.GetType());
        return serializer?.Serialize(value) ?? value?.ToString();
    }
    
    public static object Deserialize(string typeName, string value)
    {
        var serializer = DataTypeRegistry.GetSerializer(typeName);
        return serializer?.Deserialize(value);
    }
    
    public static bool TryDeserialize(string typeName, string value, out object result)
    {
        // Safe deserialization with validation
    }
}
```

5. **Update DataTreeConverter**:
   - Detect complex types in persistent data
   - Use DataTypeSerializer for encoding/decoding
   - Store as `{ "type": "Point3d", "value": "10,20,30" }`
   - Maintain backward compatibility with existing format

6. **Update PropertyManager**:
   - Use DataTypeSerializer for component property values
   - Handle Color, Point3d, etc. in component state

7. **Add Type Format Documentation**:
   - Update `gh_get` and `gh_put` tool descriptions
   - Include format reference for each type
   - Provide examples in tool schema

8. **Write Comprehensive Tests**:
   - Unit tests for each serializer (round-trip)
   - Validation tests (invalid formats)
   - Integration tests (persistent data serialization)
   - Edge cases (NaN, infinity, zero-length vectors)

**Format Specifications**:

| Type | Format | Example | Validation Rules |
|------|--------|---------|------------------|
| Color | `r,g,b,a` | `"255,128,64,255"` | 0-255 integers |
| Point3d | `x,y,z` | `"10.5,20.0,30.5"` | Valid doubles |
| Vector3d | `x,y,z` | `"1.0,0.0,0.0"` | Valid doubles |
| Line | `x1,y1,z1,x2,y2,z2` | `"0,0,0,10,10,10"` | 6 valid doubles |
| Plane | `ox,oy,oz,xx,xy,xz,yx,yy,yz` | `"0,0,0,1,0,0,0,1,0"` | 9 valid doubles |
| Circle | `cx,cy,cz,nx,ny,nz,r` | `"0,0,0,0,0,1,5.0"` | 7 doubles, r > 0 |
| Arc | `cx,cy,cz,nx,ny,nz,r,a1,a2` | `"0,0,0,0,0,1,5.0,0,1.57"` | 9 doubles, r > 0 |
| BoundingBox | `x1,y1,z1,x2,y2,z2` | `"0,0,0,10,10,10"` | 6 doubles, min < max |
| Interval | `min,max` | `"0.0,10.0"` | 2 doubles, min ≤ max |

**Benefits**:
- AI can generate valid data type strings
- Human-readable and debuggable
- Consistent encoding across all types
- Extensible for new types
- More compact than full JSON objects

**Testing Strategy**:
- Round-trip tests (serialize → deserialize → compare)
- Invalid format handling
- Edge cases (empty, null, extreme values)
- Performance benchmarks for large data trees

---

### 1.4: Component Schema Improvements (2-3 weeks)

**Objective**: Separate input/output settings, component state, and standardize position naming.

**Files to Create**:
- `src/SmartHopper.Core/Models/Components/ParameterSettings.cs`
- `src/SmartHopper.Core/Models/Components/AdditionalParameterSettings.cs`
- `src/SmartHopper.Core/Models/Components/ComponentState.cs`
- `src/SmartHopper.Core/Models/Components/Position.cs`
- `src/SmartHopper.Core/Models/Serialization/PropertyMigration.cs`

**Files to Modify**:
- `src/SmartHopper.Core/Models/Components/ComponentProperties.cs`
- `src/SmartHopper.Core.Grasshopper/Utils/Serialization/DocumentIntrospection.cs`
- `src/SmartHopper.Core.Grasshopper/Utils/Internal/GhJsonPlacer.cs`
- `src/SmartHopper.Core.Grasshopper/AITools/gh_get.cs`

**Implementation Tasks**:
1. Create `ParameterSettings` model for input/output configuration
2. Create `ComponentState` model for UI-specific state (script, slider, panel properties)
3. Create `Position` model with lowercase x, y properties
4. Extend `ComponentProperties` with:
   - `Position` (new, lowercase x/y)
   - `Params` (simple key-value dictionary)
   - `InputSettings` (array of parameter settings)
   - `OutputSettings` (array of parameter settings)
   - `ComponentState` (component-specific state)
   - Keep `Pivot` for backward compatibility (deprecated)
   - Keep `Properties` for backward compatibility (deprecated)
5. Implement property extraction in `DocumentIntrospection`:
   - Extract position from component attributes
   - Extract params (NickName, UserText, etc.)
   - Extract input/output settings from parameters
   - Extract component state (script, slider, panel)
6. Implement property application in `GhJsonPlacer`:
   - Apply position (prefer Position over Pivot)
   - Apply params
   - Apply input/output settings
   - Apply component state
7. Create `PropertyMigration` utility for converting old to new schema
8. Add `useNewSchema` parameter to `gh_get` tool (default: true)
9. Write unit tests for new schema serialization/deserialization
10. Write migration tests (old → new schema)

**Backward Compatibility**: 
- New properties are optional; old JSON will deserialize
- Deserialization prefers new properties but falls back to old
- Migration utility can convert old to new format

---

## Phase 2: GhJSON-Lite Format

### 2.1: Lite Format Converter - Level 1 (2 weeks)

**Objective**: Create structure-only format optimized for AI consumption.

**Files to Create**:
- `src/SmartHopper.Core/Models/Serialization/GhJsonLiteConverter.cs`
- `src/SmartHopper.Core/Models/Serialization/LiteFormatConfig.cs`

**Files to Modify**:
- `src/SmartHopper.Core.Grasshopper/AITools/gh_get.cs`

**Implementation Tasks**:
1. Create `GhJsonLiteConverter` with `ToLite()` method
2. Implement Level 1 reduction (structure-only):
   - Remove: selected, componentGuid, instanceGuid (keep only type)
   - Remove: componentState (UI-specific)
   - Simplify metadata (keep description, version only)
   - Simplify params (essential values only)
   - Simplify I/O settings (keep only data tree modifiers + expressions)
   - Keep: position, connections, groups, errors/warnings
3. Create `FromLite()` method to expand back to full format
4. Add `format` parameter to `gh_get` tool ("full" or "lite")
5. Write unit tests for lite conversion (full → lite → full)

**Key Reductions**:
- ~40-60% size reduction
- Maintains all behavioral information
- Removes UI-specific state
- Optimized for AI context windows

---

### 2.2: Type Dictionary Optimization - Level 2 (1 week)

**Objective**: Further compress lite format for internal use (caching, diffs).

**Files to Modify**:
- `src/SmartHopper.Core/Models/Serialization/GhJsonLiteConverter.cs`
- `src/SmartHopper.Core/Models/Document/GrasshopperDocument.cs`

**Implementation Tasks**:
1. Add `TypeDictionary` property to `GrasshopperDocument` (optional)
2. Implement Level 2 reduction:
   - Extract unique component types to dictionary
   - Replace type strings with dictionary keys
   - Omit empty arrays/objects
   - Use compact number formatting
3. Update `FromLite()` to handle type dictionary expansion
4. Write unit tests for type dictionary optimization

**Key Reductions**:
- Additional ~20-30% size reduction
- Not intended for AI consumption (internal use only)
- Fully reversible

---

### 2.3: Diff Generation Utility (1-2 weeks)

**Objective**: Generate structured diffs between two GhJSON documents.

**Files to Create**:
- `src/SmartHopper.Core/Models/Serialization/GhJsonDiff.cs`
- `src/SmartHopper.Core/Models/Serialization/DiffResult.cs`

**Implementation Tasks**:
1. Create `GhJsonDiff` class with `Compare()` method
2. Implement component-level diffing:
   - Detect added/removed/modified components
   - Track property changes (old vs new values)
3. Implement connection diffing:
   - Detect added/removed connections
4. Implement group diffing:
   - Detect modified group memberships
5. Generate summary statistics
6. Create diff visualization format (JSON)
7. Write unit tests for diff generation

**Output Format**: Structured JSON with changes, old/new values, and summary.

---

## Phase 3: Reliability and Consistency

### 3.1: Validation Framework (1-2 weeks)

**Objective**: Enhance validation with multiple levels and better error reporting.

**Files to Create**:
- `src/SmartHopper.Core/Models/Serialization/ValidationLevel.cs`
- `src/SmartHopper.Core/Models/Serialization/ValidationResult.cs`

**Files to Modify**:
- `src/SmartHopper.Core.Grasshopper/Utils/Serialization/GhJsonValidator.cs`
- `src/SmartHopper.Core/Models/Serialization/GHJsonAnalyzer.cs`

**Implementation Tasks**:
1. Create `ValidationLevel` enum (None, Syntax, Schema, Semantic, Strict)
2. Create `ValidationResult` model with errors, warnings, suggestions
3. Implement JSON Schema validation (formal schema definition)
4. Implement semantic validation:
   - Type compatibility checking
   - Parameter constraint validation
   - Circular dependency detection
   - Orphaned connection detection
5. Add validation level parameter to `GhJsonValidator.Validate()`
6. Improve error messages with paths and suggestions
7. Write unit tests for each validation level

---

### 3.2: Error Handling and Recovery (1 week)

**Objective**: Graceful degradation and auto-repair for common issues.

**Files to Modify**:
- `src/SmartHopper.Core/Models/Serialization/GHJsonConverter.cs`
- `src/SmartHopper.Core/Models/Serialization/GHJsonFixer.cs`

**Implementation Tasks**:
1. Implement graceful degradation (continue on non-critical errors)
2. Aggregate all errors (not just first)
3. Add auto-repair for common issues:
   - Invalid GUIDs → auto-generate
   - Missing positions → auto-layout
   - Invalid connections → skip
4. Implement partial loading (load valid components, skip invalid)
5. Enhance error reporting with severity levels
6. Write unit tests for error recovery

---

### 3.3: Consistency Guarantees (1 week)

**Objective**: Ensure deterministic, idempotent serialization.

**Files to Modify**:
- `src/SmartHopper.Core.Grasshopper/Utils/Serialization/DocumentIntrospection.cs`
- `src/SmartHopper.Core/Models/Serialization/GHJsonConverter.cs`

**Implementation Tasks**:
1. Implement deterministic output:
   - Stable component ordering (by ID or GUID)
   - Stable connection ordering
   - Stable property ordering
2. Implement canonical form:
   - Single representation for equivalent data
   - Normalized property values
3. Add idempotency tests:
   - Serialize → Deserialize → Serialize yields identical output
4. Write unit tests for consistency

---

## Testing Strategy

### Testing Environment

**Primary Testing Method**: Round-trip serialization using `gh_get` and `gh_put` tools:

1. Use `gh_get` to serialize components from canvas to GhJSON, exposed to UI via GhGetComponents component
2. Use `gh_put` to deserialize and place components back on canvas, exposed to UI via GhPutComponents component
3. Use `gh_get` again to serialize the placed components, exposed to UI via GhGetComponents component
4. Compare original and final GhJSON to verify consistency

This approach validates:

- Serialization correctness (gh_get)
- Deserialization correctness (gh_put)
- Idempotency (serialize → deserialize → serialize yields same result)
- Backward compatibility (old format still works)
- New features (metadata, groups, etc. are preserved)

**Note**: Ensure both `gh_get` and `gh_put` are updated with latest features before testing each phase.

### Unit Tests

- Model serialization/deserialization
- Property extraction and application
- Lite format conversion
- Diff generation
- Validation levels
- Error recovery

### Integration Tests

- Full gh_get → gh_put round-trip
- Group serialization → recreation
- New schema → old schema compatibility
- Lite format → full format expansion

### Performance Tests

- Large document serialization (1000+ components)
- Lite format size reduction benchmarks
- Diff generation performance

---

## Migration Path

### For Existing Users

**Phase 1 Rollout**:

1. Deploy with new schema support (opt-in via `useNewSchema` parameter)
2. Default to old schema for backward compatibility
3. Provide migration utility for converting existing JSON files
4. After 2-3 releases, switch default to new schema

**Phase 2 Rollout**:

1. Deploy lite format as opt-in (via `format` parameter)
2. Use for AI context optimization
3. Keep full format as default for gh_put

**Phase 3 Rollout**:

1. Deploy enhanced validation as opt-in (via `validationLevel` parameter)
2. Default to permissive mode
3. Gradually increase default validation level

### Breaking Changes

**None expected** - all changes are additive with backward compatibility maintained through:

- Optional new properties (nullable, omit if null)
- Fallback to old properties during deserialization
- Migration utilities for explicit conversion
- Feature flags for gradual adoption

---

## Success Criteria

### Phase 1

- [x] Metadata populated correctly for all documents
- [x] Groups serialize and deserialize with correct members
- [x] New schema properties extract correctly
- [x] 100% backward compatibility with old JSON
- [x] Zero breaking changes for existing users

### Phase 2

- [ ] Lite format achieves 40-60% size reduction
- [ ] Lite → full conversion is lossless for behavioral data
- [ ] Diff generation produces accurate change tracking
- [ ] AI context window usage reduced by 40%+

### Phase 3

- [ ] Validation catches 95%+ of common errors
- [ ] Auto-repair fixes 80%+ of fixable issues
- [ ] Serialize → Deserialize → Serialize is idempotent
- [ ] Error messages provide actionable guidance

---

## Risk Mitigation

### Technical Risks

**Risk**: Breaking existing workflows  
**Mitigation**: Comprehensive backward compatibility, feature flags, gradual rollout

**Risk**: Performance degradation with new schema  
**Mitigation**: Performance benchmarks, lazy loading, optional features

**Risk**: Complex migration for existing JSON files  
**Mitigation**: Automated migration utilities, clear documentation

### Process Risks

**Risk**: Scope creep  
**Mitigation**: Strict phase boundaries, MVP approach per phase

**Risk**: Insufficient testing  
**Mitigation**: Test-driven development, integration tests, user testing

---

## Related Documentation

- [GhJSON Roadmap](./roadmap.md) - High-level vision and goals
- [Format Specification](./format-specification.md) - Current format details
- [Property Whitelist](./property-whitelist.md) - Serialized properties
- [Examples](./examples.md) - Format examples
