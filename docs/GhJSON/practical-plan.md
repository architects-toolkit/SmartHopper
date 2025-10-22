# GhJSON Roadmap: Practical Implementation Plan

## Implementation Status Checklist

- **Phase 1: Enhanced Schema and Metadata** ✅ **COMPLETED**
- [x] 1.1 Document-level metadata
- [x] 1.2 Groups support
- [x] 1.3 Data type serialization
- [x] 1.4 Component schema improvements
- [x] 1.5 Property Management System V2
- [x] 1.6 Value Consolidation

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

**Phase 1 Status**: ✅ COMPLETED  
**Remaining Estimated Time**: 6-8 weeks (Phases 2-3)

---

## Phase 1: Enhanced Schema and Metadata

### 1.1: Document-Level Metadata ✅ COMPLETED

**Status**: ✅ Implemented - Document metadata with schemaVersion, timestamps, author, and environment information.

---

### 1.2: Groups Support ✅ COMPLETED

**Status**: ✅ Implemented - Groups serialization with integer ID-based member references, always included in output.

---

### 1.3: Data Type Serialization ✅ COMPLETED

**Status**: ✅ Implemented - Complete serialization system for all Grasshopper/Rhino data types with prefix-based string formats.

---

### 1.4: Component Schema Improvements ✅ COMPLETED

**Status**: ✅ Implemented - Separated properties with `params`, `inputSettings`, `outputSettings`, and `componentState` fields.

---

### 1.5: Property Management System V2 ✅ COMPLETED

**Status**: ✅ Implemented - Advanced property management with context-aware filtering and flexible configuration.

---

### 1.6: Value Consolidation ✅ COMPLETED

**Status**: ✅ Implemented - Universal `componentState.value` property for all component types (Number Slider, Panel, Scribble, Script, Value List).

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
