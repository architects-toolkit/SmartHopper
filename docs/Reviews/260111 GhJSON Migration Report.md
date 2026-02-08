# GhJSON Migration Report

**Date:** 2026-01-11  
**Status:** Completed

## Summary

SmartHopper has been migrated to fully rely on `ghjson-dotnet` for all GhJSON serialization, deserialization, validation, and canvas operations. All internal GhJSON code has been removed.

---

## Files Removed

### SmartHopper.Core.Grasshopper\Serialization\ (16 files)

- `Canvas\CanvasUtilities.cs`
- `Canvas\ComponentPlacer.cs`
- `Canvas\ConnectionManager.cs`
- `Canvas\GroupManager.cs`
- `GhJson\DeserializationOptions.cs`
- `GhJson\GhJsonDeserializer.cs`
- `GhJson\GhJsonHelpers.cs`
- `GhJson\GhJsonMerger.cs`
- `GhJson\GhJsonSerializer.cs`
- `GhJson\SerializationOptions.cs`
- `GhJson\ScriptComponents\ScriptComponentFactory.cs`
- `GhJson\ScriptComponents\ScriptParameterMapper.cs`
- `GhJson\ScriptComponents\ScriptSignatureParser.cs`
- `GhJson\Shared\AccessModeMapper.cs`
- `GhJson\Shared\ParameterMapper.cs`
- `GhJson\Shared\TypeHintMapper.cs`

### SmartHopper.Core.Grasshopper\Utils\Serialization\ (10 files)

- `ComponentSpecBuilder.cs`
- `DataTreeConverter.cs`
- `GhJsonValidator.cs`
- `PropertyFilters\PropertyFilter.cs`
- `PropertyFilters\PropertyFilterBuilder.cs`
- `PropertyFilters\PropertyFilterConfig.cs`
- `PropertyHandlers\IPropertyHandler.cs`
- `PropertyHandlers\PropertyHandlerRegistry.cs`
- `PropertyHandlers\SpecializedPropertyHandlers.cs`
- `PropertyManagerV2.cs`

### SmartHopper.Core\Models\ (15 files)

- `Components\AdditionalParameterSettings.cs`
- `Components\ComponentProperties.cs`
- `Components\ComponentProperty.cs`
- `Components\ComponentState.cs`
- `Components\ParameterSettings.cs`
- `Components\VBScriptCode.cs`
- `Connections\Connection.cs`
- `Connections\ConnectionPairing.cs`
- `Document\DocumentMetadata.cs`
- `Document\GrasshopperDocument.cs`
- `Document\GroupInfo.cs`
- `Serialization\CompactPosition.cs`
- `Serialization\GHJsonAnalyzer.cs`
- `Serialization\GHJsonConverter.cs`
- `Serialization\GHJsonFixer.cs`

### SmartHopper.Core\Serialization\ (20 files)

- `DataTypes\DataTypeRegistry.cs`
- `DataTypes\DataTypeSerializer.cs`
- `DataTypes\IDataTypeSerializer.cs`
- `DataTypes\Serializers\*.cs` (16 serializer files)
- `EmptyStringIgnoreConverter.cs`

---

## Behavior Alignment (ghjson-dotnet changes made before migration)

The following changes were made to `ghjson-dotnet` to ensure behavior parity with SmartHopper:

### 1. PersistentData → VolatileData Rename

**Location:** `GhJSON.Grasshopper/Serialization/SchemaProperties/PropertyManagerV2.cs`

When extracting `PersistentData` for an `IGH_Param` that has sources (incoming connections), the property is renamed to `VolatileData` and `PersistentData` is removed. This matches SmartHopper's AI context behavior.

### 2. RemovePivotsIfIncomplete Fixer

**Location:** `GhJSON.Core/Validation/GhJsonFixer.cs`

If not all components in a GhJSON document have valid pivot positions, all pivots are removed to prevent partial layout issues. This is invoked by `GhJsonFixer.FixAll()`.

### 3. Connection Parameter Matching Semantics

**Location:** `GhJSON.Grasshopper/Canvas/ConnectionManager.cs`

Parameter matching now uses `Name` first, then falls back to `NickName`. This aligns with how SmartHopper serializes `ParamName` in connections.

### 4. Runtime Data Extraction API

**Location:** `GhJSON.Grasshopper/Serialization/GhJsonSerializer.cs`

Added public methods:

- `ExtractRuntimeData(IEnumerable<IGH_ActiveObject>)` → `JObject`
- `ExtractParameterVolatileData(IGH_Param)` → `JObject?`

These extract volatile data from component outputs/inputs for AI inspection, matching SmartHopper's `gh_get` runtime data feature.

### 5. Connection Serialization ParamName

**Location:** `GhJSON.Grasshopper/Serialization/GhJsonSerializer.cs`

Connection `ParamName` is now serialized as `param.Name` (not `NickName`), aligning serialization with the deserialization matching logic.

---

## SmartHopper Code Retained

The following SmartHopper-specific code was **retained** as it implements SmartHopper UX features not part of the GhJSON library contract:

### gh_get Filter Logic

**Location:** `SmartHopper.Core.Grasshopper/AITools/gh_get.cs`

- Attribute filters (`+selected`, `-error`, etc.)
- Category filters (`+Vector`, `-Curve`)
- Type filters (`+params`, `+startnodes`)
- Connection depth expansion
- GUID filters

These are SmartHopper AI tool features that orchestrate what objects to serialize, not part of the serialization library itself.

### gh_put Edit Mode Logic

**Location:** `SmartHopper.Core.Grasshopper/AITools/gh_put.cs`

- User confirmation dialogs for component replacement
- External connection capture and restoration
- Undo event recording

These are SmartHopper UX features for safe component editing.

### Canvas Access Utilities

**Location:** `SmartHopper.Core.Grasshopper/Utils/Canvas/`

- `CanvasAccess.cs` - Get current canvas objects
- `ConnectionBuilder.cs` - Build connections by GUID
- `ObjectFactory.cs` - Component proxy lookup

These remain as SmartHopper utilities for canvas interaction.

---

## Inconsistencies Found and Resolved

### 1. InstanceGuid Nullability

**SmartHopper:** `InstanceGuid` was required (validation error if missing)  
**ghjson-dotnet:** `InstanceGuid` is nullable (`Guid?`)

**Resolution:** ghjson-dotnet's approach is correct - AI-generated documents may not include instanceGuids, and they're generated during deserialization. The `GhJsonFixer.InjectMissingInstanceGuids()` method handles this.

### 2. Serialization Options Naming

**SmartHopper:** `SerializationOptions.Context` enum with `Standard`, `Optimized`, `Lite`  
**ghjson-dotnet:** `SerializationContext` enum with same values

**Resolution:** Direct mapping, no behavior change.

### 3. DeserializationResult Structure

**SmartHopper:** Returned `List<IGH_DocumentObject>` components + `GuidMapping`  
**ghjson-dotnet:** Returns `DeserializationResult` with `Components`, `GuidMapping`, `IdMapping`, `Errors`, `IsSuccess`

**Resolution:** ghjson-dotnet provides richer result information. SmartHopper's `gh_put` already uses this structure.

---

## Project References

The following project references were already in place (no changes needed):

### SmartHopper.Core.csproj

```xml
<ProjectReference Include="..\..\..\ghjson-dotnet\src\GhJSON.Core\GhJSON.Core.csproj" />
```

### SmartHopper.Core.Grasshopper.csproj

```xml
<ProjectReference Include="..\..\..\ghjson-dotnet\src\GhJSON.Core\GhJSON.Core.csproj" />
<ProjectReference Include="..\..\..\ghjson-dotnet\src\GhJSON.Grasshopper\GhJSON.Grasshopper.csproj" />
```

---

## Build Status

✅ **Solution builds successfully** with only pre-existing nullable reference warnings.

---

## UX Impact

**None.** The `gh_get` and `gh_put` AI tools function identically from the user's perspective:

- Same JSON output format
- Same filter semantics
- Same placement behavior
- Same edit mode with confirmation dialogs
- Same runtime data extraction (now wired to ghjson-dotnet)

---

## Future Considerations

1. **NuGet Package:** When `ghjson-dotnet` is published as a NuGet package, update project references to use the package instead of direct project references.

2. **GhJSON Spec Updates:** Any GhJSON format changes should be made in `ghjson-dotnet` first, then consumed by SmartHopper.

3. **Test Coverage:** Add integration tests verifying SmartHopper AI tools work correctly with ghjson-dotnet.
